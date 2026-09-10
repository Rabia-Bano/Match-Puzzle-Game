// ============================================================
//  BossAttackExecutor.cs  —  MonoBehaviour
//
//  Does the actual board manipulation for each BossAttackType.
//  Kept separate from BossController on purpose — BossController
//  owns WHEN/WHICH attack fires, this owns HOW it touches the board.
//  Same separation of concerns as PetSkill (the "how") vs PetManager
//  (the "when") in the existing pet system.
//
//  Attach to: an empty "BossAttackExecutor" GameObject in BossArenaScene
//  (sibling of BossController / BoardController).
//  Wire up: boardGrid, moveCounter, hardTileData, dropStoneData.
// ============================================================

using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Match3
{
    public class BossAttackExecutor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardGrid    boardGrid;
        [SerializeField] private MoveCounter  moveCounter;
        [SerializeField] private JellyManager jellyManager;

        [Header("Obstacle Tile Data")]
        [Tooltip("A TileData asset with isHardTile = true (a 'rock' blocker). Used by ExecuteAddObstacles().")]
        [SerializeField] private TileData hardTileData;

        [Tooltip("A TileData asset with isDropStone = true (falls with gravity). Used by ExecuteStoneTiles().")]
        [SerializeField] private TileData dropStoneData;

        [Header("Lock Tiles")]
        [Tooltip("Default freeze duration if a BossAttack doesn't specify its own lockDuration (should rarely be hit — BossController always passes one).")]
        [SerializeField] private float defaultLockDuration = 6f;

        [Tooltip("Punch-scale + tint feedback duration when a tile gets frozen/thawed.")]
        [SerializeField] private float feedbackDuration = 0.25f;

        // Tiles this executor has frozen and still owns the "thaw" responsibility for.
        private readonly List<Tile> _bossFrozenTiles = new List<Tile>();

        private void Awake()
        {
            if (!boardGrid) Debug.LogError("[BossAttackExecutor] boardGrid not assigned!", this);
            if (!moveCounter) Debug.LogWarning("[BossAttackExecutor] moveCounter not assigned — ExecuteReduceMoves() will do nothing.", this);
            if (!hardTileData) Debug.LogWarning("[BossAttackExecutor] hardTileData not assigned — ExecuteAddObstacles() will do nothing.", this);
            if (!dropStoneData) Debug.LogWarning("[BossAttackExecutor] dropStoneData not assigned — ExecuteStoneTiles() will do nothing.", this);
            if (!jellyManager) Debug.LogWarning("[BossAttackExecutor] jellyManager not assigned — ExecuteAddJelly() will do nothing.", this);
        }

        // ─────────────────────────────────────────────────────
        // LOCK TILES — freezes N random normal tiles for a duration
        // ─────────────────────────────────────────────────────

        /// <summary>Locks up to <paramref name="count"/> random normal tiles for defaultLockDuration seconds.</summary>
        public void ExecuteLockTiles(int count) => ExecuteLockTiles(count, defaultLockDuration);

        /// <summary>Locks up to <paramref name="count"/> random normal tiles for <paramref name="duration"/> seconds.</summary>
        public void ExecuteLockTiles(int count, float duration)
        {
            if (boardGrid == null || boardGrid.Grid == null) return;

            List<Tile> candidates = new List<Tile>();
            for (int x = 0; x < boardGrid.Width; x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                if (t == null || t.Data == null) continue;
                if (t.State != TileState.Normal) continue;          // already locked/matched/falling — skip
                if (t.Data.isSpecial || t.Data.isHardTile || t.Data.isDropStone) continue;
                candidates.Add(t);
            }

            Shuffle(candidates);
            int n = Mathf.Min(count, candidates.Count);

            for (int i = 0; i < n; i++)
            {
                Tile t = candidates[i];
                t.SetState(TileState.Locked);
                _bossFrozenTiles.Add(t);

                t.transform.DOKill();
                t.transform.DOPunchScale(Vector3.one * 0.15f, feedbackDuration, 4, 0.6f);
            }

            Debug.Log($"[BossAttackExecutor] LockTiles: froze {n} tile(s) for {duration}s.");

            if (n > 0)
                StartCoroutine(ThawAfterDelay(duration));
        }

        private System.Collections.IEnumerator ThawAfterDelay(float duration)
        {
            yield return new WaitForSeconds(duration);
            ThawBossFrozenTiles();
        }

        /// <summary>Unfreezes every tile this executor froze (that's still on the board and still Locked). Safe to call anytime — e.g. on boss defeat, to clean up immediately.</summary>
        public void ThawBossFrozenTiles()
        {
            foreach (Tile t in _bossFrozenTiles)
            {
                if (t == null) continue;
                if (t.Data != null && t.Data.isHardTile) continue; // never touch a REAL hard-tile obstacle
                if (t.State != TileState.Locked) continue;         // already cleared/changed by something else

                t.SetState(TileState.Normal);
                t.transform.DOKill();
                t.transform.DOPunchScale(Vector3.one * 0.1f, feedbackDuration, 3, 0.5f);
            }
            _bossFrozenTiles.Clear();
        }

        // ─────────────────────────────────────────────────────
        // REDUCE MOVES
        // ─────────────────────────────────────────────────────

        public void ExecuteReduceMoves(int amount)
        {
            if (moveCounter == null) return;
            moveCounter.ReduceMoves(amount);
            Debug.Log($"[BossAttackExecutor] ReduceMoves: -{amount}.");
        }

        // ─────────────────────────────────────────────────────
        // ADD OBSTACLES — rock (hard tile) blockers
        // ─────────────────────────────────────────────────────

        public void ExecuteAddObstacles(int count = 3)
        {
            if (boardGrid == null || hardTileData == null) return;
            SpawnObstacleTiles(hardTileData, count, "AddObstacles (rock)");
        }

        // ─────────────────────────────────────────────────────
        // STONE TILES — dropdown-stone (ingredient) obstacles
        // ─────────────────────────────────────────────────────

        public void ExecuteStoneTiles(int count = 3)
        {
            if (boardGrid == null || dropStoneData == null) return;
            SpawnObstacleTiles(dropStoneData, count, "StoneTiles (dropdown stone)");
        }

        // ─────────────────────────────────────────────────────
        // JELLY — spreads a jelly layer onto random cells (doesn't touch the tile above)
        // ─────────────────────────────────────────────────────

        public void ExecuteAddJelly(int count = 1)
        {
            if (boardGrid == null || jellyManager == null) return;

            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int x = 0; x < boardGrid.Width; x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                if (jellyManager.HasJelly(x, y)) continue;         // already jellied
                Tile t = boardGrid.GetTile(x, y);
                if (t == null || t.Data == null) continue;
                if (t.Data.isHardTile) continue;                    // don't hide jelly under a hard tile
                candidates.Add(new Vector2Int(x, y));
            }

            Shuffle(candidates);
            int n = Mathf.Min(count, candidates.Count);

            for (int i = 0; i < n; i++)
                jellyManager.AddJellyAt(candidates[i].x, candidates[i].y, 1);

            Debug.Log($"[BossAttackExecutor] Jelly: spread onto {n} cell(s).");
        }

        // ─────────────────────────────────────────────────────

        private void SpawnObstacleTiles(TileData data, int count, string logLabel)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();
            for (int x = 0; x < boardGrid.Width; x++)
            for (int y = 0; y < boardGrid.Height; y++)
            {
                Tile t = boardGrid.GetTile(x, y);
                // Only replace a plain, currently-idle normal tile — never steal a
                // cell mid-swap/mid-clear, and never overwrite an existing obstacle.
                if (t == null || t.Data == null) continue;
                if (t.State != TileState.Normal) continue;
                if (t.Data.isSpecial || t.Data.isHardTile || t.Data.isDropStone) continue;
                candidates.Add(new Vector2Int(x, y));
            }

            Shuffle(candidates);
            int n = Mathf.Min(count, candidates.Count);

            for (int i = 0; i < n; i++)
            {
                Vector2Int pos = candidates[i];
                boardGrid.RemoveTile(pos.x, pos.y);
                Tile spawned = boardGrid.SpawnTile(pos.x, pos.y, data);
                if (spawned != null)
                {
                    spawned.transform.localScale = Vector3.zero;
                    spawned.transform.DOScale(Vector3.one, feedbackDuration).SetEase(Ease.OutBack);
                }
            }

            Debug.Log($"[BossAttackExecutor] {logLabel}: spawned {n} tile(s).");
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
