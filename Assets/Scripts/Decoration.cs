using UnityEngine;

public class Decoration : MonoBehaviour
{
    enum AnimationState { Dead, Sitting, Lying};
    [SerializeField] GameObject[] gameObjects;
    [SerializeField] Animator[] animators;
    [SerializeField] AnimationState state;

    void Start()
    {
        for (int i = 0; i < gameObjects.Length; i++)
        {
            if (gameObjects[i].activeSelf)
            {
                animators[i].Play(state.ToString());
                return;
            }
        }
    }
}