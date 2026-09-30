using TMPro;
using UnityEngine;

namespace Game.Firebase
{
    /// <summary>
    /// NEW — shared helper used by every email TMP_InputField in the
    /// project (Login, Register, Guest-Register/Upgrade, Forgot
    /// Password) so they all get the mobile "email" keyboard instead
    /// of the plain text keyboard.
    ///
    /// What this actually changes on-device:
    ///  - TMP_InputField.ContentType.EmailAddress makes TMP restrict
    ///    typed characters to ones valid in an email address, AND is
    ///    what TMP itself uses to decide which on-screen keyboard to
    ///    request.
    ///  - keyboardType = TouchScreenKeyboardType.EmailAddress is the
    ///    actual Android/iOS instruction that swaps the on-screen
    ///    keyboard to the "email" layout — "@" and "." get their own
    ///    dedicated keys, autocapitalization is turned off, and (on
    ///    Android/Gboard in particular) it offers the usual quick
    ///    email-domain suggestions (.com, .gmail.com etc.) above the
    ///    keyboard, exactly like the OS's own apps do for email
    ///    fields.
    ///
    /// This is a code-level fix — it doesn't depend on how the
    /// TMP_InputField's ContentType was left set in the Editor/scene,
    /// so it stays correct even if that field's Inspector value ever
    /// gets changed by mistake.
    ///
    /// Call ConfigureAsEmailField() once, in Start()/Awake(), for
    /// every email input field.
    /// </summary>
    public static class EmailFieldUtility
    {
        public static void ConfigureAsEmailField(TMP_InputField field)
        {
            if (field == null) return;

            field.contentType = TMP_InputField.ContentType.EmailAddress;
            field.keyboardType = TouchScreenKeyboardType.EmailAddress;

            // Re-applies the content type's character validation/appearance
            // immediately, in case the field was already interacted with
            // (e.g. in the Editor) before this ran.
            field.ForceLabelUpdate();
        }
    }
}
