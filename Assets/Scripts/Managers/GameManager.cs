using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Nikspector;
using static GameConstants;

/// <summary>Game Manager, which also acts like Global Referencer.</summary>
[DefaultExecutionOrder(-100)] // Ensures GameManager's awake initialization runs first
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // [Header("")]
    [field: SerializeField] public string AccountAndSceneName { get; private set; }
    [field: SerializeField] public Player Player { get; private set; }
    [field: SerializeField] public Transform Camera { get; private set; }
    [field: SerializeField] public ShurikenProjectile[] ShurikenProjectiles { get; private set; }
    [field: SerializeField] public Mesh[] ShurikenModels { get; private set; }
    [field: SerializeField] public EnemyProjectile[] EnemyProjectiles { get; private set; }
    [field: SerializeField] public XPText[] XpTexts { get; private set; }

    // [Header("UI")]
    [SerializeField] TextMeshProUGUI progressText, eventText, questText;

    // Coins, Kills, Breaks
    [SerializeField] TextMeshProUGUI coinsText, killsText, breaksText; // Hud texts
    [SerializeField] GameObject coinsParent, enemiesParent, breakablesParent; // For calculating child count and also foreaching
    int currentCoins, maxCoins, currentKills, maxKills, currentBreaks, maxBreaks;

    // For toggling hud on/off
    [SerializeField] NiksAnimator hudAnimator;
    [SerializeField] AnimationClip hudOnAnimation, hudOffAnimation;
    bool isHudOn = true, canToggleHud = true;

    // Every object gets sound from GameManager to play it
    public enum SoundType
    {
        PlayerSpin = 0, PlayerThrow = 1, PlayerJump = 2, PlayerTakeDamage1 = 3, PlayerTakeDamage2 = 4, PlayerDeath1 = 4, PlayerDeath2 = 5
        PlayerFall = 5, PlayerLevelUp = 6, ArrowImpact = 7, SpellImpact = 8, CoinTake = 9,
    }
    [field: SerializeField] public AudioClip[] Sounds { get; private set; }
    public void PlaySound(AudioSource source, params SoundType[] variants) => source.PlayOneShot(Sounds[(int)variants[Random.Range(0, variants.Length)]]);

    [Button]
    void DeleteAllPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        foreach (Transform coin in coinsParent.transform) coin.gameObject.SetActive(true); // Activate so they won't be saved OnApplicationQuit
    }

    [Button]
    void SetUpScene()
    {
        Camera = UnityEngine.Camera.main.transform;
        Player = FindFirstObjectByType<Player>();
        XpTexts = FindObjectsByType<XPText>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        ShurikenProjectiles = FindObjectsByType<ShurikenProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EnemyProjectiles = FindObjectsByType<EnemyProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    void OnApplicationQuit() => SaveData();

    void Awake()
    {
        Instance = this;
        Player.GM = this;
        Cam.Player = Player;
        ShurikenProjectile.GM = this;
        Enemy.GM = this;
        Enemy.Player = Player;
        EnemyProjectile.Player = Player;
        Hazard.GM = this;
        XPText.Cam = Camera;
        LoadData();
    }

    // Toggles hud on/off. Called by Player on hotkey
    public void ToggleHud()
    {
        if (!canToggleHud) return;
        isHudOn = !isHudOn;
        hudAnimator.Play(isHudOn ? hudOnAnimation : hudOffAnimation);
        canToggleHud = false;
        this.Invoke2(ToggleHudCD, () => canToggleHud = true);
    }

    // Update coins' hud and playerprefs, deactivate coin
    public void AddCoin(GameObject coin)
    {
        currentCoins++;
        coinsText.text = $"{currentCoins:00}/{maxCoins:00}";
        coin.SetActive(false);
    }

    public void AddKill()
    {
        currentKills++;
        killsText.text = $"{currentKills:00}/{maxKills:00}";
    }

    public void AddBreak()
    {
        currentBreaks += 1;
        breaksText.text = $"{currentBreaks:00}/{maxBreaks:00}";
    }

    void UpdateLevelProgressText() => progressText.text = $"Level Progress: {0}%";

    public void EventText(int Event)
    {
        eventText.text = Event == 1 ? "You are dead" : Event == 2 ? "Level Up!" : Event == 3 ? "You won!" : null;
        eventText.color = Event == 1 ? Color.red : Event == 2 ? Color.green : Event == 3 ? Color.green : Color.white;
        this.Invoke2(3, () => eventText.text = null);
    }

    void LoadData()
    {
        AccountAndSceneName = $"{PlayerPrefs.GetString("Account")}_{SceneManager.GetActiveScene().name}_";
        currentCoins = PlayerPrefs.GetInt(AccountAndSceneName + "TakenCoinsCount", 0);
        currentKills = PlayerPrefs.GetInt(AccountAndSceneName + "Kills", 0);
        currentBreaks = PlayerPrefs.GetInt(AccountAndSceneName + "Breaks", 0);

        maxCoins = coinsParent.transform.childCount;
        maxKills = enemiesParent.transform.childCount;
        maxBreaks = breakablesParent.transform.childCount;

        coinsText.text = $"{currentCoins:00}/{maxCoins:00}";
        killsText.text = $"{currentKills:00}/{maxKills:00}";
        breaksText.text = $"{currentBreaks:00}/{maxBreaks:00}";

        // Deactivate already taken coins on game start
        foreach (Transform coin in coinsParent.transform) coin.gameObject.SetActive(PlayerPrefs.GetInt(AccountAndSceneName + coin.name, 0) == 0);
    }

    void SaveData()
    {
        // Save taken coins
        int takenCoinsCount = 0;
        foreach (Transform coin in coinsParent.transform)
            if (!coin.gameObject.activeSelf)
            {
                takenCoinsCount++;
                PlayerPrefs.SetInt(AccountAndSceneName + coin.name, 1);
            }
        PlayerPrefs.SetInt(AccountAndSceneName + "TakenCoinsCount", takenCoinsCount);
    }
}