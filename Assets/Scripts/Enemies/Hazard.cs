using UnityEngine;
using static GameConstants;

/// <summary>Damages Player on touch; has cooldown.</summary>
public class Hazard : MonoBehaviour
{
    bool damageOnCd;

    void OnCollisionStay(Collision other)
    {
        if (damageOnCd || !other.gameObject.CompareTag(TagPlayer)) return;
        GameManager.Instance.Player.TakeDamage();
        damageOnCd = true;
        this.Invoke2(HazardDamageCooldown, () => damageOnCd = false);
    }
}