using System;
using TMPro;
using UnityEngine;

public class CardShopSlot : MonoBehaviour
{
    [SerializeField] GameObject deckSpriteHolder;
    [SerializeField] Sprite deckImage;
    [SerializeField] GameObject buyButton, equipButton;
    [SerializeField] int cost;
    [SerializeField] bool hasBeenBought;
    [SerializeField] bool isEqupied;

    int thisIndex;

    TextMeshProUGUI equipText;

    ShopManager shopManager;
    SkinManager skinManager;

    void Awake()
    {
        shopManager = FindFirstObjectByType<ShopManager>();
        skinManager = FindFirstObjectByType<SkinManager>();
        equipText = equipButton.GetComponentInChildren<TextMeshProUGUI>();

        for (int i = 0; i < skinManager.GetDecks().Count; i++)
        {
            if (skinManager.GetDecks()[i] == deckSpriteHolder)
            {
                thisIndex = i;
                break;
            }
        }
    }

    void Start()
    {
        isEqupied = skinManager.GetEquipedDeck() == deckSpriteHolder.GetComponent<DeckSkin>().GetMyDeck();

        hasBeenBought = hasBeenBought ? true : PlayerPrefs.HasKey(gameObject.name + "HasBeenBought");

        LoadEquipButton(hasBeenBought);

        if (buyButton != null)
        {
            buyButton.GetComponentInChildren<TextMeshProUGUI>().text = cost.ToString();
        }
    }

    void Update()
    {
        equipText.text = isEqupied ? "Equiped" : "Equip";
    }

    public void ShowDeck()
    {
        shopManager.OpenDeckImage(deckImage);
    }

    public void EquipThisDeck()
    {
        isEqupied = true;
        skinManager.EquipDeck(thisIndex, this);
    }

    public void UnEquipDeck()
    {
        isEqupied = false;
    }

    public void BuyDeck()
    { 
        int money = PlayerPrefs.GetInt("Score");
        if (money < cost || hasBeenBought) { return; }

        hasBeenBought = true;
        PlayerPrefs.SetInt(gameObject.name + "HasBeenBought", 1);

        LoadEquipButton(true);
    }

    void LoadEquipButton(bool b)
    {
        if (!hasBeenBought || buyButton == null || equipButton == null) { return; }

        equipButton?.SetActive(b);
        buyButton?.SetActive(!b);
    }

    public int GetThisIndex()
    {
        return thisIndex;
    }
}
