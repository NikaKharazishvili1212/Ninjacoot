using UnityEngine;
using static Utils;
using static GameConstants;

public class ShurikenProjectile : MonoBehaviour
{
    public static GameManager GM; // Initialized by GameManager
    [SerializeField] GameObject[] shurikenTypeToActivate;
    [SerializeField] AudioSource audioSource;
    Transform target;
    bool canRotate, alreadyHit;
    Vector3 initialPosition, movementDirection;
    TimerHandle lifespanHandle;

    void Update() => Move();

    // Activates random Shuriken model - if it's Kunai, it won't have a rotation animation (Called by Player)
    public void Shoot(Transform target, Transform player)
    {
        lifespanHandle?.CancelInvoke2(); // Cancel any leftover lifespan timer from previous use
        alreadyHit = false;
        int random = Random.Range(0, GM.ShurikenModels.Length);
        for (int i = 0; i < shurikenTypeToActivate.Length; i++) shurikenTypeToActivate[i].SetActive(i == random);
        canRotate = shurikenTypeToActivate[random].name != "Kunai";

        this.target = target;
        transform.position = player.position + player.up;
        transform.rotation = player.rotation;
        if (target) transform.LookAt(target.transform.position);
        movementDirection = player.forward;
        initialPosition = transform.position; // Save starting position to calculate how far it moved

        gameObject.SetActive(true);
        lifespanHandle = this.Invoke2(ShurikenLifeSpan, () => gameObject.SetActive(false));
    }

    // If having target, move towards it, otherwise just move forward (unless it touches anything)
    void Move()
    {
        if (canRotate) transform.Rotate(0, ShurikenRotationSpeed, 0); // Rotate if Shuriken model isn't Kunai

        if (target)
        {
            if (Vector3.Distance(transform.position, target.position) <= 2)
            {
                target.gameObject.GetComponent<Enemy>().DiedByProjectile();
                gameObject.SetActive(false);
            }
            else transform.position += (target.position - transform.position + Vector3.up).normalized * ShurikenMoveSpeed * Time.deltaTime;
        }
        else transform.position += movementDirection * ShurikenMoveSpeed * Time.deltaTime;

        // Deactivate if it moves too far from the start position (prevents long-range kills)
        if (Vector3.Distance(initialPosition, transform.position) > ShurikenMaxGoingDistance) gameObject.SetActive(false);
    }

    // Even without finding target, it can still hit Enemy far away on touch, or just get stuck in something
    void OnTriggerEnter(Collider other)
    {
        if (alreadyHit) return;

        if (other.gameObject.CompareTag(TagEnemy))
        {
            alreadyHit = true;
            other.gameObject.GetComponent<Enemy>().DiedByProjectile();
            gameObject.SetActive(false);
        }
        else if (other.gameObject.CompareTag(TagUntagged) || other.gameObject.CompareTag(TagTerrain))
        {
            alreadyHit = true;
            GM.PlaySound(audioSource, SoundType.ShurikenHitWall);
            movementDirection = Vector3.zero;
            canRotate = false;
        }
    }
}