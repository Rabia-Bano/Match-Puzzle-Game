using System.Collections;

namespace Match3
{
    public abstract class PetSkill
    {
        public abstract IEnumerator UseSkill(
            BoardGrid grid,
            BoardController boardController,
            GoalTracker goals,
            MoveCounter moves);
    }
}
