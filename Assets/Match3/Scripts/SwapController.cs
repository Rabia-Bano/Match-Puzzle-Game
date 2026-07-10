// ============================================================
//  SwapController.cs  —  FIXED: SpecialCombinations support added
//
//  Original SwapController ka structure ekdum same rakha.
//  Sirf ek naya field aur combo check add kiya hai.
//
//  Changes from original:
//  + [SerializeField] private SpecialCombinations specialCombinations
//  + SwapRoutine mein combo check PEHLE hota hai activator se
//  + WaitForCombinations() helper added
// ============================================================

using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public class SwapController : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private BoardGrid            boardGrid;
        [SerializeField] private InputHandler         inputHandler;
        [SerializeField] private BoardController      boardController;
        [SerializeField] private SpecialTileActivator specialActivator;
        [SerializeField] private SpecialCombinations  specialCombinations;  // ← NEW
        [SerializeField] private BoardRotation        boardRotation;
        [SerializeField] private LevelManager         levelManager;

        [Header("Animation")]
        [SerializeField] private float swapDuration    = 0.2f;
        [SerializeField] private float reverseDuration = 0.15f;

        public bool IsBusy { get; private set; }

        private void Awake() => ValidateRefs();

        private void OnEnable()
        {
            inputHandler.OnSwipeDetected += HandleSwipe;
            inputHandler.OnTileClicked   += HandleTileClick;
        }

        private void OnDisable()
        {
            inputHandler.OnSwipeDetected -= HandleSwipe;
            inputHandler.OnTileClicked   -= HandleTileClick;
        }

        private void HandleTileClick(Vector2Int cell)
        {
            if (IsBusy || boardController.IsBusy) return;
            Tile tile = boardGrid.GetTile(cell.x, cell.y);
            if (tile == null || tile.Data == null || !tile.Data.isSpecial) return;

            IsBusy = true;
            inputHandler.SetInputEnabled(false);
            specialActivator.ActivateSingle(tile);
            StartCoroutine(WaitForActivator());
        }

        private void HandleSwipe(Vector2Int from, Vector2Int to)
        {
            if (IsBusy || boardController.IsBusy) return;
            if (!IsValidSwap(from, to)) return;
            StartCoroutine(SwapRoutine(from, to));
        }

        private IEnumerator SwapRoutine(Vector2Int fromPos, Vector2Int toPos)
        {
            IsBusy = true;
            inputHandler.SetInputEnabled(false);

            Tile tileA = boardGrid.GetTile(fromPos.x, fromPos.y);
            Tile tileB = boardGrid.GetTile(toPos.x,   toPos.y);

            yield return AnimateSwap(tileA, tileB, swapDuration);
            PerformGridSwap(fromPos, toPos);

            bool aSpecial = tileA != null && tileA.Data != null && tileA.Data.isSpecial;
            bool bSpecial = tileB != null && tileB.Data != null && tileB.Data.isSpecial;

            // ── Step 1: Do Special + Special combo check (NEW) ─
            if (aSpecial && bSpecial && specialCombinations != null)
            {
                bool comboHandled = specialCombinations.TryHandleCombo(tileA, tileB);
                if (comboHandled)
                {
                    boardRotation?.RegisterMove();
                    levelManager?.OnMoveCompleted();
                    yield return StartCoroutine(WaitForCombinations());
                    yield break;
                }
            }

            // ── Step 2: Single special activation (original) ───
            if (aSpecial || bSpecial)
            {
                bool handled = specialActivator.TryActivateSwap(tileA, tileB);
                if (handled)
                {
                    boardRotation?.RegisterMove();
                    levelManager?.OnMoveCompleted();
                    yield return StartCoroutine(WaitForActivator());
                    yield break;
                }
            }

            // ── Step 3: Normal match (original) ───────────────
            if (boardController.HasMatches())
            {
                boardRotation?.RegisterMove();
                levelManager?.OnMoveCompleted();
                IsBusy = false;
                boardController.ProcessTurn();
            }
            else
            {
                yield return AnimateSwap(tileA, tileB, reverseDuration);
                PerformGridSwap(fromPos, toPos);
                IsBusy = false;
                inputHandler.SetInputEnabled(true);
            }
        }

        // ── NEW: Wait for SpecialCombinations to finish ────────
        private IEnumerator WaitForCombinations()
        {
            yield return new WaitForSeconds(0.1f);
            while (specialCombinations != null && specialCombinations.IsRunning)
                yield return null;
            IsBusy = false;
            inputHandler.SetInputEnabled(true);
        }

        private IEnumerator WaitForActivator()
        {
            yield return new WaitForSeconds(0.1f);
            while (specialActivator.IsRunning)
                yield return null;
            IsBusy = false;
            inputHandler.SetInputEnabled(true);
        }

        private void PerformGridSwap(Vector2Int a, Vector2Int b)
        {
            Tile tA = boardGrid.GetTile(a.x, a.y);
            Tile tB = boardGrid.GetTile(b.x, b.y);
            boardGrid.Grid[a.x, a.y] = tB;
            boardGrid.Grid[b.x, b.y] = tA;
            tA?.SetGridPosition(b.x, b.y);
            tB?.SetGridPosition(a.x, a.y);
        }

        private static YieldInstruction AnimateSwap(Tile tA, Tile tB, float dur)
        {
            Vector3 posA = tA.transform.position;
            Vector3 posB = tB.transform.position;
            tA.transform.DOMove(posB, dur).SetEase(Ease.OutCubic);
            tB.transform.DOMove(posA, dur).SetEase(Ease.OutCubic);
            return new WaitForSeconds(dur);
        }

        private bool IsValidSwap(Vector2Int from, Vector2Int to)
        {
            if (!boardGrid.IsInBounds(from.x, from.y)) return false;
            if (!boardGrid.IsInBounds(to.x, to.y))     return false;
            Tile tA = boardGrid.GetTile(from.x, from.y);
            Tile tB = boardGrid.GetTile(to.x, to.y);
            if (tA == null || tB == null)         return false;
            if (tA.State == TileState.Locked)     return false;
            if (tB.State == TileState.Locked)     return false;
            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);
            return (dx + dy) == 1;
        }

        private void ValidateRefs()
        {
            if (!boardGrid)        Debug.LogError("[SwapController] boardGrid missing!",        this);
            if (!inputHandler)     Debug.LogError("[SwapController] inputHandler missing!",     this);
            if (!boardController)  Debug.LogError("[SwapController] boardController missing!",  this);
            if (!specialActivator) Debug.LogError("[SwapController] specialActivator missing!", this);
            if (!levelManager)     Debug.LogError("[SwapController] levelManager missing!",     this);
            // specialCombinations optional
            if (!specialCombinations)
                Debug.LogWarning("[SwapController] specialCombinations not assigned — combo swaps disabled.", this);
        }
    }
}