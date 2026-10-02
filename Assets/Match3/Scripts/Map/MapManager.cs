using System.Collections;
using System.Collections.Generic;
using Game.Firebase;
using UnityEngine;
using UnityEngine.UI;

public class MapManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject levelNodePrefab;
    public GameObject bossNodePrefab;

    [Header("Scroll Setup")]
    public ScrollRect    mapScrollRect;
    public RectTransform scrollContent;

    [Header("Node Positions")]
    public List<Vector2> nodePositions = new List<Vector2>();

    [Header("Level Config")]
    public int totalLevels      = 15;
    public int bossEveryNLevels = 6;

    [Header("Path Line")]
    public LineRenderer pathLine;
    public Sprite       dashedSprite;
    public Sprite       solidSprite;
    public Color        unlockedPathColor = new Color(0.2f, 0.8f, 1f, 1f);
    public Color        lockedPathColor   = new Color(0.4f, 0.4f, 0.4f, 0.6f);

    private readonly List<LevelNode> _spawnedNodes        = new List<LevelNode>();
    private int                      _highestUnlockedIndex = 0;

    private void OnEnable()  => LevelNode.OnLevelSelected += HandleLevelSelected;

    private void OnDisable()
    {
        LevelNode.OnLevelSelected -= HandleLevelSelected;
        if (ProfileManager.Instance != null)
            ProfileManager.Instance.OnProfileLoaded.RemoveListener(OnProfileLoaded);
    }

    private void Start()
    {
        if (ProfileManager.Instance == null)
        {
            Debug.LogWarning("[MapManager] ProfileManager not found — building offline map.");
            BuildAndScroll();
            return;
        }

        if (ProfileManager.Instance.IsLoaded)
        {
            BuildAndScroll();
        }
        else
        {
            Debug.Log("[MapManager] Waiting for profile to load...");
            ProfileManager.Instance.OnProfileLoaded.AddListener(OnProfileLoaded);
        }
    }

    private void OnProfileLoaded()
    {
        ProfileManager.Instance?.OnProfileLoaded.RemoveListener(OnProfileLoaded);
        Debug.Log("[MapManager] Profile loaded — building map with stars.");
        BuildAndScroll();
    }

    private void BuildAndScroll()
    {
        BuildMap();
        DrawPath();
        StartCoroutine(OpenAtBottom());
    }

    private void BuildMap()
    {
        foreach (var old in _spawnedNodes)
            if (old != null) Destroy(old.gameObject);
        _spawnedNodes.Clear();
        _highestUnlockedIndex = 0;

        PlayerProfile profile = GetProfile();
        int posIndex = 0;

        for (int lvl = 1; lvl <= totalLevels; lvl++)
        {
            if (posIndex < nodePositions.Count)
            {
                LevelNode node = SpawnLevelNode(lvl, posIndex, profile);
                _spawnedNodes.Add(node);
                posIndex++;
            }

            bool isBossLevel = (lvl % bossEveryNLevels == 0);
            if (isBossLevel && bossNodePrefab != null && posIndex < nodePositions.Count)
            {
                int bossIndex = lvl / bossEveryNLevels;
                SpawnBossNode(bossIndex, lvl, posIndex, profile);
                posIndex++;
            }
        }

        Debug.Log($"[MapManager] Built {_spawnedNodes.Count} nodes. " +
                  $"Highest unlocked: {_highestUnlockedIndex}");
    }

    private LevelNode SpawnLevelNode(int levelId, int posIndex, PlayerProfile profile)
    {
        Vector2    pos = nodePositions[posIndex];
        GameObject go  = Instantiate(levelNodePrefab, scrollContent);
        go.name = $"LevelNode_{levelId}";

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = pos;

        LevelNode node = go.GetComponent<LevelNode>();
        if (node == null) return null;

        node.levelId = levelId;

        LevelNode.NodeState state;
        int stars = 0;

        if (profile != null)
        {
            stars = profile.GetStars(levelId);
            bool unlocked = profile.IsLevelUnlocked(levelId);

            if (!unlocked)
                state = LevelNode.NodeState.Locked;
            else if (stars > 0)
                state = LevelNode.NodeState.Completed;
            else
                state = LevelNode.NodeState.Unlocked;
        }
        else
        {
            state = (levelId == 1) ? LevelNode.NodeState.Unlocked : LevelNode.NodeState.Locked;
        }

        node.SetState(state, stars);

        if (state != LevelNode.NodeState.Locked)
            _highestUnlockedIndex = _spawnedNodes.Count;

        return node;
    }
    private void SpawnBossNode(int bossId, int afterLevel, int posIndex, PlayerProfile profile)
    {
        Vector2    pos = nodePositions[posIndex];
        GameObject go  = Instantiate(bossNodePrefab, scrollContent);
        go.name = $"BossNode_{bossId}";

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = pos;

        BossNodeController boss = go.GetComponent<BossNodeController>();
        if (boss == null) return;

        bool prevComplete = profile != null && profile.GetStars(afterLevel) > 0;
        boss.Setup(bossId, afterLevel, prevComplete);
    }

    private void DrawPath()
    {
        if (pathLine == null || nodePositions == null || nodePositions.Count < 2) return;

        pathLine.positionCount = nodePositions.Count;
        for (int i = 0; i < nodePositions.Count; i++)
            pathLine.SetPosition(i, new Vector3(nodePositions[i].x, nodePositions[i].y, 0f));

        var gradient = new Gradient();
        float split  = _highestUnlockedIndex > 0
            ? Mathf.Clamp01((float)_highestUnlockedIndex / nodePositions.Count) : 0f;

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(unlockedPathColor, 0f),
                new GradientColorKey(unlockedPathColor, split),
                new GradientColorKey(lockedPathColor,   split + 0.001f),
                new GradientColorKey(lockedPathColor,   1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        pathLine.colorGradient = gradient;
        pathLine.startWidth = 6f;
        pathLine.endWidth   = 6f;
    }

    private IEnumerator OpenAtBottom()
    {
        yield return new WaitForEndOfFrame();
        yield return null;
        if (mapScrollRect != null)
            mapScrollRect.verticalNormalizedPosition = 0f;
    }

    private void HandleLevelSelected(int levelId)
    {
        Debug.Log($"[MapManager] Level {levelId} selected.");
        if (GameManager.Instance == null)
        { Debug.LogWarning("[MapManager] GameManager.Instance is null."); return; }
        Match3.LevelLoader.LoadLevel(levelId);
    }

    private PlayerProfile GetProfile()
    {
        if (ProfileManager.Instance != null && ProfileManager.Instance.IsLoaded)
            return ProfileManager.Instance.Profile;
        return null;
    }
}
