using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using TMPro;

// Static utility methods for common game operations
public static partial class Utils
{
    #region Probability
    // Returns true if a random roll succeeds for the given percent
    // Usage: if (Chance(30)) print("it succeeded");
    public static bool Chance(int percent) => Random.Range(0, 100) < percent;

    // Invokes group of actions if the percent chance succeeds
    // Usage: InvokeChance(30, () => { action1(); action2(); });
    public static void InvokeChance(int percent, UnityAction action) { if (Random.Range(0, 100) < percent) action?.Invoke(); }

    // Picks one random action group from the list and invokes it
    // Usage: InvokeRandom(() => { action1(); action2(); }, () => { action3(); action4(); }, () => action5());
    public static void InvokeRandom(params UnityAction[] actions) => actions[Random.Range(0, actions.Length)]?.Invoke();

    // Returns a random element from any IList
    // Usage: audioSource.PlayOneShot(GetRandomElement(sounds));
    public static T GetRandomElement<T>(IList<T> collection) => collection[Random.Range(0, collection.Count)];
    #endregion


    #region GameObject & Component
    // Safely gets a component, adding it if missing
    // Usage: rb = GetOrAddComponent<Rigidbody2D>(gameObject);
    public static T GetOrAddComponent<T>(GameObject obj) where T : Component => obj.TryGetComponent(out T comp) ? comp : obj.AddComponent<T>();

    // Returns the first inactive GameObject in the pool
    // Usage: PoolGameObject(gameObjects)?.SetActive(true); or GameObject object = PoolGameObject(gameObjects);
    public static GameObject PoolGameObject(GameObject[] pool)
    {
        foreach (var go in pool) if (!go.activeInHierarchy) return go;
        return null;
    }

    // Returns the first inactive component from the pool
    // Usage: PoolComponent(bullets)?.Shoot(target);
    public static T PoolComponent<T>(T[] pool) where T : Component
    {
        foreach (var comp in pool) if (!comp.gameObject.activeInHierarchy) return comp;
        return null;
    }
    #endregion


    #region Transform, Math & Geometry
    // Checks if given collider is within self range using ClosestPoint for accuracy (usually used to check if character is close enough its target to attack)
    public static bool IsInRange(Transform self, Collider target, float attackRange) => Vector3.Distance(self.position, target.ClosestPoint(self.position)) <= attackRange;

    // Smoothly rotates towards given position (usually used for rotating character towards target when attacking)
    public static void RotateTowards(Transform self, Vector3 givenPos, float rotationSpeed = 10)
    {
        Vector3 direction = (givenPos - self.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        self.rotation = Quaternion.Slerp(self.rotation, lookRotation, Time.deltaTime * rotationSpeed);
    }

    // Returns a random point within radius of origin. (useful for patrols, spawning, scatter effects)
    public static Vector3 GetRandomPointInRadius(Vector3 origin, float radius) => Random.insideUnitSphere * radius + origin;

    // Moves a transform upward in world space at given speed
    public static void MoveTransformUp(Transform self, float speed) => self.Translate(Vector3.up * speed * Time.deltaTime);

    // Makes a transform always face the camera and maintain a constant screen size regardless of distance (usually used for character's 3D head UI)
    public static void SetScreenSizeBillboard(Transform self, Transform camera, float screenSize)
    {
        self.rotation = camera.rotation;
        float scaleValue = Vector3.Dot(self.position - camera.position, camera.forward) * screenSize;
        self.localScale = new Vector3(scaleValue, scaleValue, scaleValue);
    }

    // Returns true if any of 4 raycasts at capsule collider's feet touch the ground. Visualized in Scene view as green (grounded) or red (not grounded)
    // (usually used to determine if character can jump and to drive grounded animations)
    public static bool IsGrounded(CapsuleCollider collider, float rayLength = 0.3f, LayerMask? mask = null)
    {
        float radius = collider.radius * 0.9f;
        Vector3 bottom = collider.transform.position + collider.center + Vector3.down * (collider.height / 2f - 0.1f); // Slightly above bottom edge to avoid floating point misses
        Vector3[] origins = new Vector3[]{
        bottom + new Vector3( radius, 0,  0),
        bottom + new Vector3(-radius, 0,  0),
        bottom + new Vector3(0,       0,  radius),
        bottom + new Vector3(0,       0, -radius),};

        bool grounded = false;
        foreach (var origin in origins)
        {
            bool hit = mask.HasValue ? Physics.Raycast(origin, Vector3.down, rayLength, mask.Value) : Physics.Raycast(origin, Vector3.down, rayLength);
            Debug.DrawRay(origin, Vector3.down * rayLength, hit ? Color.green : Color.red);
            if (hit) grounded = true;
        }
        return grounded;
    }
    #endregion



    #region Strings & Formattings
    // Returns colored rich text string
    public static string ColoredText(string text, Color32 color) => $"<color=#{color.r:X2}{color.g:X2}{color.b:X2}>{text}</color>";
    public static string ColoredText(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    // Formats health values as "Current / Max" for health displays
    public static string FormatHealth(float current, float max) => $"{current:N0} / {max:N0}";

    // Formats health values as "Current (Percentage%)" for health displays
    public static string FormatHealthPercent(float current, float max) => $"{current:N0} ({Mathf.Round(100 * current / max)}%)";

    // Formats number with thousand separators. Usage: 1000000.WithSeparators(); → "1,000,000"
    public static string WithSeparators(this int n) => $"{n:N0}";

    // Pads number with leading zeros to given digits. Usage: ZeroPadded(2, 3) → "002"
    public static string ZeroPadded(int n, int digits) => n.ToString($"D{digits}");
    #endregion


    #region Scene & UI
    // Checks if UI is blocking mouse input; ignores elements with disabled Raycast Target
    public static bool IsUIBlockingInput() => EventSystem.current.IsPointerOverGameObject(PointerInputModule.kMouseLeftId);

    // Loads a scene asynchronously with optional loading bar and text updates and brief delay
    // Usage: StartCoroutine(LoadSceneAsync("SceneName", loadingBar, loadingText, 0.5f));
    public static IEnumerator LoadSceneAsync(string sceneName, Image loadingBar = null, TextMeshProUGUI loadingText = null, float briefDelay = 0)
    {
        // Start loading the scene asynchronously and prevent the scene from switching automatically
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        // Wait until the scene is loaded to 90% (Unity loads 0.9 max before activation)
        while (operation.progress < 0.9f)
        {
            float progress = operation.progress / 0.9f; // Normalize progress to 0-1 range
            if (loadingText) loadingText.text = $"Loading {Mathf.RoundToInt(progress * 100)}%";
            if (loadingBar) loadingBar.fillAmount = progress;
            yield return null;
        }

        // Scene is ready - show 100% briefly then activate
        if (loadingText) loadingText.text = "Loading 100%";
        if (loadingBar) loadingBar.fillAmount = 1;
        yield return new WaitForSeconds(briefDelay);
        operation.allowSceneActivation = true;
    }

    // Fades a CanvasGroup in or out over duration. Usage: StartCoroutine(Fade(canvasGroup, 0f, 1f, 0.5f));
    public static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        group.alpha = to;
    }

    // Punches a transform's local scale for a pop/bounce effect, then restores it. Usage: StartCoroutine(PunchScale(transform, 1f, 0.15f));
    public static IEnumerator PunchScale(Transform t, float peakScale, float duration)
    {
        Vector3 original = t.localScale;
        float half = duration * 0.5f;
        for (float e = 0; e < half; e += Time.deltaTime)
        {
            t.localScale = Vector3.LerpUnclamped(original, original * peakScale, e / half);
            yield return null;
        }
        for (float e = 0; e < half; e += Time.deltaTime)
        {
            t.localScale = Vector3.LerpUnclamped(original * peakScale, original, e / half);
            yield return null;
        }
        t.localScale = original;
    }
    #endregion


    #region Application
    // Application.Quit() is a no-op in Editor but still triggers OnApplicationQuit()
    public static void QuitApplication()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    #endregion
}