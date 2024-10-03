using System.Collections.Generic;
using UnityEngine;

public class Pile : MonoBehaviour
{
    [SerializeField] List<GameObject> cardsInPile;
    [SerializeField] List<GameObject> discardedPile;
    [SerializeField] Transform pileTransform;

    public void AddCardsToPile(GameObject newCard)
    {
        newCard.transform.SetParent(pileTransform);

        cardsInPile.Add(newCard);
        newCard.transform.localPosition = Vector3.zero;

        for (int i = 0; i < cardsInPile.Count; i++)
        {
            cardsInPile[i].GetComponent<SpriteRenderer>().sortingOrder = i;
        }

        newCard.GetComponent<Card>().RemoveChild();
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
        for (int i = 0; i < cardsInPile.Count; i++)
        {
            cardsInPile[i].transform.position = new Vector2(100, 100);
            cardsInPile[i].transform.SetParent(null);
        }

        cardsInPile = new List<GameObject>(0);
    }

    public bool ShouldDiscardPile()
    {
        List<int> fourCards = new List<int>(4);
        int count = 0;
        for (int i = cardsInPile.Count - 1; i >= 0; i--)
        {
            if (count < 4 && cardsInPile.Count >= 4)
            {
                count++;
                fourCards.Add(cardsInPile[i].GetComponent<Card>().GetValue());
            }
            else
            {
                break;
            }
        }

        if (fourCards[0] == fourCards[1] && fourCards[1] == fourCards[2] && fourCards[2] == fourCards[3])
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public List<GameObject> GetCardsInPile()
    {
        return cardsInPile;
    }
}
