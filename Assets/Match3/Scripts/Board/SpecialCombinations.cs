// ============================================================
//  SpecialCombinations.cs  —  Set to use BoardController's shared pipeline
//
//  Jab do special tiles swap hon to yeh class decide karti hai
//  kaun sa combo effect fire hoga.
//
//  REDESIGN (bug report ke baad — hard tile damage):
//    ClearOneObstacleAwareTile() (Bomb+Bomb aur Rainbow+Rainbow combos
//    ke liye) mein ab hard tile ka apna cell agar combo ke target area
//    mein ho to wahi DIRECT HIT gina jata hai — turant damage. Poori
//    adjacency-based DamageAndClearAdjacentHardTiles() method hata
//    di gayi hai — ab kahin bhi "paas wali cell clear hui isliye
//    hardtile bhi clear ho gayi" wala behavior nahi hai.
//
//    hardTileManager field bhi hata diya gaya hai — damage ab seedha
//    Tile.DamageObstacle() se lagta hai, manager ki zaroorat nahi.
//
//  Combo Table:
//  ┌──────────────────┬──────────────────────────────────────────┐
//  │ Combo            │ Effect                                   │
//  ├──────────────────┼──────────────────────────────────────────┤
//  │ Striped+Striped  │ Poori ROW + poora COLUMN dono clear      │
//  │ Wrapped+Striped  │ 5 rows ya 5 columns sweep (3 row pass)   │
//  │ Bomb+Bomb        │ Poora board clear                        │
//  │ Bomb+Striped     │ Sab us color ki tiles striped ban jayen  │
//  │                  │ aur phir activate hon                    │
//  └──────────────────┴──────────────────────────────────────────┘
//
//  Attach to: SpecialEffectsManager (same GameObject as effects)
//  Wire all effect references + boardController in Inspector.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    [RequireComponent(typeof(StripedTileEffect))]
    [RequireComponent(typeof(WrappedTileEffect))]
    [RequireComponent(typeof(ColorBombEffect))]
    public class SpecialCombinations : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Effect Component References")]
        [SerializeField] private StripedTileEffect stripedEffect;
        [SerializeField] private WrappedTileEffect wrappedEffect;
        [SerializeField] private ColorBombEffect   colorBombEffect;

        [Header("Board References")]
        [SerializeField] private BoardGrid            boardGrid;
        [SerializeField] private LevelManager         levelManager;
        [SerializeField] private BoardController      boardController;  // owns gravity/refill/cascade now
        [SerializeField] private SpecialTileActivator specialActivator;  // lets combo blasts chain-fire specials they catch
        [SerializeField] private JellyManager         jellyManager;      // lets combo blasts decrement jelly under tiles they clear

        [Header("Special TileData Assets (for BombCombo replacement)")]
        [SerializeField] private TileData hStripedData;
        [SerializeField] private TileData vStripedData;
        [SerializeField] private TileData wrappedData;
        [SerializeField] private TileData colorBombData;

        [Header("5-Row Sweep Settings")]
        [SerializeField] private int wrappedStripedSweepCount = 3;

        [Header("Rainbow + Other Combo Speed (color bomb + row/column/adjacent bomb)")]
        [Tooltip("Delay between spawning each replacement special tile while the " +
                 "color bomb converts same-color tiles. Lower = faster. Only affects " +
                 "the Rainbow+Other combo, nothing else.")]
        [SerializeField] private float rainbowComboSpawnStagger = 0.006f;   // was hardcoded 0.015f
        [Tooltip("Buffer pause after all replacement tiles are spawned, before they " +
                 "fire. Only affects the Rainbow+Other combo.")]
        [SerializeField] private float rainbowComboFireBuffer = 0.05f;      // was hardcoded 0.1f

        [Header("Other Special+Special Combo Speed")]
        [Tooltip("Pause between the two pulses in a Bomb+Bomb combo (Candy-Crush-style " +
                 "double pulse). Only affects Bomb+Bomb.")]
        [SerializeField] private float wrappedWrappedPulseGap = 0.04f;      // was hardcoded 0.08f
        [Tooltip("Delay per tile in the outer 5x5 ring of a Bomb+Bomb combo. Only " +
                 "affects Bomb+Bomb.")]
        [SerializeField] private float wrappedWrappedRingStagger = 0.006f;  // was hardcoded 0.015f
        [Tooltip("Delay per column while a Rainbow+Rainbow combo sweeps the whole " +
                 "board. Only affects Rainbow+Rainbow.")]
        [SerializeField] private float rainbowRainbowColumnStagger = 0.01f; // was hardcoded 0.025f

        [Header("Timings")]
        [SerializeField] private float settleDelay = 0.08f;

        // ── State ─────────────────────────────────────────────

        public bool IsRunning { get; private set; }

        private readonly List<Tile> _clearedThisCombo = new();

        // ─────────────────────────────────────────────────────
        //  PUBLIC ENTRY POINT
        // ─────────────────────────────────────────────────────

        public bool TryHandleCombo(Tile tileA, Tile tileB)
        {
            if (!IsSpecial(tileA) || !IsSpecial(tileB)) return false;

            SpecialType typeA = tileA.Data.specialType;
            SpecialType typeB = tileB.Data.specialType;

            if (typeA == SpecialType.Rainbow && typeB == SpecialType.Rainbow)
            {
                StartCoroutine(ComboEntireBoard(tileA, tileB));
                return true;
            }

            if (typeA == SpecialType.Rainbow || typeB == SpecialType.Rainbow)
            {
                Tile rainbow = typeA == SpecialType.Rainbow ? tileA : tileB;
                Tile other   = typeA == SpecialType.Rainbow ? tileB : tileA;
                StartCoroutine(ComboRainbowWithOther(rainbow, other));
                return true;
            }

            if (IsStriped(typeA) && IsStriped(typeB))
            {
                StartCoroutine(ComboStripedStriped(tileA, tileB));
                return true;
            }

            if ((typeA == SpecialType.Bomb && IsStriped(typeB))
             || (typeB == SpecialType.Bomb && IsStriped(typeA)))
            {
                Tile wrapped = typeA == SpecialType.Bomb ? tileA : tileB;
                Tile striped = typeA == SpecialType.Bomb ? tileB : tileA;
                StartCoroutine(ComboWrappedStriped(wrapped, striped));
                return true;
            }

            if (typeA == SpecialType.Bomb && typeB == SpecialType.Bomb)
            {
                StartCoroutine(ComboWrappedWrapped(tileA, tileB));
                return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────────────
        //  COMBO COROUTINES
        // ─────────────────────────────────────────────────────

        private IEnumerator ComboStripedStriped(Tile tA, Tile tB)
        {
            IsRunning = true;
            _clearedThisCombo.Clear();

            int ax = tA.GridX, ay = tA.GridY;

            RemoveBothFromBoard(tA, tB);

            PlayComboFlash(boardGrid.GridToWorld(ax, ay));

            // SPEED FIX: row and column only ever share the swap's own center
            // cell, which is already removed above (RemoveBothFromBoard) — so
            // they never touch the same live tile. Safe to fire both at once
            // instead of waiting for the row to fully finish before starting
            // the column.
            int pendingCross = 2;
            StartCoroutine(RunCounted(stripedEffect.BlastRow(ay, _clearedThisCombo), () => pendingCross--));
            StartCoroutine(RunCounted(stripedEffect.BlastColumn(ax, _clearedThisCombo), () => pendingCross--));
            yield return new WaitUntil(() => pendingCross <= 0);

            levelManager?.AddScore(_clearedThisCombo.Count * 60);

            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        private IEnumerator ComboWrappedStriped(Tile wrapped, Tile striped)
        {
            IsRunning = true;
            _clearedThisCombo.Clear();

            int cx = wrapped.GridX, cy = wrapped.GridY;
            bool isHorizontal = striped.Data.specialType == SpecialType.RowBlast;

            RemoveBothFromBoard(wrapped, striped);
            PlayComboFlash(boardGrid.GridToWorld(cx, cy));

            int halfSweep = wrappedStripedSweepCount / 2;

            // SPEED FIX: each row (or column) in the sweep is a separate line —
            // no two share a cell — so fire them all together instead of one
            // finishing before the next starts.
            int pendingSweep = 0;

            if (isHorizontal)
            {
                for (int dy = -halfSweep; dy <= halfSweep; dy++)
                {
                    int row = cy + dy;
                    if (row >= 0 && row < boardGrid.Height)
                    {
                        pendingSweep++;
                        StartCoroutine(RunCounted(stripedEffect.BlastRow(row, _clearedThisCombo), () => pendingSweep--));
                    }
                }
            }
            else
            {
                for (int dx = -halfSweep; dx <= halfSweep; dx++)
                {
                    int col = cx + dx;
                    if (col >= 0 && col < boardGrid.Width)
                    {
                        pendingSweep++;
                        StartCoroutine(RunCounted(stripedEffect.BlastColumn(col, _clearedThisCombo), () => pendingSweep--));
                    }
                }
            }

            if (pendingSweep > 0)
                yield return new WaitUntil(() => pendingSweep <= 0);

            levelManager?.AddScore(_clearedThisCombo.Count * 70);

            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        private IEnumerator ComboWrappedWrapped(Tile tA, Tile tB)
        {
            IsRunning = true;
            _clearedThisCombo.Clear();

            int cx = tA.GridX, cy = tA.GridY;
            RemoveBothFromBoard(tA, tB);

            yield return StartCoroutine(wrappedEffect.Pulse3x3(cx, cy, _clearedThisCombo));
            yield return new WaitForSeconds(wrappedWrappedPulseGap);
            yield return StartCoroutine(wrappedEffect.Pulse3x3(cx, cy, _clearedThisCombo));

            yield return StartCoroutine(Blast5x5AtPosition(cx, cy));

            levelManager?.AddScore(_clearedThisCombo.Count * 80);

            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        private IEnumerator ComboEntireBoard(Tile tA, Tile tB)
        {
            IsRunning = true;
            _clearedThisCombo.Clear();

            RemoveBothFromBoard(tA, tB);
            yield return StartCoroutine(RainbowSweepClearAll());

            levelManager?.AddScore(_clearedThisCombo.Count * 100);

            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        private IEnumerator ComboRainbowWithOther(Tile rainbow, Tile other)
        {
            IsRunning = true;
            _clearedThisCombo.Clear();

            SpecialType otherType = other.Data.specialType;
            TileData replacementData = GetDataForType(otherType);

            TileColor targetColor = other.Data.isSpecial
                ? GetMostCommonColorOnBoard()
                : other.Data.color;

            int rx = rainbow.GridX, ry = rainbow.GridY;
            RemoveBothFromBoard(rainbow, other);

            if (targetColor == TileColor.None || replacementData == null)
            {
                colorBombEffect.SetTargetColor(GetMostCommonColorOnBoard());
                yield return StartCoroutine(
                    colorBombEffect.Activate(new Vector2Int(rx, ry), _clearedThisCombo));
            }
            else
            {
                var positions = GetPositionsOfColor(targetColor);

                foreach (var pos in positions)
                {
                    // FIX: yeh tile ab replace ho raha hai (striped ban raha hai),
                    // is se pehle agar iske neeche jelly hai to wo yahin peel karo —
                    // warna yeh cell na to abhi jelly-decrement paata hai, na baad
                    // mein FireSingleSpecial() ke blast scan mein aata hai (kyunki
                    // wahan tak pahunchte pahunchte yeh position pehle hi khali
                    // ho chuki hoti hai), aur jelly permanently reh jati hai.
                    if (jellyManager != null && jellyManager.DecrementAt(pos.x, pos.y))
                        levelManager?.OnJellyCleared();

                    boardGrid.RemoveTile(pos.x, pos.y);
                    Tile newSpecial = boardGrid.SpawnTile(pos.x, pos.y, replacementData);
                    if (newSpecial != null)
                    {
                        newSpecial.RefreshVisuals();
                        newSpecial.transform.DOPunchScale(Vector3.one * 0.4f, 0.2f, 4, 0.5f);
                    }
                    yield return new WaitForSeconds(rainbowComboSpawnStagger);
                }

                yield return new WaitForSeconds(rainbowComboFireBuffer);

                var specials = new List<Tile>();
                for (int x = 0; x < boardGrid.Width;  x++)
                for (int y = 0; y < boardGrid.Height; y++)
                {
                    Tile t = boardGrid.GetTile(x, y);
                    if (t != null && t.Data != null
                        && t.Data.isSpecial
                        && t.Data.specialType == otherType)
                        specials.Add(t);
                }

                // SPEED FIX: these newly-spawned specials sit on completely
                // separate cells (no two ever share a position), so firing them
                // ONE AT A TIME (the old `yield return StartCoroutine(...)` per
                // tile, waiting for each to fully finish before starting the
                // next) was pure serial waiting for no reason — SpecialTileEffect
                // subclasses hold no mutable shared state between calls (verified:
                // only read-only Inspector fields), so running them concurrently
                // is safe. Fire them all together and wait once for all to finish,
                // instead of waiting once per tile.
                int pending = specials.Count;
                foreach (Tile sp in specials)
                    StartCoroutine(FireSingleSpecialCounted(sp, () => pending--));

                if (pending > 0)
                    yield return new WaitUntil(() => pending <= 0);
            }

            levelManager?.AddScore(_clearedThisCombo.Count * 90);

            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        // ─────────────────────────────────────────────────────
        //  OBSTACLE-AWARE SINGLE TILE CLEAR — shared by the raw
        //  combo methods below (Blast5x5AtPosition, RainbowSweepClearAll)
        //  that don't go through StripedTileEffect/WrappedTileEffect/
        //  ColorBombEffect directly.
        //    • Special tile   → chain-fire instead of erasing
        //    • Hard tile      → its OWN cell is inside this combo's target
        //                       area, so this is a DIRECT hit — damage it
        //                       right here (1 point), never adjacency-based
        //    • Dropdown stone → SKIPPED — immune to everything except
        //                       actually reaching the bottom row
        //    • Normal tile    → OnTileCleared + jelly decrement
        // ─────────────────────────────────────────────────────

        private IEnumerator ClearOneObstacleAwareTile(Tile t, List<Tile> cleared)
        {
            if (t == null || t.State == TileState.Inactive) yield break;
            if (boardGrid.GetTile(t.GridX, t.GridY) != t) yield break;

            if (t.Data != null && t.Data.isSpecial)
            {
                if (specialActivator != null)
                {
                    cleared.Add(t);
                    yield return StartCoroutine(specialActivator.ChainActivate(t));
                }
                yield break;
            }

            // FIX: this cell is inside the combo's own target area — a
            // DIRECT hit. Damage it right here instead of skipping it.
            if (t.Data != null && t.Data.isHardTile)
            {
                DamageHardTileDirect(t, cleared);
                yield break;
            }

            if (t.Data != null && t.Data.isDropStone) yield break;   // immune to everything except reaching bottom

            if (t.Data != null)
                levelManager?.OnTileCleared(t.Data);

            if (jellyManager != null && jellyManager.DecrementAt(t.GridX, t.GridY))
                levelManager?.OnJellyCleared();

            // NEW — Boss Arena fix: same bridge as SpecialTileEffect.ClearNormalTileTracked().
            // Combo blasts (this method) have their own separate clear path and were
            // never damaging the boss either.
            if (t.Data != null)
                BossDamageEvents.OnSpecialTileCleared?.Invoke(t.Data.color);

            cleared.Add(t);

            boardGrid.RemoveTile(t.GridX, t.GridY);
            t.SetState(TileState.Matched);
            t.transform.DOScale(0f, 0.12f).SetEase(Ease.InBack)
                .OnComplete(() => t.transform.localScale = Vector3.one);
        }

        /// <summary>
        /// Same direct-hit hard tile logic as SpecialTileEffect.DamageHardTileDirect() —
        /// duplicated here in a small local form because SpecialCombinations is a
        /// plain MonoBehaviour, not a SpecialTileEffect subclass.
        /// </summary>
        private void DamageHardTileDirect(Tile t, List<Tile> cleared)
        {
            bool broke = t.DamageObstacle();
            if (!broke) return;   // took damage, still standing

            levelManager?.OnHardTileCleared();
            cleared.Add(t);
            boardGrid.RemoveTile(t.GridX, t.GridY);
            t.SetState(TileState.Matched);
            t.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack)
                .OnComplete(() => t.transform.localScale = Vector3.one);
        }

        private IEnumerator RainbowSweepClearAll()
        {
            for (int x = 0; x < boardGrid.Width; x++)
            {
                // SPEED FIX: tiles inside the same column never share a cell,
                // so clear them all together — before this, each tile cost its
                // own sequential frame of wait (via `yield return
                // StartCoroutine(...)`) even though there was no real reason
                // for tile 2 to wait for tile 1 to finish first. The left-to-
                // right column sweep (the stagger below) is kept exactly as
                // before — only the wasted per-tile wait inside each column
                // is removed.
                int pendingColumn = 0;

                for (int y = 0; y < boardGrid.Height; y++)
                {
                    Tile t = boardGrid.GetTile(x, y);
                    if (t == null || t.State == TileState.Inactive) continue;

                    bool willActuallyClear = t.Data != null && !t.Data.isSpecial && !t.Data.isHardTile && !t.Data.isDropStone;
                    if (willActuallyClear)
                    {
                        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            Color rainbowColor = Color.HSVToRGB(
                                (x * boardGrid.Height + y) / (float)(boardGrid.Width * boardGrid.Height),
                                1f, 1f);
                            sr.DOColor(rainbowColor, 0.05f);
                        }
                    }

                    pendingColumn++;
                    StartCoroutine(RunCounted(ClearOneObstacleAwareTile(t, _clearedThisCombo), () => pendingColumn--));
                }

                if (pendingColumn > 0)
                    yield return new WaitUntil(() => pendingColumn <= 0);

                yield return new WaitForSeconds(rainbowRainbowColumnStagger);
            }
        }

        private IEnumerator Blast5x5AtPosition(int cx, int cy)
        {
            for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1) continue; // already cleared by 3x3

                Tile t = boardGrid.GetTile(cx + dx, cy + dy);
                yield return StartCoroutine(ClearOneObstacleAwareTile(t, _clearedThisCombo));
                yield return new WaitForSeconds(wrappedWrappedRingStagger);
            }
        }

        private IEnumerator FireSingleSpecial(Tile tile)
        {
            if (tile == null || tile.State == TileState.Inactive) yield break;
            if (boardGrid.GetTile(tile.GridX, tile.GridY) != tile) yield break;

            Vector2Int pos = new Vector2Int(tile.GridX, tile.GridY);
            SpecialType st = tile.Data.specialType;

            boardGrid.RemoveTile(pos.x, pos.y);
            SetEffectReferences();

            switch (st)
            {
                case SpecialType.RowBlast:
                case SpecialType.ColBlast:
                    yield return StartCoroutine(stripedEffect.Activate(pos, _clearedThisCombo));
                    break;
                case SpecialType.Bomb:
                    yield return StartCoroutine(wrappedEffect.Activate(pos, _clearedThisCombo));
                    break;
                case SpecialType.Rainbow:
                    yield return StartCoroutine(colorBombEffect.Activate(pos, _clearedThisCombo));
                    break;
            }
        }

        /// <summary>
        /// Wraps FireSingleSpecial() with a completion callback so several of
        /// these can be started together (StartCoroutine, no yield) and waited
        /// on once as a group — used by ComboRainbowWithOther() to fire all the
        /// newly-created specials in parallel instead of one after another.
        /// </summary>
        private IEnumerator FireSingleSpecialCounted(Tile tile, System.Action onDone)
        {
            yield return StartCoroutine(FireSingleSpecial(tile));
            onDone?.Invoke();
        }

        /// <summary>
        /// Generic version of the same pattern — runs any coroutine and reports
        /// back via callback when it's done, so several independent coroutines
        /// (e.g. BlastRow + BlastColumn, or several sweep lines) can be started
        /// together and waited on once as a group instead of one after another.
        /// </summary>
        private IEnumerator RunCounted(IEnumerator routine, System.Action onDone)
        {
            yield return StartCoroutine(routine);
            onDone?.Invoke();
        }

        // ─────────────────────────────────────────────────────
        //  SETTLE
        // ─────────────────────────────────────────────────────

        private IEnumerator Settle()
        {
            yield return new WaitForSeconds(settleDelay);
            yield return StartCoroutine(boardController.SettleAfterExternalClear());
        }

        // ─────────────────────────────────────────────────────
        //  UTILITY
        // ─────────────────────────────────────────────────────

        private void SetEffectReferences()
        {
            foreach (var eff in new SpecialTileEffect[] { stripedEffect, wrappedEffect, colorBombEffect })
            {
                eff.boardGrid        = boardGrid;
                eff.levelManager     = levelManager;
                eff.specialActivator = specialActivator;
                eff.jellyManager     = jellyManager;
            }
        }

        private void RemoveBothFromBoard(Tile a, Tile b)
        {
            if (a != null) boardGrid.RemoveTile(a.GridX, a.GridY);
            if (b != null) boardGrid.RemoveTile(b.GridX, b.GridY);
        }

        private static bool IsSpecial(Tile t) =>
            t != null && t.Data != null && t.Data.isSpecial;

        private static bool IsStriped(SpecialType t) =>
            t == SpecialType.RowBlast || t == SpecialType.ColBlast;

        private TileColor GetMostCommonColorOnBoard()
        {
            var counts = new Dictionary<TileColor, int>();
            for (int x = 0; x < boardGrid.Width;  x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                if (t == null || t.Data == null) continue;
                if (t.Data.color == TileColor.None || t.Data.isSpecial) continue;
                counts.TryGetValue(t.Data.color, out int c);
                counts[t.Data.color] = c + 1;
            }
            if (counts.Count == 0) return TileColor.None;
            TileColor best = TileColor.None; int bestCount = 0;
            foreach (var kv in counts)
                if (kv.Value > bestCount) { bestCount = kv.Value; best = kv.Key; }
            return best;
        }

        private List<Vector2Int> GetPositionsOfColor(TileColor color)
        {
            var list = new List<Vector2Int>();
            for (int x = 0; x < boardGrid.Width;  x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                if (t != null && t.Data != null
                    && !t.Data.isSpecial
                    && t.Data.color == color)
                    list.Add(new Vector2Int(x, y));
            }
            return list;
        }

        private TileData GetDataForType(SpecialType t) => t switch
        {
            SpecialType.RowBlast => hStripedData,
            SpecialType.ColBlast => vStripedData,
            SpecialType.Bomb     => wrappedData,
            SpecialType.Rainbow  => colorBombData,
            _                    => null
        };

        private void PlayComboFlash(Vector3 pos)
        {
            Camera.main?.transform.DOShakePosition(0.2f, 0.08f, 10, 45f);
        }

        // ─────────────────────────────────────────────────────

        private void Awake()
        {
            if (stripedEffect   == null) stripedEffect   = GetComponent<StripedTileEffect>();
            if (wrappedEffect   == null) wrappedEffect   = GetComponent<WrappedTileEffect>();
            if (colorBombEffect == null) colorBombEffect = GetComponent<ColorBombEffect>();

            SetEffectReferences();

            if (!boardGrid)       Debug.LogError("[SpecialCombinations] boardGrid missing!",       this);
            if (!levelManager)    Debug.LogError("[SpecialCombinations] levelManager missing!",    this);
            if (!boardController) Debug.LogError("[SpecialCombinations] boardController missing! Gravity/refill/cascade will NOT run.", this);
            if (!specialActivator) Debug.LogWarning("[SpecialCombinations] specialActivator not assigned — specials caught inside a combo blast will be silently erased instead of chain-firing.", this);
            if (!jellyManager) Debug.Log("[SpecialCombinations] jellyManager not assigned — fine if this level has no jelly obstacles.", this);
        }
    }
}