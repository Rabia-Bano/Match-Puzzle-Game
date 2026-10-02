using UnityEngine;
using DG.Tweening;

namespace Match3
{
    public enum TileState
    {
        Normal,
        Selected,
        Matched,
        Falling,
        Locked,
        Inactive
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public class Tile : MonoBehaviour
    {
        public TileData Data { get; private set; }

        public int GridX { get; private set; }

        public int GridY { get; private set; }

        public TileState State { get; private set; } = TileState.Inactive;

        public int ObstacleHP { get; private set; }

        [Header("Visual References")]
        [Tooltip("The SpriteRenderer that shows the tile face.")]
        [SerializeField] private SpriteRenderer tileRenderer;

        [Tooltip("A second SpriteRenderer used for the selection / match highlight.")]
        [SerializeField] private SpriteRenderer highlightRenderer;

        private static readonly Color _matchedTint = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color _lockedTint  = new Color(0.55f, 0.55f, 0.65f, 1f);

        public void Initialize(TileData data, int x, int y)
        {
            KillTweens();
            transform.localScale = Vector3.one * BoardGrid.TileVisualScale;

            Data  = data;
            GridX = x;
            GridY = y;

            if (tileRenderer == null)
                tileRenderer = GetComponent<SpriteRenderer>();

            if (highlightRenderer == null)
            {
                var child = transform.Find("HighlightRenderer");
                if (child != null)
                    highlightRenderer = child.GetComponent<SpriteRenderer>();
            }

            RefreshVisuals();

            if (data != null && data.isHardTile)
            {
                ObstacleHP = Mathf.Max(1, data.hardTileMaxHP);
                SetState(TileState.Locked);
            }
            else
            {
                ObstacleHP = 0;
                SetState(TileState.Normal);
            }

            GetComponent<TileVisualController>()?.StartIdleAnimation();
        }

        public bool DamageObstacle()
        {
            if (Data == null || !Data.isHardTile || ObstacleHP <= 0) return false;

            ObstacleHP--;
            RefreshObstacleDamageSprite();

            transform.DOKill();
            transform.DOPunchScale(Vector3.one * 0.12f, 0.15f, 4, 0.6f);

            return ObstacleHP <= 0;
        }

        private void RefreshObstacleDamageSprite()
        {
            if (Data == null || !Data.isHardTile || tileRenderer == null) return;
            if (Data.hardTileDamageSprites == null || Data.hardTileDamageSprites.Length == 0) return;

            int hitsTaken   = Data.hardTileMaxHP - ObstacleHP;
            int spriteIndex = Mathf.Clamp(hitsTaken - 1, 0, Data.hardTileDamageSprites.Length - 1);

            if (hitsTaken > 0)
                tileRenderer.sprite = Data.hardTileDamageSprites[spriteIndex];
        }

        public void SetGridPosition(int x, int y)
        {
            GridX = x;
            GridY = y;
        }

        public void SetState(TileState newState)
        {
            State = newState;
            ApplyStateTint();
            UpdateHighlight();
        }

        public void RefreshVisuals()
        {
            if (Data == null || tileRenderer == null) return;

            tileRenderer.sprite = Data.sprite;
            tileRenderer.color  = Color.white;
        }

        private void ApplyStateTint()
        {
            if (tileRenderer == null) return;

            tileRenderer.color = State switch
            {
                TileState.Matched  => _matchedTint,
                TileState.Locked   => _lockedTint,
                _                  => Color.white
            };
        }

        private void UpdateHighlight()
        {
            if (highlightRenderer == null) return;

            bool showHighlight = State == TileState.Selected;
            highlightRenderer.enabled = showHighlight;

            if (showHighlight && Data?.highlightSprite != null)
                highlightRenderer.sprite = Data.highlightSprite;
        }

        public void ResetForPool()
        {
            KillTweens();
            transform.localScale = Vector3.one * BoardGrid.TileVisualScale;

            Data  = null;
            GridX = -1;
            GridY = -1;
            ObstacleHP = 0;

            if (tileRenderer != null)
            {
                tileRenderer.sprite = null;
                tileRenderer.color  = Color.white;
            }

            if (highlightRenderer != null)
                highlightRenderer.enabled = false;

            SetState(TileState.Inactive);
        }

        private void KillTweens()
        {
            transform.DOKill();
            if (tileRenderer != null)     tileRenderer.DOKill();
            if (highlightRenderer != null) highlightRenderer.DOKill();
            GetComponent<TileVisualController>()?.KillAllTweens();
        }

        public override string ToString() =>
            $"Tile[{GridX},{GridY}] Color={Data?.color} State={State}";
    }
}
