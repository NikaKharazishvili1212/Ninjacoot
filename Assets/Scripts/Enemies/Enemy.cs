using UnityEngine;
using UnityEngine.AI;
using static Utils;
using static GameConstants;

[SelectionBase]
public class Enemy : MonoBehaviour
{
    public static GameManager GM; // Initialized by GameManager
    public static Player Player; // Initialized by GameManager
    [SerializeField] EnemyType enemyType;
    [SerializeField] EnemyProjectileType enemyProjectileType;
    [SerializeField] Vector2 xp = new Vector2();
    [SerializeField] float speed, attackRange, attackDelay, maxMovementDistance = 25f, patrolInterval;
    public float PatrolSpeed => speed * 0.8f;
    public float GoingBackSpeed => speed * 1.5f;
    public bool IsAlive { get; private set; } = true;
    bool hasTarget, isDoingAttackAnimation, isGoingBack, isKillenOnce;

    [SerializeField] Animator animator;
    [SerializeField] NavMeshAgent agent; Vector3 startingPosition;
    [SerializeField] new Collider collider;
    [SerializeField] AudioSource audioSource;

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
                else if (!isDoingAttackAnimation)
                {
                    agent.stoppingDistance = attackRange;
                    agent.SetDestination(Player.transform.position);
                }
            }
        }
    }

    public void HasTarget(bool hasTarget) => this.hasTarget = hasTarget;

    public void DiedByPlayer() => DeathAndRespawn();

    public void DiedByProjectile() => DeathAndRespawn(diedByPlayer: false);

    void DeathAndRespawn(bool diedByPlayer = true)
    {
        IsAlive = false;
        hasTarget = false;
        isGoingBack = false;
        collider.enabled = false;
        agent.ResetPath();
        animator.Play(diedByPlayer ? Death1Hash : Death2Hash);
        if (diedByPlayer) GM.PlaySound(audioSource, SoundType.PlayerHitEnemy1, SoundType.PlayerHitEnemy2, SoundType.PlayerHitEnemy3, SoundType.PlayerHitEnemy4, SoundType.PlayerHitEnemy5, SoundType.PlayerHitEnemy6);
        else GM.PlaySound(audioSource, SoundType.ShurikenHitEnemy);
        this.Invoke2(0.25f, () => GM.PlaySound(audioSource, SoundType.SkeletonDeath1, SoundType.SkeletonDeath2, SoundType.SkeletonDeath3, SoundType.SkeletonDeath4, SoundType.SkeletonDeath5));

        if (!isKillenOnce) { isKillenOnce = true; GM.AddKill(); }

        int randomXp = Random.Range((int)xp.x, (int)xp.y + 1);
        PoolComponent(GM.XpTexts)?.ShowXPText(transform, randomXp);
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
        if (enemyType == EnemyType.Melee)
        {
            Player.TakeDamage();
            Player.DamageSound(SoundType.SwordImpact);
        }
        else
        {
            PoolComponent(GM.EnemyProjectiles).Shoot(transform, enemyProjectileType);
            GM.PlaySound(audioSource, enemyProjectileType == EnemyProjectileType.Spell ? SoundType.SpellShoot : SoundType.ArrowShoot);
        }
    }

    // Triggered by moving animation
    void PlayRandomFootstepSound() => GM.PlaySound(audioSource, SoundType.Footsteps1, SoundType.Footsteps2, SoundType.Footsteps3, SoundType.Footsteps4, SoundType.Footsteps5, SoundType.Footsteps6);
}