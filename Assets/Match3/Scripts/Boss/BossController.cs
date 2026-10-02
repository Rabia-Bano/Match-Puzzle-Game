using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Match3
{
    public class BossController : MonoBehaviour
    {
        public static BossController Instance { get; private set; }

        [Header("Boss Config")]
        [Tooltip("Which boss this fight is. Loaded from Resources/Bosses/boss_<id> if left blank " +
                 "and PlayerPrefs 'SelectedBossId' is set (BossNodeController writes this key).")]
        [SerializeField] private BossData bossData;

        [Header("Core References")]
        [SerializeField] private BoardController   boardController;
        [SerializeField] private BoardGrid          boardGrid;
        [SerializeField] private BossAttackExecutor attackExecutor;

        [Tooltip("BossController owns input for this scene: forces it OFF in Awake() (so the board " +
                 "can never be played before the intro panel's Start button), turns it back ON from " +
                 "BeginFight(). Assign the same InputHandler the board itself uses.")]
        [SerializeField] private InputHandler inputHandler;

        [Tooltip("Shown automatically once bossData is ready. Leave blank to skip the intro entirely " +
                 "and begin the fight immediately on scene load (old behaviour).")]
        [SerializeField] private BossIntroPanel introPanel;

        [Tooltip("OPTIONAL — only needed if you want this specific fight to also have a move " +
                 "limit (BossResultManager's lose condition). Does NOT drive attack cadence.")]
        [SerializeField] private MoveCounter moveCounter;

        public BossData BossData => bossData;
        public int CurrentHealth { get; private set; }
        public int MaxHealth     { get; private set; }
        public bool IsDefeated   { get; private set; }
        public bool HasFightBegun { get; private set; }
        public BossAttack LastAttack { get; private set; }

        [Header("Events")]
        public UnityEvent OnBossDefeated;
        public UnityEvent OnBossAttack;

        public event System.Action<int> OnDamageTaken;
        public event System.Action<int> OnHealed;
        public event System.Action<int, int> OnHealthChanged;

        private Coroutine _defenseLoopHandle;
        private Coroutine _healLoopHandle;
        private int   _pendingSpecialWeaknessHits;
        private float _lastSpecialClearTime = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (bossData != null)
            {
                Debug.LogWarning($"[BossController] bossData is ALREADY assigned in the Inspector " +
                                  $"({bossData.name}) — this fight will ALWAYS use {bossData.bossName}, " +
                                  $"ignoring which boss node was tapped. If BossGameBoardScene is meant " +
                                  $"to be reused for every boss (normal setup), clear this field so it " +
                                  $"resolves dynamically from PlayerPrefs 'SelectedBossId' instead.", this);
            }

            if (bossData == null)
                bossData = LoadBossFromSelectedId();

            if (!boardController)  Debug.LogError("[BossController] boardController not assigned!", this);
            if (!boardGrid)         Debug.LogError("[BossController] boardGrid not assigned!", this);
            if (!attackExecutor)    Debug.LogError("[BossController] attackExecutor not assigned!", this);
            if (!inputHandler)      Debug.LogWarning("[BossController] inputHandler not assigned — " +
                                     "can't force input off before the intro panel / on for BeginFight().", this);
            if (bossData == null)   Debug.LogError("[BossController] No BossData assigned and none could be " +
                                     "resolved from PlayerPrefs 'SelectedBossId' — assign bossData in the Inspector.", this);

            inputHandler?.SetInputEnabled(false);
        }

        private void Start()
        {
            if (bossData == null) return;

            MaxHealth     = Mathf.Max(1, bossData.health);
            CurrentHealth = MaxHealth;
            IsDefeated    = false;
            HasFightBegun = false;

            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (introPanel != null)
            {
                introPanel.gameObject.SetActive(true);
                introPanel.ShowIntro(bossData, this);
                Debug.Log("[BossController] Showing boss intro panel — waiting for Start button.");
            }
            else
            {
                Debug.LogWarning("[BossController] No introPanel assigned — beginning fight immediately (no intro).");
                BeginFight();
            }
        }

        private void OnDestroy()
        {
            if (boardController != null)
                boardController.OnColorMatchResolved -= HandleColorMatchResolved;

            BossDamageEvents.OnSpecialTileCleared -= HandleSpecialTileCleared;
            BossDamageEvents.OnColorBombBlast     -= HandleColorBombBlast;

            if (Instance == this) Instance = null;
        }

        public void BeginFight()
        {
            if (HasFightBegun || bossData == null) return;
            HasFightBegun = true;

            inputHandler?.SetInputEnabled(true);
            BoosterManager.GetOrCreateInstance().BindToLevel(boardGrid, boardController, inputHandler, null, null);

            if (boardController != null)
                boardController.OnColorMatchResolved += HandleColorMatchResolved;

            BossDamageEvents.OnSpecialTileCleared += HandleSpecialTileCleared;
            BossDamageEvents.OnColorBombBlast     += HandleColorBombBlast;

            _defenseLoopHandle = StartCoroutine(PassiveDefenseLoop());
            _healLoopHandle    = StartCoroutine(SelfHealLoop());

            Debug.Log($"[BossController] Fight begins — {bossData.bossName}, HP {MaxHealth}, " +
                      $"weakness={bossData.weaknessTileType}. Defense every {bossData.attackIntervalSeconds}s, " +
                      $"heal every {bossData.healIntervalSeconds}s (+{bossData.healPercentPerTick}%).");
        }

        private void HandleColorMatchResolved(TileColor color, int matchSize)
        {
            if (!HasFightBegun || bossData == null || IsDefeated) return;
            if (color != bossData.weaknessTileType) return;

            float percent = GetDamagePercentForCount(matchSize);
            if (percent <= 0f) return;

            int amount = Mathf.Max(1, Mathf.CeilToInt(MaxHealth * (percent / 100f)));
            TakeDamage(amount);
        }

        private float GetDamagePercentForCount(int count)
        {
            if (count <= 0 || bossData == null) return 0f;
            if (count <= 2) return bossData.damagePercent1To2;
            if (count == 3) return bossData.damagePercent3;
            if (count == 4) return bossData.damagePercent4;
            return bossData.damagePercent5Plus;
        }

        private void HandleSpecialTileCleared(TileColor color)
        {
            if (!HasFightBegun || bossData == null || IsDefeated) return;
            if (color != bossData.weaknessTileType) return;

            _pendingSpecialWeaknessHits++;
            _lastSpecialClearTime = Time.time;
        }

        private void HandleColorBombBlast(TileColor color)
        {
            if (!HasFightBegun || bossData == null || IsDefeated) return;
            if (color != bossData.weaknessTileType) return;

            float percent = bossData.damagePercent5Plus;
            int amount = Mathf.Max(1, Mathf.CeilToInt(MaxHealth * (percent / 100f)));
            Debug.Log($"[BossController] Color Bomb blast on weakness colour → {percent}% → {amount} dmg.");
            TakeDamage(amount);
        }

        private void Update()
        {
            if (_pendingSpecialWeaknessHits <= 0) return;
            if (Time.time - _lastSpecialClearTime < bossData.specialClearBatchWindow) return;

            int count = _pendingSpecialWeaknessHits;
            _pendingSpecialWeaknessHits = 0;

            if (IsDefeated) return;

            float percent = GetDamagePercentForCount(count);
            int amount = Mathf.Max(1, Mathf.CeilToInt(MaxHealth * (percent / 100f)));
            Debug.Log($"[BossController] Special/combo damage: {count} weakness tile(s) cleared → {percent}% → {amount} dmg.");
            TakeDamage(amount);
        }

        public void TakeDamage(int amount)
        {
            if (IsDefeated || amount <= 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            OnDamageTaken?.Invoke(amount);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);

            Debug.Log($"[BossController] Boss took {amount} damage. HP: {CurrentHealth}/{MaxHealth}");

            if (CurrentHealth <= 0)
                Defeat();
        }

        public void Heal(int amount)
        {
            if (IsDefeated || amount <= 0) return;

            int before = CurrentHealth;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            if (CurrentHealth == before) return;

            OnHealed?.Invoke(CurrentHealth - before);
            OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
            Debug.Log($"[BossController] Boss self-healed +{CurrentHealth - before}. HP: {CurrentHealth}/{MaxHealth}");
        }

        private void Defeat()
        {
            if (IsDefeated) return;
            IsDefeated = true;

            if (_defenseLoopHandle != null) StopCoroutine(_defenseLoopHandle);
            if (_healLoopHandle    != null) StopCoroutine(_healLoopHandle);
            _pendingSpecialWeaknessHits = 0;

            Debug.Log("[BossController] Boss defeated!");
            OnBossDefeated?.Invoke();
        }

        private IEnumerator PassiveDefenseLoop()
        {
            while (!IsDefeated)
            {
                yield return new WaitForSeconds(bossData.attackIntervalSeconds);
                if (IsDefeated) yield break;
                yield return ExecuteEscalatingAttack();
            }
        }

        private IEnumerator ExecuteEscalatingAttack()
        {
            int severity = GetCurrentSeverity();

            BossAttackType[] pool = BuildAttackPool();
            BossAttackType chosen = pool[Random.Range(0, pool.Length)];

            BossAttack attack = new BossAttack
            {
                attackType      = chosen,
                attackParams    = severity,
                lockDuration    = bossData.freezeDuration,
                warningMessage  = WarningTextFor(chosen)
            };
            LastAttack = attack;

            OnBossAttack?.Invoke();
            Debug.Log($"[BossController] Defense incoming: {chosen} x{severity} " +
                      $"(HP {CurrentHealth}/{MaxHealth} = {(float)CurrentHealth / MaxHealth:P0}).");

            yield return new WaitForSeconds(bossData.attackWarningDelay);

            while (IsDefeated ||
                   (boardController != null && boardController.IsBusy) ||
                   (PetManager.Instance != null && PetManager.Instance.IsBusy))
            {
                if (IsDefeated) yield break;
                yield return null;
            }

            if (attackExecutor == null) yield break;

            AudioManager.Instance?.PlaySFX("boss_attack");
            JuiceManager.Instance?.Shake(0.2f, 0.1f);

            switch (chosen)
            {
                case BossAttackType.Jelly:
                    attackExecutor.ExecuteAddJelly(severity);
                    break;
                case BossAttackType.StoneTiles:
                    attackExecutor.ExecuteStoneTiles(severity);
                    break;
                case BossAttackType.AddObstacles:
                    attackExecutor.ExecuteAddObstacles(severity);
                    break;
                case BossAttackType.LockTiles:
                    attackExecutor.ExecuteLockTiles(severity, bossData.freezeDuration);
                    break;
            }
        }

        private BossAttackType[] BuildAttackPool()
        {
            if (bossData.attackPattern != null && bossData.attackPattern.Count > 0)
            {
                var distinct = new HashSet<BossAttackType>();
                foreach (BossAttack entry in bossData.attackPattern)
                    if (entry != null)
                        distinct.Add(entry.attackType);

                if (distinct.Count > 0)
                {
                    var arr = new BossAttackType[distinct.Count];
                    distinct.CopyTo(arr);
                    return arr;
                }
            }

            return new[]
            {
                BossAttackType.Jelly,
                BossAttackType.StoneTiles,
                BossAttackType.AddObstacles,
                BossAttackType.LockTiles
            };
        }

        private int GetCurrentSeverity()
        {
            float fraction = MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;
            if (fraction > bossData.severityTier2HpFraction) return 1;
            if (fraction > bossData.severityTier3HpFraction) return 2;
            return 3;
        }

        private static string WarningTextFor(BossAttackType type) => type switch
        {
            BossAttackType.Jelly        => "Boss is spreading Chain!",
            BossAttackType.StoneTiles   => "Boss is dropping stones!",
            BossAttackType.AddObstacles => "Boss is summoning rocks!",
            BossAttackType.LockTiles    => "Boss is freezing tiles!",
            _                           => "Boss is attacking!"
        };

        private IEnumerator SelfHealLoop()
        {
            while (!IsDefeated)
            {
                yield return new WaitForSeconds(bossData.healIntervalSeconds);
                if (IsDefeated) yield break;

                int amount = Mathf.Max(1, Mathf.CeilToInt(MaxHealth * (bossData.healPercentPerTick / 100f)));
                Heal(amount);
            }
        }

        private static BossData LoadBossFromSelectedId()
        {
            int id = PlayerPrefs.GetInt("SelectedBossId", -1);
            if (id < 0) return null;

            BossData data = Resources.Load<BossData>($"Bosses/boss_{id}");
            if (data == null)
                Debug.LogError($"[BossController] No BossData found at Resources/Bosses/boss_{id}.asset");
            return data;
        }
    }
}
