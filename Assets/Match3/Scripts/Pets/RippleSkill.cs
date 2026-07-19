// ============================================================
//  RippleSkill.cs  —  Ripple's power
//  Converts 5 random (non-special) tiles to whichever colour the
//  current CollectTile goal still needs. Falls back to doing
//  nothing if there's no incomplete CollectTile goal on this level
//  (e.g. a pure score/jelly level) — safer than guessing a colour
//  that doesn't help the player.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Match3
{
    public class RippleSkill : PetSkill
    {
        private const int ConvertCount = 5;

        public override IEnumerator UseSkill(
            BoardGrid grid, BoardController boardController,
            GoalTracker goals, MoveCounter moves)
        {
            if (grid == null) yield break;

            TileColor targetColor = FindGoalColor(goals);
            TileData  targetData  = FindTileDataOnBoard(grid, targetColor);

            if (targetData == null)
            {
                Debug.Log("[RippleSkill] No relevant goal colour found on board — skill fizzled.");
                yield break;
            }

            var candidates = new List<Tile>();
            for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                Tile t = grid.GetTile(x, y);
                if (t == null || t.Data == null || t.Data.isSpecial) continue;
                if (t.Data.color == targetColor) continue;   // already the right colour
                candidates.Add(t);
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int count = Mathf.Min(ConvertCount, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                Tile t = candidates[i];
                t.Initialize(targetData, t.GridX, t.GridY);
                t.transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 4, 0.6f);
            }

            Debug.Log($"[RippleSkill] Converted {count} tiles to {targetColor}.");

            yield return new WaitForSeconds(0.3f);

            if (boardController != null)
                yield return boardController.ResolveBoard();
        }

        private TileColor FindGoalColor(GoalTracker goals)
        {
            if (goals == null || goals.Goals == null) return TileColor.None;

            foreach (GoalData g in goals.Goals)
            {
                if (g == null || g.IsComplete) continue;
                if (g.goalType == GoalType.CollectTile &&
                    g.targetTile != null &&
                    g.targetTile.color != TileColor.None)
                {
                    return g.targetTile.color;
                }
            }
            return TileColor.None;
        }

        private TileData FindTileDataOnBoard(BoardGrid grid, TileColor color)
        {
            if (color == TileColor.None) return null;

            for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                Tile t = grid.GetTile(x, y);
                if (t != null && t.Data != null && t.Data.color == color)
                    return t.Data;
            }
            return null;
        }
    }
}
