using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 运行时搭建的问题上报表单（uGUI），避免在场景里手工拖拽大量 UI 组件。
/// 通过事件把"提交"和"关闭"交给 ARInspectionBootstrap 处理。
/// </summary>
public class InspectionFormUI : MonoBehaviour
{
    public event Action<IssueDraft> SubmitRequested;
    public event Action CloseRequested;

    const string DefaultHint = "1. 缓慢移动手机，识别地面平面\n2. 点击平面放置标记\n3. 填写问题并提交";
    static readonly Color AccentColor = new Color(0.16f, 0.45f, 0.95f, 1f);
    static readonly Color LabelColor = new Color(0.68f, 0.72f, 0.8f, 1f);
    static readonly Color SuccessColor = new Color(0.36f, 0.85f, 0.55f, 1f);
    static readonly Color ErrorColor = new Color(1f, 0.45f, 0.42f, 1f);

    GameObject panelRoot;
    Text hintText;
    Text toastText;
    Text backendText;
    Text coordText;
    Text resultText;
    InputField titleInput;
    InputField descriptionInput;
    Button submitButton;
    Text submitLabel;
    Button closeButton;
    readonly Image[] priorityImages = new Image[3];
    readonly Text[] priorityLabels = new Text[3];
    string selectedPriority = IssueEnums.PriorityMedium;
    Vector3 markerPosition;
    Coroutine toastRoutine;
    static Font cachedFont;

    public bool IsVisible
    {
        get { return panelRoot != null && panelRoot.activeSelf; }
    }

    void Awake()
    {
        BuildUI();
    }

    // ---------- 对外接口 ----------

    public void SetBackendLabel(string url)
    {
        if (backendText != null)
        {
            backendText.text = "后端：" + (string.IsNullOrEmpty(url) ? "未配置" : url);
        }
    }

    /// <summary>放置标记后打开表单，并记住标记的世界坐标。</summary>
    public void ShowFor(Vector3 worldPosition)
    {
        markerPosition = worldPosition;
        panelRoot.SetActive(true);
        titleInput.text = string.Empty;
        descriptionInput.text = string.Empty;
        resultText.text = string.Empty;
        SelectPriority(IssueEnums.PriorityMedium);
        UpdateCoordinateText();
        SetSubmitting(false);
    }

    public void HidePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void SetSubmitting(bool submitting)
    {
        if (submitButton != null)
        {
            submitButton.interactable = !submitting;
        }
        if (closeButton != null)
        {
            closeButton.interactable = !submitting;
        }
        if (submitLabel != null)
        {
            submitLabel.text = submitting ? "提交中…" : "提交问题";
        }
    }

    public void ShowSuccess(IssueDto issue)
    {
        resultText.color = SuccessColor;
        resultText.text = "提交成功：" + issue.id + "（" + issue.status + "），可在管理端查看";
        submitButton.interactable = false; // 同一标记不重复提交
    }

    public void ShowError(string message)
    {
        resultText.color = ErrorColor;
        resultText.text = message;
    }

    /// <summary>顶部短暂提示，例如"没点到平面"。</summary>
    public void ShowHint(string message)
    {
        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
        }
        toastText.text = message;
        toastRoutine = StartCoroutine(ClearHintAfterDelay(2.5f));
    }

    IEnumerator ClearHintAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        toastText.text = string.Empty;
        toastRoutine = null;
    }

    // ---------- 事件 ----------

    void HandleSubmitClicked()
    {
        string title = titleInput.text == null ? string.Empty : titleInput.text.Trim();
        if (string.IsNullOrEmpty(title))
        {
            ShowError("标题不能为空");
            return;
        }
        if (SubmitRequested == null)
        {
            return;
        }
        SubmitRequested.Invoke(new IssueDraft
        {
            title = title,
            description = descriptionInput.text == null ? string.Empty : descriptionInput.text.Trim(),
            priority = selectedPriority,
            position = markerPosition,
        });
    }

    void HandleCloseClicked()
    {
        if (CloseRequested != null)
        {
            CloseRequested.Invoke();
        }
    }

    // ---------- 构建 UI ----------

    void BuildUI()
    {
        var canvasGo = new GameObject("InspectionCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;

        canvasGo.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();
        BuildHintPanel(canvasGo.transform);
        BuildFormPanel(canvasGo.transform);
        panelRoot.SetActive(false);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }
        var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_LEGACY_INPUT_MANAGER
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    void BuildHintPanel(Transform parent)
    {
        var root = CreateRect(parent, "HintPanel");
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.offsetMin = new Vector2(16f, -216f);
        root.offsetMax = new Vector2(-16f, -16f);

        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.05f, 0.06f, 0.09f, 0.66f);
        background.raycastTarget = false; // 不拦截点击，避免误挡放置

        hintText = CreateText(root, "Hint", DefaultHint, 30, new Color(0.92f, 0.94f, 0.98f), TextAnchor.UpperLeft);
        hintText.lineSpacing = 1.2f;
        PlaceTop(hintText.rectTransform, 10f, 108f);

        toastText = CreateText(root, "Toast", string.Empty, 26, new Color(1f, 0.76f, 0.35f), TextAnchor.UpperLeft);
        PlaceTop(toastText.rectTransform, 126f, 30f);

        backendText = CreateText(root, "Backend", "后端：未配置", 24, new Color(0.6f, 0.66f, 0.76f), TextAnchor.UpperLeft);
        PlaceTop(backendText.rectTransform, 160f, 30f);
    }

    void BuildFormPanel(Transform parent)
    {
        var root = CreateRect(parent, "FormPanel");
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.offsetMin = new Vector2(16f, 16f);
        root.offsetMax = new Vector2(-16f, 616f); // 底部面板高度 600

        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.07f, 0.08f, 0.11f, 0.95f);

        Text titleLabel = CreateText(root, "TitleLabel", "标题（必填）", 26, LabelColor, TextAnchor.MiddleLeft);
        PlaceTop(titleLabel.rectTransform, 14f, 30f);

        titleInput = CreateInput(root, "TitleInput", "例如：入口墙面破损", false);
        PlaceTop(titleInput.GetComponent<RectTransform>(), 48f, 80f);

        Text descLabel = CreateText(root, "DescLabel", "描述（可选）", 26, LabelColor, TextAnchor.MiddleLeft);
        PlaceTop(descLabel.rectTransform, 138f, 30f);

        descriptionInput = CreateInput(root, "DescInput", "例如：左侧墙体存在裂缝", true);
        PlaceTop(descriptionInput.GetComponent<RectTransform>(), 172f, 100f);

        Text priorityLabel = CreateText(root, "PriorityLabel", "优先级", 26, LabelColor, TextAnchor.MiddleLeft);
        PlaceTop(priorityLabel.rectTransform, 282f, 30f);

        BuildPriorityButtons(root, 318f);

        coordText = CreateText(root, "CoordText", "标记位置：-", 24, new Color(0.6f, 0.66f, 0.76f), TextAnchor.MiddleLeft);
        PlaceTop(coordText.rectTransform, 392f, 30f);

        resultText = CreateText(root, "ResultText", string.Empty, 26, SuccessColor, TextAnchor.MiddleLeft);
        PlaceTop(resultText.rectTransform, 424f, 60f);

        BuildActions(root);
        panelRoot = root.gameObject;
    }

    void BuildPriorityButtons(RectTransform parent, float top)
    {
        string[] labels = { "低", "中", "高" };
        string[] values = { IssueEnums.PriorityLow, IssueEnums.PriorityMedium, IssueEnums.PriorityHigh };

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var rt = CreateRect(parent, "Priority_" + values[i]);
            rt.anchorMin = new Vector2(i / 3f, 1f);
            rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
            rt.offsetMin = new Vector2(i == 0 ? 0f : 6f, -(top + 60f));
            rt.offsetMax = new Vector2(i == 2 ? 0f : -6f, -top);

            var image = rt.gameObject.AddComponent<Image>();
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text label = CreateText(rt, "Label", labels[i], 26, Color.white, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, 0f, 0f, 0f, 0f);

            button.onClick.AddListener(() => SelectPriority(values[index]));
            priorityImages[i] = image;
            priorityLabels[i] = label;
        }

        SelectPriority(selectedPriority);
    }

    void BuildActions(RectTransform parent)
    {
        var row = CreateRect(parent, "Actions");
        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(1f, 0f);
        row.offsetMin = new Vector2(0f, 16f);
        row.offsetMax = new Vector2(0f, 92f);

        closeButton = CreateButton(row, "CloseButton", "关闭", new Color(0.22f, 0.24f, 0.3f, 1f));
        var closeRt = closeButton.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0f, 0f);
        closeRt.anchorMax = new Vector2(0.5f, 1f);
        closeRt.offsetMin = new Vector2(0f, 0f);
        closeRt.offsetMax = new Vector2(-8f, 0f);
        closeButton.onClick.AddListener(HandleCloseClicked);

        submitButton = CreateButton(row, "SubmitButton", "提交问题", AccentColor);
        var submitRt = submitButton.GetComponent<RectTransform>();
        submitRt.anchorMin = new Vector2(0.5f, 0f);
        submitRt.anchorMax = new Vector2(1f, 1f);
        submitRt.offsetMin = new Vector2(8f, 0f);
        submitRt.offsetMax = new Vector2(0f, 0f);
        submitLabel = submitButton.GetComponentInChildren<Text>();
        submitButton.onClick.AddListener(HandleSubmitClicked);
    }

    void SelectPriority(string priority)
    {
        selectedPriority = priority;
        string[] values = { IssueEnums.PriorityLow, IssueEnums.PriorityMedium, IssueEnums.PriorityHigh };
        for (int i = 0; i < values.Length; i++)
        {
            bool selected = values[i] == priority;
            if (priorityImages[i] != null)
            {
                priorityImages[i].color = selected ? PriorityColor(priority) : new Color(0.24f, 0.27f, 0.33f, 1f);
            }
            if (priorityLabels[i] != null)
            {
                priorityLabels[i].color = selected ? Color.white : new Color(0.72f, 0.75f, 0.82f);
            }
        }
    }

    void UpdateCoordinateText()
    {
        coordText.text = string.Format("标记位置：x={0:F2}  y={1:F2}  z={2:F2}",
            markerPosition.x, markerPosition.y, markerPosition.z);
    }

    static Color PriorityColor(string priority)
    {
        switch (priority)
        {
            case IssueEnums.PriorityHigh:
                return new Color(0.9f, 0.28f, 0.26f, 1f);
            case IssueEnums.PriorityLow:
                return new Color(0.12f, 0.55f, 0.9f, 1f);
            default:
                return new Color(0.95f, 0.62f, 0.12f, 1f);
        }
    }

    // ---------- 组件工厂 ----------

    static RectTransform CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor alignment)
    {
        var rt = CreateRect(parent, name);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = GetFont();
        text.fontSize = fontSize;
        text.color = color;
        text.text = content;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static InputField CreateInput(Transform parent, string name, string placeholderValue, bool multiline)
    {
        var rt = CreateRect(parent, name);
        var background = rt.gameObject.AddComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.94f);

        var input = rt.gameObject.AddComponent<InputField>();
        input.targetGraphic = background;
        input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
        input.characterLimit = multiline ? 500 : 80;
        input.caretColor = AccentColor;
        input.selectionColor = new Color(0.16f, 0.45f, 0.95f, 0.35f);

        Text text = CreateText(rt, "Text", string.Empty, 28, new Color(0.1f, 0.11f, 0.14f), TextAnchor.UpperLeft);
        text.supportRichText = false;
        Stretch(text.rectTransform, 14f, 14f, 10f, 10f);

        Text placeholder = CreateText(rt, "Placeholder", placeholderValue, 28, new Color(0.52f, 0.55f, 0.6f), TextAnchor.UpperLeft);
        placeholder.fontStyle = FontStyle.Italic;
        Stretch(placeholder.rectTransform, 14f, 14f, 10f, 10f);

        input.textComponent = text;
        input.placeholder = placeholder;
        return input;
    }

    static Button CreateButton(Transform parent, string name, string label, Color backgroundColor)
    {
        var rt = CreateRect(parent, name);
        var image = rt.gameObject.AddComponent<Image>();
        image.color = backgroundColor;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
        button.colors = colors;

        Text text = CreateText(rt, "Label", label, 28, Color.white, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
        return button;
    }

    static void PlaceTop(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(14f, -(top + height));
        rt.offsetMax = new Vector2(-14f, -top);
    }

    static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    static Font GetFont()
    {
        if (cachedFont != null)
        {
            return cachedFont;
        }
        try
        {
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (Exception)
        {
            cachedFont = null;
        }
        if (cachedFont == null)
        {
            try
            {
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (Exception)
            {
                cachedFont = null;
            }
        }
        return cachedFont;
    }
}
