using System.Collections.Generic;
using UnityEngine;

public class Card : MonoBehaviour
{
    [SerializeField] int cardValue;
    [SerializeField] GameObject back;
    [SerializeField] List<GameObject> cardStack;

    float popUpHeight;
    float zeroPoint;

    BoxCollider2D myBoxCollider;
    PlayerHand player;

    void Awake()
    {
        myBoxCollider = GetComponent<BoxCollider2D>();
        player = FindFirstObjectByType<PlayerHand>();
    }

    void Start()
    {
        popUpHeight = player.GetPopUpHeight();
    }

    void Update()
    {
        UpdateCollider();
    }

    void UpdateCollider()
    {
        myBoxCollider.offset = new Vector2(myBoxCollider.offset.x, 0);
    }

    public void SetZeroPoint()
    {
        zeroPoint = gameObject.transform.position.y;
    }

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

    public int GetValue()
    {
        return cardValue;
    }
}
