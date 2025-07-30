using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NetworkCardGenerator : NetworkBehaviour
{
    [SerializeField] NetworkPrefabRef cardPrefab;
    [SerializeField] Sprite[] cardSprites;
    [SerializeField] int cardsPerPlayer = 5;
    List<byte> deckValues;

    public override void Spawned() { if (Object.HasStateAuthority) InitDeck(); }

    void InitDeck()
    {
        deckValues = new List<byte>(); for (int suit = 0; suit < 4; suit++) for (byte v = 2; v <= 14; v++) deckValues.Add(v);
        Shuffle(deckValues); DealInitialHands();
    }

    void Shuffle<T>(List<T> list) { for (int i = 0; i < list.Count; i++) { int r = Random.Range(i, list.Count); (list[i], list[r]) = (list[r], list[i]); } }

    void DealInitialHands() { foreach (var player in Runner.ActivePlayers) for (int i = 0; i < cardsPerPlayer; i++) SpawnCardTo(player); }

    public void SpawnCardTo(PlayerRef target)
    {
        if (!Object.HasStateAuthority || deckValues.Count == 0) return;
        byte val = deckValues[0]; deckValues.RemoveAt(0);
        var netObj = Runner.Spawn(cardPrefab, Vector3.zero, Quaternion.identity, target);
        var nc = netObj.GetComponent<NetworkedCard>(); nc.Value = val; nc.FaceUp = false;
        var hands = FindObjectsOfType<NetworkPlayerHand>(); var targetHand = hands.FirstOrDefault(h => h.Object.InputAuthority == target);
        if (targetHand != null) targetHand.RPC_AddCardToHand(netObj, target);
    }
}
