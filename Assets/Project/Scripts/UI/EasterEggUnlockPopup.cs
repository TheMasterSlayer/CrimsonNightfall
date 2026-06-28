using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class EasterEggUnlockPopup
{
    private const string Message = "You unlocked an easter egg toggle!";

    public static void Show(Action onOkay = null, bool freezePlayer = true)
    {
        EnsureEventSystem();

        PlayerController player = freezePlayer ? UnityEngine.Object.FindFirstObjectByType<PlayerController>() : null;
        if (player != null)
            player.SetInputEnabled(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvasObject = new GameObject("Easter Egg Unlock Popup", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image backdrop = CreateImage(canvasObject.transform, "Backdrop", new Color(0f, 0f, 0f, 0.72f));
        Stretch(backdrop.rectTransform);

        Text text = CreateText(canvasObject.transform, "Unlock Message", Message, font, 50, new Color(0.72f, 0.035f, 0.055f));
        SetAnchors(text.rectTransform, new Vector2(0.16f, 0.46f), new Vector2(0.84f, 0.62f));
        text.fontStyle = FontStyle.Bold;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        Button okay = CreateButton(canvasObject.transform, font);
        RectTransform okayRect = okay.GetComponent<RectTransform>();
        okayRect.anchorMin = new Vector2(0.5f, 0.30f);
        okayRect.anchorMax = new Vector2(0.5f, 0.30f);
        okayRect.pivot = new Vector2(0.5f, 0.5f);
        okayRect.anchoredPosition = Vector2.zero;
        okayRect.sizeDelta = new Vector2(260f, 64f);

        okay.onClick.AddListener(() =>
        {
            if (player != null)
                player.SetInputEnabled(true);

            UnityEngine.Object.Destroy(canvasObject);
            onOkay?.Invoke();
        });
    }

    private static Button CreateButton(Transform parent, Font font)
    {
        GameObject buttonObject = new GameObject("Okay Button", typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.075f, 0.075f, 0.08f, 1f);

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.72f, 0.035f, 0.055f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.45f, 0.47f);
        colors.pressedColor = new Color(0.72f, 0.035f, 0.055f);
        button.colors = colors;

        Text label = CreateText(buttonObject.transform, "Label", "Okay", font, 26, new Color(0.88f, 0.86f, 0.84f));
        Stretch(label.rectTransform);
        label.fontStyle = FontStyle.Bold;
        return button;
    }

    private static Text CreateText(Transform parent, string name, string value, Font font, int size, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        return text;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        SetAnchors(rect, Vector2.zero, Vector2.one);
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
