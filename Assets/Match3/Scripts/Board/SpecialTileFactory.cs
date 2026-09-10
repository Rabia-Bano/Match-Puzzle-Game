// ============================================================
//  SpecialTileFactory.cs  — FIXED
//
//  Bug Fixed: Tile visually nahi badal raha tha kyunki
//  boardGrid.SpawnTile() pool se ek naya tile deta hai
//  aur Tile.Initialize() sirf sprite set karta hai —
//  lekin RefreshVisuals() call nahi ho raha tha clearly.
//
//  Fix: Ab SpawnTile ke baad explicitly tile ka
//  SpriteRenderer update hota hai aur ek scale "pop"
//  animation bhi play hoti hai taake player ko pata chale.
//
//  Special tile DATA assets Inspector mein assign karein:
//    hStripedData  → TileData_HStriped  (isSpecial=true, RowBlast)
//    vStripedData  → TileData_VStriped  (isSpecial=true, ColBlast)
//    wrappedData   → TileData_Wrapped   (isSpecial=true, Bomb)
//    colorBombData → TileData_ColorBomb (isSpecial=true, Rainbow)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class SpecialTileFactory : MonoBehaviour
    {
        [Header("Special Tile Data Assets")]
        [SerializeField] private TileData hStripedData;
        [SerializeField] private TileData vStripedData;
        [SerializeField] private TileData wrappedData;
        [SerializeField] private TileData colorBombData;

        [Header("Spawn Animation")]
        [Tooltip("Scale pop size when special tile spawns (1.4 = pops to 140% then back)")]
        [SerializeField] private float popScale = 1.4f;

        [Header("Optional FX")]
        [SerializeField] private GameObject spawnFxPrefab;

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Turns a qualifying match into a special tile at its pivot cell.
        /// Returns the PIVOT TILE'S ORIGINAL TileData (before it became
        /// special) via pivotData, plus its grid position — or null/-1,-1 if
        /// no special was created (Line3 match, or invalid data).
        ///
        /// WHY THIS RETURNS DATA NOW: the pivot tile is removed from
        /// group.Tiles right below (it transforms instead of being
        /// destroyed), so BoardController's caller previously had NO WAY to
        /// report it for goal tracking / jelly decrement — every match that
        /// spawned a special silently under-counted color-collection goals
        /// by exactly 1 (a 4-match registered as 3, a 5-match as 4). The
        /// pivot still visually "matched" for goal purposes, it just became
        /// a special tile instead of clearing — so it should still count.
        /// </summary>
        public TileData TryCreateSpecial(MatchGroup group, BoardGrid boardGrid, out int pivotX, out int pivotY)
        {
            pivotX = -1;
            pivotY = -1;

            TileData specialData = GetSpecialData(group.Shape);
            if (specialData == null) return null;    // Line3 → no special

            // Validate special data is assigned
            if (specialData.sprite == null)
            {
                Debug.LogError($"[SpecialTileFactory] {group.Shape} TileData has no sprite assigned! " +
                               "Assign a sprite to the TileData asset in the Inspector.", specialData);
                return null;
            }

            Tile pivot = PickPivot(group.Tiles, group.Shape, boardGrid);
            if (pivot == null) return null;

            int px = pivot.GridX;
            int py = pivot.GridY;
            TileData pivotOriginalData = pivot.Data; // capture BEFORE it's overwritten below

            // Remove pivot from the clear list — it becomes the special tile
            group.Tiles.Remove(pivot);

            // Remove current tile from board
            boardGrid.RemoveTile(px, py);

            // Spawn special tile from pool
            Tile special = boardGrid.SpawnTile(px, py, specialData);

            if (special == null)
            {
                Debug.LogError("[SpecialTileFactory] boardGrid.SpawnTile returned null!");
                return null;
            }

            // ── CRITICAL FIX: Force visual refresh ───────────
            // RefreshVisuals() sets the SpriteRenderer.sprite from TileData
            special.RefreshVisuals();

            // ── Pop animation so player sees it appear ────────
            special.transform.localScale = Vector3.one;
            special.transform.DOPunchScale(
                    Vector3.one * (popScale - 1f),
                    duration:  0.35f,
                    vibrato:   6,
                    elasticity: 0.5f)
                .SetEase(Ease.OutBack);

            // Optional particle
            PlaySpawnFx(special.transform.position);

            Debug.Log($"[SpecialTileFactory] ✓ Created {group.Shape} → " +
                      $"{specialData.name} at ({px},{py})");

            pivotX = px;
            pivotY = py;
            return pivotOriginalData;
        }

        // ── Shape → Data mapping ─────────────────────────────

        private TileData GetSpecialData(MatchShape shape) => shape switch
        {
            MatchShape.HLine4 => hStripedData,
            MatchShape.VLine4 => vStripedData,
            MatchShape.Line5  => colorBombData,
            MatchShape.TShape => wrappedData,
            MatchShape.LShape => wrappedData,
            _                 => null
        };

        // ── Pivot selection ───────────────────────────────────

        private static Tile PickPivot(List<Tile> tiles, MatchShape shape, BoardGrid boardGrid)
        {
            if (tiles.Count == 0) return null;

            switch (shape)
            {
                case MatchShape.Line5:
                    var sorted = new List<Tile>(tiles);
                    sorted.Sort((a, b) => a.GridX != b.GridX
                        ? a.GridX.CompareTo(b.GridX)
                        : a.GridY.CompareTo(b.GridY));
                    return sorted[sorted.Count / 2];

                case MatchShape.TShape:
                case MatchShape.LShape:
                    return FindIntersectionTile(tiles);

                default:
                    return FindCentroidTile(tiles);
            }
        }

        private static Tile FindIntersectionTile(List<Tile> tiles)
        {
            var inGroup = new HashSet<(int, int)>();
            foreach (var t in tiles) inGroup.Add((t.GridX, t.GridY));

            Tile best = null; int bestScore = -1;
            foreach (var t in tiles)
            {
                int score = 0;
                if (inGroup.Contains((t.GridX + 1, t.GridY))) score++;
                if (inGroup.Contains((t.GridX - 1, t.GridY))) score++;
                if (inGroup.Contains((t.GridX, t.GridY + 1))) score++;
                if (inGroup.Contains((t.GridX, t.GridY - 1))) score++;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best ?? tiles[tiles.Count / 2];
        }

        private static Tile FindCentroidTile(List<Tile> tiles)
        {
            float cx = 0f, cy = 0f;
            foreach (var t in tiles) { cx += t.GridX; cy += t.GridY; }
            cx /= tiles.Count; cy /= tiles.Count;

            Tile best = null; float bestDist = float.MaxValue;
            foreach (var t in tiles)
            {
                float d = Mathf.Abs(t.GridX - cx) + Mathf.Abs(t.GridY - cy);
                if (d < bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        private void PlaySpawnFx(Vector3 pos)
        {
            if (spawnFxPrefab == null) return;
            Destroy(Instantiate(spawnFxPrefab, pos, Quaternion.identity), 2f);
        }
    }
}