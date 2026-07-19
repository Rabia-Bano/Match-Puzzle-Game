// ============================================================
//  LunaSkill.cs  —  Luna's power
//  Shuffles the TileData of every non-special tile on the board,
//  TWICE in a row (each shuffle is followed by a full resolve, so any
//  matches the first shuffle creates get cleared before the second
//  shuffle scrambles the board again).
// ============================================================

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Match3
{
    public class LunaSkill : PetSkill
    {
        private const int ShuffleCount = 2;

        public override IEnumerator UseSkill(
            BoardGrid grid, BoardController boardController,
            GoalTracker goals, MoveCounter moves)
        {
            if (grid == null) yield break;

            for (int pass = 1; pass <= ShuffleCount; pass++)
            {
                yield return ShuffleOnce(grid, pass);

                if (boardController != null)
                    yield return boardController.ResolveBoard();
            }
        }

        private IEnumerator ShuffleOnce(BoardGrid grid, int passNumber)
        {
            var tiles    = new List<Tile>();
            var dataPool = new List<TileData>();

            for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                Tile t = grid.GetTile(x, y);
                if (t == null || t.Data == null || t.Data.isSpecial) continue;

                tiles.Add(t);
                dataPool.Add(t.Data);
            }

            // Fisher-Yates shuffle of the TileData pool.
            for (int i = dataPool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (dataPool[i], dataPool[j]) = (dataPool[j], dataPool[i]);
            }

            for (int i = 0; i < tiles.Count; i++)
            {
                Tile t = tiles[i];
                t.Initialize(dataPool[i], t.GridX, t.GridY);   // kills tweens + refreshes visuals
                t.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 4, 0.6f);
            }

            Debug.Log($"[LunaSkill] Shuffle pass {passNumber}/{ShuffleCount}: {tiles.Count} tiles.");

            yield return new WaitForSeconds(0.3f);
        }
    }
}
