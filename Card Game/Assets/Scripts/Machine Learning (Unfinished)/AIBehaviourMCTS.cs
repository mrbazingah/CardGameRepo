// AIBehaviourMCTS.cs
// MonoBehaviour that drives an MCTS AI player in the real game.
//
// Drop this on the same GameObject as AIHand (alongside or instead of AIBehaviourML).
// Set useMLBehaviour = true on AIHand so AIHand's own coroutine doesn't also fire.
//
// The AI reconstructs a SimGame snapshot from the live game state each turn,
// runs MCTS search (with determinization for the opponent's hidden hand),
// then maps the best action back to real card plays using the same helpers
// as AIBehaviourML.

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class AIBehaviourMCTS : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] float playDelay   = 1.0f;
    [SerializeField] float chanceDelay = 0.5f;

    [Header("MCTS Search")]
    [Tooltip("Total simulations per decision. Higher = stronger but slower.")]
    [SerializeField] int   iterationsPerMove   = 2000;
    [Tooltip("Number of opponent-hand samples per decision. Higher = better hidden-info handling.")]
    [SerializeField] int   determinizations    = 20;
    [Tooltip("UCB1 exploration constant. 1.414 is standard; lower = more exploitation.")]
    [SerializeField] float explorationConstant = 1.414f;

    // References
    Pile          pile;
    CardGenerator cardGenerator;
    GameManager   gameManager;
    AudioManager  audioManager;
    AIHand        aiHand;
    PlayerHand    playerHand;

    MCTSAgent agent;
    bool      isPlaying;
    int       cardsPerPlayer;
    bool      chanceGaveExtraTurn;

    // Which SimGame player index this AI is (derived from turn order)
    int aiPlayerIndex;

    // -----------------------------------------------------------------------
    //  Unity lifecycle
    // -----------------------------------------------------------------------
    void Awake()
    {
        pile          = FindFirstObjectByType<Pile>();
        cardGenerator = FindFirstObjectByType<CardGenerator>();
        gameManager   = FindFirstObjectByType<GameManager>();
        audioManager  = FindFirstObjectByType<AudioManager>();
        aiHand        = GetComponent<AIHand>();
        playerHand    = FindFirstObjectByType<PlayerHand>();
    }

    void Start()
    {
        cardsPerPlayer = cardGenerator.GetCardsPerPlayer();

        agent = new MCTSAgent
        {
            IterationsPerMove   = iterationsPerMove,
            Determinizations    = determinizations,
            ExplorationConstant = explorationConstant,
        };

        // AIHand turn number 1 = first player = SimGame index 0, etc.
        aiPlayerIndex = aiHand.GetTurnNumber() - 1;
        if (aiPlayerIndex < 0 || aiPlayerIndex > 1) aiPlayerIndex = 1;
    }

    void Update()
    {
        if (Time.timeScale == 0f || !gameManager.GetGameHasStarted()) return;

        if (gameManager.GetTurn() == aiHand.GetTurnNumber() && !isPlaying)
            StartCoroutine(PlayTurn());
    }

    // -----------------------------------------------------------------------
    //  Main turn coroutine — mirrors AIBehaviourML.PlayTurn exactly,
    //  replacing QLearningAgent with MCTSAgent.ChooseAction.
    // -----------------------------------------------------------------------
    IEnumerator PlayTurn()
    {
        isPlaying = true;
        yield return new WaitForSeconds(playDelay);

        while (gameManager.GetTurn() == aiHand.GetTurnNumber() && !gameManager.GetWinner())
        {
            // --- Underside phase: blind flip ---
            if (aiHand.GetHandCount() == 0 && aiHand.GetOverSideCount() == 0 && aiHand.GetUnderSideCount() > 0)
            {
                var underCards = aiHand.GetCards();
                if (underCards.Count > 0)
                {
                    int  idx     = Random.Range(0, underCards.Count);
                    var  cardObj = underCards[idx];
                    int  val     = cardObj.GetComponent<Card>().GetValue();
                    bool canPlay = CanPlay(val);

                    aiHand.RemoveCard(cardObj);
                    pile.AddCardsToPile(cardObj);
                    audioManager.PlayCardSFX();

                    if (canPlay)
                    {
                        bool shouldDiscard = ShouldDiscard(val);
                        if (shouldDiscard) StartCoroutine(pile.DiscardCardsInPile());
                        StartCoroutine(gameManager.ProcessWin("AI", val != 2 && val != 10 && val != 14));
                        if (!gameManager.GetWinner())
                        {
                            bool extraTurn = (val == 2 || shouldDiscard);
                            if (extraTurn) { yield return new WaitForSeconds(playDelay); continue; }
                            gameManager.NextTurn(cardObj);
                        }
                    }
                    else PickUpPile();
                }
                break;
            }

            // --- MCTS decision (runs on background thread to avoid frame-drop teleport) ---
            SimGame snapshot = BuildSimSnapshot();
            int     action   = -1;
            var     task     = Task.Run(() => { action = agent.ChooseAction(snapshot, aiPlayerIndex); });
            yield return new WaitUntil(() => task.IsCompleted);
            if (action < 0) action = SimGame.ACTION_PICKUP;

            if (action == SimGame.ACTION_PICKUP)
            {
                PickUpPile();
                break;
            }

            if (action == SimGame.ACTION_CHANCE)
            {
                yield return StartCoroutine(DoChanceCard());
                if (chanceGaveExtraTurn && !gameManager.GetWinner())
                {
                    yield return new WaitForSeconds(playDelay);
                    continue;
                }
                break;
            }

            // --- Play a card ---
            int  pileTop2   = pile.GetCurrentCard(false);
            var  cards      = aiHand.GetCards();
            int  targetValue;

            if (action == SimGame.ACTION_REGULAR)
            {
                var reg = cards
                    .Select(c => c.GetComponent<Card>().GetValue())
                    .Where(v => v != 2 && v != 10 && v != 14 && v >= pileTop2)
                    .OrderBy(v => v).ToList();
                targetValue = reg.Count > 0 ? reg[0] : -1;
            }
            else if (action == SimGame.ACTION_2)   targetValue = 2;
            else if (action == SimGame.ACTION_10)  targetValue = 10;
            else if (action == SimGame.ACTION_ACE) targetValue = 14;
            else targetValue = -1;

            var cardObj2 = targetValue >= 0
                ? cards.FirstOrDefault(c => c.GetComponent<Card>().GetValue() == targetValue)
                : null;

            if (cardObj2 == null)
                cardObj2 = cards.FirstOrDefault(c => CanPlay(c.GetComponent<Card>().GetValue()));

            if (cardObj2 == null) { PickUpPile(); break; }

            int playedVal = cardObj2.GetComponent<Card>().GetValue();
            PlayCard(cardObj2);
            StartCoroutine(gameManager.ProcessWin("AI", playedVal != 2 && playedVal != 10 && playedVal != 14));
            if (gameManager.GetWinner()) break;

            bool giveExtraTurn = (playedVal == 2 || ShouldDiscard(playedVal));
            if (giveExtraTurn)
            {
                yield return new WaitForSeconds(playDelay);
                continue;
            }

            gameManager.NextTurn(cardObj2);
            break;
        }

        if (aiHand.GetHandCount() < cardsPerPlayer && cardGenerator.GetDeck().Count > 0)
            cardGenerator.DrawNewCard(cardsPerPlayer - aiHand.GetHandCount(), false);

        isPlaying = false;
    }

    // -----------------------------------------------------------------------
    //  Build a SimGame snapshot from the live Unity game state.
    //  The opponent's hand is filled with what we can observe;
    //  MCTSAgent.ChooseAction calls Determinize internally to randomize
    //  the unknown portion before each search world.
    // -----------------------------------------------------------------------
    SimGame BuildSimSnapshot()
    {
        var sim = new SimGame();

        sim.gameOver    = false;
        sim.winner      = -1;
        sim.currentTurn = aiPlayerIndex;

        // --- AI's cards (fully known) ---
        sim.players[aiPlayerIndex] = new SimGame.SimPlayer();
        foreach (var go in aiHand.GetHandCards())
            sim.players[aiPlayerIndex].hand.Add(go.GetComponent<Card>().GetValue());
        foreach (var go in aiHand.GetOverSideCards())
            sim.players[aiPlayerIndex].overSide.Add(go.GetComponent<Card>().GetValue());
        foreach (var go in aiHand.GetUnderSideCards())
            sim.players[aiPlayerIndex].underSide.Add(go.GetComponent<Card>().GetValue());

        // --- Opponent's cards (partially known) ---
        int oppIndex = 1 - aiPlayerIndex;
        sim.players[oppIndex] = new SimGame.SimPlayer();

        if (playerHand != null)
        {
            // Hand cards are hidden — we add placeholders; Determinize will reshuffle them
            foreach (var go in playerHand.GetHandCards())
                sim.players[oppIndex].hand.Add(go.GetComponent<Card>().GetValue());
            // Overside is face-up and visible
            foreach (var go in playerHand.GetOverSideCards())
                sim.players[oppIndex].overSide.Add(go.GetComponent<Card>().GetValue());
            // Underside is face-down — add placeholders (Determinize handles uncertainty)
            foreach (var go in playerHand.GetUnderSideCards())
                sim.players[oppIndex].underSide.Add(go.GetComponent<Card>().GetValue());
        }

        // --- Pile ---
        sim.pile.Clear();
        foreach (var go in pile.GetCardsInPile())
            sim.pile.Add(go.GetComponent<Card>().GetValue());

        // --- Deck ---
        sim.deck.Clear();
        foreach (var go in cardGenerator.GetDeck())
            sim.deck.Add(go.GetComponent<Card>().GetValue());

        return sim;
    }

    // -----------------------------------------------------------------------
    //  Card play helpers — identical to AIBehaviourML
    // -----------------------------------------------------------------------
    void PlayCard(GameObject cardObj)
    {
        int value = cardObj.GetComponent<Card>().GetValue();
        if (!CanPlay(value)) return;

        aiHand.RemoveCard(cardObj);
        pile.AddCardsToPile(cardObj);
        audioManager.PlayCardSFX();

        if (ShouldDiscard(value)) StartCoroutine(pile.DiscardCardsInPile());

        if (aiHand.GetHandCount() < cardsPerPlayer && cardGenerator.GetDeck().Count > 0)
            cardGenerator.DrawNewCard(cardsPerPlayer - aiHand.GetHandCount(), false);
    }

    void PickUpPile()
    {
        if (gameManager.GetWinner()) return;
        var pileCards = pile.GetCardsInPile();
        pile.ClearPile();
        aiHand.AddCardsToHand(pileCards);
        foreach (var c in pileCards)
        {
            c.GetComponent<Card>().RemoveChild();
            cardGenerator.ApplyCoverOnCards(c);
        }
        gameManager.NextTurn(null);
        audioManager.PlayShufflingSFX();
    }

    IEnumerator DoChanceCard()
    {
        chanceGaveExtraTurn = false;
        GameObject chanceCard = cardGenerator.GetChanceCard();
        if (chanceCard == null) yield break;

        yield return new WaitForSeconds(chanceDelay);

        int  val     = chanceCard.GetComponent<Card>().GetValue();
        bool canPlay = val == 2 || val == 10 || val >= pile.GetCurrentCard(true);

        if (!canPlay) { PickUpPile(); yield break; }

        audioManager.PlayCardSFX();
        bool discard = ShouldDiscard(val);
        if (discard) StartCoroutine(pile.DiscardCardsInPile());
        StartCoroutine(gameManager.ProcessWin("AI", val != 2 && val != 10 && val != 14));

        if (val == 2 || discard) chanceGaveExtraTurn = true;
        else gameManager.NextTurn(chanceCard);
    }

    bool CanPlay(float value) => value == 2 || value == 10 || value >= pile.GetCurrentCard(false);

    bool ShouldDiscard(int value)
    {
        if (value == 10) return true;
        var pileCards = pile.GetCardsInPile();
        if (pileCards.Count < 4) return false;
        int top = pileCards[pileCards.Count - 1].GetComponent<Card>().GetValue();
        for (int i = pileCards.Count - 4; i < pileCards.Count; i++)
            if (pileCards[i].GetComponent<Card>().GetValue() != top) return false;
        return true;
    }
}
