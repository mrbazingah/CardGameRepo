using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pile : MonoBehaviour
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

        newCard.GetComponent<Card>().RemoveChild();
    }

    public IEnumerator DiscardCardsInPile()
    {
        yield return new WaitForSeconds(discardDelay);

        discardedPile = cardsInPile;
        cardsInPile = new List<GameObject>(0);

        audioManager.PlayShufflingSFX();
    }

    public void ClearPile()
    {
        for (int i = 0; i < cardsInPile.Count; i++)
        {
            cardsInPile[i].transform.SetParent(null);
        }

        cardsInPile = new List<GameObject>(0);
    }

    public int GetCurrentCard(bool isChance)
    {
        int currentValue = 0;

        if (cardsInPile.Count != 0 && !isChance)
        {
            currentValue = cardsInPile[cardsInPile.Count - 1].GetComponent<Card>().GetValue();
        }
        else if (cardsInPile.Count != 0 && isChance)
        {
            currentValue = cardsInPile[cardsInPile.Count - 2].GetComponent<Card>().GetValue();
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
