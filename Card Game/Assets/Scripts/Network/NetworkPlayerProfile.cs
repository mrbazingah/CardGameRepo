using TMPro;
using UnityEngine;

public class NetworkPlayerProfile : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI displayName;
    [SerializeField] GameObject readyMarker;

    public string PlayerId { get; private set; }

    public void Setup(string playerId, string name, bool isReady)
    {
        PlayerId = playerId;
        displayName.text = name;
        readyMarker.SetActive(isReady);
    }

    public void SetReady(bool isReady)
    {
        readyMarker.SetActive(isReady);
    }
}
