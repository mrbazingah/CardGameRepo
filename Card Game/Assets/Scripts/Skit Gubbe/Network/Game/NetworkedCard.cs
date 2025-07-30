using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkedCard : NetworkBehaviour
{
    [Networked] public byte Value { get; set; }
    [Networked] public bool FaceUp { get; set; }

    [SerializeField] SpriteRenderer frontRenderer;
    [SerializeField] GameObject coverObject;

    void Awake()
    {
        UpdateVisibility();
    }

    public override void Render()
    {
        UpdateVisibility();
    }

    void UpdateVisibility()
    {
        if (frontRenderer)
            frontRenderer.gameObject.SetActive(FaceUp);
        if (coverObject)
            coverObject.SetActive(!FaceUp);
    }
}
