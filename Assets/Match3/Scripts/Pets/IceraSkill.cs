// ============================================================
//  IceraSkill.cs  —  Icera's power
//  Clears 3 DIFFERENT random rows (no duplicate row picked twice).
//  Routes through BoardController.ClearTiles() + SettleAfterExternalClear()
//  so scoring, goal-tracking, gravity, refill and cascade resolution all
//  happen through the exact same path a normal match uses.
//
//  REDESIGN NOTE (bug report ke baad — hard tile damage): sirf param
//  rename hua hai — damageAdjacentHardTiles → canDamageHardTiles. Yeh
//  pet power ke 3 rows ke andar jo bhi hard tile aayegi, wo ab DIRECT
//  hit gini jayegi (adjacency nahi) — jo BoardController.ClearTiles()
//  ke naye design se match karta hai.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class IceraSkill : PetSkill
    {
        private const int RowsToClear = 3;

        public override IEnumerator UseSkill(
            BoardGrid grid, BoardController boardController,
            GoalTracker goals, MoveCounter moves)
        {
            if (grid == null || boardController == null) yield break;

            int rowCount = Mathf.Min(RowsToClear, grid.Height);

            // Pick RowsToClear distinct row indices.
            var availableRows = new List<int>();
            for (int y = 0; y < grid.Height; y++) availableRows.Add(y);

            var chosenRows = new List<int>();
            for (int i = 0; i < rowCount && availableRows.Count > 0; i++)
            {
                int pick = Random.Range(0, availableRows.Count);
                chosenRows.Add(availableRows[pick]);
                availableRows.RemoveAt(pick);
            }

            var tiles = new List<Tile>();
            foreach (int row in chosenRows)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Tile t = grid.GetTile(x, row);
                    if (t != null) tiles.Add(t);
                }
            }

            if (tiles.Count == 0) yield break;

            Debug.Log($"[IceraSkill] Clearing rows [{string.Join(",", chosenRows)}] ({tiles.Count} tiles).");

            yield return boardController.ClearTiles(tiles, canDamageHardTiles: true);
            yield return boardController.SettleAfterExternalClear();
        }
    }
}