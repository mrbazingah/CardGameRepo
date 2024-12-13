using System.Collections.Generic;
using UnityEngine;

public class DeckSkin : MonoBehaviour
{
    [SerializeField] List<Sprite> myDeck;

    public List<Sprite> GetMyDeck()
    {
        return myDeck;
    }
}
