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

        public TileData TryCreateSpecial(MatchGroup group, BoardGrid boardGrid, out int pivotX, out int pivotY)
        {
            pivotX = -1;
            pivotY = -1;

            TileData specialData = GetSpecialData(group.Shape);
            if (specialData == null) return null;

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
            TileData pivotOriginalData = pivot.Data;

            group.Tiles.Remove(pivot);

            boardGrid.RemoveTile(px, py);

            Tile special = boardGrid.SpawnTile(px, py, specialData);

            if (special == null)
            {
                Debug.LogError("[SpecialTileFactory] boardGrid.SpawnTile returned null!");
                return null;
            }

            special.RefreshVisuals();

            special.transform.localScale = Vector3.one * BoardGrid.TileVisualScale;
            special.transform.DOPunchScale(
                    Vector3.one * (popScale - 1f),
                    duration:  0.35f,
                    vibrato:   6,
                    elasticity: 0.5f)
                .SetEase(Ease.OutBack);

            PlaySpawnFx(special.transform.position);

            Debug.Log($"[SpecialTileFactory] ✓ Created {group.Shape} → " +
                      $"{specialData.name} at ({px},{py})");

            string tutorialKey = group.Shape switch
            {
                MatchShape.HLine4 => "special_striped_h",
                MatchShape.VLine4 => "special_striped_v",
                MatchShape.Line5  => "special_colorbomb",
                MatchShape.TShape => "special_wrapped",
                MatchShape.LShape => "special_wrapped",
                _                 => null
            };
            if (tutorialKey != null)
                TutorialManager.Instance?.RequestTutorial(tutorialKey, boardGrid.GridToWorld(px, py));

            pivotX = px;
            pivotY = py;
            return pivotOriginalData;
        }

        private TileData GetSpecialData(MatchShape shape) => shape switch
        {
            MatchShape.HLine4 => hStripedData,
            MatchShape.VLine4 => vStripedData,
            MatchShape.Line5  => colorBombData,
            MatchShape.TShape => wrappedData,
            MatchShape.LShape => wrappedData,
            _                 => null
        };

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
