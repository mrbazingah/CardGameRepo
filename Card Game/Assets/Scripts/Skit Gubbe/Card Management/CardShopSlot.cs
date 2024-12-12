using UnityEngine;

public class CardShopSlot : MonoBehaviour
{
    [SerializeField] Sprite[] deckSprites;
    [SerializeField] Sprite deckImage;
     
    ShopManager shopManager;

    void Awake()
    {
        shopManager = FindFirstObjectByType<ShopManager>();
    }

    public void ShowDeck()
    {
        shopManager.OpenDeckImage(deckImage);
    }
}
