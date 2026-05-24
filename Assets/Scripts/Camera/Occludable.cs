using UnityEngine;
using static GameConstants;

// Attach to any object that should fade when standing between camera and player
[RequireComponent(typeof(Renderer))]
public class Occludable : MonoBehaviour
{
    new Renderer renderer;
    MaterialPropertyBlock mpb;
    static readonly int ColorID = Shader.PropertyToID("_Color");

    void Awake()
    {
        renderer = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    public void Hide() => SetAlpha(HiddenOccludableAlpha);

    public void Show() => SetAlpha(1);

    void SetAlpha(float alpha)
    {
        renderer.GetPropertyBlock(mpb);
        Color c = renderer.material.color;  // fallback
        c.a = alpha;
        mpb.SetColor(ColorID, c);
        renderer.SetPropertyBlock(mpb);
    }
}