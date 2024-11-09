using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] float offset;
    [SerializeField] float speed;
    [SerializeField] float max, min;
    [SerializeField] GameObject leftButton, rightButton;
    [SerializeField] Color canPressColor, cantPressColor;

    int pageIndex;
    Vector2 endPos;

    void Start()
    {
        transform.position = Vector2.zero;   
    }

    void Update()
    {
        UpdatePages();
        UpdateColors();
    }

    void UpdatePages()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            GoRight();
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            GoLeft();
        }

        Vector2 startPos = transform.position;
        transform.position = Vector2.Lerp(startPos, endPos, speed);
    }

    void UpdateColors()
    {
        Color leftButtonColor;
        if (endPos.x == max)
        {
            leftButtonColor = cantPressColor;
        }
        else
        {
            leftButtonColor = canPressColor;
        }

        leftButton.GetComponent<Image>().color = leftButtonColor;
        leftButton.GetComponentInChildren<TextMeshProUGUI>().color = leftButtonColor;

        Color rightButtonColor;
        if (endPos.x == min)
        {
            rightButtonColor = cantPressColor;
        }
        else
        {
            rightButtonColor = canPressColor;
        }

        rightButton.GetComponent<Image>().color = rightButtonColor;
        rightButton.GetComponentInChildren<TextMeshProUGUI>().color = rightButtonColor;
    }

    public void GoLeft()
    {
        if (endPos.x == max) return;

        pageIndex--;
        endPos.x = 0f - offset * pageIndex;
    }

    public void GoRight()
    {
        if (endPos.x == min) return;

        pageIndex++;
        endPos = new Vector2(0f - offset * pageIndex, 0f);
    }
}
