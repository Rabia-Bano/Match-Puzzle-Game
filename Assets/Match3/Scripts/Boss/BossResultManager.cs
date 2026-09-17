// ============================================================
//  BossResultManager.cs  —  MonoBehaviour
//
//  Boss Arena's end-of-fight panel — win (reward screen with coins +
//  a FIXED set of boosters from BossData.boosterRewards) or lose
//  (retry / quit). Structurally mirrors LevelResultManager.cs's
//  win/lose panel pattern.
//
//  Attach to: "BossResultManager" GameObject in BossArenaScene
//  (sibling of BossController / BossAttackExecutor).
//  Wire up: bossController, moveCounter (optional — only if this
//  fight uses a move limit), and every panel/button below.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Game.Firebase;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Match3
{
    public class BossResultManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private BossController bossController;
        [SerializeField] private MoveCounter    moveCounter;   // optional — leave blank if this fight has no move limit
        [SerializeField] private InputHandler    inputHandler;

        // ─── WIN PANEL ────────────────────────────────────────
        [Header("— Win Panel —")]
        [SerializeField] private GameObject      winPanel;
        [SerializeField] private TMP_Text        winTitleText;
        [SerializeField] private TMP_Text        winCoinsText;
        [SerializeField] private GameObject      boosterRewardRow;      // whole row, hidden if no boosters in this fight
        [SerializeField] private Button          winContinueButton;

        [Header("Booster Reward Items (NEW — one instantiated per booster in BossData.boosterRewards)")]
        [Tooltip("Parent to instantiate one reward item per booster under. If left empty, falls back to " +
                 "boosterRewardRow's own transform.")]
        [SerializeField] private Transform  boosterRewardsContainer;
        [Tooltip("A small prefab with a child Image (icon) and a child TMP_Text (label, e.g. \"Hammer x1\"). " +
                 "One is instantiated per booster the boss rewards.")]
        [SerializeField] private GameObject boosterRewardItemPrefab;
        [Tooltip("Fallback only — used if boosterRewardItemPrefab isn't assigned. Shows every reward as one " +
                 "combined line of text instead of separate icons.")]
        [SerializeField] private TMP_Text   boosterRewardFallbackText;

        private readonly List<GameObject> _spawnedRewardItems = new List<GameObject>();

        // ─── LOSE PANEL ───────────────────────────────────────
        [Header("— Lose Panel —")]
        [SerializeField] private GameObject losePanel;
        [SerializeField] private TMP_Text   loseTitleText;
        [SerializeField] private Button     retryButton;
        [SerializeField] private Button     quitButton;

        [Header("Animation")]
        [SerializeField] private float panelShowDelay = 0.5f;

        private bool _resultShown;

        // ─────────────────────────────────────────────────────

        private void Awake()
        {
            SafeHide(winPanel);
            SafeHide(losePanel);

            // FIX (input-never-enables bug): this used to call
            // inputHandler?.SetInputEnabled(true) here — but Unity runs ALL
            // Awake() calls before ANY Start() calls, so LevelManager.Start()
            // -> InitializeLevel()'s own inputHandler.SetInputEnabled(false)
            // always ran AFTER this and silently won, leaving input permanently
            // disabled. Input is now owned entirely by BossIntroPanel — its
            // Start button is the ONLY thing that enables input, mirroring how
            // regular levels wait for the goal panel's Play button. Do NOT add
            // an input-enable call back here.

            if (bossController == null) bossController = BossController.Instance;
        }

        private void OnEnable()
        {
            if (bossController != null)
                bossController.OnBossDefeated.AddListener(HandleWin);

            if (moveCounter != null)
                moveCounter.OnMovesExhausted.AddListener(HandleLose);

            winContinueButton?.onClick.RemoveAllListeners();
            retryButton?      .onClick.RemoveAllListeners();
            quitButton?        .onClick.RemoveAllListeners();

            winContinueButton?.onClick.AddListener(GoToBossArena);
            retryButton?      .onClick.AddListener(RetryFight);
            quitButton?        .onClick.AddListener(GoToBossArena);
        }

        private void OnDisable()
        {
            if (bossController != null)
                bossController.OnBossDefeated.RemoveListener(HandleWin);

            if (moveCounter != null)
                moveCounter.OnMovesExhausted.RemoveListener(HandleLose);
        }

        /// <summary>
        /// Call this from a "Give Up" / "Quit Fight" button mid-battle if you want
        /// one available — not required by the core design (no move/time limit by
        /// default), but wired here so it Just Works if you add such a button.
        /// </summary>
        public void ForceLose() => HandleLose();

        // ─────────────────────────────────────────────────────
        // WIN
        // ─────────────────────────────────────────────────────

        private void HandleWin()
        {
            if (_resultShown) return;
            _resultShown = true;
            AudioManager.Instance?.PlaySFX("level_win");                         
            JuiceManager.Instance?.FlashScreen(Color.white, 0.4f);
            StartCoroutine(ShowWinRoutine());
        }

        private IEnumerator ShowWinRoutine()
        {
            inputHandler?.SetInputEnabled(false);
            yield return new WaitForSeconds(panelShowDelay);

            BossData bossData = bossController != null ? bossController.BossData : null;
            int coins = bossData != null ? bossData.rewardCoins : 0;
            List<BossBoosterReward> rewards = bossData != null ? bossData.boosterRewards : null;
            bool hasRewards = rewards != null && rewards.Count > 0;

            // ── Save via ProfileManager (already has these exact hooks) ──
            ProfileManager.Instance?.OnBossDefeated(bossData != null ? bossData.id : 0, coins);

            // ── FIX: grant EVERY booster in bossData.boosterRewards in full (not a random
            // single pick), through LocalSaveManager — the SAME inventory path StoreManager
            // uses (LoadBoosterInventory -> increment -> SaveBoosterInventory). This is the
            // inventory InventoryBoosterSlot/BoosterManager actually read from during
            // gameplay, unlike the old ProfileManager.AddBooster() path which only wrote to
            // PlayerProfile.boosters — a list nothing in gameplay ever reads. ──
            if (hasRewards)
            {
                Dictionary<string, int> inventory = LocalSaveManager.LoadBoosterInventory();
                foreach (BossBoosterReward reward in rewards)
                {
                    if (reward == null || string.IsNullOrEmpty(reward.boosterId) || reward.count <= 0) continue;
                    inventory.TryGetValue(reward.boosterId, out int current);
                    inventory[reward.boosterId] = current + reward.count;
                }
                LocalSaveManager.SaveBoosterInventory(inventory); // one save -> one OnProfileChanged -> UI/Firestore sync
            }

            // ── Submit to leaderboard (fire-and-forget, same pattern as LevelResultManager) ──
            int score = 0; // Boss Arena has no standalone LevelManager.Score field wired here by default —
                            // pass a real score in if you bind one; SubmitScore no-ops safely on 0/failure.
            _ = LeaderboardManager.Instance?.SubmitScore(score, bossData != null ? $"boss_{bossData.id}" : "boss");

            // ── Populate UI ──
            if (winTitleText != null)
                winTitleText.text = bossData != null ? $"{bossData.bossName} Defeated!" : "Boss Defeated!";
            if (winCoinsText != null)
                winCoinsText.text = $"+{coins} Coins";

            if (hasRewards)
            {
                SafeShow(boosterRewardRow);
                BuildBoosterRewardItems(rewards);
            }
            else
            {
                SafeHide(boosterRewardRow);
            }

            SafeShow(winPanel);
            winPanel.transform.localScale = Vector3.zero;
            winPanel.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);

            if (boosterRewardRow != null && hasRewards)
            {
                yield return new WaitForSeconds(0.3f);
                boosterRewardRow.transform.localScale = Vector3.zero;
                boosterRewardRow.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutElastic);
            }

            // Tell GameManager a boss fight just ended so anything listening
            // (analytics, HandleBossDefeated → ChangeState(Map) if you navigate
            // via this event instead of the Continue button) can react.
            GameEvents.OnBossDefeated?.Invoke();
        }

        /// <summary>Spawns one reward item (icon + label) per booster in the list under
        /// boosterRewardsContainer. Falls back to a single combined text line if no item
        /// prefab is wired up.</summary>
        private void BuildBoosterRewardItems(List<BossBoosterReward> rewards)
        {
            // Clear anything spawned by a previous fight (Retry re-enters this panel).
            for (int i = 0; i < _spawnedRewardItems.Count; i++)
                if (_spawnedRewardItems[i] != null) Destroy(_spawnedRewardItems[i]);
            _spawnedRewardItems.Clear();

            Transform parent = boosterRewardsContainer != null ? boosterRewardsContainer
                              : boosterRewardRow != null       ? boosterRewardRow.transform
                              : null;

            if (parent == null || boosterRewardItemPrefab == null)
            {
                // Fallback: no per-item prefab wired up yet — show one combined text line.
                if (boosterRewardFallbackText != null)
                {
                    var parts = new List<string>();
                    foreach (var r in rewards)
                        if (r != null && !string.IsNullOrEmpty(r.boosterId) && r.count > 0)
                            parts.Add($"x{r.count}");   // UPDATED: no booster name, just "x<count>"
                    boosterRewardFallbackText.text  = string.Join("   ", parts);
                    boosterRewardFallbackText.color = Color.black;   // UPDATED
                }
                return;
            }

            foreach (BossBoosterReward reward in rewards)
            {
                if (reward == null || string.IsNullOrEmpty(reward.boosterId) || reward.count <= 0) continue;

                GameObject item = Instantiate(boosterRewardItemPrefab, parent);
                item.SetActive(true);
                _spawnedRewardItems.Add(item);

                Image icon = item.GetComponentInChildren<Image>();
                if (icon != null) icon.sprite = Resources.Load<Sprite>($"StoreIcons/{reward.boosterId}");

                TMP_Text label = item.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text  = $"x{reward.count}";   // UPDATED: no booster name, just "x<count>"
                    label.color = Color.black;           // UPDATED
                }
            }
        }

        private static string DisplayName(string boosterId)
        {
            switch (boosterId)
            {
                case BoosterManager.Hammer:        return "Hammer";
                case BoosterManager.RowBomb:       return "Row Bomb";
                case BoosterManager.ColumnBomb:    return "Column Bomb";
                case BoosterManager.Shuffle2Tiles: return "Double Shuffle";
                case BoosterManager.ShuffleBoard:  return "Board Shuffle";
                default:                           return boosterId;
            }
        }

        // ─────────────────────────────────────────────────────
        // LOSE
        // ─────────────────────────────────────────────────────

        private void HandleLose()
        {
            if (_resultShown) return;
            _resultShown = true;
            AudioManager.Instance?.PlaySFX("level_fail");
            StartCoroutine(ShowLoseRoutine());
        }

        private IEnumerator ShowLoseRoutine()
        {
            inputHandler?.SetInputEnabled(false);
            yield return new WaitForSeconds(panelShowDelay);

            if (loseTitleText != null)
                loseTitleText.text = "Boss Escaped!";

            SafeShow(losePanel);
            losePanel.transform.localScale = Vector3.zero;
            losePanel.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);

            // Design note: a Boss Arena loss never touches regular lives/hearts —
            // this panel only ever appears from a move-limit run-out (if
            // moveCounter is wired) or an explicit ForceLose() call.
        }

        // ─────────────────────────────────────────────────────
        // NAVIGATION
        // ─────────────────────────────────────────────────────

        private void RetryFight()
        {
            DOTween.KillAll();

            // FIX: this was reloading "BossArenaScene" (the boss SELECTION
            // list) instead of the actual fight scene — Retry would have
            // dumped the player back on the boss list, not restarted the
            // fight. ReloadBossGameBoardScene() is the dedicated helper for
            // exactly this (force-reloads BossGameBoardScene / GameState.
            // BossGameplay even though we're already in that state, which a
            // plain ChangeState() call would otherwise no-op on).
            SceneLoader.Instance?.ReloadBossGameBoardScene();
        }

        private void GoToBossArena()
        {
            DOTween.KillAll();
            GameManager.Instance?.ChangeState(GameState.BossArena);
        }

        // ─────────────────────────────────────────────────────

        private static void SafeHide(GameObject go) { if (go != null) go.SetActive(false); }
        private static void SafeShow(GameObject go) { if (go != null) go.SetActive(true); }
    }
}