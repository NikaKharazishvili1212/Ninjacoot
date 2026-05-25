using UnityEngine;

public class OptimizableObject : MonoBehaviour
{
    [SerializeField] Component[] components;

    void Awake() => Toggle(false);

    public void Toggle(bool state)
    {
        foreach (var c in components)
        {
            if (c is Transform t) t.gameObject.SetActive(state);
            else if (c is Behaviour b) b.enabled = state;
            else if (c is Renderer r) r.enabled = state;
            else if (c is ParticleSystem ps) { if (state) ps.Play(); else ps.Stop(); }
        }
    }
}