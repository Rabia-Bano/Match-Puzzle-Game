// ============================================================
//  BossArenaListManager.cs  —  MonoBehaviour
//
//  Populates the "Puzzle Boss Arena" list panel (the mockup screen)
//  inside BossArenaScene with one BossLevelNode per boss. Mirrors
//  MapManager.cs's ProfileManager-wait + Build pattern, just simpler
//  (a flat horizontal/grid list instead of a scrolling path).
//
//  UPDATED unlock rule: boss N unlocks once the player has completed
//  regular Level (N * bossEveryNLevels) — e.g. Boss 1 after Level 6,
//  Boss 2 after Level 12, etc. This MUST match MapManager's inline
//  BossNodeController (the marker on the main path) and
//  LevelSession.CheckUnlocks() — all three used to disagree (this
//  file was unlocking Boss 1 always, then Boss N after Boss N-1 was
//  DEFEATED — completely different rule, which is why boss fights
//  were unlocking way too early). "Defeated" state (the replay badge)
//  is still read from PlayerProfile.highestBossDefeated — only the
//  LOCK/UNLOCK gate itself changed.
//
//  Attach to: "BossArenaListManager" GameObject in BossArenaScene
//  (a sibling of UIManager / ProfilePanel).
//  Wire up: bossNodePrefab, listContent, totalBosses.
// ============================================================

using System.Collections.Generic;
using Game.Firebase;
using UnityEngine;

public class BossArenaListManager : MonoBehaviour
{
    [Header("Prefab & Layout")]
    [Tooltip("The BossLevelNode prefab (numbered circle).")]
    public GameObject bossNodePrefab;

    [Tooltip("Parent transform the nodes get instantiated under — this is the " +
             "'Puzzle Boss Arena' panel's content area (add a Horizontal Layout " +
             "Group or Grid Layout Group component here so nodes auto-arrange).")]
    public RectTransform listContent;

    [Header("Config")]
    [Tooltip("How many total boss fights exist (how many boss_N.asset files you made).")]
    public int totalBosses = 3;

    [Tooltip("MUST match MapManager.bossEveryNLevels and LevelSession.CheckUnlocks()'s boss " +
             "cadence — currently every 6 regular levels. If you change one, change all three.")]
    public int bossEveryNLevels = 6;

    [Header("Boss Arena Board")]
    [Tooltip("Shared LevelData asset that defines every boss fight's board layout. " +
             "Same asset you'll assign on the Map's BossNode prefab too — see setup guide.")]
    public Match3.LevelData bossBoardLevelData;

    private readonly List<BossLevelNode> _spawnedNodes = new List<BossLevelNode>();

    private void OnEnable()  => BossLevelNode.OnBossLevelSelected += HandleBossSelected;
    private void OnDisable()
    {
        BossLevelNode.OnBossLevelSelected -= HandleBossSelected;
        if (ProfileManager.Instance != null)
            ProfileManager.Instance.OnProfileLoaded.RemoveListener(OnProfileLoaded);
    }

    private void Start()
    {
        if (ProfileManager.Instance == null)
        {
            Debug.LogWarning("[BossArenaListManager] ProfileManager not found — building offline list (Boss 1 unlocked only).");
            BuildList();
            return;
        }

        if (ProfileManager.Instance.IsLoaded)
        {
            BuildList();
        }
        else
        {
            Debug.Log("[BossArenaListManager] Waiting for profile to load...");
            ProfileManager.Instance.OnProfileLoaded.AddListener(OnProfileLoaded);
        }
    }

    private void OnProfileLoaded()
    {
        ProfileManager.Instance?.OnProfileLoaded.RemoveListener(OnProfileLoaded);
        BuildList();
    }

    private void BuildList()
    {
        foreach (var old in _spawnedNodes)
            if (old != null) Destroy(old.gameObject);
        _spawnedNodes.Clear();

        int highestDefeated = ProfileManager.Instance != null && ProfileManager.Instance.IsLoaded
            ? ProfileManager.Instance.Profile.highestBossDefeated
            : 0;

        PlayerProfile profile = ProfileManager.Instance != null && ProfileManager.Instance.IsLoaded
            ? ProfileManager.Instance.Profile
            : null;

        for (int id = 1; id <= totalBosses; id++)
        {
            GameObject go = Instantiate(bossNodePrefab, listContent);
            go.name = $"BossLevelNode_{id}";

            BossLevelNode node = go.GetComponent<BossLevelNode>();
            if (node == null) continue;

            // THE FIX — level-based gate (was: unlocked once the previous boss
            // was defeated, ignoring regular level progress entirely).
            int afterLevel = id * bossEveryNLevels;
            bool isUnlocked = profile != null && profile.GetStars(afterLevel) > 0;

            BossLevelNode.NodeState state;
            if (id <= highestDefeated)
                state = BossLevelNode.NodeState.Defeated;   // already beaten — replayable, still requires the level gate too
            else if (isUnlocked)
                state = BossLevelNode.NodeState.Unlocked;
            else
                state = BossLevelNode.NodeState.Locked;

            node.Setup(id, state, afterLevel);
            _spawnedNodes.Add(node);

            Debug.Log($"[BossArenaListManager] Boss {id}: needs Level {afterLevel} complete " +
                      $"(stars there = {(profile != null ? profile.GetStars(afterLevel) : -1)}) → {state}");
        }
    }

    private void HandleBossSelected(int bossId)
    {
        Debug.Log($"[BossArenaListManager] Boss {bossId} selected → launching fight.");
        BossLevelLoader.LoadBoss(bossId, bossBoardLevelData);
    }
}
