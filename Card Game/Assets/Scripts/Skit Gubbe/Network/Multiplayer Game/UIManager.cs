using UnityEngine;
using Fusion;

public class UIManager
{
    public static UIManager Instance { get; } = new UIManager();
    // Called in PlayerEntity.OnInput
    public bool TryGetPlay(out int idx)
    {
        idx = 0;
        return false; // stub
    }
    // Called in PlayerEntity.GetHandPosition
    public Vector3 GetHandSlotPosition(PlayerRef _, int __) => Vector3.zero;
}

public class CardSkinManager
{
    public static CardSkinManager Instance { get; } = new CardSkinManager();
    public Sprite GetSprite(int value) => null; // stub
}
