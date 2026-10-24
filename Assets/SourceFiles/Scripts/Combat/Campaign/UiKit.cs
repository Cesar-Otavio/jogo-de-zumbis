using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Combat
{
    // Builds UI in code with the same look as the existing HUD and menus
    public static class UiKit
    {
        public static readonly Color Accent = new Color(1.0f, 0.78f, 0.3f);
        public static readonly Color Danger = new Color(1.0f, 0.25f, 0.2f);
        public static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.08f, 0.88f);
        public static readonly Color Muted = new Color(1.0f, 1.0f, 1.0f, 0.6f);

        public static RectTransform Canvas(string name, Transform parent, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        // stretched between two anchors
        public static Image Panel(string name, RectTransform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // pinned to an anchor point with a fixed size
        public static Image Box(string name, RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI Text(RectTransform parent, string text, float size, Vector2 min, Vector2 max, TextAlignmentOptions alignment, Color color, bool bold = false)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.raycastTarget = false;
            label.outlineWidth = 0.12f;
            label.outlineColor = new Color32(0, 0, 0, 150);
            return label;
        }

        public static Button Button(RectTransform parent, string text, Vector2 min, Vector2 max, Action onClick, bool primary, float fontSize = 26)
        {
            Image background = Panel("Button_" + text, parent, min, max, primary ? Accent : new Color(1, 1, 1, 0.1f));
            background.raycastTarget = true;
            var button = background.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = primary ? new Color(1.0f, 0.9f, 0.6f) : new Color(1, 1, 1, 0.25f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            Text(background.rectTransform, text, fontSize, Vector2.zero, Vector2.one, TextAlignmentOptions.Center,
                primary ? new Color(0.1f, 0.08f, 0.04f) : Color.white, true);
            return button;
        }

        public static void SetButtonText(Button button, string text)
        {
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = text;
        }
    }
}
