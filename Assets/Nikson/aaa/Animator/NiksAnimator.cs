using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class NiksAnimator : MonoBehaviour
{
    [SerializeField] Animator Animator;
    public string CurrentlyPlayedClipName;

    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable currentClip;
    AnimationClipPlayable nextClip;

    float fadeTime;
    float fadeTimer;
    bool isFading;

    void Reset() => Animator = GetComponent<Animator>();

    void Awake()
    {
        graph = PlayableGraph.Create("NiksAnimator");
        var output = AnimationPlayableOutput.Create(graph, "Animation", Animator);

        mixer = AnimationMixerPlayable.Create(graph, 2);
        output.SetSourcePlayable(mixer);

        graph.Play();
    }

    void Update()
    {
        if (!isFading) return;

        fadeTimer += Time.deltaTime;
        float t = Mathf.Clamp01(fadeTimer / fadeTime);

        // Use smoothstep for more natural interpolation
        t = t * t * (3f - 2f * t);

        mixer.SetInputWeight(0, 1f - t);
        mixer.SetInputWeight(1, t);

        if (t >= 1f) CompleteFade();
    }

    void CompleteFade()
    {
        isFading = false;

        if (currentClip.IsValid())
        {
            mixer.DisconnectInput(0);
            currentClip.Destroy();
        }

        currentClip = nextClip;
        nextClip = default;

        // Reconnect to slot 0 with full weight
        mixer.DisconnectInput(1);
        if (currentClip.IsValid())
        {
            graph.Connect(currentClip, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
        }
        mixer.SetInputWeight(1, 0f);
    }

    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

    public NiksAnimatorState Play(AnimationClip clip, float speed = 1f)
    {
        if (isFading) CompleteFade(); // Stop any ongoing fade

        if (currentClip.IsValid()) // Disconnect and destroy old playables
        {
            mixer.DisconnectInput(0);
            currentClip.Destroy();
        }
        if (nextClip.IsValid())
        {
            mixer.DisconnectInput(1);
            nextClip.Destroy();
        }

        currentClip = AnimationClipPlayable.Create(graph, clip);
        graph.Connect(currentClip, 0, mixer, 0);

        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);

        isFading = false;
        CurrentlyPlayedClipName = clip.name;
        currentClip.SetSpeed(speed);

        return new NiksAnimatorState(currentClip);
    }

    public NiksAnimatorState CrossFade(AnimationClip clip, float duration, float speed = 1f)
    {
        if (!currentClip.IsValid()) return Play(clip, speed);

        if (isFading) CompleteFade(); // If already fading, complete the current fade first

        // Clean up any existing nextClip
        if (nextClip.IsValid())
        {
            mixer.DisconnectInput(1);
            nextClip.Destroy();
        }

        nextClip = AnimationClipPlayable.Create(graph, clip);
        graph.Connect(nextClip, 0, mixer, 1);

        fadeTime = Mathf.Max(duration, 0.01f); // Ensure non-zero fade time
        fadeTimer = 0f;
        isFading = true;

        // Reset weights for new fade
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);

        CurrentlyPlayedClipName = clip.name;
        nextClip.SetSpeed(speed);

        return new NiksAnimatorState(nextClip);
    }
}

public class NiksAnimatorState
{
    AnimationClipPlayable playable;

    public NiksAnimatorState(AnimationClipPlayable playable) { this.playable = playable; }

    public float Speed
    {
        get => (float)playable.GetSpeed();
        set => playable.SetSpeed(value);
    }
}