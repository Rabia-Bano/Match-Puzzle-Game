using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Firebase;
using Match3;

/// ---------------------------------------------------------------------
/// UPDATED:
///  1) GuestRegisterPopup is no longer built at runtime with code
///     (BuildGuestRegisterPopup() + the MakeGO/AddLabel/AddInput/AddBtn/
///     Stretch helpers have all been removed). It now follows the exact
///     same pattern as avatarPickerPopup: build the whole popup by hand
///     in the Unity Editor as a child of panelRoot, and assign every
///     piece to the [SerializeField] references below. See the setup
///     guide in chat for the exact hierarchy to build.
///  2) NEW — Guest Email Verification panel: after UpgradeGuestAccount()
///     succeeds, AuthManager no longer fires OnRegisterSuccess right
///     away — it fires OnVerificationRequired(email) instead (same as
///     the main Register() flow). This panel shows that "check your
///     inbox" step, with Resend + Continue + Later buttons, also built
///     by hand in the Editor.
///  3) NEW — SetLoading(true) now starts a watchdog coroutine (same
///     fix as LoginUIController) so the loading overlay / disabled
///     buttons can never stay stuck if a callback never arrives.
/// ---------------------------------------------------------------------
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

    // Avatar picker popup — built manually in the Unity Editor, code only wires it.
    [Header("Avatar Picker (built in Editor)")]
    [SerializeField] private GameObject avatarPickerPopup;     // the whole popup root GameObject
    [SerializeField] private Transform  avatarGridContainer;   // empty GameObject with GridLayoutGroup, 3 columns
    [SerializeField] private GameObject avatarSlotPrefab;      // a Button+Image prefab, one per avatar

    // -----------------------------------------------------------------
    // Guest Register popup — NOW built manually in the Unity Editor,
    // exactly like avatarPickerPopup above. Code only wires these refs.
    // -----------------------------------------------------------------
    [Header("Guest Register Popup (built in Editor)")]
    [SerializeField] private GameObject     guestRegisterPopup;   // the whole popup root GameObject
    [SerializeField] private TMP_InputField regUsernameInput;
    [SerializeField] private TMP_InputField regEmailInput;
    [SerializeField] private TMP_InputField regPasswordInput;
    [SerializeField] private TMP_InputField regConfirmInput;
    [SerializeField] private TMP_Text       regErrorText;
    [SerializeField] private Button         regCancelButton;
    [SerializeField] private Button         regSubmitButton;      // "Create Account"

    // -----------------------------------------------------------------
    // NEW — Guest email verification popup, also built in the Editor.
    // Shown after regSubmitButton succeeds (AuthManager.OnVerificationRequired).
    // -----------------------------------------------------------------
    [Header("Guest Verification Popup (built in Editor, NEW)")]
    [SerializeField] private GameObject guestVerificationPanel;
    [SerializeField] private TMP_Text   guestVerificationEmailText;
    [SerializeField] private Button     guestVerificationContinueButton;  // "I've verified, Continue"
    [SerializeField] private Button     guestVerificationResendButton;    // "Resend Email"
    [SerializeField] private TMP_Text   guestVerificationResendConfirmText;
    [SerializeField] private Button     guestVerificationLaterButton;     // "Later" — keep playing, verify later
    [Tooltip("NEW — BUG FIX: verification errors ('not verified yet', resend failed, etc.) used to be routed to regErrorText, which lives inside GuestRegisterPopup — a GameObject that is already INACTIVE while this panel is showing, so the message was set correctly in code but never actually visible on screen. This is a dedicated error text living inside VerificationPanel itself so it is always visible when needed. Add a TMP_Text here (red, initially inactive) as a child of VerificationPanel.")]
    [SerializeField] private TMP_Text   guestVerificationErrorText;

    [Header("Loading Watchdog (NEW)")]
    [Tooltip("If no AuthManager response arrives within this many seconds, the UI unlocks itself with a timeout error instead of staying stuck.")]
    [SerializeField] private float loadingTimeoutSeconds = 15f;

    private bool      _isEditingName     = false;
    private bool      _isSavingName      = false;
    private Coroutine _hideErrorCoroutine;
    private Coroutine _loadingWatchdog;
    private bool      _lostFocusWhileVerifying = false;   // NEW — see OnApplicationFocus below

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

        // Guest register popup buttons (Editor-built — code only wires clicks)
        regCancelButton?.onClick.AddListener(OnRegisterCancelClicked);
        regSubmitButton?.onClick.AddListener(OnRegisterSubmit);

        // Guest verification popup buttons (NEW)
        guestVerificationContinueButton?.onClick.AddListener(OnGuestVerificationContinueClicked);
        guestVerificationResendButton?.onClick.AddListener(OnGuestVerificationResendClicked);
        guestVerificationLaterButton?.onClick.AddListener(OnGuestVerificationLaterClicked);

        PopulateAvatarGrid();
        if (avatarPickerPopup != null) avatarPickerPopup.SetActive(false);
        if (guestRegisterPopup != null) guestRegisterPopup.SetActive(false);
        if (guestVerificationPanel != null) guestVerificationPanel.SetActive(false);
        if (guestVerificationResendConfirmText != null) guestVerificationResendConfirmText.gameObject.SetActive(false);

        if (ProfileManager.Instance != null)
        {
            ProfileManager.Instance.OnProfileLoaded.AddListener(RefreshUI);
            ProfileManager.Instance.OnProfileSaved.AddListener(OnSaveComplete);
            ProfileManager.Instance.OnProfileError.AddListener(ShowError);
            ProfileManager.Instance.OnAvatarLoaded.AddListener(SetAvatarSprite);
        }

        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnRegisterSuccess.AddListener(OnGuestUpgradeSuccess);
            AuthManager.Instance.OnVerificationRequired.AddListener(OnGuestVerificationRequired);
            AuthManager.Instance.OnVerificationEmailResent.AddListener(OnGuestVerificationEmailResent);

            // NEW — single persistent subscription for the whole guest-upgrade
            // flow's errors (both "submit register form" errors AND "check
            // verification" errors land here). Replaces the old per-click
            // AddListener/RemoveListener pattern, which was fragile and (along
            // with routing everything to regErrorText — see the BUG FIX note
            // on guestVerificationErrorText above) was why verification errors
            // never actually showed on screen.
            AuthManager.Instance.OnAuthError.AddListener(OnGuestFlowError);
        }

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
        {
            AuthManager.Instance.OnRegisterSuccess.RemoveListener(OnGuestUpgradeSuccess);
            AuthManager.Instance.OnVerificationRequired.RemoveListener(OnGuestVerificationRequired);
            AuthManager.Instance.OnVerificationEmailResent.RemoveListener(OnGuestVerificationEmailResent);
            AuthManager.Instance.OnAuthError.RemoveListener(OnGuestFlowError);
        }

        StopWatchdog();
    }

    // ── Show / Hide ───────────────────────────────────────────

    public void Show()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        SetLoading(false);   // ALWAYS off when opening
        HideError();
        if (guestRegisterPopup     != null) guestRegisterPopup.SetActive(false);
        if (guestVerificationPanel != null) guestVerificationPanel.SetActive(false);
        if (avatarPickerPopup      != null) avatarPickerPopup.SetActive(false);

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
        if (guestRegisterPopup     != null) guestRegisterPopup.SetActive(false);
        if (guestVerificationPanel != null) guestVerificationPanel.SetActive(false);
        if (avatarPickerPopup      != null) avatarPickerPopup.SetActive(false);
        SetLoading(false);   // Always turn off loading when hiding
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── Refresh UI ────────────────────────────────────────────

    public void RefreshUI()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;

        PlayerProfile p = ProfileManager.Instance?.CurrentProfile;
        bool isGuest = AuthManager.IsGuest;
        // NEW — FIX: once LinkWithCredentialAsync succeeds, Firebase's IsAnonymous
        // flips to false immediately — BEFORE the email is verified — so isGuest
        // alone would already show the "member" UI (Logout button) right after
        // tapping "Later", even though nothing has been verified yet. This extra
        // check keeps the UI in its "still needs to verify" state until it's real.
        bool pendingVerification = AuthManager.IsPendingEmailVerification;

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
                emailText.text = pendingVerification ? $"{mail}  (Not verified)" : mail;
            }
        }

        // ── Avatar ──
        // Only fall back to the default sprite when there's NEITHER a preset
        // avatarId NOR an uploaded avatarUrl — otherwise a chosen preset would
        // get silently reset back to the default every time RefreshUI() runs.
        bool hasAnyAvatar = p != null && (!string.IsNullOrEmpty(p.avatarId) || !string.IsNullOrEmpty(p.avatarUrl));
        if (avatarImage != null)
        {
            if (!hasAnyAvatar)
            {
                avatarImage.sprite = defaultAvatarSprite;
            }
            else if (!string.IsNullOrEmpty(p.avatarId))
            {
                // Resolve directly instead of relying only on the OnAvatarLoaded
                // event — that event fires once at app/profile-load time, which
                // usually happens BEFORE this panel is ever opened (it starts
                // inactive), so the event gets missed the first time around.
                var preset = Resources.Load<AvatarPresetData>("Avatars/" + p.avatarId);
                if (preset != null && preset.sprite != null)
                    avatarImage.sprite = preset.sprite;
            }
            // else: avatarUrl-based uploaded photo — arrives via OnAvatarLoaded once downloaded.
        }

        // ── Stats (safe if profile null) ──
        if (levelText           != null) levelText.text           = (p?.level ?? 1).ToString();
        if (totalScoreText      != null) totalScoreText.text      = (p?.totalScore ?? 0).ToString("N0");
        if (coinsText           != null) coinsText.text           = (p?.coins ?? 0).ToString("N0");
        if (levelsCompletedText != null) levelsCompletedText.text = (p?.levelsCompleted ?? 0).ToString();
        if (petsCountText       != null) petsCountText.text       = (p?.pets?.Count ?? 0).ToString();

        // ── Button visibility ──
        // NEW — FIX: "showRegisterUI" now also covers the pending-verification
        // state, not just isGuest, so the Logout button doesn't appear (and
        // Save-Account/Register doesn't disappear) the instant linking succeeds
        // but before the email is actually verified.
        bool showRegisterUI = isGuest || pendingVerification;
        if (logoutButton   != null) logoutButton.gameObject.SetActive(!showRegisterUI);
        if (registerButton != null) registerButton.gameObject.SetActive(showRegisterUI);

        // Preset avatars are local-only (no Storage upload, no account needed),
        // so unlike the old photo-upload flow, Guests CAN change their avatar too.
        if (changeAvatarButton != null) changeAvatarButton.interactable = true;
        if (editNameButton     != null) editNameButton.gameObject.SetActive(!showRegisterUI);

        if (p != null) RefreshPetIcons(p);
    }

    private void RefreshPetIcons(PlayerProfile p)
    {
        if (petsContainer == null) return;
        foreach (Transform child in petsContainer) Destroy(child.gameObject);

        // Real pet definitions (sprite + name) instead of unicode glyphs —
        // the old ★ character wasn't in the TMP font asset AND its color
        // was never set (defaulted to white-on-white = invisible).
        PetData[] allPets = Resources.LoadAll<PetData>("Pets");
        System.Array.Sort(allPets, (a, b) => a.unlockAfterLevel.CompareTo(b.unlockAfterLevel));

        int slotCount = Mathf.Max(5, allPets.Length);

        for (int i = 0; i < slotCount; i++)
        {
            PetData pet = i < allPets.Length ? allPets[i] : null;
            bool isUnlocked = pet != null && p.pets.Contains(pet.id);

            GameObject slot = new GameObject($"Pet_{i}");
            slot.AddComponent<RectTransform>();
            slot.transform.SetParent(petsContainer, false);
            Image slotImg = slot.AddComponent<Image>();
            LayoutElement le = slot.AddComponent<LayoutElement>();
            le.preferredWidth = 40; le.preferredHeight = 40;

            if (isUnlocked)
            {
                slotImg.color = new Color(0.85f, 0.93f, 1f, 1f);

                if (pet.sprite != null)
                {
                    GameObject iconGO = new GameObject("Icon");
                    iconGO.transform.SetParent(slot.transform, false);
                    Image iconImg = iconGO.AddComponent<Image>();
                    iconImg.sprite = pet.sprite;
                    iconImg.color  = Color.white;
                    RectTransform iconRT = iconGO.GetComponent<RectTransform>();
                    iconRT.anchorMin = new Vector2(0.1f, 0.1f);
                    iconRT.anchorMax = new Vector2(0.9f, 0.9f);
                    iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
                }
                else
                {
                    // Fallback if sprite isn't assigned yet — ASCII letter,
                    // color EXPLICITLY set so it's actually visible.
                    TMP_Text icon = new GameObject("Icon").AddComponent<TextMeshProUGUI>();
                    icon.transform.SetParent(slot.transform, false);
                    RectTransform iconRT = icon.GetComponent<RectTransform>();
                    iconRT.anchorMin = Vector2.zero; iconRT.anchorMax = Vector2.one;
                    iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
                    icon.alignment = TextAlignmentOptions.Center;
                    icon.fontSize  = 22;
                    icon.color     = new Color(0.15f, 0.35f, 0.65f, 1f);
                    icon.text      = string.IsNullOrEmpty(pet.petName) ? "P" : pet.petName.Substring(0, 1).ToUpper();
                }
            }
            else
            {
                slotImg.color = new Color(0.88f, 0.88f, 0.90f);

                TMP_Text icon = new GameObject("Icon").AddComponent<TextMeshProUGUI>();
                icon.transform.SetParent(slot.transform, false);
                RectTransform iconRT = icon.GetComponent<RectTransform>();
                iconRT.anchorMin = Vector2.zero; iconRT.anchorMax = Vector2.one;
                iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
                icon.alignment = TextAlignmentOptions.Center;
                icon.fontSize  = 14;
                icon.color     = new Color(0.55f, 0.55f, 0.6f, 1f);
                icon.text      = pet != null ? $"Lv{pet.unlockAfterLevel}" : "-";
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
        if (avatarPickerPopup != null) avatarPickerPopup.SetActive(true);
    }

    /// <summary>Wired directly to the popup's Close ("X") Button OnClick() in the
    /// Inspector — no code wiring needed for that button.</summary>
    public void CloseAvatarPopup()
    {
        if (avatarPickerPopup != null) avatarPickerPopup.SetActive(false);
    }

    private void SelectPresetAvatar(string avatarId)
    {
        ProfileManager.Instance?.SetPresetAvatar(avatarId);
        CloseAvatarPopup();
    }

    // ── POPULATE AVATAR GRID ──────────────────────────────────
    // Fills the Editor-built avatarGridContainer with one instance of
    // avatarSlotPrefab per AvatarPresetData found under Resources/Avatars/.
    // Everything else (popup layout, title, close button, divider, grid
    // columns) is built by hand in the Unity Editor — this method only wires data.
    private void PopulateAvatarGrid()
    {
        if (avatarGridContainer == null || avatarSlotPrefab == null)
        {
            Debug.LogWarning("[ProfilePanel] PopulateAvatarGrid: avatarGridContainer or avatarSlotPrefab not assigned in Inspector.");
            return;
        }

        // Clear any leftover placeholder children left in the grid for editing convenience.
        for (int i = avatarGridContainer.childCount - 1; i >= 0; i--)
            Destroy(avatarGridContainer.GetChild(i).gameObject);

        AvatarPresetData[] presets = Resources.LoadAll<AvatarPresetData>("Avatars");
        Debug.Log($"[ProfilePanel] PopulateAvatarGrid: found {presets.Length} preset(s) in Resources/Avatars/.");

        foreach (AvatarPresetData preset in presets)
        {
            if (preset == null || preset.sprite == null) continue;
            string id = preset.id;

            GameObject slot = Instantiate(avatarSlotPrefab, avatarGridContainer);
            Image slotImg = slot.GetComponent<Image>();
            if (slotImg != null) { slotImg.sprite = preset.sprite; slotImg.preserveAspect = true; }

            Button slotBtn = slot.GetComponent<Button>();
            if (slotBtn != null) slotBtn.onClick.AddListener(() => SelectPresetAvatar(id));
        }
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
        if (editNameButton   != null) editNameButton.gameObject.SetActive(!editing && !AuthManager.IsGuest && !AuthManager.IsPendingEmailVerification);
        if (saveNameButton   != null) saveNameButton.gameObject.SetActive(editing);
        if (cancelNameButton != null) cancelNameButton.gameObject.SetActive(editing);
    }

    // ── Logout ────────────────────────────────────────────────

    private void OnLogoutClicked()
    {
        // Logout is immediate — no loading screen/blocking wait.
        // Fire-and-forget a cloud push so if internet is available the
        // latest progress also reaches the cloud (levels already
        // auto-push on completion — this is just an extra safety push
        // at logout time).
        Debug.Log("[ProfilePanel] Logging out...");
        _ = CloudSyncManager.Instance?.SyncAfterLevelAsync();
        Hide();
        AuthManager.Instance?.Logout();
    }

    // ── GUEST REGISTER FLOW ───────────────────────────────────

    private void OnRegisterClicked()
    {
        // NEW — FIX: if the guest's account is already linked and only
        // waiting on verification (they tapped "Later" earlier), this same
        // button must NOT reopen the registration form — UpgradeGuestAccount()
        // would immediately fail with "No guest account to upgrade" because
        // _currentUser.IsAnonymous is already false at this point. Instead,
        // just reopen the verification panel directly.
        if (AuthManager.IsPendingEmailVerification)
        {
            ReopenVerificationPanel();
            return;
        }

        if (guestRegisterPopup != null)
        {
            ClearRegisterForm();
            guestRegisterPopup.SetActive(true);
        }
    }

    /// <summary>NEW — re-shows the verification panel for the currently signed-in
    /// (already-linked-but-unverified) account, without going through
    /// UpgradeGuestAccount() again.</summary>
    private void ReopenVerificationPanel()
    {
        string email = AuthManager.CurrentUser?.Email ?? "";
        OnGuestVerificationRequired(email);
    }

    private void OnRegisterCancelClicked()
    {
        if (guestRegisterPopup != null) guestRegisterPopup.SetActive(false);
    }

    private void OnRegisterSubmit()
    {
        string username = regUsernameInput?.text.Trim() ?? "";
        string email    = regEmailInput?.text.Trim()    ?? "";
        string password = regPasswordInput?.text        ?? "";
        string confirm  = regConfirmInput?.text         ?? "";

        if (username.Length < 3)
        { ShowRegError("Username must be at least 3 characters."); return; }
        if (!email.Contains("@") || !email.Contains("."))
        { ShowRegError("Please enter a valid email address."); return; }
        if (password.Length < 6)
        { ShowRegError("Password must be at least 6 characters."); return; }
        if (password != confirm)
        { ShowRegError("Password and Confirm Password do not match."); return; }

        ShowRegError("");
        if (regErrorText != null) regErrorText.gameObject.SetActive(false);

        SetLoading(true);

        // AuthManager handles token refresh + link internally
        AuthManager.Instance?.UpgradeGuestAccount(username, email, password);
    }

    /// <summary>
    /// NEW — single error handler for the ENTIRE guest-upgrade flow
    /// (both the register-form submit and the later verification check).
    /// Routes the message to whichever popup is actually on screen right
    /// now, so it's always visible — this is the fix for "error nahi
    /// dikhta": the old code always wrote to regErrorText, which is a
    /// child of GuestRegisterPopup and stays invisible while
    /// VerificationPanel is the one showing.
    /// </summary>
    private void OnGuestFlowError(string message)
    {
        StopWatchdog();
        SetLoading(false);

        if (guestVerificationResendButton != null) guestVerificationResendButton.interactable = true;

        if (guestVerificationPanel != null && guestVerificationPanel.activeInHierarchy)
        {
            ShowGuestVerificationError(message);
        }
        else if (guestRegisterPopup != null && guestRegisterPopup.activeInHierarchy)
        {
            ShowRegError(message);
        }
        else
        {
            // Neither guest popup is open — fall back to the main profile error text.
            ShowError(message);
        }
    }

    // ── NEW — Guest Email Verification ─────────────────────────

    /// <summary>
    /// Fired by AuthManager once UpgradeGuestAccount() links the account.
    /// Switches from the register form to the "check your inbox" panel.
    /// </summary>
    private void OnGuestVerificationRequired(string email)
    {
        StopWatchdog();
        SetLoading(false);

        if (guestRegisterPopup != null) guestRegisterPopup.SetActive(false);
        if (guestVerificationPanel != null) guestVerificationPanel.SetActive(true);

        if (guestVerificationEmailText != null)
            guestVerificationEmailText.text = string.IsNullOrEmpty(email)
                ? "Please verify your email address."
                : $"We sent a verification link to {email}.\nPlease check your inbox (and Spam folder).";

        if (guestVerificationResendConfirmText != null)
            guestVerificationResendConfirmText.gameObject.SetActive(false);

        // Clear any leftover error from a previous attempt.
        if (guestVerificationErrorText != null)
            guestVerificationErrorText.gameObject.SetActive(false);
    }

    private void OnGuestVerificationContinueClicked()
    {
        SetLoading(true);
        if (guestVerificationErrorText != null) guestVerificationErrorText.gameObject.SetActive(false);
        AuthManager.Instance?.CheckEmailVerifiedAndContinue();
        // On success -> AuthManager fires OnRegisterSuccess -> OnGuestUpgradeSuccess() below.
        // On failure -> AuthManager fires OnAuthError -> OnGuestFlowError() (already subscribed
        // once in Start()) -> shown via guestVerificationErrorText since this panel is active.
    }

    private void OnGuestVerificationResendClicked()
    {
        if (guestVerificationResendButton != null) guestVerificationResendButton.interactable = false;
        AuthManager.Instance?.ResendVerificationEmail();
    }

    private void OnGuestVerificationEmailResent()
    {
        if (guestVerificationResendButton != null) guestVerificationResendButton.interactable = true;

        if (guestVerificationResendConfirmText != null)
        {
            guestVerificationResendConfirmText.text = "Verification email sent again.";
            guestVerificationResendConfirmText.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// "Later" button — the guest's account is already linked to this
    /// email/password (LinkWithCredentialAsync already succeeded), so the
    /// player can keep playing under the new account right away. Only the
    /// emailVerified flag stays false until they come back and verify —
    /// nothing here is undone by closing this panel.
    /// </summary>
    private void OnGuestVerificationLaterClicked()
    {
        if (guestVerificationPanel != null) guestVerificationPanel.SetActive(false);
        FinishGuestUpgradeUI();
    }

    private void OnGuestUpgradeSuccess()
    {
        StopWatchdog();
        SetLoading(false);

        // Close both popups
        if (guestRegisterPopup     != null) guestRegisterPopup.SetActive(false);
        if (guestVerificationPanel != null) guestVerificationPanel.SetActive(false);

        FinishGuestUpgradeUI();
    }

    /// <summary>
    /// Shared tail-end of the guest-upgrade flow — updates the in-memory
    /// profile immediately (so the name/email show instantly) and reloads
    /// from Firestore in the background. Used both by the normal success
    /// path and by "Later".
    /// </summary>
    private void FinishGuestUpgradeUI()
    {
        string newUsername = regUsernameInput?.text.Trim() ?? "";
        string newEmail    = regEmailInput?.text.Trim()    ?? "";

        if (ProfileManager.Instance?.CurrentProfile != null && !string.IsNullOrEmpty(newUsername))
        {
            ProfileManager.Instance.CurrentProfile.displayName = newUsername;
            ProfileManager.Instance.CurrentProfile.email       = newEmail;
        }

        // NEW — FIX: only reload from Firestore when verification is actually
        // done. While still pending (e.g. right after "Later"), Firestore still
        // has the OLD "Guest_xxxx" placeholder data (the real username/email
        // are only written once CheckEmailVerifiedAndContinue() confirms
        // verification) — reloading here would silently overwrite the optimistic
        // update above and flip the name/email back to the guest placeholder.
        if (AuthManager.CurrentUser != null && !AuthManager.IsPendingEmailVerification)
            ProfileManager.Instance?.LoadProfile(AuthManager.CurrentUser.UserId);

        RefreshUI();
        StartCoroutine(HidePanelAfterDelay(0.5f));
    }

    private IEnumerator HidePanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Hide();
    }

    private void ClearRegisterForm()
    {
        if (regUsernameInput != null) regUsernameInput.text = "";
        if (regEmailInput    != null) regEmailInput.text    = "";
        if (regPasswordInput != null) regPasswordInput.text = "";
        if (regConfirmInput  != null) regConfirmInput.text  = "";
        if (regErrorText != null)
        {
            regErrorText.text = "";
            regErrorText.gameObject.SetActive(false);
        }
    }

    private void ShowRegError(string msg)
    {
        if (regErrorText == null) return;
        regErrorText.text = msg;
        regErrorText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }

    /// <summary>NEW — dedicated error display for the verification panel (see BUG FIX note on guestVerificationErrorText field).</summary>
    private void ShowGuestVerificationError(string msg)
    {
        if (guestVerificationErrorText == null)
        {
            // Fallback so the message is at least visible somewhere if the
            // Inspector field hasn't been wired yet — see chat setup guide.
            Debug.LogWarning("[ProfilePanel] guestVerificationErrorText is not assigned in the Inspector — showing error on the main profile error text instead.");
            ShowError(msg);
            return;
        }
        guestVerificationErrorText.text = msg;
        guestVerificationErrorText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
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

    // ── Loading + Watchdog (NEW) ────────────────────────────────

    private void SetLoading(bool show)
    {
        if (loadingOverlay != null) loadingOverlay.SetActive(show);

        if (regSubmitButton != null) regSubmitButton.interactable = !show;
        if (guestVerificationContinueButton != null) guestVerificationContinueButton.interactable = !show;

        // Same watchdog pattern as LoginUIController — guarantees the
        // loading overlay/buttons can never stay stuck no matter what
        // interrupts the underlying Firebase call.
        StopWatchdog();
        if (show)
            _loadingWatchdog = StartCoroutine(LoadingWatchdogRoutine());
    }

    private IEnumerator LoadingWatchdogRoutine()
    {
        yield return new WaitForSecondsRealtime(loadingTimeoutSeconds);

        Debug.LogWarning("[ProfilePanel] Loading watchdog fired — no response in time. Force-unlocking UI.");
        SetLoading(false);
        ShowRegError("Request timed out. Check your internet connection and try again.");
    }

    private void StopWatchdog()
    {
        if (_loadingWatchdog != null)
        {
            StopCoroutine(_loadingWatchdog);
            _loadingWatchdog = null;
        }
    }

    // ── NEW — same "Resend Email looks like a pause" fix as LoginUIController ──
    // See the long comment in LoginUIController.cs for the full explanation:
    // this is a genuine OS focus-loss event (a security dialog or the Mail
    // app opening), not a scripted GameState.Paused. These handlers just make
    // returning from it smooth, and silently re-check verification so the
    // player doesn't have to tap "Continue" again if they already verified.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            if (guestVerificationPanel != null && guestVerificationPanel.activeSelf)
            {
                _lostFocusWhileVerifying = true;
                Debug.Log("[ProfilePanel] Lost focus while guest verification panel was open.");
            }
            return;
        }

        Debug.Log("[ProfilePanel] Regained focus.");

        if (_lostFocusWhileVerifying)
        {
            _lostFocusWhileVerifying = false;
            StopWatchdog();
            SetLoading(false);

            if (guestVerificationPanel != null && guestVerificationPanel.activeSelf && AuthManager.Instance != null)
            {
                Debug.Log("[ProfilePanel] Auto re-checking email verification after regaining focus.");
                AuthManager.Instance.CheckEmailVerifiedAndContinue();
            }
        }
    }
}