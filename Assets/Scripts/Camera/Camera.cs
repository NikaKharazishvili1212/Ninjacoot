using UnityEngine;
using System.Collections.Generic;
using static GameConstants;

public class Cam : MonoBehaviour
{
    public static Player Player; // Initialized by GameManager

    // Occlusion things
    [SerializeField] LayerMask occludableLayer;

    readonly Dictionary<int, Occludable> occluderCache = new();
    HashSet<Occludable> hiddenLastFrame = new();
    HashSet<Occludable> hitThisFrame = new();

    void Start()
    {
        // Sets camera's initial pitch (up-down tilt) and positions it behind and above the Player
        transform.rotation = Quaternion.Euler(CameraPitch, transform.rotation.eulerAngles.y, 0);
        transform.position = Player.transform.position - transform.forward * CameraZoom + Vector3.up * CameraUpPosition;
    }

    void Update()
    {
        FollowPlayer();
        HandleRotation();
        HandleOcclusion();
    }

    // Keep camera positioned behind and above the Player
    void FollowPlayer() => transform.position = Player.transform.position - transform.forward * CameraZoom + Vector3.up * CameraUpPosition;

    // Rotate camera with rotation hotkeys. Also slowly follow Player's horizontal movement
    void HandleRotation()
    {
        if (!Player.IsAlive) return;
        if (Mathf.Abs(Player.rotateCameraInput) > 0.1f)
        {
            Quaternion PlayerRotation = Quaternion.Euler(CameraPitch, transform.rotation.eulerAngles.y + Player.rotateCameraInput * CameraHotkeyRotationSpeed, 0);
            transform.rotation = Quaternion.Lerp(transform.rotation, PlayerRotation, Time.deltaTime * CameraHotkeyRotationSpeed);
        }
        else if (Mathf.Abs(Player.moveInput.x) > 0.1f)
        {
            Quaternion PlayerRotation = Quaternion.Euler(CameraPitch, transform.rotation.eulerAngles.y + Player.moveInput.x * CameraAutoRotationSpeed, 0);
            transform.rotation = Quaternion.Lerp(transform.rotation, PlayerRotation, Time.deltaTime * CameraAutoRotationSpeed);
        }
    }

    void HandleOcclusion()
    {
        hitThisFrame.Clear();
        CastAndCollectHits();
        RestoreNonHitObjects();
        (hiddenLastFrame, hitThisFrame) = (hitThisFrame, hiddenLastFrame); // swap, no alloc
    }

    void CastAndCollectHits()
    {
        Vector3 direction = Player.transform.position + transform.up - transform.position;
        RaycastHit[] hits = Physics.RaycastAll(transform.position, direction.normalized, direction.magnitude, occludableLayer);

        foreach (var hit in hits)
        {
            Occludable occludable = GetOrCacheOccludable(hit.collider);
            if (!occludable) continue;
            hitThisFrame.Add(occludable);
            if (!hiddenLastFrame.Contains(occludable)) occludable.Hide();
        }
        Debug.DrawLine(transform.position, Player.transform.position + transform.up, Color.cyan);
    }

    void RestoreNonHitObjects() { foreach (var occludable in hiddenLastFrame) if (!hitThisFrame.Contains(occludable)) occludable.Show(); }

    Occludable GetOrCacheOccludable(Collider col)
    {
        int id = col.transform.GetInstanceID();
        if (!occluderCache.TryGetValue(id, out var occludable))
        {
            occludable = col.GetComponent<Occludable>();
            if (occludable) occluderCache[id] = occludable;
        }
        return occludable;
    }
}