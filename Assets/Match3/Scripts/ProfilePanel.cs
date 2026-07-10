using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Firebase;

public class ProfilePanel : MonoBehaviour
{
    [Header("Panel Root")]
    public GameObject panelRoot;

    [Header("Avatar")]
    public Image   avatarImage;
    public Sprite  defaultAvatarSprite;
    public Button  changeAvatarButton;

    [Header("Name")]
    public TMP_Text       displayNameText;
    public TMP_Text       emailText;
    public TMP_InputField editNameInput;
    public Button         editNameButton;
    public Button         saveNameButton;
    public Button         cancelNameButton;

    [Header("Stats")]
    public TMP_Text levelText;
    public TMP_Text totalScoreText;
    public TMP_Text coinsText;
    public TMP_Text levelsCompletedText;

    [Header("Pets")]
    public TMP_Text  petsCountText;
    public Transform petsContainer;

    [Header("Buttons")]
    public Button     logoutButton;
    public Button     registerButton;
    public TMP_Text   errorText;
    public GameObject loadingOverlay;

    [HideInInspector] public Button closeButton;

    // Guest popup refs
    private GameObject     _guestRegisterPopup;
    private TMP_InputField _regUsernameInput;
    private TMP_InputField _regEmailInput;
    private TMP_InputField _regPasswordInput;
    private TMP_InputField _regConfirmInput;
    private TMP_Text       _regErrorText;

    private bool      _isEditingName     = false;
    private bool      _isSavingName      = false;
    private Coroutine _hideErrorCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────

    private void Start()
    {
        closeButton?.onClick.AddListener(Hide);
        editNameButton?.onClick.AddListener(StartEditName);
        saveNameButton?.onClick.AddListener(ConfirmEditName);
        cancelNameButton?.onClick.AddListener(CancelEditName);
        changeAvatarButton?.onClick.AddListener(OnChangeAvatarClicked);
        logoutButton?.onClick.AddListener(OnLogoutClicked);
        registerButton?.onClick.AddListener(OnRegisterClicked);

        BuildGuestRegisterPopup();

        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.OnProfileLoaded.AddListener(RefreshUI);
            ProfileManager.Instance.OnProfileSaved.AddListener(OnSaveComplete);
            ProfileManager.Instance.OnProfileError.AddListener(ShowError);
            ProfileManager.Instance.OnAvatarLoaded.AddListener(SetAvatarSprite);
        }

        if (AuthManager.Instance != null)
            AuthManager.Instance.OnRegisterSuccess.AddListener(OnGuestUpgradeSuccess);

        SetEditMode(false);
        HideError();
        SetLoading(false);  // Always start with loading OFF

        if (ProfileManager.Instance != null && ProfileManager.Instance.IsProfileLoaded)
            RefreshUI();
    }

    private void OnDestroy()
    {
        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.OnProfileLoaded.RemoveListener(RefreshUI);
            ProfileManager.Instance.OnProfileSaved.RemoveListener(OnSaveComplete);
            ProfileManager.Instance.OnProfileError.RemoveListener(ShowError);
            ProfileManager.Instance.OnAvatarLoaded.RemoveListener(SetAvatarSprite);
        }
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnRegisterSuccess.RemoveListener(OnGuestUpgradeSuccess);
    }

    // ── Show / Hide ───────────────────────────────────────────

    public void Show()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        SetLoading(false);   // ALWAYS off when opening
        HideError();
        if (_guestRegisterPopup != null) _guestRegisterPopup.SetActive(false);

        // Refresh immediately, then again after 1 second
        // in case profile was still loading from Firebase
        RefreshUI();
        StartCoroutine(DelayedRefresh());
    }

    private IEnumerator DelayedRefresh()
    {
        yield return new WaitForSeconds(1f);
        RefreshUI();
    }

    public void Hide()
    {
        if (_isEditingName) CancelEditName();
        if (_guestRegisterPopup != null) _guestRegisterPopup.SetActive(false);
        SetLoading(false);   // Always turn off loading when hiding
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── Refresh UI ────────────────────────────────────────────

    public void RefreshUI()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;

        PlayerProfile p = ProfileManager.Instance?.CurrentProfile;
        bool isGuest = AuthManager.IsGuest;

        // ── Display Name ──
        if (displayNameText != null)
        {
            if (isGuest)
            {
                displayNameText.text = "Guest Player";
            }
            else if (p != null && !string.IsNullOrEmpty(p.displayName))
            {
                displayNameText.text = p.displayName;
            }
            else
            {
                // Fallback: use Firebase Auth displayName
                string authName = AuthManager.CurrentUser?.DisplayName;
                displayNameText.text = !string.IsNullOrEmpty(authName) ? authName : "Player";
            }
        }

        // ── Email ──
        if (emailText != null)
        {
            if (isGuest)
                emailText.text = "Playing as Guest";
            else
            {
                string mail = p?.email ?? AuthManager.CurrentUser?.Email ?? "";
                emailText.text = mail;
            }
        }

        // ── Avatar ──
        if (avatarImage != null && (p == null || string.IsNullOrEmpty(p.avatarUrl)))
            avatarImage.sprite = defaultAvatarSprite;

        // ── Stats (safe if profile null) ──
        if (levelText           != null) levelText.text           = (p?.level ?? 1).ToString();
        if (totalScoreText      != null) totalScoreText.text      = (p?.totalScore ?? 0).ToString("N0");
        if (coinsText           != null) coinsText.text           = (p?.coins ?? 0).ToString("N0");
        if (levelsCompletedText != null) levelsCompletedText.text = (p?.levelsCompleted ?? 0).ToString();
        if (petsCountText       != null) petsCountText.text       = (p?.pets?.Count ?? 0).ToString();

        // ── Button visibility ──
        if (logoutButton   != null) logoutButton.gameObject.SetActive(!isGuest);
        if (registerButton != null) registerButton.gameObject.SetActive(isGuest);

        if (changeAvatarButton != null) changeAvatarButton.interactable = !isGuest;
        if (editNameButton     != null) editNameButton.gameObject.SetActive(!isGuest);

        if (p != null) RefreshPetIcons(p);
    }

    private void RefreshPetIcons(PlayerProfile p)
    {
        if (petsContainer == null) return;
        foreach (Transform child in petsContainer) Destroy(child.gameObject);

        for (int i = 0; i < 5; i++)
        {
            GameObject slot = new GameObject($"Pet_{i}");
            slot.AddComponent<RectTransform>();
            slot.transform.SetParent(petsContainer, false);
            Image slotImg = slot.AddComponent<Image>();
            LayoutElement le = slot.AddComponent<LayoutElement>();
            le.preferredWidth = 40; le.preferredHeight = 40;

            TMP_Text icon = new GameObject("Icon").AddComponent<TextMeshProUGUI>();
            icon.transform.SetParent(slot.transform, false);
            RectTransform iconRT = icon.GetComponent<RectTransform>();
            iconRT.anchorMin = Vector2.zero; iconRT.anchorMax = Vector2.one;
            iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
            icon.alignment = TextAlignmentOptions.Center;

            if (i < p.pets.Count)
            {
                slotImg.color = new Color(0.85f, 0.93f, 1f);
                icon.fontSize = 20; icon.text = "★";
            }
            else
            {
                slotImg.color = new Color(0.88f, 0.88f, 0.90f);
                icon.fontSize = 16; icon.text = "○";
                icon.color = new Color(0.6f, 0.6f, 0.65f);
            }
        }
    }

    // ── Avatar ────────────────────────────────────────────────

    public void SetAvatarSprite(Sprite sprite)
    {
        if (avatarImage != null && sprite != null)
            avatarImage.sprite = sprite;
    }

    private void OnChangeAvatarClicked()
    {
        Debug.Log("[ProfilePanel] Change avatar — integrate NativeGallery here.");
    }

    // ── Display Name Edit ─────────────────────────────────────

    private void StartEditName()
    {
        if (_isSavingName) return;
        _isEditingName = true;
        SetEditMode(true);
        if (editNameInput != null)
        {
            string current = ProfileManager.Instance?.CurrentProfile?.displayName ?? "";
            editNameInput.text = current;
            editNameInput.Select();
        }
    }

    private void ConfirmEditName()
    {
        if (editNameInput == null || _isSavingName) return;
        string newName = editNameInput.text.Trim();

        if (string.IsNullOrEmpty(newName)) { ShowError("Name cannot be empty."); return; }
        if (newName.Length < 3)            { ShowError("Minimum 3 characters."); return; }
        if (newName.Length > 20)           { ShowError("Maximum 20 characters."); return; }

        _isSavingName = true;

        // Update locally first (instant feedback)
        if (displayNameText != null) displayNameText.text = newName;
        SetEditMode(false);
        _isEditingName = false;

        // Save in background — NO loading overlay
        ProfileManager.Instance?.UpdateDisplayName(newName);

        // Reset flag after short delay
        StartCoroutine(ResetSavingFlag());
    }

    private IEnumerator ResetSavingFlag()
    {
        yield return new WaitForSeconds(1f);
        _isSavingName = false;
    }

    private void CancelEditName()
    {
        SetEditMode(false);
        _isEditingName = false;
        _isSavingName  = false;
        HideError();
    }

    private void SetEditMode(bool editing)
    {
        if (displayNameText  != null) displayNameText.gameObject.SetActive(!editing);
        if (editNameInput    != null) editNameInput.gameObject.SetActive(editing);
        if (editNameButton   != null) editNameButton.gameObject.SetActive(!editing && !AuthManager.IsGuest);
        if (saveNameButton   != null) saveNameButton.gameObject.SetActive(editing);
        if (cancelNameButton != null) cancelNameButton.gameObject.SetActive(editing);
    }

    // ── Logout ────────────────────────────────────────────────

    private void OnLogoutClicked()
    {
        // Logout directly — no save, no delay, no loading screen
        // Profile auto-saves in background when levels complete anyway
        Debug.Log("[ProfilePanel] Logging out...");
        Hide();
        AuthManager.Instance?.Logout();
    }

    // ── GUEST REGISTER FLOW ───────────────────────────────────

    private void OnRegisterClicked()
    {
        if (_guestRegisterPopup != null)
        {
            ClearRegisterForm();
            _guestRegisterPopup.SetActive(true);
        }
    }

    private void OnRegisterSubmit()
    {
        string username = _regUsernameInput?.text.Trim() ?? "";
        string email    = _regEmailInput?.text.Trim()    ?? "";
        string password = _regPasswordInput?.text        ?? "";
        string confirm  = _regConfirmInput?.text         ?? "";

        if (username.Length < 3)
        { ShowRegError("Username minimum 3 characters."); return; }
        if (!email.Contains("@") || !email.Contains("."))
        { ShowRegError("Enter a valid email address."); return; }
        if (password.Length < 6)
        { ShowRegError("Password minimum 6 characters."); return; }
        if (password != confirm)
        { ShowRegError("Passwords do not match."); return; }

        ShowRegError("");
        if (_regErrorText != null) _regErrorText.gameObject.SetActive(false);

        // AuthManager handles token refresh + link internally
        AuthManager.Instance?.UpgradeGuestAccount(username, email, password);

        if (AuthManager.Instance != null)
            AuthManager.Instance.OnAuthError.AddListener(OnUpgradeError);
    }

    private void OnGuestUpgradeSuccess()
    {
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnAuthError.RemoveListener(OnUpgradeError);

        // 1. Close the register popup
        if (_guestRegisterPopup != null) _guestRegisterPopup.SetActive(false);

        // 2. Update in-memory profile IMMEDIATELY (don't wait for Firestore reload)
        //    so the name/email shows instantly in the UI
        string newUsername = _regUsernameInput?.text.Trim() ?? "";
        string newEmail    = _regEmailInput?.text.Trim()    ?? "";

        if (ProfileManager.Instance?.CurrentProfile != null && !string.IsNullOrEmpty(newUsername))
        {
            ProfileManager.Instance.CurrentProfile.displayName = newUsername;
            ProfileManager.Instance.CurrentProfile.email       = newEmail;
        }

        // 3. Reload from Firestore in background to confirm sync
        if (AuthManager.CurrentUser != null)
            ProfileManager.Instance?.LoadProfile(AuthManager.CurrentUser.UserId);

        // 4. Refresh UI immediately with new in-memory data
        RefreshUI();

        // 5. Hide the profile panel — user can reopen it to see updated info
        //    (panel was already open when user clicked "Save Progress")
        StartCoroutine(HidePanelAfterDelay(0.5f));
    }

    private System.Collections.IEnumerator HidePanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Hide();
    }

    private void OnUpgradeError(string message)
    {
        if (AuthManager.Instance != null)
            AuthManager.Instance.OnAuthError.RemoveListener(OnUpgradeError);
        ShowRegError(message);
    }

    private void ClearRegisterForm()
    {
        if (_regUsernameInput != null) _regUsernameInput.text = "";
        if (_regEmailInput    != null) _regEmailInput.text    = "";
        if (_regPasswordInput != null) _regPasswordInput.text = "";
        if (_regConfirmInput  != null) _regConfirmInput.text  = "";
        if (_regErrorText != null)
        {
            _regErrorText.text = "";
            _regErrorText.gameObject.SetActive(false);
        }
    }

    private void ShowRegError(string msg)
    {
        if (_regErrorText == null) return;
        _regErrorText.text = msg;
        _regErrorText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }

    // ── BUILD GUEST REGISTER POPUP ────────────────────────────

    private void BuildGuestRegisterPopup()
    {
        if (panelRoot == null) return;

        _guestRegisterPopup = MakeGO("GuestRegisterPopup", panelRoot.transform);
        RectTransform rt = _guestRegisterPopup.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        _guestRegisterPopup.AddComponent<Image>().color = new Color(0, 0, 0, 0.72f);

        GameObject card = MakeGO("PopupCard", _guestRegisterPopup.transform);
        RectTransform cardRT = card.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.14f, 0.16f);
        cardRT.anchorMax = new Vector2(0.86f, 0.84f);
        cardRT.offsetMin = cardRT.offsetMax = Vector2.zero;
        card.AddComponent<Image>().color = Color.white;

        VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(30, 30, 30, 30);
        vlg.spacing = 30;
        vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
        vlg.childControlHeight = false; vlg.childForceExpandHeight = false;

        AddLabel(card.transform, "Create Account", 40, FontStyles.Bold, Color.black, TextAlignmentOptions.Center, 40);
        AddLabel(card.transform, "Your coins, pets and level progress will be saved to your new account.",
                 30, FontStyles.Normal, new Color(0.3f, 0.55f, 0.35f), TextAlignmentOptions.Center, 30 );

        _regUsernameInput = AddInput(card.transform, "Username (min 3 chars)", false, 70);
        _regEmailInput    = AddInput(card.transform, "Email address",          false, 70);
        _regPasswordInput = AddInput(card.transform, "Password (min 6 chars)", true,  70);
        _regConfirmInput  = AddInput(card.transform, "Confirm password",       true,  70);

        GameObject errGO = MakeGO("RegError", card.transform);
        _regErrorText = errGO.AddComponent<TextMeshProUGUI>();
        _regErrorText.fontSize = 30;
        _regErrorText.color = new Color(0.85f, 0.2f, 0.2f);
        _regErrorText.alignment = TextAlignmentOptions.Center;
        errGO.AddComponent<LayoutElement>().preferredHeight = 24;
        errGO.SetActive(false);

        GameObject btnRow = MakeGO("BtnRow", card.transform);
        HorizontalLayoutGroup bHLG = btnRow.AddComponent<HorizontalLayoutGroup>();
        bHLG.spacing = 14; bHLG.childForceExpandWidth = true; bHLG.childControlHeight = true;
        btnRow.AddComponent<LayoutElement>().preferredHeight = 60;

        Button cancelBtn = AddBtn(btnRow.transform, "Cancel", new Color(0.78f,0.78f,0.80f), Color.white, 30);

        Button submitBtn = AddBtn(btnRow.transform, "Create Account", new Color(0.18f,0.74f,0.47f), Color.white, 30);
        
        cancelBtn.onClick.AddListener(() => _guestRegisterPopup.SetActive(false));
        submitBtn.onClick.AddListener(OnRegisterSubmit);
        _guestRegisterPopup.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────

    private GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        return go;
    }

    private void AddLabel(Transform parent, string text, float size,
                          FontStyles style, Color color,
                          TextAlignmentOptions align, float height)
    {
        GameObject go = MakeGO("Label", parent);
        TMP_Text t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = style;
        t.color = color; t.alignment = align;
        go.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private TMP_InputField AddInput(Transform parent, string placeholder,
                                    bool isPassword, float height)
    {
        GameObject go = MakeGO("Input", parent);
        go.AddComponent<Image>().color = new Color(0.95f, 0.96f, 0.98f);
        TMP_InputField field = go.AddComponent<TMP_InputField>();
        go.AddComponent<LayoutElement>().preferredHeight = height;

        GameObject area = MakeGO("Area", go.transform);
        RectTransform aRT = area.GetComponent<RectTransform>();
        aRT.anchorMin = Vector2.zero; aRT.anchorMax = Vector2.one;
        aRT.offsetMin = new Vector2(14, 8); aRT.offsetMax = new Vector2(-14, -8);
        area.AddComponent<RectMask2D>();

        TMP_Text txt = MakeGO("Txt", area.transform).AddComponent<TextMeshProUGUI>();
        txt.fontSize = 30; txt.color = new Color(0.1f, 0.1f, 0.1f);
        Stretch(txt.gameObject);

        TMP_Text ph = MakeGO("Ph", area.transform).AddComponent<TextMeshProUGUI>();
        ph.text = placeholder; ph.fontSize = 30;
        ph.color = new Color(0.65f, 0.65f, 0.65f); ph.fontStyle = FontStyles.Italic;
        Stretch(ph.gameObject);

        field.textViewport = aRT; field.textComponent = txt; field.placeholder = ph;
        field.characterLimit = 40;
        if (isPassword) field.contentType = TMP_InputField.ContentType.Password;
        return field;
    }

    private Button AddBtn(Transform parent, string label,
                          Color bg, Color textColor, float fontSize)
    {
        GameObject go = MakeGO(label, parent);
        go.AddComponent<Image>().color = bg;
        Button btn = go.AddComponent<Button>();
        TMP_Text txt = MakeGO("L", go.transform).AddComponent<TextMeshProUGUI>();
        txt.text = label; txt.fontSize = fontSize;
        txt.fontStyle = FontStyles.Bold; txt.color = textColor;
        txt.alignment = TextAlignmentOptions.Center;
        Stretch(txt.gameObject);
        return btn;
    }

    private void Stretch(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── Callbacks ─────────────────────────────────────────────

    private void OnSaveComplete()
    {
        // Background save complete — no loading overlay needed
        Debug.Log("[ProfilePanel] Profile saved.");
    }

    private void ShowError(string message)
    {
        if (errorText == null) return;
        errorText.text = message;
        errorText.gameObject.SetActive(true);
        if (_hideErrorCoroutine != null) StopCoroutine(_hideErrorCoroutine);
        _hideErrorCoroutine = StartCoroutine(HideErrorAfterDelay(3f));
    }

    private void HideError()
    {
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    private IEnumerator HideErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideError();
    }

    private void SetLoading(bool show)
    {
        if (loadingOverlay != null) loadingOverlay.SetActive(show);
    }
}