using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

// Server-authoritative rules engine for the play phase.
// The server tracks both players' hand/under/over lists (registered by
// NetworkCardGenerator after dealing), validates every play request against
// the Skit Gubbe rules, and pushes results to clients:
//   - targeted ClientRpcs mutate the acting player's own hand display
//   - a broadcast ClientRpc updates the opponent display on the other side
//   - the pile itself replicates through NetworkPile's NetworkList
public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    // ---------------------------------------------------------------------
    // Replicated state
    // ---------------------------------------------------------------------

    NetworkVariable<bool> gameHasStarted = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    NetworkVariable<ulong> currentTurnClientId = new NetworkVariable<ulong>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool GetGameHasStarted() => gameHasStarted.Value;
    public ulong GetCurrentTurnClientId() => currentTurnClientId.Value;
    public bool IsMyTurn => gameHasStarted.Value && NetworkManager.Singleton != null
        && currentTurnClientId.Value == NetworkManager.Singleton.LocalClientId;

    // ---------------------------------------------------------------------
    // Server-only state
    // ---------------------------------------------------------------------

    class ServerPlayerState
    {
        public ulong clientId;
        public List<CardNetData> hand = new List<CardNetData>();
        public List<CardNetData> under = new List<CardNetData>();
        public List<CardNetData> over = new List<CardNetData>();
    }

    readonly Dictionary<ulong, ServerPlayerState> players = new Dictionary<ulong, ServerPlayerState>();
    readonly HashSet<ulong> readyForStart = new HashSet<ulong>();

    // Restricts follow-up plays in the same turn to this value (0 = unrestricted).
    int savedCardValue;
    bool canEndTurn;

    enum OpponentAction : byte { Played = 0, Drew = 1, PickedUpPile = 2 }

    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) { Instance = null; }
    }

    // ---------------------------------------------------------------------
    // Registration (server, called by NetworkCardGenerator after dealing)
    // ---------------------------------------------------------------------

    public void ServerRegisterPlayer(ulong clientId, CardNetData[] hand, CardNetData[] under, CardNetData[] over)
    {
        if (!IsServer) { return; }

        ServerPlayerState state = new ServerPlayerState { clientId = clientId };
        state.hand.AddRange(hand);
        state.under.AddRange(under);
        state.over.AddRange(over);
        players[clientId] = state;

        Debug.Log($"[NGM] Registered player {clientId} — hand={state.hand.Count} under={state.under.Count} over={state.over.Count}");
    }

    // Keeps server hand/over state consistent when a player swaps side cards pre-game.
    public void ServerApplySwap(ulong clientId, CardNetData[] newOverSide)
    {
        if (!IsServer || !players.TryGetValue(clientId, out ServerPlayerState state)) { return; }

        // Cards that left the overSide go back to the hand.
        foreach (CardNetData old in state.over)
        {
            bool stillThere = false;
            foreach (CardNetData d in newOverSide) { if (d.Equals(old)) { stillThere = true; break; } }
            if (!stillThere) { state.hand.Add(old); }
        }

        // Cards that entered the overSide leave the hand.
        foreach (CardNetData entering in newOverSide)
        {
            state.hand.RemoveAll(c => c.Equals(entering));
        }

        state.over.Clear();
        state.over.AddRange(newOverSide);
    }

    // ---------------------------------------------------------------------
    // Game start
    // ---------------------------------------------------------------------

    // Wire the in-scene Start button to this.
    public void RequestStartGame()
    {
        RequestStartGameServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestStartGameServerRpc(ServerRpcParams p = default)
    {
        if (gameHasStarted.Value) { return; }

        readyForStart.Add(p.Receive.SenderClientId);
        Debug.Log($"[NGM] Start requested by {p.Receive.SenderClientId} — ready={readyForStart.Count}/{players.Count}");

        if (readyForStart.Count < 2 || players.Count < 2) { return; }

        currentTurnClientId.Value = DetermineStartingPlayer();
        savedCardValue = 0;
        canEndTurn = false;
        gameHasStarted.Value = true;

        Debug.Log($"[NGM] Game started — first turn: {currentTurnClientId.Value}");
    }

    ulong DetermineStartingPlayer()
    {
        // Lowest non-special card starts (2s and 10s excluded, aces are 14 = high).
        ulong best = NetworkManager.ServerClientId;
        int bestLowest = int.MaxValue;

        foreach (ServerPlayerState state in players.Values)
        {
            int lowest = int.MaxValue;
            foreach (CardNetData c in state.hand)
            {
                if (c.Value != 2 && c.Value != 10 && c.Value < lowest) { lowest = c.Value; }
            }

            if (lowest < bestLowest)
            {
                bestLowest = lowest;
                best = state.clientId;
            }
        }

        return best;
    }

    // ---------------------------------------------------------------------
    // Play requests
    // ---------------------------------------------------------------------

    [ServerRpc(RequireOwnership = false)]
    public void PlayCardServerRpc(CardNetData card, ServerRpcParams p = default)
    {
        ulong sender = p.Receive.SenderClientId;

        if (!gameHasStarted.Value || sender != currentTurnClientId.Value) { Reject(sender, "not your turn"); return; }
        if (!players.TryGetValue(sender, out ServerPlayerState state)) { Reject(sender, "unknown player"); return; }

        List<CardNetData> active = GetActiveList(state);
        bool isUnderPhase = active == state.under;

        int index = IndexOf(active, card.CardId);
        if (index < 0) { Reject(sender, "card not in your active pile"); return; }
        card = active[index];

        if (savedCardValue != 0 && card.Value != savedCardValue && !isUnderPhase)
        {
            Reject(sender, "must continue with value " + savedCardValue);
            return;
        }

        NetworkPile pile = NetworkPile.Instance;
        if (pile == null) { Reject(sender, "no pile in scene"); return; }

        bool playable = CanPlayOnPile(card.Value, pile.GetTopValue());

        if (isUnderPhase)
        {
            // Underside cards are played blind: the card always goes to the pile.
            // If it does not beat the pile, the player picks everything up.
            active.RemoveAt(index);
            pile.ServerAddCard(card, sender);
            NotifyPlayed(sender, card);

            if (playable) { ResolveValidPlay(state, card, pile, drawAfter: false); }
            else { ServerPickUpPile(state, pile); }
            return;
        }

        if (!playable)
        {
            // Only a legal "dump and pick up" when nothing in the active pile is playable.
            if (HasCardToPlay(active, pile.GetTopValue())) { Reject(sender, "you have a playable card"); return; }

            active.RemoveAt(index);
            pile.ServerAddCard(card, sender);
            NotifyPlayed(sender, card);
            ServerPickUpPile(state, pile);
            return;
        }

        active.RemoveAt(index);
        if (active == state.over) { NetworkCardGenerator.Instance.ServerRemoveFromOverSide(sender, card.CardId); }

        pile.ServerAddCard(card, sender);
        NotifyPlayed(sender, card);
        ResolveValidPlay(state, card, pile, drawAfter: active == state.hand);
    }

    void ResolveValidPlay(ServerPlayerState state, CardNetData card, NetworkPile pile, bool drawAfter)
    {
        bool burned = ShouldBurn(card.Value, pile);

        if (burned)
        {
            pile.ServerBurn();
            savedCardValue = 0;
            canEndTurn = false;
            // Turn is kept after a burn.
        }
        else if (card.Value == 2)
        {
            // A 2 resets the pile requirement and the player goes again.
            savedCardValue = 0;
            canEndTurn = false;
        }
        else if (HasSameValueCard(state.hand, card.Value))
        {
            // Chain: may keep playing this value, may end the turn instead.
            savedCardValue = card.Value;
            canEndTurn = true;
        }
        else
        {
            savedCardValue = 0;
            canEndTurn = false;
            AdvanceTurn();
        }

        if (drawAfter) { ServerDrawUp(state); }
        SendTurnState(state.clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void EndTurnServerRpc(ServerRpcParams p = default)
    {
        ulong sender = p.Receive.SenderClientId;
        if (!gameHasStarted.Value || sender != currentTurnClientId.Value || !canEndTurn) { return; }

        savedCardValue = 0;
        canEndTurn = false;
        AdvanceTurn();
        SendTurnState(sender);
    }

    // ---------------------------------------------------------------------
    // Rules (mirrors singleplayer PlayerHand)
    // ---------------------------------------------------------------------

    static bool CanPlayOnPile(int value, int pileTop)
    {
        return value >= pileTop || value == 2 || value == 10;
    }

    bool ShouldBurn(int playedValue, NetworkPile pile)
    {
        if (playedValue == 10) { return true; }
        return pile.ServerTopFourSame();
    }

    static bool HasSameValueCard(List<CardNetData> hand, int value)
    {
        foreach (CardNetData c in hand) { if (c.Value == value) { return true; } }
        return false;
    }

    static bool HasCardToPlay(List<CardNetData> list, int pileTop)
    {
        foreach (CardNetData c in list) { if (CanPlayOnPile(c.Value, pileTop)) { return true; } }
        return false;
    }

    List<CardNetData> GetActiveList(ServerPlayerState state)
    {
        if (state.hand.Count > 0) { return state.hand; }
        if (state.over.Count > 0) { return state.over; }
        return state.under;
    }

    static int IndexOf(List<CardNetData> list, int cardId)
    {
        for (int i = 0; i < list.Count; i++) { if (list[i].CardId == cardId) { return i; } }
        return -1;
    }

    // ---------------------------------------------------------------------
    // Turn / draw / pickup (server)
    // ---------------------------------------------------------------------

    void AdvanceTurn()
    {
        foreach (ulong id in players.Keys)
        {
            if (id != currentTurnClientId.Value)
            {
                currentTurnClientId.Value = id;
                return;
            }
        }
    }

    void ServerDrawUp(ServerPlayerState state)
    {
        NetworkCardGenerator gen = NetworkCardGenerator.Instance;
        int cardsPerPlayer = gen.GetCardsPerPlayer();

        int needed = cardsPerPlayer - state.hand.Count;
        if (needed <= 0 || gen.GetRemainingDeckCount() <= 0) { return; }

        CardNetData[] drawn = gen.DrawCards(Mathf.Min(needed, gen.GetRemainingDeckCount()));
        if (drawn == null || drawn.Length == 0) { return; }

        state.hand.AddRange(drawn);

        ReceiveDrawnCardsClientRpc(drawn, TargetParams(state.clientId));
        OpponentActionClientRpc(state.clientId, (byte)OpponentAction.Drew, drawn.Length, default);
    }

    void ServerPickUpPile(ServerPlayerState state, NetworkPile pile)
    {
        CardNetData[] collected = pile.ServerTakeAll(state.clientId);

        if (collected.Length > 0)
        {
            state.hand.AddRange(collected);
            ReceivePileCardsClientRpc(collected, TargetParams(state.clientId));
            OpponentActionClientRpc(state.clientId, (byte)OpponentAction.PickedUpPile, collected.Length, default);
        }

        savedCardValue = 0;
        canEndTurn = false;
        AdvanceTurn();
        SendTurnState(state.clientId);
    }

    void NotifyPlayed(ulong actor, CardNetData card)
    {
        OpponentActionClientRpc(actor, (byte)OpponentAction.Played, 1, card);
    }

    void SendTurnState(ulong clientId)
    {
        TurnStateClientRpc(canEndTurn, savedCardValue, TargetParams(clientId));
    }

    void Reject(ulong clientId, string reason)
    {
        Debug.Log($"[NGM] Rejected play from {clientId}: {reason}");
        PlayRejectedClientRpc(reason, TargetParams(clientId));
    }

    static ClientRpcParams TargetParams(ulong clientId)
    {
        return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } } };
    }

    // ---------------------------------------------------------------------
    // ClientRpcs — route results to the local hand / opponent displays
    // ---------------------------------------------------------------------

    [ClientRpc]
    void ReceiveDrawnCardsClientRpc(CardNetData[] cards, ClientRpcParams rpcParams = default)
    {
        NetworkPlayerHand hand = FindFirstObjectByType<NetworkPlayerHand>();
        if (hand != null) { hand.ReceiveDrawnCards(cards); }
    }

    [ClientRpc]
    void ReceivePileCardsClientRpc(CardNetData[] cards, ClientRpcParams rpcParams = default)
    {
        NetworkPlayerHand hand = FindFirstObjectByType<NetworkPlayerHand>();
        if (hand != null) { hand.ReceivePileCards(cards); }
    }

    [ClientRpc]
    void TurnStateClientRpc(bool endTurnAllowed, int savedValue, ClientRpcParams rpcParams = default)
    {
        NetworkPlayerHand hand = FindFirstObjectByType<NetworkPlayerHand>();
        if (hand != null) { hand.SetTurnState(endTurnAllowed, savedValue); }
    }

    [ClientRpc]
    void PlayRejectedClientRpc(string reason, ClientRpcParams rpcParams = default)
    {
        Debug.LogWarning("[NGM] Play rejected by server: " + reason);
    }

    [ClientRpc]
    void OpponentActionClientRpc(ulong actorId, byte action, int count, CardNetData card)
    {
        if (NetworkManager.Singleton.LocalClientId == actorId)
        {
            // This is the server's confirmation of our own play — remove the card
            // locally only now, so the visual state always matches the server.
            if ((OpponentAction)action == OpponentAction.Played)
            {
                NetworkPlayerHand hand = FindFirstObjectByType<NetworkPlayerHand>();
                if (hand != null) { hand.RemovePlayedCard(card); }
            }
            return;
        }

        NetworkOpponentHand opponent = FindFirstObjectByType<NetworkOpponentHand>();
        if (opponent == null) { return; }

        switch ((OpponentAction)action)
        {
            case OpponentAction.Played:
                opponent.OnOpponentPlayedCard(card);
                break;
            case OpponentAction.Drew:
                opponent.OnOpponentDrewCards(count);
                break;
            case OpponentAction.PickedUpPile:
                opponent.OnOpponentPickedUpPile(count);
                break;
        }
    }
}