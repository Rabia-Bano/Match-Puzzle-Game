using System.Collections.Generic;
using UnityEngine;

namespace Match3
{
    public class ObjectPool : MonoBehaviour
    {
        [Header("Pool Configuration")]
        [Tooltip("The Tile prefab to instantiate. Must have a Tile component.")]
        [SerializeField] private Tile tilePrefab;

        [Tooltip("How many tiles to pre-create at Start(). " +
                 "Recommended: board width × height + 20 %.")]
        [SerializeField] private int initialSize = 80;

        [Tooltip("If the pool runs out, should it expand automatically?")]
        [SerializeField] private bool allowExpansion = true;

        private readonly Stack<Tile> _available = new();
        private int _totalCreated;

        private void Awake()
        {
            if (tilePrefab == null)
            {
                Debug.LogError("[ObjectPool] tilePrefab is not assigned!", this);
                return;
            }

            for (int i = 0; i < initialSize; i++)
                _available.Push(CreateTile());

            Debug.Log($"[ObjectPool] Pre-warmed with {initialSize} tiles.");
        }

        public Tile Get()
        {
            if (_available.Count == 0)
            {
                if (!allowExpansion)
                {
                    Debug.LogError("[ObjectPool] Pool exhausted and expansion is disabled.");
                    return null;
                }

                int grow = Mathf.Max(1, _totalCreated / 2);
                Debug.LogWarning($"[ObjectPool] Pool empty — expanding by {grow} tiles.");
                for (int i = 0; i < grow; i++)
                    _available.Push(CreateTile());
            }

            Tile tile = _available.Pop();
            tile.gameObject.SetActive(true);
            return tile;
        }

        public void Return(Tile tile)
        {
            if (tile == null) return;

            tile.gameObject.SetActive(false);
            tile.transform.SetParent(transform);
            _available.Push(tile);
        }

        public int AvailableCount => _available.Count;
        public int TotalCreated   => _totalCreated;

        private Tile CreateTile()
        {
            Tile tile = Instantiate(tilePrefab, transform);
            tile.gameObject.SetActive(false);
            tile.gameObject.name = $"Tile_{_totalCreated:000}";
            _totalCreated++;
            return tile;
        }
    }
}
