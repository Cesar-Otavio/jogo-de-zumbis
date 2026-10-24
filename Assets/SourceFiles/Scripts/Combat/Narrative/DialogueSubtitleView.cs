using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Combat
{
    /* Dialogue subtitles: who is speaking and what, at the bottom centre, on their own canvas so they
     * also show over cinematics. Timed segments (when a line has them) follow the voice; otherwise the
     * whole line shows for its duration. Size and on/off come from the accessibility settings.
     */
    public class DialogueSubtitleView : MonoBehaviour
    {
        private RectTransform _panel;
        private CanvasGroup _group;
        private Image _back;
        private TextMeshProUGUI _speaker;
        private TextMeshProUGUI _text;
        private DialogueLine _shown;
        private string _shownText;

        private void Start()
        {
            // above the cinematic letterbox (140)
            RectTransform root = UiKit.Canvas("DialogueSubtitles", transform, 150);
            _panel = new GameObject("Subtitle", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _panel.SetParent(root, false);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.0f);
            _panel.pivot = new Vector2(0.5f, 0.0f);
            _panel.anchoredPosition = new Vector2(0.0f, 96.0f);
            _panel.sizeDelta = new Vector2(1000.0f, 110.0f);
            _group = _panel.GetComponent<CanvasGroup>();
            _group.alpha = 0.0f;
            _group.blocksRaycasts = false;

            _back = UiKit.Panel("Back", _panel, Vector2.zero, Vector2.one, new Color(0.03f, 0.035f, 0.04f, 0.62f));
            _speaker = UiKit.Text(_panel, "", 17, new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), TextAlignmentOptions.Bottom, Color.white, true);
            _speaker.rectTransform.pivot = new Vector2(0.5f, 0.0f);
            _speaker.rectTransform.sizeDelta = new Vector2(0.0f, 26.0f);
            _speaker.rectTransform.anchoredPosition = new Vector2(0.0f, -30.0f);
            _speaker.characterSpacing = 6.0f;
            _text = UiKit.Text(_panel, "", 26, new Vector2(0.03f, 0.0f), new Vector2(0.97f, 1.0f), TextAlignmentOptions.Center, Color.white);
            _text.rectTransform.offsetMax = new Vector2(0.0f, -30.0f);
            _text.rectTransform.offsetMin = new Vector2(0.0f, 10.0f);
            _text.textWrappingMode = TextWrappingModes.Normal;
        }

        private void LateUpdate()
        {
            DialogueDirector director = DialogueDirector.Instance;
            DialogueLine line = director != null ? director.Current : null;
            bool show = line != null && GameSettings.DialogueSubtitles && !string.IsNullOrEmpty(line.Text);
            float dt = Time.unscaledDeltaTime;
            _group.alpha = Mathf.MoveTowards(_group.alpha, show ? 1.0f : 0.0f, dt * (show ? 8.0f : 5.0f));
            if (!show) return;

            string text = TextAt(line, director.CurrentElapsed);
            if (line != _shown || text != _shownText)
            {
                _shown = line;
                _shownText = text;
                _speaker.text = DialogueLibrary.SpeakerName(line.Speaker);
                _speaker.color = DialogueLibrary.SpeakerColor(line.Speaker);
                bool narrator = line.Speaker == DialogueSpeaker.Narrator;
                _text.fontStyle = narrator ? FontStyles.Italic : FontStyles.Normal;
                _text.text = narrator ? text : "“" + text + "”";
            }

            // the panel hugs the text, scaled by the accessibility setting
            float scale = GameSettings.SubtitleScale;
            // during a cinematic, sit on the lower letterbox bar instead of the picture
            _panel.anchoredPosition = new Vector2(0.0f, CinematicPlayer.Playing ? 18.0f : 96.0f);
            _text.fontSize = 26.0f * scale;
            _speaker.fontSize = 17.0f * scale;
            float width = Mathf.Min(1100.0f * scale, Mathf.Max(420.0f, _text.GetPreferredValues(_text.text, 1000.0f * scale, 0.0f).x + 60.0f));
            float height = _text.GetPreferredValues(_text.text, width - 60.0f, 0.0f).y + 46.0f * scale;
            _panel.sizeDelta = new Vector2(width, height);
        }

        private static string TextAt(DialogueLine line, float elapsed)
        {
            if (line.Segments == null || line.Segments.Length == 0) return line.Text;
            string text = line.Segments[0].Text;
            foreach (SubtitleSegment segment in line.Segments)
            {
                if (elapsed >= segment.Time) text = segment.Text;
            }

            return text;
        }
    }
}
