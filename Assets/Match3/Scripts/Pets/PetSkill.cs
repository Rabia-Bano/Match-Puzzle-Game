// ============================================================
//  PetSkill.cs  —  abstract base (plain C# class, NOT a MonoBehaviour)
//
//  Every pet power (Icera / Sparky / Luna / Ripple) is a small class
//  that implements UseSkill(). PetManager owns exactly ONE
//  instance of the equipped pet's skill and runs it as a
//  coroutine when the player taps the skill button.
//
//  NOTE on the signature: the brief asked for
//      abstract void UseSkill(BoardGrid grid, GoalTracker goals, MoveCounter moves)
//  I changed this to an IEnumerator that also receives BoardController,
//  for one reason tied directly to your existing architecture:
//  BoardController.ClearTiles() / ResolveBoard() / SettleAfterExternalClear()
//  are already the single authoritative "clear tiles safely" path in the
//  project (per the comments at the top of BoardController.cs — it even
//  explicitly says boosters/pet powers should call ClearTiles() then
//  SettleAfterExternalClear() instead of re-implementing clear+gravity+
//  refill+cascade logic). A synchronous `void` skill can't await that
//  coroutine, and re-implementing clearing here would reintroduce the
//  exact "duplicated clear logic" bug you already fixed once. Routing
//  pet skills through BoardController also means they automatically
//  get the DOTween-kill-before-pooling safety Tile.ResetForPool() gives
//  every other clear path.
// ============================================================

using System.Collections;

namespace Match3
{
    public abstract class PetSkill
    {
        /// <summary>
        /// Executes the pet's power. Implementations should leave the board
        /// in a fully-settled state before returning (i.e. finish by yielding
        /// on BoardController.SettleAfterExternalClear() or ResolveBoard()
        /// if they touched the grid).
        /// </summary>
        public abstract IEnumerator UseSkill(
            BoardGrid grid,
            BoardController boardController,
            GoalTracker goals,
            MoveCounter moves);
    }
}