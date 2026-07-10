// ============================================================
//  CanvasBuilder.cs  —  Editor Tool  (FIXED)
//  Place in: Assets/Match3/Editor/
//  Menu: Match3 > Build Game Canvas
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace Match3
{
    public static class CanvasBuilder
    {
        [MenuItem("Match3/Build Game Canvas")]
        public static void BuildCanvas()
        {
            // ── Delete old canvas if exists ───────────────────
            var old = GameObject.Find("GameCanvas");
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
                Debug.Log("[CanvasBuilder] Old GameCanvas removed.");
            }

            // ── Root Canvas ───────────────────────────────────
            var canvasGO = new GameObject("GameCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Build Game Canvas");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(700, 1100);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // ══════════════════════════════════════════════════
            //  TOP BAR
            // ══════════════════════════════════════════════════
            var topBar = MakePanel(canvasGO, "TopBar",
                new Vector2(700, 130),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -65),
                new Color(0.12f, 0.45f, 0.80f, 1f));

            // Score section — LEFT
            var scoreSec = MakePanel(topBar, "ScoreSection",
                new Vector2(220, 110),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(110, 0),
                Color.clear);

            MakeTMP(scoreSec, "ScoreText", "Score: 0",
                new Vector2(10, 20), new Vector2(200, 30),
                20, FontStyles.Bold, TextAlignmentOptions.Left);

            var scoreBarBG = MakePanel(scoreSec, "ScoreBarBG",
                new Vector2(200, 16),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -18),
                new Color(0.08f, 0.18f, 0.35f));

            var scoreBarFill = MakePanel(scoreBarBG, "ScoreBarFill",
                new Vector2(200, 16),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0, 0),
                new Color(1f, 0.55f, 0f));
            MakeFillImage(scoreBarFill, 0.07f);

            // Move box — CENTER
            var moveBox = MakePanel(topBar, "MoveBox",
                new Vector2(170, 118),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 0),
                new Color(0.08f, 0.30f, 0.70f, 1f));

            MakeTMP(moveBox, "MoveLabel", "Move",
                new Vector2(0, 32), new Vector2(160, 30),
                19, FontStyles.Bold, TextAlignmentOptions.Center);

            MakeTMP(moveBox, "MoveNumber", "30",
                new Vector2(0, -12), new Vector2(160, 60),
                52, FontStyles.Bold, TextAlignmentOptions.Center);

            // Goals section — RIGHT
            var goalsSec = MakePanel(topBar, "GoalsSection",
                new Vector2(210, 110),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-105, 0),
                Color.clear);

            MakeTMP(goalsSec, "GoalsLabel", "Goals",
                new Vector2(0, 32), new Vector2(200, 26),
                19, FontStyles.Bold, TextAlignmentOptions.Center);

            // GoalIconContainer — HorizontalLayoutGroup
            var goalContainerGO = new GameObject("GoalIconContainer");
            goalContainerGO.transform.SetParent(goalsSec.transform, false);
            var gcRT = goalContainerGO.AddComponent<RectTransform>();
            gcRT.sizeDelta        = new Vector2(200, 60);
            gcRT.anchoredPosition = new Vector2(0, -10);
            var hlg = goalContainerGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing            = 8;
            hlg.childAlignment     = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            var csf = goalContainerGO.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ══════════════════════════════════════════════════
            //  PET HP PANEL
            // ══════════════════════════════════════════════════
            var petPanel = MakePanel(canvasGO, "PetHPPanel",
                new Vector2(700, 52),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 192),
                new Color(0f, 0f, 0f, 0.60f));

            // Pet avatar
            var petAvatar = MakePanel(petPanel, "PetAvatar",
                new Vector2(44, 44),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(28, 0),
                new Color(1f, 1f, 1f, 0.15f));

            MakeTMP(petPanel, "PetLabel", "Pet HP",
                new Vector2(-270, 0), new Vector2(80, 26),
                13, FontStyles.Normal, TextAlignmentOptions.Left);

            var hpBG = MakePanel(petPanel, "PetHPBarBG",
                new Vector2(460, 16),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(60, 0),
                new Color(0.1f, 0.1f, 0.1f, 0.8f));

            var hpFill = MakePanel(hpBG, "PetHPBarFill",
                new Vector2(460, 16),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0, 0),
                new Color(0.15f, 0.85f, 0.25f));
            MakeFillImage(hpFill, 0.8f);

            MakeTMP(hpBG, "PetHPText", "80/100",
                new Vector2(0, 0), new Vector2(460, 16),
                11, FontStyles.Bold, TextAlignmentOptions.Center);

            // ══════════════════════════════════════════════════
            //  BOTTOM PANEL — 5 booster slots
            // ══════════════════════════════════════════════════
            var bottomPanel = MakePanel(canvasGO, "BottomPanel",
                new Vector2(700, 135),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 67),
                new Color(0.10f, 0.38f, 0.72f, 1f));

            var bHLG = bottomPanel.AddComponent<HorizontalLayoutGroup>();
            bHLG.spacing             = 10;
            bHLG.padding             = new RectOffset(14, 14, 14, 14);
            bHLG.childAlignment      = TextAnchor.MiddleCenter;
            bHLG.childForceExpandWidth  = true;
            bHLG.childForceExpandHeight = false;

            string[] slotNames  = { "Pet",    "Hammer", "Crystal", "Beam",  "Refresh" };
            Color[]  slotColors = {
                new Color(0.55f, 0.20f, 0.80f),
                new Color(0.35f, 0.38f, 0.45f),
                new Color(0.10f, 0.55f, 0.90f),
                new Color(0.15f, 0.65f, 0.85f),
                new Color(0.15f, 0.60f, 0.75f)
            };

            for (int i = 0; i < 5; i++)
                MakeBoosterSlot(bottomPanel, slotNames[i], slotColors[i], i);

            // ══════════════════════════════════════════════════
            //  SETTINGS BUTTON
            // ══════════════════════════════════════════════════
            var settingsBtn = MakePanel(canvasGO, "SettingsButton",
                new Vector2(44, 44),
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-26, 26),
                new Color(0.15f, 0.45f, 0.75f, 0.9f));
            settingsBtn.AddComponent<Button>();
            MakeTMP(settingsBtn, "SettingsIcon", "=",
                new Vector2(0, 0), new Vector2(44, 44),
                22, FontStyles.Bold, TextAlignmentOptions.Center);

            // ══════════════════════════════════════════════════
            //  WIN PANEL
            // ══════════════════════════════════════════════════
            var winPanel = MakeFullScreen(canvasGO, "WinPanel",
                new Color(0f, 0f, 0f, 0.78f));
            winPanel.SetActive(false);

            var winCard = MakePanel(winPanel, "WinCard",
                new Vector2(500, 560),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 0),
                new Color(0.08f, 0.38f, 0.78f));

            MakeTMP(winCard, "WinTitle", "Level Complete!",
                new Vector2(0, 218), new Vector2(480, 44),
                36, FontStyles.Bold, TextAlignmentOptions.Center);

            MakeTMP(winCard, "WinScoreText", "Score: 0",
                new Vector2(0, 162), new Vector2(480, 32),
                24, FontStyles.Normal, TextAlignmentOptions.Center);

            MakeTMP(winCard, "WinMovesText", "Moves left: 0",
                new Vector2(0, 124), new Vector2(480, 28),
                18, FontStyles.Normal, TextAlignmentOptions.Center);

            // Stars row
            var starsRowGO = new GameObject("StarsRow");
            starsRowGO.transform.SetParent(winCard.transform, false);
            var srRT = starsRowGO.AddComponent<RectTransform>();
            srRT.sizeDelta        = new Vector2(340, 80);
            srRT.anchoredPosition = new Vector2(0, 48);
            var srHLG = starsRowGO.AddComponent<HorizontalLayoutGroup>();
            srHLG.spacing        = 18;
            srHLG.childAlignment = TextAnchor.MiddleCenter;
            srHLG.childForceExpandWidth  = false;
            srHLG.childForceExpandHeight = false;

            for (int i = 1; i <= 3; i++)
            {
                var star = MakePanel(starsRowGO, $"Star_{i}",
                    new Vector2(76, 76),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Color(0.9f, 0.75f, 0.1f));
                MakeTMP(star, "StarTxt", "*",
                    new Vector2(0, 0), new Vector2(76, 76),
                    44, FontStyles.Bold, TextAlignmentOptions.Center);
            }

            MakeButton(winCard, "NextLevelButton", "Next Level",
                new Vector2(0, -68), new Vector2(280, 58),
                new Color(0.15f, 0.70f, 0.28f));

            MakeButton(winCard, "WinReplayButton", "Replay",
                new Vector2(0, -142), new Vector2(280, 50),
                new Color(0.40f, 0.42f, 0.55f));

            // ══════════════════════════════════════════════════
            //  LOSE PANEL
            // ══════════════════════════════════════════════════
            var losePanel = MakeFullScreen(canvasGO, "LosePanel",
                new Color(0f, 0f, 0f, 0.82f));
            losePanel.SetActive(false);

            var loseCard = MakePanel(losePanel, "LoseCard",
                new Vector2(500, 420),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 0),
                new Color(0.48f, 0.08f, 0.08f));

            MakeTMP(loseCard, "LoseTitle", "Out of Moves!",
                new Vector2(0, 152), new Vector2(480, 44),
                34, FontStyles.Bold, TextAlignmentOptions.Center);

            MakeTMP(loseCard, "LoseScoreText", "Score: 0",
                new Vector2(0, 96), new Vector2(480, 32),
                24, FontStyles.Normal, TextAlignmentOptions.Center);

            MakeButton(loseCard, "LoseReplayButton", "Try Again",
                new Vector2(0, 12), new Vector2(280, 58),
                new Color(0.85f, 0.35f, 0.08f));

            MakeButton(loseCard, "LoseMenuButton", "Main Menu",
                new Vector2(0, -58), new Vector2(280, 50),
                new Color(0.28f, 0.30f, 0.42f));

            // ── EventSystem ───────────────────────────────────
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            Selection.activeGameObject = canvasGO;
            Debug.Log("[CanvasBuilder] GameCanvas built successfully! " +
                      "Now add GameHUD script to GameCanvas and wire references.");
        }

        // ══════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════

        static GameObject MakePanel(GameObject parent, string name,
            Vector2 size, Vector2 ancMin, Vector2 ancMax,
            Vector2 pos, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta        = size;
            rt.anchorMin        = ancMin;
            rt.anchorMax        = ancMax;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        static GameObject MakeFullScreen(GameObject parent,
            string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        static TextMeshProUGUI MakeTMP(GameObject parent, string name,
            string text, Vector2 pos, Vector2 size,
            float fontSize, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta        = size;
            rt.anchoredPosition = pos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color     = Color.white;
            return tmp;
        }

        static void MakeFillImage(GameObject go, float initialFill)
        {
            var img = go.GetComponent<Image>();
            if (img == null) return;
            img.type        = Image.Type.Filled;
            img.fillMethod  = Image.FillMethod.Horizontal;
            img.fillOrigin  = (int)Image.OriginHorizontal.Left;
            img.fillAmount  = initialFill;
        }

        static void MakeBoosterSlot(GameObject parent,
            string label, Color color, int index)
        {
            var slot = new GameObject($"Slot_{label}");
            slot.transform.SetParent(parent.transform, false);
            var rt = slot.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 100);

            var img = slot.AddComponent<Image>();
            img.color = new Color(
                color.r * 0.55f,
                color.g * 0.55f,
                color.b * 0.55f, 0.85f);

            slot.AddComponent<Button>();

            // Icon area
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(slot.transform, false);
            var iconRT = iconGO.AddComponent<RectTransform>();
            iconRT.sizeDelta        = new Vector2(58, 58);
            iconRT.anchoredPosition = new Vector2(0, 12);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.color = color;

            // Label
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(slot.transform, false);
            var lblRT = lblGO.AddComponent<RectTransform>();
            lblRT.sizeDelta        = new Vector2(90, 20);
            lblRT.anchoredPosition = new Vector2(0, -30);
            var tmp = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 12;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;

            // Add BoosterSlotUI to non-pet slots
            if (index > 0)
                slot.AddComponent<BoosterSlotUI>();
        }

        static void MakeButton(GameObject parent, string name,
            string label, Vector2 pos, Vector2 size, Color color)
        {
            var go = MakePanel(parent, name, size,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, color);
            go.AddComponent<Button>();
            MakeTMP(go, "Text", label, Vector2.zero, size,
                20, FontStyles.Bold, TextAlignmentOptions.Center);
        }
    }
}
#endif