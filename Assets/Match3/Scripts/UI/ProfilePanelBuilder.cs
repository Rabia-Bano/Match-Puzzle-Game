// ============================================================
//  ProfilePanelBuilder.cs
//
//  WHAT THIS DOES:
//  Ye script Unity mein puri ProfilePanel UI automatically
//  banata hai — koi manual UI work nahi karna.
//
//  HOW TO USE:
//  1. Kisi bhi scene mein empty GameObject banao "ProfileBuilder"
//  2. Is script ko us par attach karo
//  3. Inspector mein Canvas assign karo
//  4. Play karo — poori ProfilePanel ban jayegi
//  5. Play band karo — Panel Hierarchy mein saved rahega
//  6. Is script (ProfilePanelBuilder) ko GameObject se REMOVE karo
//  7. Ab ProfilePanel.cs use hogi — builder ki zaroorat nahi
//
//  GUEST vs REGISTERED:
//  - Guest player ko "Register Account" button dikhega
//  - Registered player ko "Logout" button dikhega
//  - Guest register kare to progress safe rehti hai (same UID)
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Firebase;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ProfilePanelBuilder : MonoBehaviour
{
    [Header("Required — Drag your scene's Canvas here")]
    public Canvas targetCanvas;

    [Header("Optional Sprites (chod do agar nahi hain)")]
    public Sprite defaultAvatarSprite;
    public Sprite closeIconSprite;
    public Sprite editIconSprite;
    public Sprite cameraIconSprite;

    [Header("Colors")]
    public Color headerBgColor     = new Color(0.20f, 0.47f, 0.75f, 1f);  // Blue
    public Color statCardColor     = new Color(0.94f, 0.96f, 0.99f, 1f);  // Light blue-grey
    public Color logoutBtnColor    = new Color(0.90f, 0.25f, 0.25f, 1f);  // Red
    public Color registerBtnColor  = new Color(0.18f, 0.70f, 0.45f, 1f);  // Green
    public Color saveBtnColor      = new Color(0.20f, 0.60f, 0.86f, 1f);  // Blue

    // ── Called from Inspector button or automatically in Start ──
    [ContextMenu("Build Profile Panel")]
    public void BuildPanel()
    {
        if (targetCanvas == null)
        {
            Debug.LogError("[ProfilePanelBuilder] Canvas assign nahi ki! Inspector mein drag karo.");
            return;
        }

        // Delete old panel if rebuilding
        Transform existing = targetCanvas.transform.Find("ProfilePanel");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
            Debug.Log("[ProfilePanelBuilder] Old ProfilePanel deleted — rebuilding...");
        }

        GameObject panel = BuildProfilePanel(targetCanvas.transform);

#if UNITY_EDITOR
        Selection.activeGameObject = panel;
        EditorUtility.SetDirty(targetCanvas.gameObject);
#endif
    }

    // ── MAIN BUILDER ──────────────────────────────────────────

    private GameObject BuildProfilePanel(Transform canvasRoot)
    {
        // ── Root: full-screen dimmed overlay ──────────────────
        GameObject root = CreateGO("ProfilePanel", canvasRoot);
        RectTransform rootRT = AddFullStretch(root);
        Image dimmer = root.AddComponent<Image>();
        dimmer.color = new Color(0, 0, 0, 0.65f);
        root.SetActive(false);   // Hidden by default

        // ── Center card ───────────────────────────────────────
        GameObject card = CreateGO("Card", root.transform);
        RectTransform cardRT = card.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.03f, 0.04f);
        cardRT.anchorMax = new Vector2(0.97f, 0.96f);
        cardRT.offsetMin = cardRT.offsetMax = Vector2.zero;
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = Color.white;
        AddRoundedCorners(card);

        VerticalLayoutGroup cardVLG = card.AddComponent<VerticalLayoutGroup>();
        cardVLG.childControlWidth  = true;
        cardVLG.childControlHeight = false;
        cardVLG.childForceExpandWidth = true;
        cardVLG.childForceExpandHeight = false;
        cardVLG.spacing = 0;
        cardVLG.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter cardCSF = card.AddComponent<ContentSizeFitter>();
        cardCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── SECTION 1: Header (blue) ──────────────────────────
        GameObject header = BuildHeader(card.transform, root);

        // ── SECTION 2: Stats grid ─────────────────────────────
        GameObject statsSection = BuildStatsSection(card.transform);

        // ── SECTION 3: Pets row ───────────────────────────────
        GameObject petsSection = BuildPetsSection(card.transform);

        // ── SECTION 4: Action buttons ─────────────────────────
        GameObject buttonsSection = BuildButtonsSection(card.transform, root);

        // ── ProfilePanel.cs attach karo ───────────────────────
        ProfilePanel profilePanel = root.AddComponent<ProfilePanel>();
        WireProfilePanel(profilePanel, root, header, statsSection,
                         petsSection, buttonsSection);

        return root;
    }

    // ── SECTION BUILDERS ──────────────────────────────────────

    private GameObject BuildHeader(Transform parent, GameObject panelRoot)
    {
        GameObject header = CreateGO("Header", parent);
        SetHeight(header, 320f);
        Image headerBg = header.AddComponent<Image>();
        headerBg.color = headerBgColor;

        // Close button (top-right)
        GameObject closeBtn = CreateGO("CloseButton", header.transform);
        SetAnchored(closeBtn, new Vector2(1,1), new Vector2(1,1), new Vector2(-12,-12), 36, 36);
        Button closeBtnComp = closeBtn.AddComponent<Button>();
        Image closeBtnImg = closeBtn.AddComponent<Image>();
        closeBtnImg.color = new Color(1,1,1,0.25f);
        if (closeIconSprite != null) closeBtnImg.sprite = closeIconSprite;
        else
        {
            // Text fallback
            GameObject closeLabel = CreateGO("X", closeBtn.transform);
            TMP_Text ct = closeLabel.AddComponent<TextMeshProUGUI>();
            ct.text = "✕"; ct.fontSize = 16; ct.color = Color.white;
            ct.alignment = TextAlignmentOptions.Center;
            AddFullStretch(closeLabel);
        }

        // Avatar container (center of header)
        GameObject avatarContainer = CreateGO("AvatarContainer", header.transform);
        RectTransform acRT = avatarContainer.GetComponent<RectTransform>();
        acRT.anchorMin = new Vector2(0.5f, 1f);
        acRT.anchorMax = new Vector2(0.5f, 1f);
        acRT.sizeDelta = new Vector2(150, 150);
        acRT.anchoredPosition = new Vector2(0, -115f);

        // Avatar circle
        GameObject avatarGO = CreateGO("AvatarImage", avatarContainer.transform);
        RectTransform avatarRT = avatarGO.GetComponent<RectTransform>();
        avatarRT.anchorMin = Vector2.zero; avatarRT.anchorMax = Vector2.one;
        avatarRT.offsetMin = avatarRT.offsetMax = Vector2.zero;
        Image avatarImg = avatarGO.AddComponent<Image>();
        avatarImg.color = new Color(0.70f, 0.85f, 0.98f, 1f);
        if (defaultAvatarSprite != null) avatarImg.sprite = defaultAvatarSprite;
        // Circular mask
        Mask avatarMask = avatarGO.AddComponent<Mask>();
        avatarMask.showMaskGraphic = true;

        // Camera icon (change avatar) — bottom-right of avatar
        GameObject camBtn = CreateGO("ChangeAvatarButton", avatarContainer.transform);
        SetAnchored(camBtn, new Vector2(1,0), new Vector2(1,0), new Vector2(-2, 2), 26, 26);
        Button camBtnComp = camBtn.AddComponent<Button>();
        Image camBtnImg = camBtn.AddComponent<Image>();
        camBtnImg.color = new Color(0.10f, 0.35f, 0.62f, 1f);
        if (cameraIconSprite != null) camBtnImg.sprite = cameraIconSprite;
        else
        {
            TMP_Text camLabel = CreateGO("CamLabel", camBtn.transform).AddComponent<TextMeshProUGUI>();
            camLabel.text = "📷"; camLabel.fontSize = 12;
            camLabel.alignment = TextAlignmentOptions.Center;
            AddFullStretch(camLabel.gameObject);
        }

        // Display name row (name + edit pencil)
        GameObject nameRow = CreateGO("NameRow", header.transform);
        SetAnchored(nameRow, new Vector2(0,1), new Vector2(1,1), new Vector2(0, -245f), 0, 50);
        HorizontalLayoutGroup nameRowHLG = nameRow.AddComponent<HorizontalLayoutGroup>();
        nameRowHLG.childAlignment = TextAnchor.MiddleCenter;
        nameRowHLG.spacing = 6;
        nameRowHLG.childControlHeight = true;
        nameRowHLG.childForceExpandWidth = false;

        GameObject displayNameGO = CreateGO("DisplayNameText", nameRow.transform);
        TMP_Text displayNameTxt = displayNameGO.AddComponent<TextMeshProUGUI>();
        displayNameTxt.text = "Player Name";
        displayNameTxt.fontSize = 40; displayNameTxt.fontStyle = FontStyles.Bold;
        displayNameTxt.color = Color.white;
        displayNameTxt.alignment = TextAlignmentOptions.Center;
        LayoutElement dnLE = displayNameGO.AddComponent<LayoutElement>();
        dnLE.preferredWidth = 350; dnLE.preferredHeight = 28;

        GameObject editBtnGO = CreateGO("EditNameButton", nameRow.transform);
        Button editBtnComp = editBtnGO.AddComponent<Button>();
        Image editBtnImg = editBtnGO.AddComponent<Image>();
        editBtnImg.color = Color.clear;
        LayoutElement editLE = editBtnGO.AddComponent<LayoutElement>();
        editLE.preferredWidth = 28; editLE.preferredHeight = 28;
        TMP_Text editIcon = CreateGO("EditIcon", editBtnGO.transform).AddComponent<TextMeshProUGUI>();
        editIcon.text = "✏"; editIcon.fontSize = 14; editIcon.color = Color.white;
        editIcon.alignment = TextAlignmentOptions.Center;
        AddFullStretch(editIcon.gameObject);

        // Edit name INPUT (hidden by default)
        GameObject editInputGO = CreateGO("EditNameInput", header.transform);
        SetAnchored(editInputGO, new Vector2(0.1f,1), new Vector2(0.9f,1),
                    new Vector2(0,-148f), 0, 36);
        TMP_InputField inputComp = editInputGO.AddComponent<TMP_InputField>();
        Image inputBg = editInputGO.AddComponent<Image>();
        inputBg.color = Color.white;
        // Input child text area
        GameObject textArea = CreateGO("TextArea", editInputGO.transform);
        AddFullStretch(textArea);
        textArea.AddComponent<RectMask2D>();
        GameObject inputText = CreateGO("Text", textArea.transform);
        TMP_Text inputTxt = inputText.AddComponent<TextMeshProUGUI>();
        inputTxt.fontSize = 14; inputTxt.color = Color.black;
        AddFullStretch(inputText);
        GameObject placeholder = CreateGO("Placeholder", textArea.transform);
        TMP_Text placeholderTxt = placeholder.AddComponent<TextMeshProUGUI>();
        placeholderTxt.text = "Enter name...";
        placeholderTxt.fontSize = 14; placeholderTxt.color = new Color(0.5f,0.5f,0.5f);
        AddFullStretch(placeholder);
        inputComp.textViewport    = textArea.GetComponent<RectTransform>();
        inputComp.textComponent   = inputTxt;
        inputComp.placeholder     = placeholderTxt;
        inputComp.characterLimit  = 20;
        editInputGO.SetActive(false);  // Hidden by default

        // Save / Cancel row (hidden by default)
        GameObject saveRow = CreateGO("SaveCancelRow", header.transform);
        SetAnchored(saveRow, new Vector2(0.1f,1), new Vector2(0.9f,1),
                    new Vector2(0,-190f), 0, 32);
        HorizontalLayoutGroup saveRowHLG = saveRow.AddComponent<HorizontalLayoutGroup>();
        saveRowHLG.spacing = 8; saveRowHLG.childForceExpandWidth = true;
        saveRowHLG.childControlHeight = true;

        GameObject cancelBtnGO = MakeTextButton("CancelNameButton", saveRow.transform,
                                                "Cancel", Color.grey, Color.white, 13);
        GameObject saveBtnGO   = MakeTextButton("SaveNameButton", saveRow.transform,
                                                "Save ✓", saveBtnColor, Color.white, 13);
        saveRow.SetActive(false);  // Hidden by default

        // Email text
        GameObject emailGO = CreateGO("EmailText", header.transform);
        SetAnchored(emailGO, new Vector2(0,1), new Vector2(1,1), new Vector2(0,-285f), 0, 35);
        TMP_Text emailTxt = emailGO.AddComponent<TextMeshProUGUI>();
        emailTxt.text = "player@email.com";
        emailTxt.fontSize = 30; emailTxt.color = new Color(1,1,1,0.75f);
        emailTxt.alignment = TextAlignmentOptions.Center;

        return header;
    }

    private GameObject BuildStatsSection(Transform parent)
    {
        GameObject section = CreateGO("StatsSection", parent);
        SetHeight(section, 320f);
        Image sectionBg = section.AddComponent<Image>();
        sectionBg.color = Color.white;

        // 2x2 Grid
        GameObject grid = CreateGO("StatsGrid", section.transform);
        RectTransform gridRT = grid.GetComponent<RectTransform>();
        gridRT.anchorMin = Vector2.zero; gridRT.anchorMax = Vector2.one;
        gridRT.offsetMin = new Vector2(12, 10);
        gridRT.offsetMax = new Vector2(-12, -10);
        GridLayoutGroup glg = grid.AddComponent<GridLayoutGroup>();
        glg.cellSize        = new Vector2(0, 120);   // width auto via constraint
        glg.spacing         = new Vector2(15, 15);
        glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;
        glg.childAlignment  = TextAnchor.UpperCenter;

        // ContentSizeFitter so grid fills
        ContentSizeFitter gridCSF = grid.AddComponent<ContentSizeFitter>();
        gridCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        gridCSF.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // 4 stat cards
        MakeStatCard("LevelText",           grid.transform, "Level",          "1",      statCardColor);
        MakeStatCard("TotalScoreText",       grid.transform, "Total Score",    "0",      statCardColor);
        MakeStatCard("CoinsText",            grid.transform, "Coins",          "0",      statCardColor);
        MakeStatCard("LevelsCompletedText",  grid.transform, "Levels Done",    "0",      statCardColor);

        return section;
    }

    private GameObject BuildPetsSection(Transform parent)
    {
        GameObject section = CreateGO("PetsSection", parent);
        SetHeight(section, 180f);
        Image sectionBg = section.AddComponent<Image>();
        sectionBg.color = new Color(0.97f, 0.97f, 0.99f, 1f);

        // Label
        GameObject label = CreateGO("PetsLabel", section.transform);
        SetAnchored(label, new Vector2(0,1), new Vector2(1,1),
                    new Vector2(14,-2), 0, 18);
        TMP_Text labelTxt = label.AddComponent<TextMeshProUGUI>();
        labelTxt.text = "🐾 Pets Collected";
        labelTxt.fontSize = 30; labelTxt.color = new Color(0.4f,0.4f,0.5f);

        // Pets count badge
        GameObject countGO = CreateGO("PetsCountText", section.transform);
        SetAnchored(countGO, new Vector2(0,1), new Vector2(0,1),
                    new Vector2(132,-2), 30, 18);
        TMP_Text countTxt = countGO.AddComponent<TextMeshProUGUI>();
        countTxt.text = "0"; countTxt.fontSize = 30;
        countTxt.color = new Color(0.2f,0.5f,0.85f);
        countTxt.fontStyle = FontStyles.Bold;

        // Pets HorizontalLayoutGroup container
        GameObject petsContainer = CreateGO("PetsContainer", section.transform);
        SetAnchored(petsContainer, new Vector2(0,0), new Vector2(1,1),
                    new Vector2(12,8), 0, -28);
        HorizontalLayoutGroup petsHLG = petsContainer.AddComponent<HorizontalLayoutGroup>();
        petsHLG.spacing = 15; petsHLG.childAlignment = TextAnchor.MiddleLeft;
        petsHLG.childControlHeight = true; petsHLG.childForceExpandWidth = false;

        // 5 placeholder pet slots (ProfilePanel will replace these at runtime)
        for (int i = 0; i < 5; i++)
        {
            GameObject slot = CreateGO($"PetSlot_{i}", petsContainer.transform);
            Image slotImg = slot.AddComponent<Image>();
            slotImg.color = (i < 1)
                ? new Color(0.85f, 0.93f, 1f, 1f)   // first slot: unlocked
                : new Color(0.90f, 0.90f, 0.93f, 1f); // rest: locked
            LayoutElement slotLE = slot.AddComponent<LayoutElement>();
            slotLE.preferredWidth = 80; slotLE.preferredHeight = 80;

            TMP_Text slotTxt = CreateGO("SlotIcon", slot.transform).AddComponent<TextMeshProUGUI>();
            slotTxt.text = (i < 1) ? "🐉" : "🔒";
            slotTxt.fontSize = 32;
            slotTxt.alignment = TextAlignmentOptions.Center;
            AddFullStretch(slotTxt.gameObject);
        }

        return section;
    }

    private GameObject BuildButtonsSection(Transform parent, GameObject panelRoot)
    {
        GameObject section = CreateGO("ButtonsSection", parent);
        SetHeight(section, 190f);
        Image sectionBg = section.AddComponent<Image>();
        sectionBg.color = Color.white;

        VerticalLayoutGroup sectionVLG = section.AddComponent<VerticalLayoutGroup>();
        sectionVLG.padding = new RectOffset(16, 16, 10, 10);
        sectionVLG.spacing = 8;
        sectionVLG.childControlWidth = true; sectionVLG.childForceExpandWidth = true;
        sectionVLG.childControlHeight = false; sectionVLG.childForceExpandHeight = false;

        // Error text (hidden by default)
        GameObject errorGO = CreateGO("ErrorText", section.transform);
        TMP_Text errorTxt = errorGO.AddComponent<TextMeshProUGUI>();
        errorTxt.text = "";
        errorTxt.fontSize = 11; errorTxt.color = new Color(0.85f,0.2f,0.2f);
        errorTxt.alignment = TextAlignmentOptions.Center;
        LayoutElement errorLE = errorGO.AddComponent<LayoutElement>();
        errorLE.preferredHeight = 16;
        errorGO.SetActive(false);

        // Logout button (shown for registered players)
        GameObject logoutBtnGO = MakeTextButton("LogoutButton", section.transform,
                                                "Logout", logoutBtnColor, Color.white, 24);
        LayoutElement logoutLE = logoutBtnGO.AddComponent<LayoutElement>();
        logoutLE.preferredHeight = 70;

        // Register button (shown for guest players — hidden by default)
        GameObject registerBtnGO = MakeTextButton("RegisterButton", section.transform,
                                                  "Save Progress — Create Account",
                                                  registerBtnColor, Color.white, 22);
        LayoutElement registerLE = registerBtnGO.AddComponent<LayoutElement>();
        registerLE.preferredHeight = 70;
        registerBtnGO.SetActive(false);  // ProfilePanel will toggle based on IsGuest

        // Loading overlay (full card — hidden by default)
        GameObject loadingOverlay = CreateGO("LoadingOverlay", panelRoot.transform);
        SetAnchored(loadingOverlay, Vector2.zero, Vector2.one,
                    new Vector2(50, 80), new Vector2(-50, -80));
        Image loadingBg = loadingOverlay.AddComponent<Image>();
        loadingBg.color = new Color(0, 0, 0, 0.45f);
        GameObject loadingLabel = CreateGO("LoadingLabel", loadingOverlay.transform);
        AddFullStretch(loadingLabel);
        TMP_Text loadingTxt = loadingLabel.AddComponent<TextMeshProUGUI>();
        loadingTxt.text = "⏳ Saving...";
        loadingTxt.fontSize = 16; loadingTxt.color = Color.white;
        loadingTxt.alignment = TextAlignmentOptions.Center;
        loadingOverlay.SetActive(false);

        return section;
    }

    // ── WIRE PROFILEPANEL.CS FIELDS ──────────────────────────

    private void WireProfilePanel(ProfilePanel pp, GameObject root,
        GameObject header, GameObject statsSection,
        GameObject petsSection, GameObject buttonsSection)
    {
        Transform cardT = root.transform.Find("Card");

        pp.panelRoot           = root;

        // Header refs
        Transform headerT = cardT.Find("Header");
        pp.avatarImage         = headerT.Find("AvatarContainer/AvatarImage")?.GetComponent<Image>();
        pp.changeAvatarButton  = headerT.Find("AvatarContainer/ChangeAvatarButton")?.GetComponent<Button>();
        pp.displayNameText     = headerT.Find("NameRow/DisplayNameText")?.GetComponent<TMP_Text>();
        pp.editNameInput       = headerT.Find("EditNameInput")?.GetComponent<TMP_InputField>();
        pp.editNameButton      = headerT.Find("NameRow/EditNameButton")?.GetComponent<Button>();
        pp.saveNameButton      = headerT.Find("SaveCancelRow/SaveNameButton")?.GetComponent<Button>();
        pp.cancelNameButton    = headerT.Find("SaveCancelRow/CancelNameButton")?.GetComponent<Button>();
        pp.emailText           = headerT.Find("EmailText")?.GetComponent<TMP_Text>();

        // Stats refs
        Transform gridT = cardT.Find("StatsSection/StatsGrid");
        pp.levelText            = gridT?.Find("LevelText/ValueText")?.GetComponent<TMP_Text>();
        pp.totalScoreText       = gridT?.Find("TotalScoreText/ValueText")?.GetComponent<TMP_Text>();
        pp.coinsText            = gridT?.Find("CoinsText/ValueText")?.GetComponent<TMP_Text>();
        pp.levelsCompletedText  = gridT?.Find("LevelsCompletedText/ValueText")?.GetComponent<TMP_Text>();

        // Pets refs
        Transform petsT = cardT.Find("PetsSection");
        pp.petsCountText   = petsT?.Find("PetsCountText")?.GetComponent<TMP_Text>();
        pp.petsContainer   = petsT?.Find("PetsContainer");

        // Button refs
        Transform btnT = cardT.Find("ButtonsSection");
        pp.logoutButton    = btnT?.Find("LogoutButton")?.GetComponent<Button>();
        pp.registerButton  = btnT?.Find("RegisterButton")?.GetComponent<Button>();
        pp.errorText       = btnT?.Find("ErrorText")?.GetComponent<TMP_Text>();
        pp.loadingOverlay  = root.transform.Find("Card/LoadingOverlay")?.gameObject
                             ?? root.transform.Find("LoadingOverlay")?.gameObject;

        pp.defaultAvatarSprite = defaultAvatarSprite;

        Debug.Log("[ProfilePanelBuilder] ProfilePanel.cs wired successfully.");
    }

    // ── HELPERS ───────────────────────────────────────────────

    private GameObject CreateGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        // RectTransform MUST be added before parenting to Canvas
        // otherwise Unity gives it a regular Transform (no UI layout)
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        return go;
    }

    private RectTransform AddFullStretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private void SetHeight(GameObject go, float height)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        LayoutElement le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight       = height;
    }

    /// anchorPivot = anchor + pivot combined (for corner-anchored elements)
    private void SetAnchored(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
                              Vector2 anchoredPos, float width, float height)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin    = anchorMin;
        rt.anchorMax    = anchorMax;
        rt.sizeDelta    = new Vector2(width, height);
        rt.anchoredPosition = anchoredPos;
    }

    private void AddRoundedCorners(GameObject go)
    {
        // Unity default Image with no sprite = square corners.
        // For actual rounded corners assign a rounded-rect sprite in Inspector later.
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.type = Image.Type.Sliced;
    }

    private void MakeStatCard(string name, Transform parent,
                               string label, string value, Color bg)
    {
        GameObject card = CreateGO(name, parent);
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = bg;
        LayoutElement le = card.AddComponent<LayoutElement>();
        le.preferredHeight = 120;

        VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.spacing = 2;
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;

        // Label
        TMP_Text labelTxt = CreateGO("LabelText", card.transform).AddComponent<TextMeshProUGUI>();
        labelTxt.text = label; labelTxt.fontSize = 10;
        labelTxt.color = new Color(0.45f, 0.45f, 0.55f);
        labelTxt.alignment = TextAlignmentOptions.Center;

        // Value (this is what ProfilePanel.cs sets at runtime)
        TMP_Text valueTxt = CreateGO("ValueText", card.transform).AddComponent<TextMeshProUGUI>();
        valueTxt.text = value; valueTxt.fontSize = 42;
        valueTxt.fontStyle = FontStyles.Bold;
        valueTxt.color = new Color(0.15f, 0.15f, 0.25f);
        valueTxt.alignment = TextAlignmentOptions.Center;
    }

    private GameObject MakeTextButton(string name, Transform parent,
                                       string label, Color bg, Color textColor, float fontSize)
    {
        GameObject btn = CreateGO(name, parent);
        Image btnImg = btn.AddComponent<Image>();
        btnImg.color = bg;
        Button btnComp = btn.AddComponent<Button>();

        // Hover tint
        ColorBlock cb = btnComp.colors;
        cb.highlightedColor = new Color(bg.r * 0.9f, bg.g * 0.9f, bg.b * 0.9f);
        cb.pressedColor     = new Color(bg.r * 0.75f, bg.g * 0.75f, bg.b * 0.75f);
        btnComp.colors = cb;

        TMP_Text labelTxt = CreateGO("Label", btn.transform).AddComponent<TextMeshProUGUI>();
        labelTxt.text = label; labelTxt.fontSize = fontSize;
        labelTxt.color = textColor; labelTxt.fontStyle = FontStyles.Bold;
        labelTxt.alignment = TextAlignmentOptions.Center;
        AddFullStretch(labelTxt.gameObject);

        return btn;
    }

    private void SetAnchored(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
                              Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin  = anchorMin;  rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;  rt.offsetMax  = offsetMax;
    }
}