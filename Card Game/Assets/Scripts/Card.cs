using UnityEngine;

public class Card : MonoBehaviour
{
    [SerializeField] int cardValue;

    public void SetValue(int value)
    {
        cardValue = value;
    }

    public int GetValue()
    {
        return cardValue;
    }
}
