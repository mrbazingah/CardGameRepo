using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class NetworkPile : NetworkBehaviour
{
    [SerializeField] Transform pileTransform;
    [SerializeField] GameObject pileCardPrefab;
    List<byte> pileValues = new List<byte>();

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AddToPile(byte value, RpcInfo info = default)
    {
        pileValues.Add(value);
        if (pileCardPrefab) { var go = Instantiate(pileCardPrefab, pileTransform.position, Quaternion.identity, pileTransform); var sr = go.GetComponent<SpriteRenderer>(); if (sr) sr.sortingOrder = pileValues.Count - 1; }
    }

    public byte GetTopValue() { return pileValues.Count > 0 ? pileValues[pileValues.Count - 1] : (byte)0; }
}