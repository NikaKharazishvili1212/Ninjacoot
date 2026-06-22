#if UNITY_EDITOR
using UnityEngine;

namespace Nikson
{
    public class CameraController : MonoBehaviour
    {
        public float moveSpeed = 15;
        public float shiftMultiplier = 4;
        float mouseSensitivity = 2; // Rotation

        float yaw;
        float pitch;

        void OnEnable()
        {
            yaw = transform.eulerAngles.y;
            pitch = transform.eulerAngles.x;
        }

        void Update()
        {
            // Cursor lock
            if (Input.GetMouseButtonDown(1)) Cursor.lockState = CursorLockMode.Locked;
            if (Input.GetMouseButtonUp(1)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

            // Rotation (right mouse held)
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                pitch = Mathf.Clamp(pitch, -89f, 89f);
                transform.eulerAngles = new Vector3(pitch, yaw, 0f);
            }

            // Movement
            Vector3 dir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) dir += transform.forward;
            if (Input.GetKey(KeyCode.S)) dir -= transform.forward;
            if (Input.GetKey(KeyCode.D)) dir += transform.right;
            if (Input.GetKey(KeyCode.A)) dir -= transform.right;
            if (Input.GetKey(KeyCode.E)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) dir -= Vector3.up;

            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? shiftMultiplier : 1f);
            transform.position += dir.normalized * speed * Time.deltaTime;
        }
    }
}
#endif