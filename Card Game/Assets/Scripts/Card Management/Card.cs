using UnityEngine;

public class Card : MonoBehaviour
{
    [SerializeField] int cardValue;
    [SerializeField] GameObject back;
    [SerializeField] float zeroPoint;

    BoxCollider2D myBoxCollider;
    PlayerHand player;

    void Awake()
    {
        myBoxCollider = GetComponent<BoxCollider2D>();
        player = FindFirstObjectByType<PlayerHand>();
    }

    void Update()
    {
        UpdateCollider();
    }

    void UpdateCollider()
    {
        if (!player.GetCards().Contains(gameObject) || player.GetCardsIndex() != 2) { return; }

        float currentPosition = gameObject.transform.position.y;
        myBoxCollider.offset = new Vector2(myBoxCollider.offset.x, zeroPoint - currentPosition);
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
