using UnityEngine;

public class NetworkCard : MonoBehaviour
{
    [SerializeField] int cardValue;
    [SerializeField] int cardId;
    [SerializeField] GameObject back;
    [SerializeField] GameObject highlight;
    [SerializeField] float handZeroPoint;
    [SerializeField] float sideZeroPoint;
    [Space]
    [SerializeField] Color darkColor;

    public Vector2 basePosition;

    bool hasBeenTurned;

    BoxCollider2D myBoxCollider;
    NetworkPlayerHand player;
    SpriteRenderer mySpriteRenderer;
    SpriteRenderer highlightSpriteRenderer;

    void Awake()
    {
        myBoxCollider = GetComponent<BoxCollider2D>();
        player = FindFirstObjectByType<NetworkPlayerHand>();
        mySpriteRenderer = GetComponent<SpriteRenderer>();
        highlightSpriteRenderer = highlight.GetComponent<SpriteRenderer>();
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
        if (player == null) return;

        if (player.GetHandCards().Contains(gameObject))
        {
            float currentPosition = transform.position.y;
            myBoxCollider.offset = new Vector2(myBoxCollider.offset.x, handZeroPoint - currentPosition);
        }
        else if (player.GetOverSideCards().Contains(gameObject) || player.GetUnderSideCards().Contains(gameObject))
        {
            float currentPosition = transform.position.y;
            myBoxCollider.offset = new Vector2(myBoxCollider.offset.x, sideZeroPoint - currentPosition);
        }

        highlightSpriteRenderer.sortingOrder = mySpriteRenderer.sortingOrder - 1;
    }

    public void SetCardId(int id) => cardId = id;
    public int GetCardId() => cardId;

    public void SetValue(int value) => cardValue = value;
    public int GetValue() => cardValue;

    public void ApplyChild(GameObject newChild) => back = newChild;
    public void RemoveChild() { if (back != null) Destroy(back); }

    public void Rotate(float rot, bool b)
    {
        transform.rotation = Quaternion.Euler(0, 0, rot);
        hasBeenTurned = b;
    }

    public void SetHighlight(bool b) => highlight.SetActive(b);
    public void ChangeColor(bool active) => mySpriteRenderer.color = active ? Color.white : darkColor;
    public GameObject GetBack() => back;
    public bool GetHasBeenTurned() => hasBeenTurned;
}
