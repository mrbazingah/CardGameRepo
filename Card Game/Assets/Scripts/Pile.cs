using System.Collections.Generic;
using UnityEngine;

public class Pile : MonoBehaviour
{
    [SerializeField] List<GameObject> cardsInPile;
    [SerializeField] List<GameObject> discardedPile;
    [SerializeField] Transform pileTransform;
    [SerializeField] Transform discardedPileTransform;

    public void AddCardsToPile(GameObject newCard)
    {
        newCard.transform.SetParent(pileTransform);

        cardsInPile.Add(newCard);
        newCard.transform.localPosition = Vector3.zero;

        for (int i = 0; i < cardsInPile.Count; i++)
        {
            cardsInPile[i].GetComponent<SpriteRenderer>().sortingOrder = i;
        }
    }

    public int GetCurrentCard()
    {
        int currentValue = 0;

        if (cardsInPile.Count != 0)
        {
           currentValue = cardsInPile[cardsInPile.Count - 1].GetComponent<Card>().GetValue();
        }

        return currentValue;
    }

    public void DiscardCardsInPile()
    {
        discardedPile = cardsInPile;
        cardsInPile = new List<GameObject>(0);

        for (int i = 0; i < discardedPile.Count; i++)
        {
            discardedPile[i].transform.position = new Vector2(100, 100);
        }
    }

    public void ClearPile()
    {
        cardsInPile = new List<GameObject>(0);
    }

    public List<GameObject> GetCardsInPile()
    {
        return cardsInPile;
    }
}
