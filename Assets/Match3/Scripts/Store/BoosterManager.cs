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

        public const string Hammer         = "hammer";
        public const string RowBomb        = "row_bomb";
        public const string ColumnBomb     = "column_bomb";
        public const string Shuffle2Tiles  = "shuffle_2tiles";
        public const string ShuffleBoard   = "shuffle_board";

        private BoardGrid       _grid;
        private BoardController _board;
        private InputHandler    _input;
        private GoalTracker     _goals;
        private MoveCounter     _moves;

        public bool IsBound  { get; private set; }
        public bool IsBusy   { get; private set; }
        public bool IsTargeting { get; private set; }

        public event Action<string> OnBoosterUsed;

        public event Action<string> OnTargetingStarted;

        public event Action OnTargetingEnded;

        public event Action OnBound;

        private Action<Vector2Int> _pendingTargetHandler;
        private string _pendingBoosterId;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

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

            OnBound?.Invoke();
        }

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

        public bool CanUse(string boosterId)
        {
            if (!IsBound || IsBusy || IsTargeting) return false;

            Dictionary<string, int> inventory = LocalSaveManager.LoadBoosterInventory();
            return inventory.TryGetValue(boosterId, out int count) && count > 0;
        }

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

            if (!SpendOne(boosterId)) return;

            handler?.Invoke(cell);
        }

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
            if (!IsSwappableTile(tapped)) return;

            if (_firstSwapCell == null)
            {
                _firstSwapCell = cell;
                return;
            }

            if (_firstSwapCell.Value == cell) return;

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

        private bool IsSwappableTile(Tile t) =>
            t != null && t.Data != null && !t.Data.isSpecial && !t.Data.isHardTile && !t.Data.isDropStone;

        private bool SpendOne(string boosterId)
        {
            Dictionary<string, int> inventory = LocalSaveManager.LoadBoosterInventory();
            if (!inventory.TryGetValue(boosterId, out int count) || count <= 0)
            {
                Debug.LogWarning($"[BoosterManager] SpendOne('{boosterId}') — none owned, aborting.");
                return false;
            }

            inventory[boosterId] = count - 1;
            LocalSaveManager.SaveBoosterInventory(inventory);
            return true;
        }
    }
}
