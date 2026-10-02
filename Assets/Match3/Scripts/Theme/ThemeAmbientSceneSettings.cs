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
