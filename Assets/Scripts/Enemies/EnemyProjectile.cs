using UnityEngine;
using static GameConstants;

/// <summary>Pooled by Enemy to shot specific projectile to the Player.</summary>
public class EnemyProjectile : MonoBehaviour
{
    public static Player Player; // Initialized by GameManager
    [SerializeField] GameObject[] projectileTypeToActivate;
    ProjectileType type;
    float lifespan;
    bool alreadyHit;

    void Update()
    {
        // If the projectile is an Arrow, it will move in the direction it is facing; if it's a Spell, it will move towards the Player. Arrow moves faster
        if (type == ProjectileType.Arrow) transform.position += transform.forward.normalized * EnemyProjectileArrowSpeed * Time.deltaTime;
        else transform.position += (Player.transform.position - transform.position + Vector3.up).normalized * EnemyProjectileSpellSpeed * Time.deltaTime;

        // When its lifespan reaches 0 or when the Player dies, disable the projectile to be pooled again later
        if (lifespan <= 0 || !Player.IsAlive) gameObject.SetActive(false);
        else lifespan -= Time.deltaTime;
    }

    public void Shoot(Transform shooter, ProjectileType type)
    {
        alreadyHit = false;
        this.type = type;
        // Projectile teleports near the shooter Enemy with correct rotation
        transform.position = shooter.position + shooter.up;
        transform.rotation = shooter.rotation;
        lifespan = EnemyProjectileLifespan;
        for (int i = 0; i < projectileTypeToActivate.Length; i++) projectileTypeToActivate[i].SetActive(i == (int)type - 1);
        gameObject.SetActive(true);
    }

    void OnTriggerEnter(Collider collision)
    {
        if (!collision.gameObject.CompareTag(TagPlayer) || alreadyHit) return;
        alreadyHit = true;
        if (!(type == ProjectileType.Spell && Player.isSpinning)) Player.TakeDamage(); // Spell is countered by Player's spin
        gameObject.SetActive(false);
        // PlaySound(type == ProjectileType.Arrow ? "ArrowImpact" : "MageImpact");
    }
}