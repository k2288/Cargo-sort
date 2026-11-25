using UnityEngine;
using UnityEngine.UI;

namespace CargoSort.Game
{
    /// <summary>The "Enjoying the game?" popup that asks the player to rate the game and comment in the store.</summary>
    public sealed partial class Hud
    {
        private GameObject ratePanel;

        private void BuildRatePanel(RectTransform safe)
        {
            ratePanel = MakePopup(safe, new Vector2(900, 900), out var box);
            Title(box, Loc.T("rate_title"), 340);

            var stars = MakeRect("Stars", box);
            Anchor(stars, new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(760, 140));
            for (int i = 0; i < 5; i++)
            {
                var st = Icon(stars, "star", 120);
                st.color = new Color32(0xFF, 0xD2, 0x3A, 0xFF);
                Anchor(st.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 2) * 140, i == 2 ? 12 : 0), new Vector2(120, 120));
                var outline = st.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(0x8A, 0x4A, 0x00, 0xFF);
                outline.effectDistance = new Vector2(4, -4);
            }

            var body = MakeText(box, Loc.T("rate_body"), 40, TextAnchor.MiddleCenter);
            body.color = Muted;
            Anchor(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(780, 130));

            var yes = MakeCandy(box, Yellow, () => { ratePanel.SetActive(false); RateYes?.Invoke(); }, Loc.T("rate_yes"), 54, "star", 56);
            Anchor(yes.Root, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(620, 160));
            var later = MakeCandy(box, Blue, () => { ratePanel.SetActive(false); RateLater?.Invoke(); }, Loc.T("rate_later"), 40, null, 0, 30);
            Anchor(later.Root, new Vector2(0.5f, 0.5f), new Vector2(-170, -310), new Vector2(320, 110));
            var never = MakeCandy(box, Quiet, () => { ratePanel.SetActive(false); RateNever?.Invoke(); }, Loc.T("rate_never"), 36, null, 0, 30);
            Anchor(never.Root, new Vector2(0.5f, 0.5f), new Vector2(170, -310), new Vector2(320, 110));
            ratePanel.SetActive(false);
        }

        public void ShowRate()
        {
            ratePanel.SetActive(true);
            ratePanel.transform.SetAsLastSibling();
            PopIn(ratePanel);
        }
    }
}
