using System.Collections.Generic;
using UnityEngine;

public class SkinManager : MonoBehaviour
{
    [SerializeField] List<GameObject> decks;
    [SerializeField] int deckEquipedIndex;

    List<Sprite> spritesInDeck;

    public void EquipDeck(int index)
    {
        deckEquipedIndex = index;
        spritesInDeck = decks[deckEquipedIndex].GetComponent<DeckSkin>().GetMyDeck();

        PlayerPrefs.SetInt("DeckEquipedIndex", deckEquipedIndex);
    }

    public List<Sprite> GetEquipedDeck()
    {
        deckEquipedIndex = PlayerPrefs.GetInt("DeckEquipedIndex");
        EquipDeck(deckEquipedIndex);

        return spritesInDeck;
    }

    public List<GameObject> GetDecks()
    {
        return decks;
    }
}
