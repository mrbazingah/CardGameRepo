using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NetworkPlayerHand : NetworkBehaviour
{
    [SerializeField] Transform handTransform;
    [SerializeField] float baseCardSpacing = 150f;
    [SerializeField] float maxHandWidth = 1000f;
    [SerializeField] GameObject endTurnButton;
    NetworkRunner runner; GameManagerNetwork gm; List<NetworkedCard> hand = new List<NetworkedCard>();
    public int HandCount => hand.Count;
    public override void Spawned() { runner = Runner; gm = FindObjectOfType<GameManagerNetwork>(); if (HasInputAuthority && endTurnButton) endTurnButton.SetActive(false); }

    void Update()
    {
        if (!HasInputAuthority || !gm.GameStarted) return;
        bool myTurn = gm.CurrentTurn == Object.InputAuthority; if (endTurnButton) endTurnButton.SetActive(myTurn);
        if (myTurn && Input.GetMouseButtonDown(0)) { Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition); RaycastHit2D hit = Physics2D.Raycast(wp, Vector2.zero); if (hit.collider) { var netObj = hit.collider.GetComponentInParent<NetworkObject>(); if (netObj) { var nc = netObj.GetComponent<NetworkedCard>(); if (nc != null && hand.Contains(nc)) RPC_PlayCard(netObj); } } }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_PlayCard(NetworkObject cardObj, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;
        var nc = cardObj.GetComponent<NetworkedCard>(); nc.FaceUp = true; runner.Despawn(cardObj);
        var pile = FindObjectOfType<NetworkPile>(); pile.RPC_AddToPile(nc.Value);
        FindObjectOfType<GameManagerNetwork>().CheckProcessWin(); FindObjectOfType<GameManagerNetwork>().LocalEndTurn();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AddCardToHand(NetworkObject card, PlayerRef who, RpcInfo info = default)
    {
        if (who != Object.InputAuthority) return; LocalAdd(card);
    }

    public void LocalAdd(NetworkObject netObj) { var nc = netObj.GetComponent<NetworkedCard>(); hand.Add(nc); SortAndArrange(); }
    public int GetLowestValueExcluding(params byte[] excludes) { return hand.Where(c => !excludes.Contains(c.Value)).Select(c => (int)c.Value).DefaultIfEmpty(int.MaxValue).Min(); }

    void SortAndArrange()
    {
        hand = hand.OrderBy(c => c.Value).ToList(); int count = hand.Count; if (count == 0) return;
        float spacing = Mathf.Min(baseCardSpacing, maxHandWidth / count);
        for (int i = 0; i < count; i++) { var go = hand[i].gameObject; go.transform.SetParent(handTransform); float x = spacing * (i - (count - 1) / 2f); go.transform.localPosition = new Vector3(x, 0f, 0f); var sr = go.GetComponent<SpriteRenderer>(); if (sr) sr.sortingOrder = i; }
    }
}