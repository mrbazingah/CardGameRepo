using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkCard : NetworkBehaviour
{
    [Networked] private int value { get; set; }
    [Networked] private bool isFaceUp { get; set; }

    [SerializeField] private SpriteRenderer faceRenderer;
    [SerializeField] private SpriteRenderer backRenderer;

    public void Initialize(int cardValue)
    {
        if (Object.HasStateAuthority)
        {
            value = cardValue;
            isFaceUp = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        faceRenderer.sprite = CardSkinManager.Instance.GetSprite(value);
        faceRenderer.enabled = isFaceUp;
        backRenderer.enabled = !isFaceUp;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RpcFlipCard()
    {
        if (Object.HasStateAuthority)
            isFaceUp = !isFaceUp;
    }

    public int GetValue() => value;
}
