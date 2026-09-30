// ============================================================
//  ThemeAmbientSceneSettings.cs  —  NEW (optional, one per scene)
//
//  Put this on ANY GameObject in a scene to change how the theme's
//  ambient animation (snow / leaves / sand / stars) behaves in THAT
//  scene only. Scenes without it use the defaults (full density).
//
//  Suggested:
//    GameBoardScene      → densityMultiplier 0.5  (gems stay readable)
//    BossGameBoardScene  → densityMultiplier 0.5
//    Map / Login / Store → leave default (1.0)
// ============================================================

using UnityEngine;

namespace Match3.Theme
{
    public class ThemeAmbientSceneSettings : MonoBehaviour
    {
        [Tooltip("Untick to hide the ambient effect completely in this scene.")]
        public bool showAmbientEffect = true;

        [Tooltip("1 = normal amount, 0.5 = half the particles, 2 = double.")]
        [Range(0f, 2f)] public float densityMultiplier = 1f;

        [Tooltip("Tick to use the sortingOrder below for this scene's effect canvas.")]
        public bool overrideSortingOrder = false;

        [Tooltip("Higher = drawn on top of more UI. Negative = behind the scene UI.")]
        public int sortingOrder = 1;
    }
}
