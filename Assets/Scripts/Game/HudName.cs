using UnityEngine;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>The popup where the player types the name shown on the Rush Week leaderboard.</summary>
    public sealed partial class Hud
    {
        private GameObject namePanel;
        private InputField nameInput;

        private void BuildNamePanel(RectTransform safe)
        {
            namePanel = MakePopup(safe, new Vector2(900, 760), out var box);
            Title(box, Loc.T("name_label"), 290);

            var field = Img(box, Art.Rounded, new Color32(0x24, 0x0E, 0x5C, 0xFF), 36);
            Anchor(field.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(740, 130));
            var entered = MakeText(field.rectTransform, "", 52, TextAnchor.MiddleCenter, Art.DisplayFont);
            Stretch(entered.rectTransform);
            entered.rectTransform.offsetMin = new Vector2(24, 0);
            entered.rectTransform.offsetMax = new Vector2(-24, 0);
            var hint = MakeText(field.rectTransform, Loc.T("name_hint"), 44, TextAnchor.MiddleCenter);
            hint.color = new Color(1, 1, 1, 0.35f);
            Stretch(hint.rectTransform);
            // Img and MakeText turn raycasting off (they are decoration); an input field must receive the tap.
            field.raycastTarget = true;
            entered.raycastTarget = true;
            hint.raycastTarget = true;
            nameInput = field.gameObject.AddComponent<InputField>();
            nameInput.targetGraphic = field;
            nameInput.textComponent = entered;
            nameInput.placeholder = hint;
            nameInput.characterLimit = 16;
            nameInput.lineType = InputField.LineType.SingleLine;
            nameInput.shouldHideMobileInput = false;

            var rules = MakeText(box, Loc.T("name_rules"), 32, TextAnchor.MiddleCenter);
            rules.color = Muted;
            Anchor(rules.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(740, 50));

            var save = MakeCandy(box, Yellow, () =>
            {
                string text = nameInput.text.Trim();
                namePanel.SetActive(false);
                if (text.Length > 0) RenamePlayer?.Invoke(text);
            }, Loc.T("name_save"), 54, null, 0, 40);
            Anchor(save.Root, new Vector2(0.5f, 0.5f), new Vector2(-190, -170), new Vector2(340, 130));
            var cancel = MakeCandy(box, Red, () => namePanel.SetActive(false), null, 0, "x", 56, 40);
            Anchor(cancel.Root, new Vector2(0.5f, 0.5f), new Vector2(190, -170), new Vector2(340, 130));
            namePanel.SetActive(false);
        }

        private void ShowNamePanel()
        {
            nameInput.text = RushOnline.Name;
            namePanel.SetActive(true);
            namePanel.transform.SetAsLastSibling();
            toast.transform.SetAsLastSibling();
            PopIn(namePanel);
        }
    }
}
