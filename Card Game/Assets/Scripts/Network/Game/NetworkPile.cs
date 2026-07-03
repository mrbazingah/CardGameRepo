using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

// Replicated discard pile. The server mutates the NetworkList (add / burn / take-all)
// and clients rebuild the visual pile from a coalesced diff, the same pattern used by
// NetworkOpponentHand for the overSide display.
//
// Scene setup: in-scene object in the Game Scene with a NetworkObject component.
// Assign pileTransform (where cards stack), playerHandPoint / opponentHandPoint
// (spawn and pickup animation origins), offscreenPoint (burn destination),
// the card prefab and the 52-sprite list.
public class NetworkPile : NetworkBehaviour
{
    public static NetworkPile Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] GameObject cardPrefab;
    [SerializeField] List<Sprite> cardSprites;

    [Header("Points")]
    [SerializeField] Transform pileTransform;
    [SerializeField] Transform playerHandPoint;
    [SerializeField] Transform opponentHandPoint;
    [SerializeField] Transform offscreenPoint;

    [Header("Motion")]
    [SerializeField] float lerpSpeed = 5f;
    [SerializeField] float maxRotation = 15f;

    // Replicated pile contents, bottom to top.
    NetworkList<CardNetData> pileCards = new NetworkList<CardNetData>();

    // Who caused the latest change — used by clients to pick animation origins.
    NetworkVariable<ulong> lastActorClientId = new NetworkVariable<ulong>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 0 = none, 1 = burned, 2 = picked up. Set before a clear so clients know
    // how to animate the outgoing cards.
    NetworkVariable<byte> lastClearReason = new NetworkVariable<byte>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    readonly List<GameObject> visualCards = new List<GameObject>();
    bool pileDirty;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        pileCards.OnListChanged += OnPileChanged;
    }

    public override void OnNetworkDespawn()
    {
        pileCards.OnListChanged -= OnPileChanged;
        if (Instance == this) { Instance = null; }
    }

    void OnPileChanged(NetworkListEvent<CardNetData> _)
    {
        pileDirty = true;
    }

    void LateUpdate()
    {
        if (pileDirty)
        {
            pileDirty = false;
            RebuildFromList();
        }

        // Settle visual cards onto the pile position.
        for (int i = 0; i < visualCards.Count; i++)
        {
            GameObject card = visualCards[i];
            if (card == null) { continue; }
            card.transform.position = Vector2.Lerp(card.transform.position, pileTransform.position, lerpSpeed * Time.deltaTime);
        }
    }

    // Server API (called by NetworkGameManager)
    public void ServerAddCard(CardNetData card, ulong byClientId)
    {
        if (!IsServer) { return; }
        lastActorClientId.Value = byClientId;
        lastClearReason.Value = 0;
        pileCards.Add(card);
    }

    public void ServerBurn()
    {
        if (!IsServer) { return; }
        lastClearReason.Value = 1;
        pileCards.Clear();
    }

    public CardNetData[] ServerTakeAll(ulong pickerClientId)
    {
        if (!IsServer) { return new CardNetData[0]; }

        CardNetData[] taken = new CardNetData[pileCards.Count];
        for (int i = 0; i < pileCards.Count; i++) { taken[i] = pileCards[i]; }

        lastActorClientId.Value = pickerClientId;
        lastClearReason.Value = 2;
        pileCards.Clear();

        return taken;
    }

    public bool ServerTopFourSame()
    {
        if (!IsServer || pileCards.Count < 4) { return false; }

        int top = pileCards[pileCards.Count - 1].Value;
        for (int i = pileCards.Count - 2; i >= pileCards.Count - 4; i--)
        {
            if (pileCards[i].Value != top) { return false; }
        }
        return true;
    }

    // Shared reads
    public int GetTopValue()
    {
        return pileCards.Count == 0 ? 0 : pileCards[pileCards.Count - 1].Value;
    }

    public int GetCount() => pileCards.Count;

    // Visuals (diff against the replicated list)
    void RebuildFromList()
    {
        // Cleared pile: animate everything out according to the clear reason.
        if (pileCards.Count == 0 && visualCards.Count > 0)
        {
            byte reason = lastClearReason.Value;
            bool pickedUpByMe = reason == 2 && lastActorClientId.Value == NetworkManager.Singleton.LocalClientId;

            foreach (GameObject card in visualCards)
            {
                if (card == null) { continue; }

                if (pickedUpByMe)
                {
                    // The local hand spawns its own copies at the pile position,
                    // so the pile visuals just vanish underneath them.
                    Destroy(card);
                }
                else
                {
                    Vector3 target = reason == 2 ? opponentHandPoint.position : offscreenPoint.position;
                    StartCoroutine(AnimateOutAndDestroy(card, target));
                }
            }

            visualCards.Clear();
            return;
        }

        // Added cards: spawn any list entries that have no visual yet.
        for (int i = visualCards.Count; i < pileCards.Count; i++)
        {
            visualCards.Add(SpawnPileCard(pileCards[i], i));
        }
    }

    GameObject SpawnPileCard(CardNetData data, int stackIndex)
    {
        GameObject card = Instantiate(cardPrefab);
        card.transform.SetParent(pileTransform != null ? pileTransform : transform);

        bool playedByMe = lastActorClientId.Value == NetworkManager.Singleton.LocalClientId;
        Transform origin = playedByMe ? playerHandPoint : opponentHandPoint;
        card.transform.position = origin != null ? origin.position : pileTransform.position;

        NetworkCard nc = card.GetComponent<NetworkCard>();
        if (nc != null)
        {
            nc.SetCardId(data.CardId);
            nc.SetValue(data.Value);
            nc.Rotate(Random.Range(-maxRotation, maxRotation), true);
        }

        SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
        if (cardSprites != null && data.CardId < cardSprites.Count)
        {
            sr.sprite = cardSprites[data.CardId];
            sr.color = Color.white;
        }
        sr.sortingOrder = 100 + stackIndex;

        return card;
    }

    IEnumerator AnimateOutAndDestroy(GameObject card, Vector3 target)
    {
        while (card != null && (card.transform.position - target).sqrMagnitude > 1f)
        {
            card.transform.position = Vector3.Lerp(card.transform.position, target, lerpSpeed * Time.deltaTime);
            yield return null;
        }
        if (card != null) { Destroy(card); }
    }
}