using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// MultiPlayMode_Oracle 씬의 UI를 코드로 만드는 도우미.
/// 씬 파일에 UI를 직접 배치하지 않아서, 씬은 카메라 + OracleMatchUI 오브젝트 하나만 있으면 된다.
/// 좌표는 1920x1080 기준, (0,0)이 화면 가운데.
/// </summary>
public static class OracleUi
{
    public static readonly Color PanelColor = new Color(0.10f, 0.11f, 0.15f, 0.92f);
    public static readonly Color ButtonColor = new Color(0.22f, 0.45f, 0.85f);
    public static readonly Color ButtonDisabledColor = new Color(0.30f, 0.30f, 0.34f);
    public static readonly Color HpColor = new Color(0.85f, 0.22f, 0.25f);

    public static TMP_FontAsset Font;

    public static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(string name, Transform parent)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Image Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        RectTransform rect = Rect(name, parent, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size,
        string text, float fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rect = Rect(name, parent, position, size);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null) tmp.font = Font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    public static Button Button(string name, Transform parent, Vector2 position, Vector2 size, string label,
        UnityAction onClick, float fontSize = 32)
    {
        Image image = Panel(name, parent, position, size, ButtonColor);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var colors = button.colors;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        button.colors = colors;

        Text("Label", image.transform, Vector2.zero, size, label, fontSize);
        return button;
    }

    public static void SetLabel(Button button, string label)
    {
        button.GetComponentInChildren<TextMeshProUGUI>().text = label;
    }

    public static TMP_InputField Input(string name, Transform parent, Vector2 position, Vector2 size,
        string placeholder, int characterLimit)
    {
        // TMP_InputField는 OnEnable 전에 textComponent가 있어야 하므로 꺼둔 채로 조립한다
        RectTransform rect = Rect(name, parent, position, size);
        rect.gameObject.SetActive(false);
        rect.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);

        RectTransform area = Stretch("Text Area", rect);
        area.offsetMin = new Vector2(16, 6);
        area.offsetMax = new Vector2(-16, -6);
        area.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI hint = Text("Placeholder", area, Vector2.zero, Vector2.zero, placeholder, 30,
            TextAlignmentOptions.Left);
        Fill(hint.rectTransform);
        hint.color = new Color(0.4f, 0.4f, 0.45f);
        hint.fontStyle = FontStyles.Italic;

        TextMeshProUGUI text = Text("Text", area, Vector2.zero, Vector2.zero, "", 30, TextAlignmentOptions.Left);
        Fill(text.rectTransform);
        text.color = new Color(0.08f, 0.08f, 0.1f);
        text.textWrappingMode = TextWrappingModes.NoWrap;

        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = hint;
        input.characterLimit = characterLimit;
        if (Font != null) input.fontAsset = Font;

        rect.gameObject.SetActive(true);
        return input;
    }

    private static void Fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// <summary>왼쪽부터 ratio만큼 채워지는 바. 반환값은 채우는 Image.</summary>
    public static Image Bar(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        Image back = Panel(name, parent, position, size, new Color(0f, 0f, 0f, 0.6f));
        Image fill = Panel("Fill", back.transform, Vector2.zero, size, color);
        fill.rectTransform.anchorMin = new Vector2(0f, 0f);
        fill.rectTransform.anchorMax = new Vector2(1f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        return fill;
    }

    public static void SetBar(Image fill, float ratio)
    {
        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
    }

    // =========================================================
    // 텍스트 표기
    // =========================================================

    private static readonly Dictionary<string, string> EffectNames = new Dictionary<string, string>
    {
        ["Attack"] = "공격",
        ["Defend"] = "방어",
        ["Weak"] = "약화",
        ["Vulnerable"] = "취약",
        ["Strength"] = "힘",
        ["Harden"] = "단단함",
        ["Guard"] = "가드",
        ["PoseBreak"] = "자세 붕괴",
        ["EnergyHeal"] = "코스트 회복",
        ["AddDump"] = "쓰레기 카드",
        ["Burning"] = "화상",
        ["CostToCooldown"] = "코스트→쿨",
        ["CooldownToCost"] = "쿨→코스트",
        ["DivideCooldown"] = "쿨 평준화",
        ["TimeSkip"] = "시간 단축",
        ["Quicker"] = "가속",
        ["Preserve"] = "보존",
        ["Disposable"] = "일회용",
        ["DefenseToCooldown"] = "방어→쿨",
        ["EnergyDrain"] = "코스트 흡수",
    };

    public static string EffectName(string type) =>
        type != null && EffectNames.TryGetValue(type, out string name) ? name : type;

    public static string CardEffects(CardEffectView[] effects)
    {
        if (effects == null || effects.Length == 0) return "<color=#888>(효과 없음)</color>";

        var builder = new StringBuilder();
        foreach (CardEffectView effect in effects)
        {
            if (builder.Length > 0) builder.Append('\n');
            builder.Append(EffectName(effect.type)).Append(' ').Append(effect.magnitude);
            builder.Append(effect.target == "User" ? " <color=#8fd>나</color>" : " <color=#f99>상대</color>");
            if (effect.category == "Continuous") builder.Append(" <color=#fd8>지속</color>");
            else if (effect.category == "Mix") builder.Append(" <color=#fd8>즉발+지속</color>");
        }
        return builder.ToString();
    }

    public static string StatusEffects(EffectView[] effects)
    {
        if (effects == null || effects.Length == 0) return "";

        var builder = new StringBuilder();
        foreach (EffectView effect in effects)
        {
            if (builder.Length > 0) builder.Append("  ");
            builder.Append(EffectName(effect.type)).Append(' ').Append(effect.magnitude);
            if (effect.infinite) builder.Append("∞");
        }
        return builder.ToString();
    }
}

/// <summary>카드 한 장 그림. 클릭은 onClick으로 받는다.</summary>
public sealed class OracleCardWidget
{
    private static readonly Color InstantColor = new Color(0.35f, 0.18f, 0.18f);
    private static readonly Color ContinuousColor = new Color(0.16f, 0.25f, 0.38f);
    private static readonly Color MixColor = new Color(0.30f, 0.22f, 0.40f);

    public RectTransform Root { get; }

    private readonly Image background;
    private readonly Image outline;
    private readonly Image art;
    private readonly TextMeshProUGUI nameText;
    private readonly TextMeshProUGUI costText;
    private readonly TextMeshProUGUI cooldownText;
    private readonly TextMeshProUGUI effectsText;
    private readonly TextMeshProUGUI keyText;
    private readonly CanvasGroup group;
    private readonly Button button;

    public OracleCardWidget(Transform parent, Vector2 position, Vector2 size, UnityAction onClick, string key = "")
    {
        outline = OracleUi.Panel("Card", parent, position, size + new Vector2(10, 10), Color.clear);
        Root = outline.rectTransform;
        group = Root.gameObject.AddComponent<CanvasGroup>();

        background = OracleUi.Panel("Background", Root, Vector2.zero, size, InstantColor);

        float artHeight = size.y * 0.42f;
        art = OracleUi.Panel("Art", background.transform, new Vector2(0, size.y * 0.5f - 42 - artHeight * 0.5f),
            new Vector2(size.x - 16, artHeight), Color.white);
        art.preserveAspect = true;

        float small = Mathf.Max(18, size.x * 0.11f);
        nameText = OracleUi.Text("Name", background.transform, new Vector2(0, size.y * 0.5f - 20),
            new Vector2(size.x - 70, 36), "", small);
        costText = OracleUi.Text("Cost", background.transform, new Vector2(-size.x * 0.5f + 22, size.y * 0.5f - 20),
            new Vector2(40, 36), "", small + 4);
        costText.color = new Color(0.55f, 0.85f, 1f);
        cooldownText = OracleUi.Text("Cooldown", background.transform, new Vector2(size.x * 0.5f - 22, size.y * 0.5f - 20),
            new Vector2(40, 36), "", small + 4);
        cooldownText.color = new Color(1f, 0.85f, 0.4f);

        float effectsTop = size.y * 0.5f - 46 - artHeight;
        float effectsHeight = effectsTop + size.y * 0.5f - 8;
        effectsText = OracleUi.Text("Effects", background.transform,
            new Vector2(0, effectsTop - effectsHeight * 0.5f), new Vector2(size.x - 14, effectsHeight), "",
            small * 0.85f, TextAlignmentOptions.Top);
        effectsText.overflowMode = TextOverflowModes.Truncate;

        keyText = OracleUi.Text("Key", Root, new Vector2(0, -size.y * 0.5f - 22), new Vector2(size.x, 30), key, 24);
        keyText.color = new Color(1f, 1f, 1f, 0.6f);

        if (onClick != null)
        {
            button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(onClick);
        }
    }

    public void SetVisible(bool visible) => Root.gameObject.SetActive(visible);

    public void Set(CardView card, Sprite sprite, bool selected = false, bool dimmed = false)
    {
        if (card == null || card.IsEmpty)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        background.color = card.cardType == "Continuous" ? ContinuousColor : card.cardType == "Mix" ? MixColor : InstantColor;
        outline.color = selected ? new Color(1f, 0.85f, 0.2f) : Color.clear;
        group.alpha = dimmed ? 0.45f : 1f;

        art.sprite = sprite;
        art.enabled = sprite != null;

        nameText.text = card.name;
        costText.text = card.cost.ToString();
        cooldownText.text = card.cooldownLeft.ToString();
        effectsText.text = OracleUi.CardEffects(card.effects);
    }
}
