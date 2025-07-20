using Fusion;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(NetworkObject))]
public class NetworkCardGenerator : NetworkBehaviour
{
    [SerializeField] private NetworkPrefabRef cardPrefab;
    private List<int> deck = new List<int>();

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
            BuildDeck();
    }

    private void BuildDeck()
    {
        deck.Clear();
        for (int i = 1; i <= 52; i++) deck.Add(i);
        Shuffle(deck);
    }

    public void DealHands(PlayerEntity[] players, int cardsPerPlayer)
    {
        if (!Object.HasStateAuthority) return;
        foreach (var p in players)
        {
            for (int i = 0; i < cardsPerPlayer; i++)
            {
                int id = deck[0]; deck.RemoveAt(0);
                Vector3 pos = p.GetHandPosition(i);
                var obj = Runner.Spawn(cardPrefab, pos, Quaternion.identity,
                    (r, o) => o.GetComponent<NetworkCard>().Initialize(id)
                );
                p.AddCardToHand(obj.GetComponent<NetworkCard>());
            }
        }
    }

    public GameObject DrawCard()
    {
        if (!Object.HasStateAuthority || deck.Count == 0) return null;
        int id = deck[0]; deck.RemoveAt(0);
        var obj = Runner.Spawn(cardPrefab, Vector3.zero, Quaternion.identity,
            (r, o) => o.GetComponent<NetworkCard>().Initialize(id)
        );
        return obj;
    }

    private void Shuffle(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            int tmp = list[i]; list[i] = list[r]; list[r] = tmp;
        }
    }
}