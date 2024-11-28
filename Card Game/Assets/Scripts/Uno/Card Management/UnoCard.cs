using UnityEngine;

public class UnoCard : MonoBehaviour
{
    [SerializeField] int cardValue, colorIndex;
    [SerializeField] GameObject back;
    [SerializeField] float handZeroPoint;

    BoxCollider2D myBoxCollider;
    UnoPlayerHand player;

    void Awake()
    {
        myBoxCollider = GetComponent<BoxCollider2D>();
        player = FindFirstObjectByType<UnoPlayerHand>();
    }

    void Update()
    {
        UpdateCollider();
    }

    void UpdateCollider()
    {
        if (player.GetHandCards().Contains(gameObject))
        {
            float currentPosition = gameObject.transform.position.y;
            myBoxCollider.offset = new Vector2(myBoxCollider.offset.x, handZeroPoint - currentPosition);
        }
    }

    public void SetValue(int value)
    {
        cardValue = value;
    }

    public void SetColor(int color)
    {
        colorIndex = color; 
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
