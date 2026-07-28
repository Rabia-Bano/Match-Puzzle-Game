// ============================================================
//  Tile.cs  —  MonoBehaviour
//
//  BUG FIX: ResetForPool() now kills any in-flight DOTween
//  animation (pop/scale/fade/punch/move) on this tile BEFORE
//  it goes back into the pool.
//
//  Why this mattered: every clear path (normal match, special
//  blast, combo) ends with BoardGrid.RemoveTile(), which calls
//  ResetForPool() and immediately returns the tile to ObjectPool.
//  The pool can hand that SAME GameObject straight back out for
//  a refill a moment later. If the old "shrink to zero" tween
//  from the clear animation was still running, it kept driving
//  this tile's scale toward 0 — so the freshly refilled tile
//  would flicker in and then vanish, leaving what looked like a
//  permanent hole on the board. This was rare with a single
//  3-match, but with a special+special combo (a full row/column/
//  5x5/whole-board clear) dozens of tiles get cleared-and-reused
//  in the same instant, making the bug show up reliably.
//
//  Attach to the Tile prefab.
//  The prefab needs:
//    • SpriteRenderer      (named "TileRenderer"  — child or root)
//    • SpriteRenderer      (named "HighlightRenderer" — child, starts disabled)
//    • Collider2D          (for touch/mouse input)
//
//  BoardGrid.SpawnTile() injects TileData + position after spawn.
// ============================================================

using UnityEngine;
using DG.Tweening;

namespace Match3
{
    // ── State enum ───────────────────────────────────────────

    /// <summary>Tracks what the tile is currently doing.</summary>
    public enum TileState
    {
        Normal,    // sitting idle on the board
        Selected,  // player has tapped / clicked it
        Matched,   // part of a detected match, about to be cleared
        Falling,   // dropping into an empty cell below
        Locked,    // cannot be moved (obstacle / ice / cage layer)
        Inactive   // returned to the pool, not visible
    }

    // ── Component ────────────────────────────────────────────

    [RequireComponent(typeof(SpriteRenderer))]
    public class Tile : MonoBehaviour
    {
        // ── Public data ──────────────────────────────────────

        /// <summary>The ScriptableObject describing this tile's colour and behaviour.</summary>
        public TileData Data { get; private set; }

        /// <summary>Column index on the board (X axis, left = 0).</summary>
        public int GridX { get; private set; }

        /// <summary>Row index on the board (Y axis, bottom = 0).</summary>
        public int GridY { get; private set; }

        /// <summary>Current logical state of this tile.</summary>
        public TileState State { get; private set; } = TileState.Inactive;

        /// <summary>
        /// Remaining hit points for a hard-tile obstacle (0 for every other
        /// tile). Set from Data.hardTileMaxHP in Initialize(), reduced by
        /// DamageObstacle() — called by HardTileManager whenever a normal
        /// tile clears in an adjacent cell.
        /// </summary>
        public int ObstacleHP { get; private set; }

        // ── Serialised visual refs ────────────────────────────

        [Header("Visual References")]
        [Tooltip("The SpriteRenderer that shows the tile face.")]
        [SerializeField] private SpriteRenderer tileRenderer;

        [Tooltip("A second SpriteRenderer used for the selection / match highlight.")]
        [SerializeField] private SpriteRenderer highlightRenderer;

        // ── Internal ──────────────────────────────────────────

        private static readonly Color _matchedTint = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color _lockedTint  = new Color(0.55f, 0.55f, 0.65f, 1f);

        // ── Initialisation ────────────────────────────────────

        /// <summary>
        /// Called by BoardGrid / TileSpawner right after spawning.
        /// Sets data, position, and refreshes visuals.
        /// </summary>
        public void Initialize(TileData data, int x, int y)
        {
            // Defensive second kill — in case anything queued a tween on this
            // GameObject between ObjectPool.Get() and this call.
            KillTweens();
            transform.localScale = Vector3.one;

            Data  = data;
            GridX = x;
            GridY = y;

            // Validate visual references (auto-find as fallback)
            if (tileRenderer == null)
                tileRenderer = GetComponent<SpriteRenderer>();

            if (highlightRenderer == null)
            {
                var child = transform.Find("HighlightRenderer");
                if (child != null)
                    highlightRenderer = child.GetComponent<SpriteRenderer>();
            }

            RefreshVisuals();

            // Hard tiles spawn Locked — this is what makes MatchFinder skip
            // them (can't be part of a colour match) and SwapController
            // reject swapping them (both checks already existed in the
            // project). Dropdown stones spawn Normal instead — they CAN be
            // swapped with a normal tile (helping maneuver them), and are
            // still excluded from colour matches, but via an explicit
            // isDropStone check in MatchFinder rather than the Locked state
            // (see MatchFinder.cs). Since only hard tiles stay Locked, the
            // existing SwapController check ("neither swapped tile may be
            // Locked") now naturally still blocks swapping WITH a hard tile,
            // while allowing stone <-> normal-tile swaps.
            if (data != null && data.isHardTile)
            {
                ObstacleHP = Mathf.Max(1, data.hardTileMaxHP);
                SetState(TileState.Locked);
            }
            else
            {
                ObstacleHP = 0;
                SetState(TileState.Normal);
            }
        }

        // ── Hard tile damage ──────────────────────────────────

        /// <summary>
        /// Applies 1 point of damage to a hard-tile obstacle and updates its
        /// crack-stage sprite. Returns true if this damage broke it (HP hit 0).
        /// Does nothing (returns false) if this tile isn't a hard tile or is
        /// already broken. Called by HardTileManager.
        /// </summary>
        public bool DamageObstacle()
        {
            if (Data == null || !Data.isHardTile || ObstacleHP <= 0) return false;

            ObstacleHP--;
            RefreshObstacleDamageSprite();

            transform.DOKill();
            transform.DOPunchScale(Vector3.one * 0.12f, 0.15f, 4, 0.6f);

            return ObstacleHP <= 0;
        }

        private void RefreshObstacleDamageSprite()
        {
            if (Data == null || !Data.isHardTile || tileRenderer == null) return;
            if (Data.hardTileDamageSprites == null || Data.hardTileDamageSprites.Length == 0) return;

            int hitsTaken   = Data.hardTileMaxHP - ObstacleHP;
            int spriteIndex = Mathf.Clamp(hitsTaken - 1, 0, Data.hardTileDamageSprites.Length - 1);

            if (hitsTaken > 0)
                tileRenderer.sprite = Data.hardTileDamageSprites[spriteIndex];
        }
        // ── Grid position ─────────────────────────────────────

        /// <summary>Updates the grid indices (does NOT move the transform).</summary>
        public void SetGridPosition(int x, int y)
        {
            GridX = x;
            GridY = y;
        }

        // ── State machine ─────────────────────────────────────

        /// <summary>Transitions the tile to a new state and updates visuals.</summary>
        public void SetState(TileState newState)
        {
            State = newState;
            ApplyStateTint();
            UpdateHighlight();
        }

        // ── Visual helpers ────────────────────────────────────

        /// <summary>Applies the correct sprite and base colour from TileData.</summary>
        public void RefreshVisuals()
        {
            if (Data == null || tileRenderer == null) return;

            tileRenderer.sprite = Data.sprite;
            tileRenderer.color  = Color.white;   // reset any tint from previous state
        }

        private void ApplyStateTint()
        {
            if (tileRenderer == null) return;

            tileRenderer.color = State switch
            {
                TileState.Matched  => _matchedTint,
                TileState.Locked   => _lockedTint,
                _                  => Color.white
            };
        }

        private void UpdateHighlight()
        {
            if (highlightRenderer == null) return;

            bool showHighlight = State == TileState.Selected;
            highlightRenderer.enabled = showHighlight;

            if (showHighlight && Data?.highlightSprite != null)
                highlightRenderer.sprite = Data.highlightSprite;
        }

        // ── Pool helpers ──────────────────────────────────────

        /// <summary>
        /// Resets tile to a clean state before returning to the pool.
        /// FIX: kills any still-running tween FIRST, and resets scale/alpha
        /// to their resting values, so a pooled-and-reused tile never
        /// inherits a leftover shrink/fade/move animation from its
        /// previous life on the board.
        /// </summary>
        public void ResetForPool()
        {
            KillTweens();
            transform.localScale = Vector3.one;

            Data  = null;
            GridX = -1;
            GridY = -1;
            ObstacleHP = 0;

            if (tileRenderer != null)
            {
                tileRenderer.sprite = null;
                tileRenderer.color  = Color.white;
            }

            if (highlightRenderer != null)
                highlightRenderer.enabled = false;

            SetState(TileState.Inactive);
        }

        /// <summary>
        /// Kills every DOTween tween targeting this tile's transform AND its
        /// renderers (scale/move/punch tweens live on transform; fade/color
        /// tweens live on the SpriteRenderer). Safe to call even if nothing
        /// is currently animating.
        /// </summary>
        private void KillTweens()
        {
            transform.DOKill();
            if (tileRenderer != null)     tileRenderer.DOKill();
            if (highlightRenderer != null) highlightRenderer.DOKill();
        }

        // ── Debug ─────────────────────────────────────────────

        public override string ToString() =>
            $"Tile[{GridX},{GridY}] Color={Data?.color} State={State}";
    }
}