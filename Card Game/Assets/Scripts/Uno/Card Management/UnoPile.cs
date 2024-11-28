using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnoPile : MonoBehaviour
{
    [SerializeField] List<GameObject> cardsInPile;
    [SerializeField] List<GameObject> discardedPile;
    [SerializeField] Transform pileTransform;
    [SerializeField] float discardDelay;
    [SerializeField] float lerpSpeed;

    AudioManager audioManager;

    void Awake()
    {
        audioManager = FindFirstObjectByType<AudioManager>();
    }

    void Update()
    {
        LerpCardsToPile();
    }

    void LerpCardsToPile()
    {
        for (int i = 0; i < cardsInPile.Count; i++)
        {
            cardsInPile[i].transform.position = Vector2.Lerp(cardsInPile[i].transform.position, pileTransform.position, lerpSpeed * Time.deltaTime);
        }

        for (int i = 0; i < discardedPile.Count; i++)
        {
            discardedPile[i].transform.position = Vector2.Lerp(discardedPile[i].transform.position, new Vector2(-10, 0), lerpSpeed * Time.deltaTime);
        }
    }

    public void AddCardsToPile(GameObject newCard)
    {
        newCard.transform.SetParent(pileTransform);

        cardsInPile.Add(newCard);

        for (int i = 0; i < cardsInPile.Count; i++)
        {
            cardsInPile[i].GetComponent<SpriteRenderer>().sortingOrder = i;
        }

        newCard.GetComponent<UnoCard>().RemoveChild();
    }

    public int GetCurrentCard()
    {
        int currentValue = 0;

        if (cardsInPile.Count != 0)
        {
            currentValue = cardsInPile[cardsInPile.Count - 1].GetComponent<UnoCard>().GetValue();
        }

        return currentValue;
    }

    public Transform GetPilePosition()
    {
        return pileTransform;
    }

    public List<GameObject> GetCardsInPile()
    {
        return cardsInPile;
    }
}
