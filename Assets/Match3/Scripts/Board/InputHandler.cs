using System;
using UnityEngine;

namespace Match3
{
    public class InputHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardGrid boardGrid;

        [Header("Settings")]
        [SerializeField] private float     minSwipePixels = 20f;
        [SerializeField] private LayerMask tileLayerMask  = ~0;

        public event Action<Vector2Int, Vector2Int> OnSwipeDetected;

        public event Action<Vector2Int> OnTileClicked;

        private Vector3    _touchStartWorld;
        private Vector3    _touchStartScreen;
        private Vector2Int _fromCell;
        private bool       _isDragging;
        private bool       _swipeFired;

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
            if (boardGrid == null)
                Debug.LogError("[InputHandler] boardGrid not assigned!", this);
        }

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            HandleMouse();
#else
            HandleTouch();
#endif
        }

        private void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
                OnPressBegin(Input.mousePosition);

            if (Input.GetMouseButton(0) && _isDragging && !_swipeFired)
                OnPressMoved(Input.mousePosition);

            if (Input.GetMouseButtonUp(0))
                OnPressEnd(Input.mousePosition);
        }

        private void HandleTouch()
        {
            if (Input.touchCount == 0) return;
            Touch t = Input.GetTouch(0);

            switch (t.phase)
            {
                case TouchPhase.Began:
                    OnPressBegin(t.position); break;
                case TouchPhase.Moved:
                    if (_isDragging && !_swipeFired)
                        OnPressMoved(t.position);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    OnPressEnd(t.position); break;
            }
        }

        private void OnPressBegin(Vector3 screenPos)
        {
            Vector3 worldPos = ScreenToWorld(screenPos);
            Collider2D hit   = Physics2D.OverlapPoint(worldPos, tileLayerMask);
            if (hit == null) return;
            if (!boardGrid.WorldToGrid(worldPos, out int gx, out int gy)) return;

            _touchStartWorld  = worldPos;
            _touchStartScreen = screenPos;
            _fromCell         = new Vector2Int(gx, gy);
            _isDragging       = true;
            _swipeFired       = false;
        }

        private void OnPressMoved(Vector3 screenPos)
        {
            if (!_isDragging) return;

            Vector3 worldPos = ScreenToWorld(screenPos);
            Vector2 delta    = worldPos - _touchStartWorld;
            float minWorld   = minSwipePixels / _cam.pixelHeight * (_cam.orthographicSize * 2f);

            if (delta.magnitude < minWorld) return;

            Vector2Int dir    = SwipeDirection(delta);
            Vector2Int toCell = _fromCell + dir;

            _swipeFired = true;
            OnSwipeDetected?.Invoke(_fromCell, toCell);
        }

        private void OnPressEnd(Vector3 screenPos)
        {
            if (_isDragging && !_swipeFired)
            {
                Vector2 screenDelta = (Vector2)screenPos - (Vector2)_touchStartScreen;
                float minPx = minSwipePixels * 0.5f;

                if (screenDelta.magnitude < minPx)
                    OnTileClicked?.Invoke(_fromCell);
            }

            _isDragging = false;
            _swipeFired = false;
        }

        private Vector3 ScreenToWorld(Vector3 screenPos)
        {
            screenPos.z = -_cam.transform.position.z;
            return _cam.ScreenToWorldPoint(screenPos);
        }

        private static Vector2Int SwipeDirection(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return delta.x > 0 ? Vector2Int.right : Vector2Int.left;
            return delta.y > 0 ? Vector2Int.up : Vector2Int.down;
        }

        public void SetInputEnabled(bool enabled) => this.enabled = enabled;
    }
}
