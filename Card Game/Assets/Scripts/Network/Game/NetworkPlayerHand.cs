using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerHand : NetworkBehaviour
{
    public void AddHandCards(GameObject newCard) { }
    public void SetUnderSideCards(List<GameObject> newCards) { }
    public void SetOverSideCards(List<GameObject> newCards) { }
    public void SortHandCards() { }
    public bool CanChance() => false;
    public bool GetTurn() => false;
    public List<GameObject> GetCurrentCards() => new List<GameObject>();
    public List<GameObject> GetHandCards() => new List<GameObject>();
    public List<GameObject> GetOverSideCards() => new List<GameObject>();
    public List<GameObject> GetUnderSideCards() => new List<GameObject>();
}
