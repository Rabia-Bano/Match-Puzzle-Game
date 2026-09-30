// ============================================================
//  LevelDataEditor.cs  —  Custom Inspector (EDITOR ONLY)
//
//  MUST live inside a folder named "Editor" (e.g. Assets/Scripts/Editor/)
//  — Unity strips Editor folders from the Android build automatically.
//
//  Adds a clickable "Board Painter" grid under every LevelData asset:
//    1. Pick a brush (Blank / Jelly / Hard Tile / Stone / Erase)
//    2. Click cells on the grid — the TOP row on screen is the TOP row
//       of the board (y = height-1), the bottom row is y = 0.
//  It simply edits blankPositions / jellyPositions / hardTilePositions /
//  stonePositions for you, so you never have to type coordinates.
// ============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Match3.EditorTools
{
    [CustomEditor(typeof(LevelData))]
    public class LevelDataEditor : Editor
    {
        private enum Brush { Blank, Jelly, HardTile, Stone, Erase }

        private static Brush _brush = Brush.Blank;
        private static readonly string[] BrushLabels = { "Blank (hole)", "Jelly", "Hard Tile", "Stone", "Erase" };

        private static readonly Color BlankColor = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color JellyColor = new Color(1f, 0.55f, 0.85f);
        private static readonly Color HardColor  = new Color(0.55f, 0.45f, 0.35f);
        private static readonly Color StoneColor = new Color(0.6f, 0.75f, 1f);
        private static readonly Color EmptyColor = new Color(0.85f, 0.95f, 0.85f);

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var level = (LevelData)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Board Painter", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select a brush, then click cells. Top row on screen = top row of the board.\n" +
                "Clicking a cell that already has that brush removes it (toggle).\n" +
                "Blank = empty hole: no gem, no obstacle, no jelly, tiles fall through it.",
                MessageType.Info);

            _brush = (Brush)GUILayout.Toolbar((int)_brush, BrushLabels);
            EditorGUILayout.Space(6);

            int w = level.width, h = level.height;
            float size = Mathf.Clamp((EditorGUIUtility.currentViewWidth - 60f) / Mathf.Max(1, w), 18f, 36f);

            Color oldBg = GUI.backgroundColor;
            for (int y = h - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(y.ToString(), GUILayout.Width(18));
                for (int x = 0; x < w; x++)
                {
                    string label; Color c;
                    Describe(level, x, y, out label, out c);
                    GUI.backgroundColor = c;
                    if (GUILayout.Button(label, GUILayout.Width(size), GUILayout.Height(size)))
                        Paint(level, x, y);
                }
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = oldBg;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(18));
            for (int x = 0; x < w; x++) GUILayout.Label(x.ToString(), GUILayout.Width(size));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            int blanks = level.blankPositions?.Length ?? 0;
            EditorGUILayout.LabelField($"Playable cells: {w * h - blanks}   |   Blank: {blanks}");

            if (GUILayout.Button("Clear ALL blank cells"))
            {
                Undo.RecordObject(level, "Clear blank cells");
                level.blankPositions = new Vector2Int[0];
                EditorUtility.SetDirty(level);
            }

            if (level.useTimer)
                EditorGUILayout.HelpBox($"Timer ON: {level.timeLimitSeconds / 60}:{level.timeLimitSeconds % 60:00} " +
                                        $"+ {level.moveLimit} moves. Whichever runs out first = level lost.",
                                        MessageType.None);
        }

        private static bool Has(Vector2Int[] arr, int x, int y)
        {
            if (arr == null) return false;
            foreach (var p in arr) if (p.x == x && p.y == y) return true;
            return false;
        }

        private static void Describe(LevelData l, int x, int y, out string label, out Color color)
        {
            if (Has(l.blankPositions, x, y))    { label = "X"; color = BlankColor; return; }
            if (Has(l.hardTilePositions, x, y)) { label = "H"; color = HardColor;  return; }
            if (Has(l.stonePositions, x, y))    { label = "S"; color = StoneColor; return; }
            if (Has(l.jellyPositions, x, y))    { label = "J"; color = JellyColor; return; }
            label = ""; color = EmptyColor;
        }

        private static Vector2Int[] Toggle(Vector2Int[] arr, int x, int y, bool? forceState = null)
        {
            var list = new List<Vector2Int>(arr ?? new Vector2Int[0]);
            int idx = list.FindIndex(p => p.x == x && p.y == y);
            bool want = forceState ?? idx < 0;
            if (want && idx < 0) list.Add(new Vector2Int(x, y));
            if (!want && idx >= 0) list.RemoveAt(idx);
            return list.ToArray();
        }

        private static void Paint(LevelData l, int x, int y)
        {
            Undo.RecordObject(l, "Paint level cell");

            switch (_brush)
            {
                case Brush.Blank:
                    bool makeBlank = !Has(l.blankPositions, x, y);
                    l.blankPositions = Toggle(l.blankPositions, x, y, makeBlank);
                    if (makeBlank)   // a hole can't hold anything else
                    {
                        l.jellyPositions    = Toggle(l.jellyPositions,    x, y, false);
                        l.hardTilePositions = Toggle(l.hardTilePositions, x, y, false);
                        l.stonePositions    = Toggle(l.stonePositions,    x, y, false);
                    }
                    break;

                case Brush.Jelly:
                    if (Has(l.blankPositions, x, y)) { Debug.LogWarning("Can't put jelly on a blank cell."); return; }
                    l.jellyPositions = Toggle(l.jellyPositions, x, y);
                    break;

                case Brush.HardTile:
                    if (Has(l.blankPositions, x, y)) { Debug.LogWarning("Can't put a hard tile on a blank cell."); return; }
                    l.hardTilePositions = Toggle(l.hardTilePositions, x, y);
                    l.stonePositions    = Toggle(l.stonePositions, x, y, false);
                    break;

                case Brush.Stone:
                    if (Has(l.blankPositions, x, y)) { Debug.LogWarning("Can't put a stone on a blank cell."); return; }
                    l.stonePositions    = Toggle(l.stonePositions, x, y);
                    l.hardTilePositions = Toggle(l.hardTilePositions, x, y, false);
                    break;

                case Brush.Erase:
                    l.blankPositions    = Toggle(l.blankPositions,    x, y, false);
                    l.jellyPositions    = Toggle(l.jellyPositions,    x, y, false);
                    l.hardTilePositions = Toggle(l.hardTilePositions, x, y, false);
                    l.stonePositions    = Toggle(l.stonePositions,    x, y, false);
                    break;
            }

            EditorUtility.SetDirty(l);
        }
    }
}
#endif
