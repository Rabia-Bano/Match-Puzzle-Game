using TMPro;
using UnityEngine;

namespace Game.Firebase
{
    public static class EmailFieldUtility
    {
        public static void ConfigureAsEmailField(TMP_InputField field)
        {
            if (field == null) return;

            field.contentType = TMP_InputField.ContentType.EmailAddress;
            field.keyboardType = TouchScreenKeyboardType.EmailAddress;

            field.ForceLabelUpdate();
        }
    }
}
