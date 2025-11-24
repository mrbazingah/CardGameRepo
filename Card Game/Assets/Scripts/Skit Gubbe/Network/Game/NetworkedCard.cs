using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkedCard : NetworkBehaviour
{
    [Networked] public byte Value { get; set; }
    [Networked] public bool FaceUp { get; set; }
    [Networked] public bool IsSideCard { get; set; }
    [Networked] public bool IsChanceCard { get; set; }
    [Networked] public float Rotation { get; set; }

    [SerializeField] SpriteRenderer frontRenderer;
    [SerializeField] GameObject coverObject;

    void Awake()
    {
        UpdateVisibility();
    }

    public override void Render()
    {
        UpdateVisibility();
        if (Rotation != 0)
        {
            transform.rotation = Quaternion.Euler(0, 0, Rotation);
        }
    }

    void UpdateVisibility()
    {
        if (frontRenderer)
            frontRenderer.gameObject.SetActive(FaceUp);
        if (coverObject)
            coverObject.SetActive(!FaceUp);
    }

    public void SetRotation(float rot)
    {
        Rotation = rot;
    }
}
