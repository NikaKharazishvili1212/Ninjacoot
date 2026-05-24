using UnityEngine;

public static class GameConstants
{
    // Others
    public const float HiddenOccludableAlpha = 0.5f;

    // UI
    public const float ToggleHudCD = 0.5f;

    // Player
    public const float PlayerJumpStrength = 10;
    public const float PlayerReviveDelay = 3;
    public const float PlayerRotationSpeed = 20;

    // Enemy
    public const float EnemyRespawnInterval = 20;
    public const float EnemyPatrolRadius = 12;
    public const float EnemyDeathDeactivateDelay = 3;
    public const float HazardDamageCooldown = 0.8f;

    // Enemy Projectile
    public const float EnemyProjectileLifespan = 4;
    public const float EnemyProjectileArrowSpeed = 35;
    public const float EnemyProjectileSpellSpeed = 7;

    // Shuriken
    public const float ShurikenMoveSpeed = 20;
    public const float ShurikenRotationSpeed = 20;
    public const float ShurikenMaxGoingDistance = 30;
    public const float ShurikenLifeSpan = 10;
    public const float ShurikenDetectionRadius = 15;
    public const float ShurikenDetectionAngle = 45;

    // XpText
    public const float XpTextLifespan = 1.5f;
    public const float XpTextMoveUpSpeed = 0.2f;

    // Camera
    public const float CameraZoom = 8;
    public const float CameraPitch = 10;
    public const float CameraAutoRotationSpeed = 10;
    public const float CameraHotkeyRotationSpeed = 14;

    // Tags
    public const string TagUntagged = "Untagged";
    public const string TagPlayer = "Player";
    public const string TagEnemy = "Enemy";
    public const string TagCoin = "Coin";
    public const string TagTerrain = "Terrain";
    public const string TagWater = "Water";

    // Animation Hashes
    public static readonly int IdleHash = Animator.StringToHash("Idle");
    public static readonly int MovingStateHash = Animator.StringToHash("MovingState");
    public static readonly int MovingSpeedAnimatorHash = Animator.StringToHash("MovingSpeedAnimator");
    public static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    public static readonly int Spin1Hash = Animator.StringToHash("Spin1");
    public static readonly int Spin2Hash = Animator.StringToHash("Spin2");
    public static readonly int ThrowHash = Animator.StringToHash("Throw");
    public static readonly int GetHitHash = Animator.StringToHash("GetHit");
    public static readonly int DeathHash = Animator.StringToHash("Death");
    public static readonly int ReviveHash = Animator.StringToHash("Revive");
    public static readonly int Attack1Hash = Animator.StringToHash("Attack1");
    public static readonly int Attack2Hash = Animator.StringToHash("Attack2");
    public static readonly int Death1Hash = Animator.StringToHash("Death1");
    public static readonly int Death2Hash = Animator.StringToHash("Death2");
    public static readonly int RespawnHash = Animator.StringToHash("Respawn");
}