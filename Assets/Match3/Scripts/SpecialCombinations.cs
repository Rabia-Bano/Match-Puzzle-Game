// ============================================================
//  SpecialCombinations.cs  —  Set to use BoardController's shared pipeline
//
//  Before: Settle() here had its OWN gravity implementation AND its own
//  match-cascade loop — a third copy of logic that BoardController /
//  GravitySystem / BoardRefiller already own, and a second copy of what
//  SpecialTileActivator used to do too.
//
//  Now: Settle() just calls BoardController.SettleAfterExternalClear(),
//  so every "board settles after tiles are removed" path (normal match,
//  single special, special+special combo) runs through the exact same
//  gravity -> refill -> cascade code.
//
//  Jab do special tiles swap hon to yeh class decide karti hai
//  kaun sa combo effect fire hoga — that pattern-selection logic
//  (which combo, which cells it hits, its score multiplier) is
//  UNIQUE per combo and is kept exactly as before.
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
//
//  SwapController is klass ko call karta hai:
//    bool handled = specialCombinations.TryHandleCombo(tileA, tileB);
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
        [SerializeField] private BoardGrid       boardGrid;
        [SerializeField] private LevelManager    levelManager;
        [SerializeField] private BoardController boardController;  // owns gravity/refill/cascade now

        [Header("Special TileData Assets (for BombCombo replacement)")]
        [SerializeField] private TileData hStripedData;
        [SerializeField] private TileData vStripedData;
        [SerializeField] private TileData wrappedData;
        [SerializeField] private TileData colorBombData;

        [Header("5-Row Sweep Settings")]
        [Tooltip("Wrapped+Striped combo mein kitni rows/cols sweep hongi (3 = center ± 1)")]
        [SerializeField] private int wrappedStripedSweepCount = 3;

        [Header("Timings")]
        [SerializeField] private float settleDelay = 0.2f;

        // ── State ─────────────────────────────────────────────

        public bool IsRunning { get; private set; }

        // Final cleared tiles list for THIS combo — used only for the
        // combo's bonus-score multiplier (60/70/80/90/100). The actual
        // tile-clear + goal-notify work happens inside each effect's
        // ClearSingleTile() (unique wave/pulse animation per special type).
        private readonly List<Tile> _clearedThisCombo = new();

        // ─────────────────────────────────────────────────────
        //  PUBLIC ENTRY POINT
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// SwapController yahan call karta hai jab do tiles swap hon.
        /// Returns true agar ek combo handle hua (SwapController ko
        /// normal match check skip karna chahiye).
        /// </summary>
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
        //  COMBO COROUTINES  (pattern selection + bonus multiplier —
        //  unchanged, this is unique per-combo logic, not duplication)
        // ─────────────────────────────────────────────────────

        // ── 1. Striped + Striped → Full Row AND Column ────────
        private IEnumerator ComboStripedStriped(Tile tA, Tile tB)
        {
            IsRunning = true;
            _clearedThisCombo.Clear();

            int ax = tA.GridX, ay = tA.GridY;
            int bx = tB.GridX, by = tB.GridY;

            RemoveBothFromBoard(tA, tB);

            PlayComboFlash(boardGrid.GridToWorld(ax, ay));
            PlayComboFlash(boardGrid.GridToWorld(bx, by));

            yield return StartCoroutine(stripedEffect.BlastRow(ay, _clearedThisCombo));
            yield return StartCoroutine(stripedEffect.BlastColumn(ax, _clearedThisCombo));

            if (bx != ax)
                yield return StartCoroutine(stripedEffect.BlastColumn(bx, _clearedThisCombo));
            if (by != ay)
                yield return StartCoroutine(stripedEffect.BlastRow(by, _clearedThisCombo));

            levelManager?.AddScore(_clearedThisCombo.Count * 60);

            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        // ── 2. Wrapped + Striped → 5-Row/Column Sweep ─────────
        private IEnumerator ComboWrappedStriped(Tile wrapped, Tile striped)
        {
            IsRunning = true;
            _clearedThisCombo.Clear();

            int cx = wrapped.GridX, cy = wrapped.GridY;
            bool isHorizontal = striped.Data.specialType == SpecialType.RowBlast;

            RemoveBothFromBoard(wrapped, striped);
            PlayComboFlash(boardGrid.GridToWorld(cx, cy));

            int halfSweep = wrappedStripedSweepCount / 2;  // default = 1

            if (isHorizontal)
            {
                for (int dy = -halfSweep; dy <= halfSweep; dy++)
                {
                    int row = cy + dy;
                    if (row >= 0 && row < boardGrid.Height)
                        yield return StartCoroutine(stripedEffect.BlastRow(row, _clearedThisCombo));
                }
            }
            else
            {
                for (int dx = -halfSweep; dx <= halfSweep; dx++)
                {
                    int col = cx + dx;
                    if (col >= 0 && col < boardGrid.Width)
                        yield return StartCoroutine(stripedEffect.BlastColumn(col, _clearedThisCombo));
                }
            }

            levelManager?.AddScore(_clearedThisCombo.Count * 70);

            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        // ── 3. Wrapped + Wrapped → 5x5 blast then board clear ─
        private IEnumerator ComboWrappedWrapped(Tile tA, Tile tB)
        {
            IsRunning = true;
            _clearedThisCombo.Clear();

            int cx = tA.GridX, cy = tA.GridY;
            RemoveBothFromBoard(tA, tB);

            yield return StartCoroutine(wrappedEffect.Pulse3x3(cx, cy, _clearedThisCombo));
            yield return new WaitForSeconds(0.2f);
            yield return StartCoroutine(wrappedEffect.Pulse3x3(cx, cy, _clearedThisCombo));

            yield return StartCoroutine(Blast5x5AtPosition(cx, cy));

            levelManager?.AddScore(_clearedThisCombo.Count * 80);

            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        // ── 4. Color Bomb + Color Bomb → Entire Board ─────────
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

        // ── 5. Color Bomb + Striped/Wrapped → Replace color with special ─
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
                    boardGrid.RemoveTile(pos.x, pos.y);
                    Tile newSpecial = boardGrid.SpawnTile(pos.x, pos.y, replacementData);
                    if (newSpecial != null)
                    {
                        newSpecial.RefreshVisuals();
                        newSpecial.transform.DOPunchScale(Vector3.one * 0.4f, 0.2f, 4, 0.5f);
                    }
                    yield return new WaitForSeconds(0.03f);
                }

                yield return new WaitForSeconds(0.2f);

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

                foreach (Tile sp in specials)
                    yield return StartCoroutine(FireSingleSpecial(sp));
            }

            levelManager?.AddScore(_clearedThisCombo.Count * 90);

            yield return StartCoroutine(Settle());
            IsRunning = false;
        }

        // ─────────────────────────────────────────────────────
        //  HELPER COROUTINES
        // ─────────────────────────────────────────────────────

        private IEnumerator RainbowSweepClearAll()
        {
            for (int x = 0; x < boardGrid.Width; x++)
            {
                for (int y = 0; y < boardGrid.Height; y++)
                {
                    Tile t = boardGrid.GetTile(x, y);
                    if (t == null || t.State == TileState.Inactive) continue;

                    if (t.Data != null && !t.Data.isSpecial)
                        levelManager?.OnTileCleared(t.Data);

                    _clearedThisCombo.Add(t);

                    SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        Color rainbowColor = Color.HSVToRGB(
                            (x * boardGrid.Height + y) / (float)(boardGrid.Width * boardGrid.Height),
                            1f, 1f);
                        sr.DOColor(rainbowColor, 0.05f);
                    }

                    boardGrid.RemoveTile(x, y);
                    t.SetState(TileState.Matched);
                    t.transform.DOScale(0f, 0.1f)
                        .SetEase(Ease.InBack)
                        .OnComplete(() => t.transform.localScale = Vector3.one);
                }
                yield return new WaitForSeconds(0.05f);
            }
        }

        private IEnumerator Blast5x5AtPosition(int cx, int cy)
        {
            for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1) continue; // already cleared by 3x3

                Tile t = boardGrid.GetTile(cx + dx, cy + dy);
                if (t == null || t.State == TileState.Inactive) continue;

                if (t.Data != null && !t.Data.isSpecial)
                    levelManager?.OnTileCleared(t.Data);

                _clearedThisCombo.Add(t);

                boardGrid.RemoveTile(t.GridX, t.GridY);
                t.SetState(TileState.Matched);
                t.transform.DOScale(0f, 0.12f).SetEase(Ease.InBack)
                    .OnComplete(() => t.transform.localScale = Vector3.one);

                yield return new WaitForSeconds(0.03f);
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

        // ─────────────────────────────────────────────────────
        //  SETTLE — now delegates gravity + refill + cascade to
        //  BoardController instead of keeping a third copy of it.
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
                eff.boardGrid    = boardGrid;
                eff.levelManager = levelManager;
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
        //  AWAKE — validation
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
        }
    }
}