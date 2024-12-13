using System.Collections.Generic;
using UnityEngine;

public class CardShopSlot : MonoBehaviour
{
    [SerializeField] List<Sprite> deckSprites;
    [SerializeField] Sprite deckImage;
     
    ShopManager shopManager;
    SkinManager skinManager;

    void Awake()
    {
        shopManager = FindFirstObjectByType<ShopManager>();
        skinManager = FindFirstObjectByType<SkinManager>();
    }

    public void ShowDeck()
    {
        shopManager.OpenDeckImage(deckImage);
    }

    public void EquipThisDeck()
    {
        int index = 0;

        for (int i = 0; skinManager.GetDecks().Count > 0; i++)
        {
            if (skinManager.GetDecks()[i].GetComponent<DeckSkin>().GetMyDeck() == deckSprites)
            {
                index = i;
                break;
            }
        }

        skinManager.EquipDeck(index);
    }
}
