using Fusion;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(NetworkObject))]
public class NetworkPile : NetworkBehaviour
{
    [Networked] private NetworkLinkedList<NetworkObject> cards { get; }

    // Remove this whole override—
    // public override void Spawned()
    // {
    //     if (Object.HasStateAuthority)
    //         cards = new NetworkLinkedList<NetworkObject>();
    // }

    public void AddCard(NetworkObject card)
    {
        if (!Object.HasStateAuthority) return;
        cards.Add(card);
    }

    public List<NetworkObject> GetCards() => new List<NetworkObject>(cards);

    public void Clear()
    {
        if (!Object.HasStateAuthority) return;
        cards.Clear();
    }
}
