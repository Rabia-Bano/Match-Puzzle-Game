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
        [SerializeField] private MoveCounter    moveCounter;
        [SerializeField] private InputHandler    inputHandler;

        [Header("— Win Panel —")]
        [SerializeField] private GameObject      winPanel;
        [SerializeField] private TMP_Text        winTitleText;
        [SerializeField] private TMP_Text        winCoinsText;
        [SerializeField] private GameObject      boosterRewardRow;
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

        [Header("— Lose Panel —")]
        [SerializeField] private GameObject losePanel;
        [SerializeField] private TMP_Text   loseTitleText;
        [SerializeField] private Button     retryButton;
        [SerializeField] private Button     quitButton;

        [Header("Animation")]
        [SerializeField] private float panelShowDelay = 0.5f;

        private bool _resultShown;

        private void Awake()
        {
            SafeHide(winPanel);
            SafeHide(losePanel);

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

        public void ForceLose() => HandleLose();

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

            ProfileManager.Instance?.OnBossDefeated(bossData != null ? bossData.id : 0, coins);

            if (hasRewards)
            {
                Dictionary<string, int> inventory = LocalSaveManager.LoadBoosterInventory();
                foreach (BossBoosterReward reward in rewards)
                {
                    if (reward == null || string.IsNullOrEmpty(reward.boosterId) || reward.count <= 0) continue;
                    inventory.TryGetValue(reward.boosterId, out int current);
                    inventory[reward.boosterId] = current + reward.count;
                }
                LocalSaveManager.SaveBoosterInventory(inventory);
            }

            int score = 0;
            _ = LeaderboardManager.Instance?.SubmitScore(score, bossData != null ? $"boss_{bossData.id}" : "boss");

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

            GameEvents.OnBossDefeated?.Invoke();
        }

        private void BuildBoosterRewardItems(List<BossBoosterReward> rewards)
        {
            for (int i = 0; i < _spawnedRewardItems.Count; i++)
                if (_spawnedRewardItems[i] != null) Destroy(_spawnedRewardItems[i]);
            _spawnedRewardItems.Clear();

            Transform parent = boosterRewardsContainer != null ? boosterRewardsContainer
                              : boosterRewardRow != null       ? boosterRewardRow.transform
                              : null;

            if (parent == null || boosterRewardItemPrefab == null)
            {
                if (boosterRewardFallbackText != null)
                {
                    var parts = new List<string>();
                    foreach (var r in rewards)
                        if (r != null && !string.IsNullOrEmpty(r.boosterId) && r.count > 0)
                            parts.Add($"x{r.count}");
                    boosterRewardFallbackText.text  = string.Join("   ", parts);
                    boosterRewardFallbackText.color = Color.black;
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
                    label.text  = $"x{reward.count}";
                    label.color = Color.black;
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
        }

        private void RetryFight()
        {
            DOTween.KillAll();

            SceneLoader.Instance?.ReloadBossGameBoardScene();
        }

        private void GoToBossArena()
        {
            DOTween.KillAll();
            GameManager.Instance?.ChangeState(GameState.BossArena);
        }

        private static void SafeHide(GameObject go) { if (go != null) go.SetActive(false); }
        private static void SafeShow(GameObject go) { if (go != null) go.SetActive(true); }
    }
}
