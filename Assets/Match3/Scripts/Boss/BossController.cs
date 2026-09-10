// ============================================================
//  BossController.cs  —  MonoBehaviour
//
//  The Boss Arena "brain".
//
//  DAMAGE RULE: only matches of bossData.weaknessTileType hurt the
//  boss. Damage is tiered by how many weakness tiles were cleared at
//  once — the exact percentages (default 1-2 tiles=1%, 3=2%, 4=3%,
//  5+=5%) now live on BossData (damagePercent1To2/3/4/5Plus) so each
//  boss can be tuned separately. This
//  applies to BOTH a regular match's size AND the total weakness
//  tiles cleared by one special-tile blast/combo (batched together —
//  see HandleSpecialTileCleared). Other colours still clear normally,
//  they just don't damage the boss.
//
//  PASSIVE DEFENSE LOOP / SELF-HEAL LOOP / DAMAGE SUBSCRIPTIONS now
//  ONLY start once BeginFight() is called — NOT automatically in
//  Start(). This is the fix for "boss kept attacking / matches kept
//  landing while the intro panel was still up (or invisible)":
//  previously everything auto-started the instant the scene loaded,
//  with nothing actually gating it behind the player pressing Start.
//
//  INTRO PANEL — BossController (proven-reliable: Awake() always
//  resolves bossData correctly, since regular damage already worked)
//  now DIRECTLY calls introPanel.ShowIntro(bossData) itself once
//  ready, instead of the old design where BossIntroPanel had to
//  subscribe to a LevelManager static event in its own OnEnable().
//  That old design silently failed whenever BossIntroPanel's
//  GameObject happened to start inactive in the scene (OnEnable never
//  ran → subscription never happened → the event fired into nothing).
//  A direct method call from a guaranteed-active object removes that
//  entire class of timing bug. BossController also forces the
//  GameObject active itself before calling Show(), so even an
//  accidentally-inactive panel GameObject in the scene self-heals.
//
//  Attach to: an empty "BossController" GameObject in BossGameBoardScene.
//  Wire up: bossData, boardController, boardGrid, attackExecutor,
//  inputHandler, introPanel. moveCounter is OPTIONAL (only used by
//  BossResultManager for an optional move-limit lose condition).
// ============================================================

using System.Collections;
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

        // NOTE: attack cadence, escalation thresholds, damage tiers, batch
        // window, and heal pacing all moved to BossData (per-boss tuning) —
        // see bossData.attackIntervalSeconds, .severityTier2HpFraction,
        // .damagePercent3, .healIntervalSeconds etc. below. This was a set of
        // MonoBehaviour fields before, which meant every boss fought with
        // identical pacing since BossGameBoardScene is one shared scene.

        // ── Public state ───────────────────────────────────────

        public BossData BossData => bossData;
        public int CurrentHealth { get; private set; }
        public int MaxHealth     { get; private set; }
        public bool IsDefeated   { get; private set; }
        public bool HasFightBegun { get; private set; }
        public BossAttack LastAttack { get; private set; }

        // ── Events ─────────────────────────────────────────────

        [Header("Events")]
        public UnityEvent OnBossDefeated;
        public UnityEvent OnBossAttack;

        public event System.Action<int> OnDamageTaken;      // amount
        public event System.Action<int> OnHealed;            // amount
        public event System.Action<int, int> OnHealthChanged; // (current, max)

        // ── Private ────────────────────────────────────────────

        private Coroutine _defenseLoopHandle;
        private Coroutine _healLoopHandle;
        private int   _pendingSpecialWeaknessHits;
        private float _lastSpecialClearTime = -1f;

        // ─────────────────────────────────────────────────────
        // LIFECYCLE
        // ─────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // MOST LIKELY CAUSE of "every boss fight shows Boss 1's data":
            // BossGameBoardScene is ONE shared scene reused for every boss —
            // bossData is only auto-resolved from PlayerPrefs when this field
            // is left EMPTY. If it was ever manually dragged in (e.g. while
            // testing Boss 1 before the selection list existed), it silently
            // overrides BossLevelLoader/BossNodeController's per-tap selection
            // forever, for every boss, since Awake() never even checks
            // PlayerPrefs when this is already non-null.
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

            // Board must be completely inert until BeginFight() — do this in
            // Awake (runs before ANY Start()) rather than waiting on whatever
            // LevelManager does or doesn't do in this scene.
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
                // Force it active even if it was accidentally left unchecked in
                // the scene — a direct call works on a disabled component, but
                // panelRoot.SetActive(true) inside it would still be invisible
                // if THIS parent GameObject itself is inactive.
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

            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Call this (from BossIntroPanel's Start button, or immediately if you
        /// skip the intro) to actually begin the fight: enables input, wires up
        /// every damage source, and kicks off the defense/heal loops. Safe to
        /// call more than once — later calls are no-ops.
        /// </summary>
        public void BeginFight()
        {
            if (HasFightBegun || bossData == null) return;
            HasFightBegun = true;

            inputHandler?.SetInputEnabled(true);
            BoosterManager.GetOrCreateInstance().BindToLevel(boardGrid, boardController, inputHandler, null, null);

            if (boardController != null)
                boardController.OnColorMatchResolved += HandleColorMatchResolved;

            BossDamageEvents.OnSpecialTileCleared += HandleSpecialTileCleared;

            _defenseLoopHandle = StartCoroutine(PassiveDefenseLoop());
            _healLoopHandle    = StartCoroutine(SelfHealLoop());

            Debug.Log($"[BossController] Fight begins — {bossData.bossName}, HP {MaxHealth}, " +
                      $"weakness={bossData.weaknessTileType}. Defense every {bossData.attackIntervalSeconds}s, " +
                      $"heal every {bossData.healIntervalSeconds}s (+{bossData.healPercentPerTick}%).");
        }

        // ─────────────────────────────────────────────────────
        // DAMAGE (called via BoardController's color-aware match event)
        // ─────────────────────────────────────────────────────

        private void HandleColorMatchResolved(TileColor color, int matchSize)
        {
            if (!HasFightBegun || bossData == null || IsDefeated) return;
            if (color != bossData.weaknessTileType) return;

            float percent = GetDamagePercentForCount(matchSize);
            if (percent <= 0f) return;

            int amount = Mathf.Max(1, Mathf.CeilToInt(MaxHealth * (percent / 100f)));
            TakeDamage(amount);
        }

        /// <summary>
        /// Shared damage-tier lookup — used for BOTH a regular match's size AND
        /// the total weakness-colour tiles cleared by one special-tile blast/combo.
        /// Reads this boss's own tiers (bossData.damagePercent1To2 / 3 / 4 / 5Plus)
        /// — no longer a hardcoded 1%/2%/3%/5%, so each boss can be tuned separately.
        /// </summary>
        private float GetDamagePercentForCount(int count)
        {
            if (count <= 0 || bossData == null) return 0f;
            if (count <= 2) return bossData.damagePercent1To2;
            if (count == 3) return bossData.damagePercent3;
            if (count == 4) return bossData.damagePercent4;
            return bossData.damagePercent5Plus; // 5+
        }

        /// <summary>
        /// Handles damage from special-tile blasts / combos (striped, wrapped,
        /// color-bomb, Bomb+Bomb, Rainbow+X). These clear tiles one at a time
        /// through SpecialTileEffect / SpecialCombinations' own paths,
        /// completely bypassing BoardController's match-group loop — so
        /// HandleColorMatchResolved() above never sees them on its own.
        ///
        /// Every weakness-colour tile cleared this way bumps a counter and
        /// stamps the time; Update() below waits for specialClearBatchWindow
        /// of silence (the WHOLE blast finished clearing) before scoring it —
        /// so a striped tile wiping out 6 weakness tiles in one row is scored
        /// as a single "5+" hit (5%), not six separate 1% pokes.
        ///
        /// Deliberately NOT a coroutine (unlike earlier versions of this file):
        /// StartCoroutine silently does nothing if this component is ever
        /// disabled when the event fires. Tallying here + flushing from
        /// Update() has no such dependency — Update() simply skips ticks
        /// while disabled and catches up the moment it's active again.
        /// </summary>
        private void HandleSpecialTileCleared(TileColor color)
        {
            if (!HasFightBegun || bossData == null || IsDefeated) return;
            if (color != bossData.weaknessTileType) return;

            _pendingSpecialWeaknessHits++;
            _lastSpecialClearTime = Time.time;
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

        /// <summary>
        /// Applies damage to the boss. Public so pet skills, boosters, or a
        /// special-tile blast on the boss's weakness colour can also call
        /// this directly if you want them to hurt the boss too.
        /// </summary>
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

        /// <summary>Boss self-heal — capped at MaxHealth. No-ops once defeated.</summary>
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

        // ─────────────────────────────────────────────────────
        // PASSIVE DEFENSE LOOP — every N seconds, forever (only after BeginFight())
        // ─────────────────────────────────────────────────────

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

            // Pick ONE hurdle type at random each tick — over several ticks the
            // boss cycles through a mix of jelly / stone / rock / freeze.
            BossAttackType[] pool =
            {
                BossAttackType.Jelly,
                BossAttackType.StoneTiles,
                BossAttackType.AddObstacles,
                BossAttackType.LockTiles
            };
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

            // Waits until BOTH the board and the equipped pet are fully idle —
            // guarantees the attack only ever touches a fully-settled board
            // (fixes hurdles landing on cells mid-combo-clear).
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

        /// <summary>1 = healthy (&gt;66% HP), 2 = hurt (33–66%), 3 = desperate (&lt;33%) — "wo khud ko bachane ki koshish karta hai".</summary>
        private int GetCurrentSeverity()
        {
            float fraction = MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;
            if (fraction > bossData.severityTier2HpFraction) return 1;
            if (fraction > bossData.severityTier3HpFraction) return 2;
            return 3;
        }

        private static string WarningTextFor(BossAttackType type) => type switch
        {
            BossAttackType.Jelly        => "Boss is spreading jelly!",
            BossAttackType.StoneTiles   => "Boss is dropping stones!",
            BossAttackType.AddObstacles => "Boss is summoning rocks!",
            BossAttackType.LockTiles    => "Boss is freezing tiles!",
            _                           => "Boss is attacking!"
        };

        // ─────────────────────────────────────────────────────
        // SELF-HEAL LOOP — every N seconds, forever (only after BeginFight())
        // ─────────────────────────────────────────────────────

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

        // ─────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────

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
