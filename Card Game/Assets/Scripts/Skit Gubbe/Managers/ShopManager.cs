using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    [SerializeField] GameObject deckImageGameObject;
    [SerializeField] Image deckImage;

    void Start()
    {
        deckImageGameObject.SetActive(false);
    }

    public void OpenDeckImage(Sprite deckSprite)
    {
        deckImageGameObject.SetActive(true);
        deckImage.sprite = deckSprite;
    }

    public void CloseDeckImage()
    {
        deckImageGameObject.SetActive(false);
    }
}
