using System.Collections.Generic;
using UnityEngine;

public class Card : MonoBehaviour
{
    [SerializeField] int cardValue;
    [SerializeField] GameObject child;
    [SerializeField] List<GameObject> cardStack;

    public void SetValue(int value)
    {
        cardValue = value;
    }

    public void ApplyChild(GameObject newChild)
    {
        child = newChild;
    }

    public void RemoveChild()
    {
        if (child != null)
        {
            Destroy(child);
        }
    }

    public GameObject GetChild()
    {
        return child;
    }

    public int GetValue()
    {
        return cardValue;
    }
}
