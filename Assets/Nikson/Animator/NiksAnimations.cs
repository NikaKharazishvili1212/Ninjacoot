using UnityEngine;

// To create Scriptable Object: right click > create > Scriptable Object > NiksAnimations
[CreateAssetMenu(fileName = "NiksAnimations", menuName = "Scriptable Object/NiksAnimations")]
public class NiksAnimations : ScriptableObject
{
    public AnimationClip[] Animations;
}