using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Combat
{
    /* Draws the PickupFeed: a short column of rows at the lower right, above the weapon readout.
     * Each row slides in, pops a little when its amount grows, and fades out after a few seconds.
     */
    public class PickupFeedView : MonoBehaviour
    {
        public float Lifetime = 3.4f;
        public float RowHeight = 46.0f;
        public Vector2 Anchor = new Vector2(-40.0f, 300.0f);

        private class Row
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Image Background;
            public Image Accent;
            public Image Icon;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Amount;
            public PickupFeed.Entry Entry;
            public int LastAmount;
            public float Pop;
            public float Slot;
        }

        private readonly Row[] _rows = new Row[PickupFeed.MaxEntries];

        public void Build(RectTransform root)
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                var row = new Row();
                var go = new GameObject("PickupRow", typeof(RectTransform), typeof(CanvasGroup));
                row.Rect = go.GetComponent<RectTransform>();
                row.Rect.SetParent(root, false);
                row.Rect.anchorMin = row.Rect.anchorMax = new Vector2(1.0f, 0.0f);
                row.Rect.pivot = new Vector2(1.0f, 0.0f);
                row.Rect.sizeDelta = new Vector2(330.0f, RowHeight - 6.0f);
                row.Group = go.GetComponent<CanvasGroup>();
                row.Group.alpha = 0.0f;
                row.Group.blocksRaycasts = false;

                row.Background = UiKit.Panel("Back", row.Rect, Vector2.zero, Vector2.one, new Color(0.06f, 0.07f, 0.08f, 0.72f));
                row.Accent = UiKit.Panel("Accent", row.Rect, Vector2.zero, new Vector2(0.0f, 1.0f), Color.white);
                row.Accent.rectTransform.sizeDelta = new Vector2(3.0f, 0.0f);
                row.Accent.rectTransform.pivot = new Vector2(0.0f, 0.5f);

                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.rectTransform.SetParent(row.Rect, false);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0.0f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(10.0f, 0.0f);
                icon.rectTransform.sizeDelta = new Vector2(34.0f, 34.0f);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                row.Icon = icon;

                row.Label = UiKit.Text(row.Rect, "", 19, new Vector2(0.0f, 0.0f), new Vector2(0.78f, 1.0f), TextAlignmentOptions.Left, Color.white);
                row.Label.rectTransform.offsetMin = new Vector2(54.0f, 0.0f);
                row.Label.textWrappingMode = TextWrappingModes.NoWrap;
                row.Label.overflowMode = TextOverflowModes.Ellipsis;
                row.Amount = UiKit.Text(row.Rect, "", 21, new Vector2(0.72f, 0.0f), new Vector2(0.97f, 1.0f), TextAlignmentOptions.Right, Color.white, true);
                _rows[i] = row;
            }
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            PickupFeed.Active.RemoveAll(e => now - e.Updated > Lifetime + 0.4f);
            int count = PickupFeed.Active.Count;
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < _rows.Length; i++)
            {
                Row row = _rows[i];
                // newest at the bottom
                int index = count - 1 - i;
                PickupFeed.Entry entry = index >= 0 ? PickupFeed.Active[index] : null;
                if (entry == null)
                {
                    row.Group.alpha = Mathf.MoveTowards(row.Group.alpha, 0.0f, dt * 6.0f);
                    row.Entry = null;
                    continue;
                }

                if (row.Entry != entry)
                {
                    row.Entry = entry;
                    row.LastAmount = entry.Amount;
                    row.Icon.sprite = entry.Icon;
                    row.Icon.enabled = entry.Icon != null;
                    row.Icon.color = entry.Tint;
                    row.Accent.color = entry.Tint;
                    row.Label.text = entry.Label;
                    row.Label.color = entry.Negative ? entry.Tint : Color.white;
                }

                if (entry.Amount != row.LastAmount)
                {
                    row.LastAmount = entry.Amount;
                    row.Pop = 1.0f;
                }

                row.Amount.text = entry.Amount > 0 ? "+" + entry.Amount : "";
                row.Amount.color = entry.Tint;

                float age = now - entry.Started;
                float idle = now - entry.Updated;
                float fadeIn = Mathf.Clamp01(age / 0.15f);
                float fadeOut = Mathf.Clamp01((Lifetime - idle) / 0.45f);
                row.Group.alpha = Mathf.Min(fadeIn, fadeOut);

                // slide in from the right, settle into its slot, a small pop when it grows
                row.Slot = Mathf.Lerp(row.Slot, i, 1.0f - Mathf.Exp(-14.0f * dt));
                row.Pop = Mathf.MoveTowards(row.Pop, 0.0f, dt * 5.0f);
                float slide = (1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.Clamp01(age / 0.22f))) * 60.0f;
                row.Rect.anchoredPosition = Anchor + new Vector2(slide, row.Slot * RowHeight);
                row.Rect.localScale = Vector3.one * (1.0f + 0.08f * row.Pop);
            }
        }
    }
}
