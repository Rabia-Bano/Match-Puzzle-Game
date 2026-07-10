// ============================================================
//  TileData.cs  —  ScriptableObject
//  Create via: Assets > Create > Match3 > Tile Data
//
//  Each colour/type in your game gets ONE TileData asset.
//  Drag them into TileSpawner.availableTiles[].
// ============================================================

using UnityEngine;

namespace Match3
{
    // ── Enums ────────────────────────────────────────────────

    /// <summary>
    /// The visual / logical colour of a tile.
    /// Add more colours here and create matching TileData assets.
    /// </summary>
    public enum TileColor
    {
        None   = 0,
        Red    = 1,
        Blue   = 2,
        Green  = 3,
        Yellow = 4,
        Purple = 5,
        Orange = 6
    }

    /// <summary>
    /// Extra behaviour that a special tile can carry.
    /// RowBlast clears the whole row; ColBlast clears the column, etc.
    /// </summary>
    public enum SpecialType
    {
        None      = 0,
        RowBlast  = 1,   // clears the entire row
        ColBlast  = 2,   // clears the entire column
        Bomb      = 3,   // clears a 3×3 area
        Rainbow   = 4    // clears all tiles of a target colour
    }

    // ── ScriptableObject ─────────────────────────────────────

    [CreateAssetMenu(
        fileName = "TileData_New",
        menuName  = "Match3/Tile Data",
        order     = 0)]
    public class TileData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique numeric ID. Must match across scenes / save data.")]
        public int id;

        [Tooltip("The sprite rendered on the tile face.")]
        public Sprite sprite;

        [Tooltip("Which colour group this tile belongs to.")]
        public TileColor color;

        // ── Special tile ──────────────────────────────────────
        [Header("Special Behaviour")]
        [Tooltip("Is this tile a special / power-up tile?")]
        public bool isSpecial;

        [Tooltip("Only relevant when isSpecial == true.")]
        public SpecialType specialType;

        // ── Visual polish ─────────────────────────────────────
        [Header("Visuals")]
        [Tooltip("Optional highlight / glow sprite shown when selected.")]
        public Sprite highlightSprite;

        [Tooltip("Particle effect prefab played when this tile is cleared.")]
        public GameObject clearParticlePrefab;

        // ── Helper ────────────────────────────────────────────

        /// <summary>Returns true if two tiles are the same colour (and neither is None).</summary>
        public static bool SameColor(TileData a, TileData b)
        {
            if (a == null || b == null)          return false;
            if (a.color == TileColor.None)       return false;
            return a.color == b.color;
        }
    }
}
 