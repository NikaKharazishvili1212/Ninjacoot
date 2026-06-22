using UnityEngine;

namespace Nikson
{
    [DisallowMultipleComponent]
    public class HealthBar : MonoBehaviour
    {
        const float ScreenSize = 0.08f;
        [SerializeField] new Transform camera;
        [SerializeField] Renderer healthBar;

        // Used to pass per-instance shader properties without creating a new material
        MaterialPropertyBlock healthBarBlock;

        float health = 100, maxHealth = 100;

        void Awake()
        {
            healthBarBlock = new MaterialPropertyBlock();

            // Aspect ratio ensures the border appears visually uniform on all edges
            healthBarBlock.SetFloat("_Aspect", healthBar.transform.localScale.x / healthBar.transform.localScale.y);
            healthBar.SetPropertyBlock(healthBarBlock);
        }

        void Update()
        {
            SetScreenSizeBillboard();
            if (Input.GetKeyDown(KeyCode.Alpha1)) TakeDamage(10);
            if (Input.GetKeyDown(KeyCode.Alpha2)) TakeDamage(-10); // Heal
        }

        // Keeps the health bar facing the camera at a consistent screen size regardless of distance
        void SetScreenSizeBillboard()
        {
            transform.rotation = camera.rotation;
            float scaleValue = Vector3.Dot(transform.position - camera.position, camera.forward) * ScreenSize;
            transform.localScale = new Vector3(scaleValue, scaleValue, scaleValue);
        }

        // Take damage or heal and update health bar fill as well
        void TakeDamage(float damage)
        {
            health = Mathf.Clamp(health - damage, 0, maxHealth);

            healthBarBlock.SetFloat("_FillAmount", health / maxHealth);
            healthBar.SetPropertyBlock(healthBarBlock);
        }
    }
}