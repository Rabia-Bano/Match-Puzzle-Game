// ============================================================
//  GoalIconPrefabBuilder.cs  —  Editor Helper
//  Menu: Match3 > Build GoalIcon Prefab
//
//  Creates the GoalIconPrefab that GameHUD auto-instantiates
//  for each goal. Run ONCE then save as prefab.
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Match3
{
    public static class GoalIconPrefabBuilder
    {
        [MenuItem("Match3/Build GoalIcon Prefab")]
        public static void BuildGoalIconPrefab()
        {
            // Root card
            GameObject root = new GameObject("GoalIconPrefab");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(72, 90);

            var rootImg = root.AddComponent<Image>();
            rootImg.color = new Color(0.1f, 0.3f, 0.6f, 0.85f);

            var outline = root.AddComponent<Outline>();
            outline.effectColor    = new Color(0.5f, 0.8f, 1f, 0.7f);
            outline.effectDistance = new Vector2(2, -2);

            root.AddComponent<GoalIconRuntime>();

            // ── Icon ──────────────────────────────────────────
            GameObject iconGO = new GameObject("GoalIcon");
            iconGO.transform.SetParent(root.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.sizeDelta        = new Vector2(50, 50);
            iconRT.anchoredPosition = new Vector2(0, 18);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.preserveAspect = true;

            // ── Count text ────────────────────────────────────
            GameObject countGO = new GameObject("CountText");
            countGO.transform.SetParent(root.transform, false);
            var countRT = countGO.AddComponent<RectTransform>();
            countRT.sizeDelta        = new Vector2(68, 22);
            countRT.anchoredPosition = new Vector2(0, -18);
            var countTMP = countGO.AddComponent<TextMeshProUGUI>();
            countTMP.text      = "20";
            countTMP.fontSize  = 16;
            countTMP.fontStyle = FontStyles.Bold;
            countTMP.alignment = TextAlignmentOptions.Center;
            countTMP.color     = Color.white;

            // ── Progress bar BG ───────────────────────────────
            GameObject barBG = new GameObject("ProgressBarBG");
            barBG.transform.SetParent(root.transform, false);
            var barBGRT = barBG.AddComponent<RectTransform>();
            barBGRT.sizeDelta        = new Vector2(60, 8);
            barBGRT.anchoredPosition = new Vector2(0, -38);
            barBG.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.2f);

            GameObject barFill = new GameObject("ProgressBar");
            barFill.transform.SetParent(barBG.transform, false);
            var barFillRT = barFill.AddComponent<RectTransform>();
            barFillRT.sizeDelta        = new Vector2(60, 8);
            barFillRT.anchoredPosition = Vector2.zero;
            barFillRT.anchorMin        = new Vector2(0, 0.5f);
            barFillRT.anchorMax        = new Vector2(0, 0.5f);
            barFillRT.pivot            = new Vector2(0, 0.5f);
            var barFillImg = barFill.AddComponent<Image>();
            barFillImg.color      = new Color(1f, 0.7f, 0.1f);
            barFillImg.type       = Image.Type.Filled;
            barFillImg.fillMethod = Image.FillMethod.Horizontal;
            barFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFillImg.fillAmount = 0f;

            // ── Complete overlay (tick) ────────────────────────
            GameObject completeGO = new GameObject("CompleteOverlay");
            completeGO.transform.SetParent(root.transform, false);
            var completeRT = completeGO.AddComponent<RectTransform>();
            completeRT.sizeDelta        = new Vector2(72, 90);
            completeRT.anchoredPosition = Vector2.zero;
            var completeImg = completeGO.AddComponent<Image>();
            completeImg.color = new Color(0.1f, 0.7f, 0.2f, 0.85f);

            GameObject tickGO = new GameObject("Tick");
            tickGO.transform.SetParent(completeGO.transform, false);
            var tickRT = tickGO.AddComponent<RectTransform>();
            tickRT.sizeDelta        = new Vector2(60, 60);
            tickRT.anchoredPosition = Vector2.zero;
            var tickTMP = tickGO.AddComponent<TextMeshProUGUI>();
            tickTMP.text      = "✓";
            tickTMP.fontSize  = 36;
            tickTMP.fontStyle = FontStyles.Bold;
            tickTMP.alignment = TextAlignmentOptions.Center;
            tickTMP.color     = Color.white;

            completeGO.SetActive(false);

            // ── Wire GoalIconRuntime references ───────────────
            var runtime = root.GetComponent<GoalIconRuntime>();
            // Use SerializedObject to set serialized fields
            var so = new SerializedObject(runtime);
            so.FindProperty("goalIconImage").objectReferenceValue    = iconImg;
            so.FindProperty("countText").objectReferenceValue        = countTMP;
            so.FindProperty("progressBar").objectReferenceValue      = barFillImg;
            so.FindProperty("completeOverlay").objectReferenceValue  = completeGO;
            so.FindProperty("background").objectReferenceValue       = rootImg.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── Save as prefab ────────────────────────────────
            string path = "Assets/Match3/Prefabs/GoalIconPrefab.prefab";
            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, path, out success);
            Object.DestroyImmediate(root);

            if (success)
                Debug.Log($"[GoalIconPrefabBuilder] Prefab saved at {path}");
            else
                Debug.LogError("[GoalIconPrefabBuilder] Failed to save prefab!");

            AssetDatabase.Refresh();
        }
    }
}
#endif
