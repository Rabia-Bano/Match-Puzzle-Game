// ============================================================
//  ObjectPool.cs  —  MonoBehaviour
//
//  A simple, inspector-configurable pool for Tile GameObjects.
//
//  Setup:
//    1. Create an empty GameObject named "TilePool".
//    2. Attach ObjectPool to it.
//    3. Assign the Tile prefab to `tilePrefab`.
//    4. Set `initialSize` (e.g. width × height + 20 % headroom).
//    5. Wire the ObjectPool reference into BoardGrid.tilePool.
//
//  Get()    — takes a Tile from the pool (expands if empty).
//  Return() — puts a Tile back and deactivates it.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class ObjectPool : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Pool Configuration")]
        [Tooltip("The Tile prefab to instantiate. Must have a Tile component.")]
        [SerializeField] private Tile tilePrefab;

        [Tooltip("How many tiles to pre-create at Start(). " +
                 "Recommended: board width × height + 20 %.")]
        [SerializeField] private int initialSize = 80;

        [Tooltip("If the pool runs out, should it expand automatically?")]
        [SerializeField] private bool allowExpansion = true;

        // ── Internal state ────────────────────────────────────

        private readonly Stack<Tile> _available = new();
        private int _totalCreated;

        // ── Lifecycle ─────────────────────────────────────────

        private void Awake()
        {
            if (tilePrefab == null)
            {
                Debug.LogError("[ObjectPool] tilePrefab is not assigned!", this);
                return;
            }

            // Pre-warm the pool
            for (int i = 0; i < initialSize; i++)
                _available.Push(CreateTile());

            Debug.Log($"[ObjectPool] Pre-warmed with {initialSize} tiles.");
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Returns a ready-to-use Tile from the pool.
        /// The tile's GameObject is activated; call Tile.Initialize()
        /// afterwards to configure it for its grid position.
        /// </summary>
        public Tile Get()
        {
            if (_available.Count == 0)
            {
                if (!allowExpansion)
                {
                    Debug.LogError("[ObjectPool] Pool exhausted and expansion is disabled.");
                    return null;
                }

                // Expand: double the current capacity
                int grow = Mathf.Max(1, _totalCreated / 2);
                Debug.LogWarning($"[ObjectPool] Pool empty — expanding by {grow} tiles.");
                for (int i = 0; i < grow; i++)
                    _available.Push(CreateTile());
            }

            Tile tile = _available.Pop();
            tile.gameObject.SetActive(true);
            return tile;
        }

        /// <summary>
        /// Returns a Tile back to the pool.
        /// Callers should call tile.ResetForPool() before this
        /// (BoardGrid.RemoveTile() does this automatically).
        /// </summary>
        public void Return(Tile tile)
        {
            if (tile == null) return;

            tile.gameObject.SetActive(false);
            tile.transform.SetParent(transform);   // re-parent to pool container
            _available.Push(tile);
        }

        // ── Stats (optional debug) ────────────────────────────

        public int AvailableCount => _available.Count;
        public int TotalCreated   => _totalCreated;

        // ── Internal helpers ──────────────────────────────────

        private Tile CreateTile()
        {
            // Instantiate under this transform to keep the hierarchy tidy
            Tile tile = Instantiate(tilePrefab, transform);
            tile.gameObject.SetActive(false);
            tile.gameObject.name = $"Tile_{_totalCreated:000}";
            _totalCreated++;
            return tile;
        }
    }
}
