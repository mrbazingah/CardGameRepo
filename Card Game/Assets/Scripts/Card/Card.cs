using UnityEngine;

public class Card : MonoBehaviour
{
    [SerializeField] int cardValue;
    [SerializeField] GameObject back;
    [SerializeField] GameObject highlight;
    [SerializeField] float handZeroPoint;
    [SerializeField] float sideZeroPoint;

    public Vector2 basePosition;

    bool hasBeenTurned;

    BoxCollider2D myBoxCollider;
    PlayerHand player;

    void Awake()
    {
        myBoxCollider = GetComponent<BoxCollider2D>();
        player = FindFirstObjectByType<PlayerHand>();
    }

    void Start()
    {
        SetHighlight(false);
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
        else if (player.GetOverSideCards().Contains(gameObject) || player.GetUnderSideCards().Contains(gameObject))
        {
            float currentPosition = gameObject.transform.position.y;
            myBoxCollider.offset = new Vector2(myBoxCollider.offset.x, sideZeroPoint - currentPosition);
        }
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

    public void Rotate(float rot, bool b)
    {
        transform.rotation = Quaternion.Euler(0, 0, rot);
        hasBeenTurned = b;
    }

    public void SetHighlight(bool b)
    {
        highlight.SetActive(b);
    }

    public GameObject GetBack()
    {
        return back;
    }

    public int GetValue()
    {
        return cardValue;
    }

    public bool GetHasBeenTurned()
    {
        return hasBeenTurned;
    }
}
