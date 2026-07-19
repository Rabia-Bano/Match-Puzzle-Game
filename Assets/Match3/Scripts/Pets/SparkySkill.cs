// ============================================================
//  SparkySkill.cs  —  Sparky's power
//  Adds 5 bonus moves to the current level.
//
//  Uses MoveCounter.AddBonusMoves() (raises TotalMoves too) instead of
//  AddMoves(), because AddMoves() clamps to the level's starting
//  TotalMoves — the bonus would silently do nothing if the player still
//  has full moves left. AddBonusMoves() always applies.
// ============================================================

using System.Collections;
using UnityEngine;

namespace Match3
{
    public class SparkySkill : PetSkill
    {
        private const int BonusMoves = 5;

        public override IEnumerator UseSkill(
            BoardGrid grid, BoardController boardController,
            GoalTracker goals, MoveCounter moves)
        {
            if (moves != null)
            {
                moves.AddBonusMoves(BonusMoves);
                Debug.Log($"[SparkySkill] +{BonusMoves} guaranteed moves.");
            }

            yield break;
        }
    }
}
