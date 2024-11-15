using System.Collections.Generic;
using UnityEngine;

public class Card : MonoBehaviour
{
    [SerializeField] int cardValue;
    [SerializeField] GameObject back;
    [SerializeField] GameObject visuals;
    [SerializeField] List<GameObject> cardStack;

    public void SetValue(int value)
    {
        cardValue = value;
    }

    public void ApplyChild(GameObject newChild)
    {
        back = newChild;
    }

    public void RemoveChild()
    {
        if (back != null)
        {
            Destroy(back);
        }
    }

    public GameObject GetBack()
    {
        return back;
    }

    public GameObject GetVisuals()
    {
        return visuals;
    }

    public int GetValue()
    {
        return cardValue;
    }
}
