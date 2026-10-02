using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.Firebase;
using Match3;
using DG.Tweening;

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

    [Header("Avatar Picker (built in Editor)")]
    [SerializeField] private GameObject avatarPickerPopup;
    [SerializeField] private Transform  avatarGridContainer;
    [SerializeField] private GameObject avatarSlotPrefab;

    [Header("Avatar Purchase (NEW)")]
    [Tooltip("Coin icon drawn inside every Buy button (use the same coin sprite as the TopBar).")]
    [SerializeField] private Sprite   avatarCoinSprite;
    [Tooltip("Optional padlock drawn on the top-right corner of LOCKED avatars.")]
    [SerializeField] private Sprite   avatarLockSprite;
    [Tooltip("Optional background sprite for the Buy button (e.g. a rounded green/wooden button). " +
             "Leave empty for a simple rounded dark pill.")]
    [SerializeField] private Sprite   buyButtonSprite;
    [Tooltip("Optional — your OWN Buy button prefab (Button + a child TMP text; optional child Image named \"CoinIcon\"). " +
             "If empty, the button is created by code.")]
    [SerializeField] private GameObject buyButtonPrefab;
    [Tooltip("Size of the Buy button in pixels (width, height).")]
    [SerializeField] private Vector2  buyButtonSize = new Vector2(120f, 42f);
    [Tooltip("Gap between the bottom of the avatar and the top of the Buy button.")]
    [SerializeField] private float    buyButtonGap = 6f;
    [SerializeField] private Color    buyButtonColor  = new Color(0.15f, 0.55f, 0.2f, 1f);
    [SerializeField] private Color    priceTextColor  = new Color(1f, 0.9f, 0.3f, 1f);
    [Tooltip("Price colour when the player can't afford it yet.")]
    [SerializeField] private Color    cantAffordColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Header("Avatar Popup Texts (optional)")]
    [Tooltip("TMP text inside the popup showing the player's coins.")]
    [SerializeField] private TMP_Text avatarPopupCoinsText;
    [Tooltip("TMP text inside the popup for short messages (\"Fox unlocked!\", \"Not enough coins\"). Start it INACTIVE.")]
    [SerializeField] private TMP_Text avatarPopupMessageText;

    private Coroutine _avatarMsgRoutine;

    [Header("Guest Register Popup (built in Editor)")]
    [SerializeField] private GameObject     guestRegisterPopup;
    [SerializeField] private TMP_InputField regUsernameInput;
    [SerializeField] private TMP_InputField regEmailInput;
    [SerializeField] private TMP_InputField regPasswordInput;
    [SerializeField] private TMP_InputField regConfirmInput;
    [SerializeField] private TMP_Text       regErrorText;
    [SerializeField] private Button         regCancelButton;
    [SerializeField] private Button         regSubmitButton;

    [Header("Guest Verification Popup (built in Editor, NEW)")]
    [SerializeField] private GameObject guestVerificationPanel;
    [SerializeField] private TMP_Text   guestVerificationEmailText;
    [SerializeField] private Button     guestVerificationContinueButton;
    [SerializeField] private Button     guestVerificationResendButton;
    [SerializeField] private TMP_Text   guestVerificationResendConfirmText;
    [SerializeField] private Button     guestVerificationLaterButton;
    [Tooltip("NEW — BUG FIX: verification errors ('not verified yet', resend failed, etc.) used to be routed to regErrorText, which lives inside GuestRegisterPopup — a GameObject that is already INACTIVE while this panel is showing, so the message was set correctly in code but never actually visible on screen. This is a dedicated error text living inside VerificationPanel itself so it is always visible when needed. Add a TMP_Text here (red, initially inactive) as a child of VerificationPanel.")]
    [SerializeField] private TMP_Text   guestVerificationErrorText;

    [Header("Loading Watchdog (NEW)")]
    [Tooltip("If no AuthManager response arrives within this many seconds, the UI unlocks itself with a timeout error instead of staying stuck.")]
    [SerializeField] private float loadingTimeoutSeconds = 15f;

    private bool      _isEditingName     = false;
    private bool      _isSavingName      = false;
    private Coroutine _hideErrorCoroutine;
    private Coroutine _loadingWatchdog;
    private bool      _lostFocusWhileVerifying = false;

    private void Start()
    {
        closeButton?.onClick.AddListener(Hide);
        editNameButton?.onClick.AddListener(StartEditName);
        saveNameButton?.onClick.AddListener(ConfirmEditName);
        cancelNameButton?.onClick.AddListener(CancelEditName);
        changeAvatarButton?.onClick.AddListener(OnChangeAvatarClicked);
        logoutButton?.onClick.AddListener(OnLogoutClicked);
        registerButton?.onClick.AddListener(OnRegisterClicked);

        regCancelButton?.onClick.AddListener(OnRegisterCancelClicked);
        regSubmitButton?.onClick.AddListener(OnRegisterSubmit);

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

            AuthManager.Instance.OnAuthError.AddListener(OnGuestFlowError);
        }

        SetEditMode(false);
        HideError();
        SetLoading(false);

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

    public void Show()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        SetLoading(false);
        HideError();
        if (guestRegisterPopup     != null) guestRegisterPopup.SetActive(false);
        if (guestVerificationPanel != null) guestVerificationPanel.SetActive(false);
        if (avatarPickerPopup      != null) avatarPickerPopup.SetActive(false);

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
        SetLoading(false);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void RefreshUI()
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy) return;

        PlayerProfile p = ProfileManager.Instance?.CurrentProfile;
        bool isGuest = AuthManager.IsGuest;
        bool pendingVerification = AuthManager.IsPendingEmailVerification;

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
                string authName = AuthManager.CurrentUser?.DisplayName;
                displayNameText.text = !string.IsNullOrEmpty(authName) ? authName : "Player";
            }
        }

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

        bool hasAnyAvatar = p != null && (!string.IsNullOrEmpty(p.avatarId) || !string.IsNullOrEmpty(p.avatarUrl));
        if (avatarImage != null)
        {
            if (!hasAnyAvatar)
            {
                avatarImage.sprite = defaultAvatarSprite;
            }
            else if (!string.IsNullOrEmpty(p.avatarId))
            {
                var preset = Resources.Load<AvatarPresetData>("Avatars/" + p.avatarId);
                if (preset != null && preset.sprite != null)
                    avatarImage.sprite = preset.sprite;
            }
        }

        if (levelText           != null) levelText.text           = (p?.level ?? 1).ToString();
        if (totalScoreText      != null) totalScoreText.text      = (p?.totalScore ?? 0).ToString("N0");
        if (coinsText           != null) coinsText.text           = (p?.coins ?? 0).ToString("N0");
        if (levelsCompletedText != null) levelsCompletedText.text = (p?.levelsCompleted ?? 0).ToString();
        if (petsCountText       != null) petsCountText.text       = (p?.pets?.Count ?? 0).ToString();

        bool showRegisterUI = isGuest || pendingVerification;
        if (logoutButton   != null) logoutButton.gameObject.SetActive(!showRegisterUI);
        if (registerButton != null) registerButton.gameObject.SetActive(showRegisterUI);

        if (changeAvatarButton != null) changeAvatarButton.interactable = true;
        if (editNameButton     != null) editNameButton.gameObject.SetActive(!showRegisterUI);

        if (p != null) RefreshPetIcons(p);
    }

    private void RefreshPetIcons(PlayerProfile p)
    {
        if (petsContainer == null) return;
        foreach (Transform child in petsContainer) Destroy(child.gameObject);

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

    public void SetAvatarSprite(Sprite sprite)
    {
        if (avatarImage != null && sprite != null)
            avatarImage.sprite = sprite;
    }

    private void OnChangeAvatarClicked()
    {
        PopulateAvatarGrid();
        SetAvatarMessage("");
        RefreshAvatarPopupCoins();
        if (avatarPickerPopup != null) avatarPickerPopup.SetActive(true);
    }

    public void CloseAvatarPopup()
    {
        if (avatarPickerPopup != null) avatarPickerPopup.SetActive(false);
    }

    private void SelectPresetAvatar(string avatarId)
    {
        ProfileManager.Instance?.SetPresetAvatar(avatarId);
        CloseAvatarPopup();
    }

    private void PopulateAvatarGrid()
    {
        if (avatarGridContainer == null || avatarSlotPrefab == null)
        {
            Debug.LogWarning("[ProfilePanel] PopulateAvatarGrid: avatarGridContainer or avatarSlotPrefab not assigned in Inspector.");
            return;
        }

        for (int i = avatarGridContainer.childCount - 1; i >= 0; i--)
            Destroy(avatarGridContainer.GetChild(i).gameObject);

        List<AvatarPresetData> presets = AvatarShopManager.GetAll();
        int coins = AvatarShopManager.CurrentCoins;
        Debug.Log($"[ProfilePanel] PopulateAvatarGrid: {presets.Count} avatar(s), player coins = {coins}.");

        foreach (AvatarPresetData preset in presets)
        {
            AvatarPresetData captured = preset;
            GameObject slot = Instantiate(avatarSlotPrefab, avatarGridContainer);

            bool owned    = AvatarShopManager.IsOwned(preset);
            bool equipped = AvatarShopManager.IsEquipped(preset);

            Image slotImg = slot.GetComponent<Image>();
            if (slotImg != null)
            {
                slotImg.sprite = preset.sprite;
                slotImg.preserveAspect = true;
                slotImg.color = owned ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            }

            if (equipped)
            {
                Outline outline = slot.GetComponent<Outline>();
                if (outline == null) outline = slot.AddComponent<Outline>();
                outline.effectColor    = new Color(0.2f, 0.85f, 0.3f, 1f);
                outline.effectDistance = new Vector2(4f, -4f);
            }

            Button slotBtn = slot.GetComponent<Button>();
            if (slotBtn != null)
            {
                slotBtn.onClick.RemoveAllListeners();
                slotBtn.onClick.AddListener(() => OnAvatarSlotClicked(captured));
            }

            if (!owned)
            {
                if (avatarLockSprite != null) AddLockIcon(slot.transform);
                AddBuyButton(slot.transform, captured, coins >= preset.price);
            }
        }
    }

    private void OnAvatarSlotClicked(AvatarPresetData preset)
    {
        AudioManager.Instance?.PlaySFX("button_click");

        if (AvatarShopManager.IsOwned(preset))
            SelectPresetAvatar(preset.id);
        else
            SetAvatarMessage($"Tap the coin button under {preset.NameOrId} to buy it.");
    }

    private void OnBuyButtonClicked(AvatarPresetData preset, Transform button)
    {
        AudioManager.Instance?.PlaySFX("button_click");

        var result = AvatarShopManager.TryPurchase(preset);
        if (result != AvatarShopManager.PurchaseResult.Success)
        {
            SetAvatarMessage(AvatarShopManager.MessageFor(result, preset));
            if (button != null)
            {
                button.DOKill();
                button.DOShakePosition(0.35f, new Vector3(10f, 0f, 0f), 20).SetUpdate(true);
            }
            return;
        }

        AvatarShopManager.Equip(preset);
        RefreshAvatarPopupCoins();
        RefreshUI();
        PopulateAvatarGrid();
        SetAvatarMessage($"{preset.NameOrId} unlocked!");
    }

    private void AddLockIcon(Transform slot)
    {
        var lockGo = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
        lockGo.transform.SetParent(slot, false);
        var rt = (RectTransform)lockGo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(34f, 34f);
        rt.anchoredPosition = new Vector2(4f, 4f);
        var img = lockGo.GetComponent<Image>();
        img.sprite = avatarLockSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    private void AddBuyButton(Transform slot, AvatarPresetData preset, bool canAfford)
    {
        GameObject btnGo;
        TMP_Text   priceText;

        if (buyButtonPrefab != null)
        {
            btnGo = Instantiate(buyButtonPrefab, slot, false);
            priceText = btnGo.GetComponentInChildren<TMP_Text>(true);
            Transform coinT = btnGo.transform.Find("CoinIcon");
            if (coinT != null && avatarCoinSprite != null && coinT.TryGetComponent(out Image ci)) ci.sprite = avatarCoinSprite;
        }
        else
        {
            btnGo = new GameObject("BuyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(slot, false);

            var bg = btnGo.GetComponent<Image>();
            if (buyButtonSprite != null)
            {
                bg.sprite = buyButtonSprite;
                bg.type   = Image.Type.Sliced;
                bg.color  = Color.white;
            }
            else
            {
                bg.color = buyButtonColor;
            }

            var hl = btnGo.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.spacing = 6f;
            hl.padding = new RectOffset(8, 8, 2, 2);
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            float h = buyButtonSize.y;
            if (avatarCoinSprite != null)
            {
                var coin = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
                coin.transform.SetParent(btnGo.transform, false);
                ((RectTransform)coin.transform).sizeDelta = new Vector2(h * 0.65f, h * 0.65f);
                var cImg = coin.GetComponent<Image>();
                cImg.sprite = avatarCoinSprite;
                cImg.preserveAspect = true;
                cImg.raycastTarget = false;
            }

            var txtGo = new GameObject("PriceText", typeof(RectTransform));
            txtGo.transform.SetParent(btnGo.transform, false);
            ((RectTransform)txtGo.transform).sizeDelta = new Vector2(buyButtonSize.x * 0.6f, h);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize  = h * 0.55f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            priceText = tmp;
        }

        var rt = (RectTransform)btnGo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.sizeDelta = buyButtonSize;
        rt.anchoredPosition = new Vector2(0f, -buyButtonGap);

        if (priceText != null)
        {
            priceText.text  = preset.price.ToString("N0");
            priceText.color = canAfford ? priceTextColor : cantAffordColor;
        }

        Button btn = btnGo.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnBuyButtonClicked(preset, btnGo.transform));
        }
    }

    private void RefreshAvatarPopupCoins()
    {
        if (avatarPopupCoinsText != null)
            avatarPopupCoinsText.text = AvatarShopManager.CurrentCoins.ToString("N0");
    }

    private void SetAvatarMessage(string msg)
    {
        if (avatarPopupMessageText == null)
        {
            if (!string.IsNullOrEmpty(msg)) ShowError(msg);
            return;
        }
        avatarPopupMessageText.text = msg;
        avatarPopupMessageText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
        if (_avatarMsgRoutine != null) StopCoroutine(_avatarMsgRoutine);
        if (!string.IsNullOrEmpty(msg)) _avatarMsgRoutine = StartCoroutine(ClearAvatarMessageAfter(3f));
    }

    private IEnumerator ClearAvatarMessageAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (avatarPopupMessageText != null) avatarPopupMessageText.gameObject.SetActive(false);
    }

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

        if (displayNameText != null) displayNameText.text = newName;
        SetEditMode(false);
        _isEditingName = false;

        ProfileManager.Instance?.UpdateDisplayName(newName);

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

    private void OnLogoutClicked()
    {
        Debug.Log("[ProfilePanel] Logging out...");
        _ = CloudSyncManager.Instance?.SyncAfterLevelAsync();
        Hide();
        AuthManager.Instance?.Logout();
    }

    private void OnRegisterClicked()
    {
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

        AuthManager.Instance?.UpgradeGuestAccount(username, email, password);
    }

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
            ShowError(message);
        }
    }

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

        if (guestVerificationErrorText != null)
            guestVerificationErrorText.gameObject.SetActive(false);
    }

    private void OnGuestVerificationContinueClicked()
    {
        SetLoading(true);
        if (guestVerificationErrorText != null) guestVerificationErrorText.gameObject.SetActive(false);
        AuthManager.Instance?.CheckEmailVerifiedAndContinue();
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

    private void OnGuestVerificationLaterClicked()
    {
        if (guestVerificationPanel != null) guestVerificationPanel.SetActive(false);
        FinishGuestUpgradeUI();
    }

    private void OnGuestUpgradeSuccess()
    {
        StopWatchdog();
        SetLoading(false);

        if (guestRegisterPopup     != null) guestRegisterPopup.SetActive(false);
        if (guestVerificationPanel != null) guestVerificationPanel.SetActive(false);

        FinishGuestUpgradeUI();
    }

    private void FinishGuestUpgradeUI()
    {
        string newUsername = regUsernameInput?.text.Trim() ?? "";
        string newEmail    = regEmailInput?.text.Trim()    ?? "";

        if (ProfileManager.Instance?.CurrentProfile != null && !string.IsNullOrEmpty(newUsername))
        {
            ProfileManager.Instance.CurrentProfile.displayName = newUsername;
            ProfileManager.Instance.CurrentProfile.email       = newEmail;
        }

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

    private void ShowGuestVerificationError(string msg)
    {
        if (guestVerificationErrorText == null)
        {
            Debug.LogWarning("[ProfilePanel] guestVerificationErrorText is not assigned in the Inspector — showing error on the main profile error text instead.");
            ShowError(msg);
            return;
        }
        guestVerificationErrorText.text = msg;
        guestVerificationErrorText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }

    private void OnSaveComplete()
    {
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

        if (regSubmitButton != null) regSubmitButton.interactable = !show;
        if (guestVerificationContinueButton != null) guestVerificationContinueButton.interactable = !show;

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
