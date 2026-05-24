using UnityEngine;
using UnityEngine.AI;
using static Utils;
using static GameConstants;

public enum EnemyType { Melee = 0, Ranged = 1 }
public enum ProjectileType { None = 0, Arrow = 1, Spell = 2 }

[SelectionBase]
public class Enemy : MonoBehaviour
{
    public static GameManager GM;
    public static Player Player;
    [SerializeField] EnemyType enemyType;
    [SerializeField] ProjectileType projectileType;
    [SerializeField] Vector2 xp = new Vector2();
    [SerializeField] float speed, attackRange, attackDelay, maxMovementDistance = 25f, patrolInterval;
    public float PatrolSpeed => speed * 0.8f;
    public float GoingBackSpeed => speed * 1.5f;
    public bool IsAlive { get; private set; } = true;
    bool hasTarget, isDoingAttackAnimation, isGoingBack, isKillenOnce;

    [SerializeField] new Collider collider;
    [SerializeField] Animator animator;
    [SerializeField] NavMeshAgent agent; Vector3 startingPosition;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip getHitByShurikenSound, attackSound;
    [SerializeField] AudioClip[] footstepSounds, getHitByPlayerSounds, deathSounds;

    void Start()
    {
        startingPosition = transform.position;
        transform.Rotate(0, Random.Range(0, 361), 0); // Starts with a random rotation
        if (enemyType == EnemyType.Melee) { attackRange = 1.5f; attackDelay = 1f; speed = 7.5f; }
        else { attackRange = 8f; attackDelay = 1.5f; speed = 6f; }
        agent.speed = speed; agent.angularSpeed = 1000f; agent.acceleration = 1000f; agent.radius = 0.4f;
    }

    void Update()
    {
        PatrolDetectChase();
        UpdateAnimations();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag(TagPlayer))
        {
            hasTarget = true;
            agent.ResetPath();
        }
    }

    // We need this check to auto update NPC's animations(Idle, Running, Walking)
    void UpdateAnimations() => animator.SetInteger(MovingStateHash, agent.velocity.magnitude == 0 ? 0 : (hasTarget || isGoingBack) ? 2 : 1);

    void PatrolDetectChase()
    {
        if (IsAlive)
        {
            patrolInterval = Mathf.Max(0, patrolInterval - Time.deltaTime);
            // NPC patrols nearby his "patrolRadius" in every random interval
            if (!hasTarget)
            {
                if (patrolInterval > 0) return; // Patrol on CD
                patrolInterval = Random.Range(5f, 10f);
                if (NavMesh.SamplePosition(Random.insideUnitSphere * EnemyPatrolRadius + startingPosition, out NavMeshHit hit, EnemyPatrolRadius, NavMesh.AllAreas))
                {
                    agent.speed = speed * 0.8f;  // Moves a bit slow when patrolling
                    agent.stoppingDistance = 1.1f;
                    agent.SetDestination(hit.position);
                }
            }
            else if (!isGoingBack)
            {
                if (!Player.IsAlive) // If Target dies, reset target
                {
                    hasTarget = false;
                    return;
                }

                // In agressive stance, moves with its normal speed and always rotating towards the Target
                agent.speed = speed;
                transform.rotation = Quaternion.LookRotation(new Vector3(Player.transform.position.x - transform.position.x, 0, Player.transform.position.z - transform.position.z));  // Npc always looks(rotates) at Player
                // When the Player touches NPC while the Player is spinning, NPC dies
                if (Vector3.Distance(transform.position, Player.transform.position) <= 1.5f && Player.isSpinning) DiedByPlayer();
                // If NPC moves too far away from his "startingPosition", it will go back and nullify target
                if (Vector3.Distance(transform.position, startingPosition) > maxMovementDistance)
                {
                    isGoingBack = true;
                    hasTarget = false;
                    agent.speed = GoingBackSpeed;
                    this.Invoke2(0.1f, () => agent.SetDestination(startingPosition));
                    this.Invoke2(3f, () => { if (isGoingBack) isGoingBack = false; });  // We need "isGoingBack" check to avoid bug when Player kills the NPC while it's going back
                }
                // Attack the Player if he is in NPC's "attackRange" and the NPC doesn't already do the attack animation
                else if (Vector3.Distance(transform.position, Player.transform.position) <= attackRange && !isDoingAttackAnimation)
                {
                    if (IsAlive)  // We need to check again if "IsAlive" to avoid a bug where the NPC does attack animation when dead
                    {
                        isDoingAttackAnimation = true;
                        animator.Play(Chance(50) ? Attack1Hash : Attack2Hash); // Does random of 2 attack animations for: Swordsman, Archer, Mage
                        this.Invoke2(attackDelay, () => isDoingAttackAnimation = false);
                    }
                }
                // Else chase the Target
                else
                {
                    agent.stoppingDistance = attackRange;
                    agent.SetDestination(Player.transform.position);
                }
            }
        }
    }

    public void DiedByPlayer() => DeathAndRespawn();

    public void DiedByProjectile() => DeathAndRespawn(diedByProjectile: true);

    void DeathAndRespawn(bool diedByProjectile = false)
    {
        IsAlive = false;
        hasTarget = false;
        isGoingBack = false;
        collider.enabled = false;
        agent.ResetPath();
        animator.Play(diedByProjectile ? Death2Hash : Death1Hash);
        if (diedByProjectile) PlaySound("GetHitByShruiken");
        else PlaySound("GetHitByPlayer");
        this.Invoke2(0.25f, () => PlaySound("Death"));

        if (!isKillenOnce) { isKillenOnce = true; GameManager.Instance.AddKill(); }

        int randomXp = Random.Range((int)xp.x, (int)xp.y + 1);
        PoolComponent(GameManager.Instance.XpTexts)?.ShowXPText(transform, randomXp);
        Player.GainXP(randomXp);

        this.Invoke2(EnemyDeathDeactivateDelay, () => gameObject.SetActive(false));
        this.Invoke2(EnemyRespawnInterval, () =>
        {
            gameObject.SetActive(true);
            collider.enabled = true;
            agent.Warp(startingPosition);
            animator.Play(RespawnHash);
            this.Invoke2(1, () => IsAlive = true);
        });
    }

    // Enemy's attack animation calls for this method when NPC is attacking to deal instant damage if melee, or just shoot projectile
    void DealDamage()
    {
        if (enemyType == EnemyType.Melee) Player.TakeDamage();
        else PoolComponent(GM.EnemyProjectiles).Shoot(transform, projectileType);
    }

    // Enemy's attack animation calls for this method's "Attack" sound
    void PlaySound(string soundName)
    {
        switch (soundName)
        {
            case "Footsteps": audioSource.PlayOneShot(footstepSounds[Random.Range(0, footstepSounds.Length)]); break;
            case "GetHitByPlayer": audioSource.PlayOneShot(getHitByPlayerSounds[Random.Range(0, getHitByPlayerSounds.Length)]); break;
            case "GetHitByShruiken": audioSource.PlayOneShot(getHitByShurikenSound); break;
            case "Death": audioSource.PlayOneShot(deathSounds[Random.Range(0, deathSounds.Length)]); break;
            case "Attack": audioSource.PlayOneShot(attackSound); break;
        }
    }
}