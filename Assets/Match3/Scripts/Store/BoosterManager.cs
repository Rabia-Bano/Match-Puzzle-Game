// ============================================================
//  BoosterManager.cs  —  Singleton MonoBehaviour (DontDestroyOnLoad)
//  Attach to: NOTHING manually — created automatically via
//  GetOrCreateInstance() the same way PetManager.cs is.
//
//  Runs the actual POWER of every store-bought booster:
//    hammer          — destroy one tapped tile
//    row_bomb        — clear the tapped tile's entire row
//    column_bomb     — clear the tapped tile's entire column
//    shuffle_2tiles  — tap TWO tiles, their colours swap places (no need
//                      to be adjacent, no need to form a match) — a "free
//                      swap" booster. Any new match formed resolves automatically.
//    shuffle_board   — reshuffles every plain tile's colour across the whole
//                      board (no targeting). Any new match formed resolves
//                      automatically, same convention as the every-5-moves
//                      board rotation feature.
//
//  Works in BOTH GameBoardScene (regular level) and BossGameBoardScene
//  (Boss Arena) — bind it once per scene via BindToLevel(), same pattern
//  PetManager already uses.
//
//  Tile-clearing effects route through BoardController.ClearTiles() +
//  SettleAfterExternalClear() with isExternalClear:true — the SAME path
//  IceraSkill.cs (a pet power) already uses, so goals/score update AND
//  Boss Arena damage apply automatically with zero boss-specific code here.
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class BoosterManager : MonoBehaviour
    {
        public static BoosterManager Instance { get; private set; }

        public static BoosterManager GetOrCreateInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("BoosterManager");
            DontDestroyOnLoad(go);
            return go.AddComponent<BoosterManager>();
        }

        // ── Canonical booster ids — MUST match Store/StoreManager.cs catalog ids ──
        public const string Hammer         = "hammer";
        public const string RowBomb        = "row_bomb";
        public const string ColumnBomb     = "column_bomb";
        public const string Shuffle2Tiles  = "shuffle_2tiles";
        public const string ShuffleBoard   = "shuffle_board";

        // ── Bound-scene references (set by BindToLevel) ──────────────
        private BoardGrid       _grid;
        private BoardController _board;
        private InputHandler    _input;
        private GoalTracker     _goals;   // optional (null in Boss Arena)
        private MoveCounter     _moves;   // optional (null in Boss Arena)

        public bool IsBound  { get; private set; }
        public bool IsBusy   { get; private set; }
        public bool IsTargeting { get; private set; }

        /// <summary>Fires when a booster's effect finishes successfully. Passes the booster id.</summary>
        public event Action<string> OnBoosterUsed;

        /// <summary>Fires when Hammer/Row Bomb/Column Bomb enter "tap a tile" mode —
        /// UI can show a "Select a tile" banner and a Cancel button.</summary>
        public event Action<string> OnTargetingStarted;

        /// <summary>Fires when targeting ends (used OR cancelled).</summary>
        public event Action OnTargetingEnded;

        /// <summary>Fires at the end of BindToLevel(), once this manager is actually
        /// ready to answer CanUse() for the new scene. UI (InventoryBoosterSlot) must
        /// listen to this and re-run Refresh() — see the FIX note on BindToLevel().</summary>
        public event Action OnBound;

        private Action<Vector2Int> _pendingTargetHandler;
        private string _pendingBoosterId;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Call once per scene load (LevelManager.Start() for regular levels,
        /// BossController.BeginFight() for Boss Arena). goals/moves may be null.</summary>
        public void BindToLevel(BoardGrid grid, BoardController board, InputHandler input,
                                 GoalTracker goals = null, MoveCounter moves = null)
        {
            if (IsTargeting) CancelTargeting();

            _grid  = grid;
            _board = board;
            _input = input;
            _goals = goals;
            _moves = moves;

            IsBound = grid != null && board != null;
            IsBusy  = false;

            if (!IsBound)
                Debug.LogError("[BoosterManager] BindToLevel() got a null grid/board — boosters won't work this scene.");

            // FIX ("boosters sometimes active, sometimes not" / shuffle_2tiles looking
            // totally disconnected): this method runs from LevelManager.Start() /
            // BossController.BeginFight() — but every InventoryBoosterSlot's OnEnable()
            // (which calls Refresh() -> CanUse()) runs BEFORE any Start() in the scene,
            // per Unity's Awake-all -> OnEnable-all -> Start-all execution order. So
            // every slot's first Refresh() was reading IsBound as still false and
            // greying itself out — and nothing ever told it to check again, since
            // BindToLevel used to fire no event at all. Whether a slot "happened" to
            // get refreshed afterwards depended entirely on whether some UNRELATED
            // event (LocalSaveManager.OnProfileChanged, a booster being used elsewhere,
            // etc.) fired later in that same session — which is exactly why it looked
            // random ("sometimes works"). Firing OnBound here lets every slot re-check
            // the instant binding is actually done.
            OnBound?.Invoke();
        }

        // ============================================================
        //  PUBLIC ENTRY POINT — call from InventoryBoosterSlot.cs
        // ============================================================

        public bool TryActivate(string boosterId)
        {
            if (!CanUse(boosterId)) return false;

            switch (boosterId)
            {
                case Hammer:         BeginTargeting(boosterId, cell => StartCoroutine(RunHammer(cell)));     return true;
                case RowBomb:        BeginTargeting(boosterId, cell => StartCoroutine(RunRowBomb(cell)));    return true;
                case ColumnBomb:     BeginTargeting(boosterId, cell => StartCoroutine(RunColumnBomb(cell))); return true;
                case Shuffle2Tiles:  BeginShuffle2TilesTargeting();                                          return true;
                case ShuffleBoard:   StartCoroutine(RunShuffleBoard());                                      return true;
                default:
                    Debug.LogWarning($"[BoosterManager] '{boosterId}' has no gameplay implementation.");
                    return false;
            }
        }

        /// <summary>Checks inventory + scene-bound state, WITHOUT spending anything.
        /// Use this to decide whether a booster button should be interactable.</summary>
        public bool CanUse(string boosterId)
        {
            if (!IsBound || IsBusy || IsTargeting) return false;

            Dictionary<string, int> inventory = LocalSaveManager.LoadBoosterInventory();
            return inventory.TryGetValue(boosterId, out int count) && count > 0;
        }

        /// <summary>Cancels an in-progress targeting session (single-tap Hammer/Row
        /// Bomb/Column Bomb OR the two-tap Shuffle 2 Tiles). Nothing is spent from
        /// inventory if cancelled before the full tap sequence completes.</summary>
        public void CancelTargeting()
        {
            if (!IsTargeting) return;

            if (_input != null)
            {
                _input.OnTileClicked -= HandleTargetCellClicked;
                _input.OnTileClicked -= HandleShuffle2TilesClicked;
            }

            _pendingTargetHandler = null;
            _pendingBoosterId = null;
            _firstSwapCell = null;
            IsTargeting = false;
            OnTargetingEnded?.Invoke();
        }

        // ============================================================
        //  TARGETING (Hammer / Row Bomb / Column Bomb — all need one tap)
        // ============================================================

        private void BeginTargeting(string boosterId, Action<Vector2Int> onCellChosen)
        {
            if (_input == null)
            {
                Debug.LogError("[BoosterManager] BeginTargeting: no InputHandler bound — falling back to center-tile target.");
                onCellChosen?.Invoke(new Vector2Int(_grid.Width / 2, _grid.Height / 2));
                return;
            }

            _pendingBoosterId = boosterId;
            _pendingTargetHandler = onCellChosen;
            IsTargeting = true;
            _input.OnTileClicked += HandleTargetCellClicked;
            OnTargetingStarted?.Invoke(boosterId);
        }

        private void HandleTargetCellClicked(Vector2Int cell)
        {
            if (!IsTargeting) return;

            Action<Vector2Int> handler = _pendingTargetHandler;
            string boosterId = _pendingBoosterId;

            if (_input != null) _input.OnTileClicked -= HandleTargetCellClicked;
            IsTargeting = false;
            _pendingTargetHandler = null;
            _pendingBoosterId = null;
            OnTargetingEnded?.Invoke();

            // Spend from inventory now that the player actually confirmed a
            // target — cancelling before this point costs nothing.
            if (!SpendOne(boosterId)) return;

            handler?.Invoke(cell);
        }

        // ============================================================
        //  BOOSTER EFFECTS
        // ============================================================

        /// <summary>Hammer — destroys exactly the tapped tile (works on hard tiles too, 1 hit).</summary>
        private IEnumerator RunHammer(Vector2Int cell)
        {
            IsBusy = true;
            Tile tile = _grid.GetTile(cell.x, cell.y);
            if (tile != null)
            {
                yield return _board.ClearTiles(new[] { tile }, canDamageHardTiles: true, isExternalClear: true);
                yield return _board.SettleAfterExternalClear();
            }
            IsBusy = false;
            OnBoosterUsed?.Invoke(Hammer);
        }

        /// <summary>Row Bomb — clears every tile in the tapped tile's row.</summary>
        private IEnumerator RunRowBomb(Vector2Int cell)
        {
            IsBusy = true;
            var tiles = new List<Tile>();
            for (int x = 0; x < _grid.Width; x++)
            {
                Tile t = _grid.GetTile(x, cell.y);
                if (t != null) tiles.Add(t);
            }

            if (tiles.Count > 0)
            {
                yield return _board.ClearTiles(tiles, canDamageHardTiles: true, isExternalClear: true);
                yield return _board.SettleAfterExternalClear();
            }
            IsBusy = false;
            OnBoosterUsed?.Invoke(RowBomb);
        }

        /// <summary>Column Bomb — clears every tile in the tapped tile's column.</summary>
        private IEnumerator RunColumnBomb(Vector2Int cell)
        {
            IsBusy = true;
            var tiles = new List<Tile>();
            for (int y = 0; y < _grid.Height; y++)
            {
                Tile t = _grid.GetTile(cell.x, y);
                if (t != null) tiles.Add(t);
            }

            if (tiles.Count > 0)
            {
                yield return _board.ClearTiles(tiles, canDamageHardTiles: true, isExternalClear: true);
                yield return _board.SettleAfterExternalClear();
            }
            IsBusy = false;
            OnBoosterUsed?.Invoke(ColumnBomb);
        }

        // ── Shuffle 2 Tiles — a separate two-tap targeting flow ─────────
        // Deliberately NOT reusing BeginTargeting()/HandleTargetCellClicked()
        // (the single-tap helper above) because that helper spends one unit
        // of inventory on every confirmed tap — for a two-tap booster we only
        // want to spend ONCE, after both tiles are chosen.

        private Vector2Int? _firstSwapCell;

        private void BeginShuffle2TilesTargeting()
        {
            if (_input == null)
            {
                Debug.LogError("[BoosterManager] BeginShuffle2TilesTargeting: no InputHandler bound.");
                return;
            }

            _firstSwapCell = null;
            _pendingBoosterId = Shuffle2Tiles;
            IsTargeting = true;
            _input.OnTileClicked += HandleShuffle2TilesClicked;
            OnTargetingStarted?.Invoke(Shuffle2Tiles);
        }

        private void HandleShuffle2TilesClicked(Vector2Int cell)
        {
            if (!IsTargeting) return;

            Tile tapped = _grid.GetTile(cell.x, cell.y);
            if (!IsSwappableTile(tapped)) return; // special/hard/drop-stone tile or empty cell — ignore, keep waiting

            if (_firstSwapCell == null)
            {
                _firstSwapCell = cell;
                return; // first tile confirmed, wait for the second tap
            }

            if (_firstSwapCell.Value == cell) return; // tapped the same tile twice — ignore, keep waiting for a DIFFERENT second tile

            Vector2Int first  = _firstSwapCell.Value;
            Vector2Int second = cell;

            _input.OnTileClicked -= HandleShuffle2TilesClicked;
            IsTargeting = false;
            _firstSwapCell = null;
            _pendingBoosterId = null;
            OnTargetingEnded?.Invoke();

            if (!SpendOne(Shuffle2Tiles)) return;
            StartCoroutine(RunShuffle2Tiles(first, second));
        }

        /// <summary>Swaps the colour/data of exactly two tapped tiles in place
        /// (positions on the grid don't change, only which tile holds which
        /// colour) — then resolves the board so any match the swap creates
        /// clears automatically.</summary>
        private IEnumerator RunShuffle2Tiles(Vector2Int a, Vector2Int b)
        {
            IsBusy = true;

            Tile tileA = _grid.GetTile(a.x, a.y);
            Tile tileB = _grid.GetTile(b.x, b.y);

            if (tileA != null && tileB != null)
            {
                TileData dataA = tileA.Data;
                TileData dataB = tileB.Data;
                tileA.Initialize(dataB, tileA.GridX, tileA.GridY);
                tileB.Initialize(dataA, tileB.GridX, tileB.GridY);

                yield return StartCoroutine(_board.ResolveBoard());
            }

            IsBusy = false;
            OnBoosterUsed?.Invoke(Shuffle2Tiles);
        }

        /// <summary>Shuffle Board — no targeting. Randomly redistributes colours
        /// among every plain (non-special, non-hard, non-drop-stone) tile
        /// currently on the board, then resolves so any match the reshuffle
        /// creates clears automatically — same convention BoardRotation.cs
        /// already uses for the every-5-moves board rotation.</summary>
        private IEnumerator RunShuffleBoard()
        {
            if (!SpendOne(ShuffleBoard)) yield break;

            IsBusy = true;

            var plainTiles = new List<Tile>();
            var dataPool   = new List<TileData>();

            for (int x = 0; x < _grid.Width; x++)
            for (int y = 0; y < _grid.Height; y++)
            {
                Tile t = _grid.GetTile(x, y);
                if (!IsSwappableTile(t)) continue;
                plainTiles.Add(t);
                dataPool.Add(t.Data);
            }

            // Fisher-Yates shuffle of the data pool, then reassign in place.
            for (int i = dataPool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (dataPool[i], dataPool[j]) = (dataPool[j], dataPool[i]);
            }

            for (int i = 0; i < plainTiles.Count; i++)
                plainTiles[i].Initialize(dataPool[i], plainTiles[i].GridX, plainTiles[i].GridY);

            yield return StartCoroutine(_board.ResolveBoard());

            IsBusy = false;
            OnBoosterUsed?.Invoke(ShuffleBoard);
        }

        /// <summary>A tile is safe to move data in/out of for Shuffle 2 Tiles /
        /// Shuffle Board — excludes special tiles, hard/obstacle tiles, and
        /// drop-stones, which never participate in a reshuffle.</summary>
        private bool IsSwappableTile(Tile t) =>
            t != null && t.Data != null && !t.Data.isSpecial && !t.Data.isHardTile && !t.Data.isDropStone;

        // ============================================================
        //  INVENTORY
        // ============================================================

        /// <summary>Decrements one unit of boosterId from the owned inventory.
        /// Returns false (and does nothing) if the player doesn't actually have one.</summary>
        private bool SpendOne(string boosterId)
        {
            Dictionary<string, int> inventory = LocalSaveManager.LoadBoosterInventory();
            if (!inventory.TryGetValue(boosterId, out int count) || count <= 0)
            {
                Debug.LogWarning($"[BoosterManager] SpendOne('{boosterId}') — none owned, aborting.");
                return false;
            }

            inventory[boosterId] = count - 1;
            LocalSaveManager.SaveBoosterInventory(inventory); // fires LocalSaveManager.OnProfileChanged -> UI refreshes owned-count
            return true;
        }
    }
}