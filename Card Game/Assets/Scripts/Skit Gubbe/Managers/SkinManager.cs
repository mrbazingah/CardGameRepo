using System.Collections.Generic;
using UnityEngine;

public class SkinManager : MonoBehaviour
{
    [SerializeField] List<GameObject> decks;
    [SerializeField] int deckEquipedIndex;
    [SerializeField] List<Sprite> spritesInDeck;
    [SerializeField] List<CardShopSlot> slots;

    CardShopSlot slot;

    void Start()
    {
        deckEquipedIndex = PlayerPrefs.GetInt("DeckEquipedIndex");
        CardShopSlot tempSlot = null;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) { break; }

            if (slots[i].GetThisIndex() == deckEquipedIndex)
            {
                tempSlot = slots[i];
                break;
            }
        }

        EquipDeck(deckEquipedIndex, tempSlot);
    }

    public void EquipDeck(int index, CardShopSlot thisSlot)
    {
        if (thisSlot == slot && slot != null)
        {
            return;
        }

        if (slot != null)
        {
            slot.UnEquipDeck();
        }
        
        deckEquipedIndex = index;
        slot = thisSlot;

        if (PlayerPrefs.GetInt("DeckEquipedIndex") != deckEquipedIndex)
        {
            PlayerPrefs.SetInt("DeckEquipedIndex", deckEquipedIndex);
        }
    }

    public List<Sprite> GetEquipedDeck()
    {
        return decks[deckEquipedIndex].GetComponent<DeckSkin>().GetMyDeck();
    }

    public List<GameObject> GetDecks()
    {
        return decks;
    }
}
