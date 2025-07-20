using Fusion;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(NetworkObject))]
public class PlayerEntity : NetworkBehaviour
{
    private PlayerRef owner;
    private List<NetworkCard> hand = new List<NetworkCard>();

    public void Init(PlayerRef _owner)
    {
        owner = _owner;
        Object.AssignInputAuthority(owner);
    }

    public override void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (!Object.HasInputAuthority) return;
        var data = new PlayerInputData();
        if (UIManager.Instance.TryGetPlay(out int idx))
        {
            data.playCard = true;
            data.cardIndex = idx;
        }
        input.Set(data);
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out PlayerInputData d)) return;
        if (d.playCard && d.cardIndex < hand.Count)
            PlayCardRPC(d.cardIndex);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void PlayCardRPC(int idx)
    {
        var card = hand[idx].Object;
        NetworkGameManager.Instance.PlayCardRequest(this, card);
    }

    public Vector3 GetHandPosition(int index) =>
        UIManager.Instance.GetHandSlotPosition(owner, index);

    public void AddCardToHand(NetworkCard c) => hand.Add(c);
}
