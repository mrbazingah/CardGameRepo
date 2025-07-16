using Fusion;
using TMPro;
using UnityEngine;

public class PlayerProfileNetwork : NetworkBehaviour
{
    [Networked] public string DisplayName { get; set; }

    public override void Spawned()
    {
        TMP_Text nameText = GetComponentInChildren<TMP_Text>();
        if (nameText != null)
        {
            nameText.text = DisplayName;
        }
    }

    public override void Render()
    {
        TMP_Text nameText = GetComponentInChildren<TMP_Text>();
        if (nameText != null)
        {
            nameText.text = DisplayName;
        }
    }
}
