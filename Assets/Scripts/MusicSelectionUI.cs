using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

public class MusicSelectionUI : MonoBehaviour
{
    [System.Serializable]
    public class MusicOption
    {
        public string title;
        public AudioClip clip;
    }

    [SerializeField] private MusicOption[] options;
    [SerializeField] private RectTransform canvasRoot;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private string targetSceneName = "GameScene";
    [SerializeField] private Vector3 canvasPosition = new Vector3(0f, 1.35f, 2.45f);
    [SerializeField] private Vector3 canvasEulerAngles = Vector3.zero;
    [SerializeField] private Vector2 canvasSize = new Vector2(720f, 460f);
    [SerializeField] private float canvasScale = 0.0032f;
    [SerializeField] private int selectedIndex;
    [SerializeField] private bool enableControllerInput = true;
    [SerializeField] private bool buildPanelOnStart = false;
    [SerializeField] private float stickThreshold = 0.55f;
    [SerializeField] private float inputRepeatDelay = 0.32f;

    private const string PanelName = "MusicSelectionPanel";
    private const float CardSpacing = 430f;

    private RectTransform trackRoot;
    private readonly List<InputDevice> inputDevices = new();
    private float nextInputTime;

    private void Start()
    {
        if (buildPanelOnStart)
        {
            RebuildUI();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (trackRoot != null)
        {
            Vector2 target = GetTrackPosition();
            trackRoot.anchoredPosition = Vector2.Lerp(trackRoot.anchoredPosition, target, Time.deltaTime * 12f);
        }

        HandleControllerInput();
    }

    [ContextMenu("Build Music Selection Panel")]
    public void RebuildUI()
    {
        Transform parent = canvasRoot != null ? canvasRoot : null;
        ClampSelectedIndex();

        if (parent == null)
        {
            parent = CreateStandaloneCanvas().transform;
        }
        else
        {
            EnsureCanvasRaycasters(parent.gameObject);
        }

        RemoveExistingPanels(parent);

        var panelRoot = new GameObject(PanelName);
        panelRoot.transform.SetParent(parent, false);

        var rootRt = panelRoot.AddComponent<RectTransform>();
        rootRt.sizeDelta = canvasSize;
        rootRt.localScale = Vector3.one * canvasScale;
        rootRt.anchoredPosition = Vector2.zero;

        CreatePanel(panelRoot.transform);
        CreateNeonBorder(
            panelRoot.transform,
            "PanelNeonBorder",
            4f,
            18f,
            new Color(0.15f, 1f, 1f, 1f),
            new Color(0.1f, 0.8f, 1f, 0.32f));
        CreateTitle(panelRoot.transform);
        CreateCarousel(panelRoot.transform);
    }

    [ContextMenu("Remove Music Selection Panel")]
    public void RemoveGeneratedPanel()
    {
        Transform parent = canvasRoot != null ? canvasRoot : transform;
        RemoveExistingPanels(parent);
        trackRoot = null;
    }

    private void RemoveExistingPanels(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name == PanelName)
            {
                DestroyGeneratedObject(child.gameObject);
            }
        }
    }

    private void CreatePanel(Transform parent)
    {
        var panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(parent, false);

        var panelRt = panelObj.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        var image = panelObj.AddComponent<Image>();
        image.color = new Color(0.02f, 0.02f, 0.06f, 0.78f);
    }

    private void CreateTitle(Transform parent)
    {
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(parent, false);

        var titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -28f);
        titleRt.sizeDelta = new Vector2(-48f, 86f);

        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 42f;
        titleText.color = new Color(0.2f, 1f, 1f, 1f);
        titleText.text = "SELECT MUSIC";
    }

    private void CreateCarousel(Transform parent)
    {
        int count = options == null ? 0 : options.Length;
        CreateArrowButton(parent, "LeftArrow", "<", new Vector2(-300f, -20f), () => MoveSelection(-1));
        CreateArrowButton(parent, "RightArrow", ">", new Vector2(300f, -20f), () => MoveSelection(1));

        var viewportObj = new GameObject("MusicViewport");
        viewportObj.transform.SetParent(parent, false);

        var viewportRt = viewportObj.AddComponent<RectTransform>();
        viewportRt.sizeDelta = new Vector2(430f, 210f);
        viewportRt.anchoredPosition = new Vector2(0f, -22f);

        var maskImage = viewportObj.AddComponent<Image>();
        maskImage.color = new Color(0f, 0f, 0f, 0.001f);

        var mask = viewportObj.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var trackObj = new GameObject("MusicTrack");
        trackObj.transform.SetParent(viewportObj.transform, false);
        trackRoot = trackObj.AddComponent<RectTransform>();
        trackRoot.anchorMin = new Vector2(0.5f, 0.5f);
        trackRoot.anchorMax = new Vector2(0.5f, 0.5f);
        trackRoot.pivot = new Vector2(0.5f, 0.5f);
        trackRoot.sizeDelta = new Vector2(Mathf.Max(1, count) * CardSpacing, 190f);
        trackRoot.anchoredPosition = GetTrackPosition();

        for (int i = 0; i < count; i++)
        {
            MusicOption option = options[i];
            if (option == null || option.clip == null)
            {
                continue;
            }

            var buttonObj = new GameObject($"MusicCard_{i + 1}");
            buttonObj.transform.SetParent(trackRoot, false);

            var buttonRt = buttonObj.AddComponent<RectTransform>();
            buttonRt.sizeDelta = new Vector2(380f, 160f);
            buttonRt.anchoredPosition = new Vector2(i * CardSpacing, 0f);

            var image = buttonObj.AddComponent<Image>();
            image.sprite = buttonSprite;
            image.type = Image.Type.Simple;
            image.color = new Color(1f, 0.24842751f, 0.2580632f, 0.85f);
            image.raycastTarget = true;

            CreateNeonBorder(
                buttonObj.transform,
                "CardNeonBorder",
                4f,
                14f,
                new Color(0.1f, 1f, 1f, 1f),
                new Color(1f, 0.2f, 0.85f, 0.34f));

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9685534f, 0.3807206f, 0.38710162f, 1f);
            colors.pressedColor = new Color(1f, 0.2f, 0.8f, 1f);
            colors.selectedColor = new Color(0.70440245f, 0.35220113f, 0.6326825f, 1f);
            colors.disabledColor = new Color(0.3f, 0.3f, 0.5f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            string title = string.IsNullOrWhiteSpace(option.title) ? option.clip.name : option.title;
            AudioClip clip = option.clip;
            button.onClick.AddListener(() => SelectMusic(clip, title));

            CreateButtonLabel(buttonObj.transform, title);
        }
    }

    private void HandleControllerInput()
    {
        if (!enableControllerInput || Time.unscaledTime < nextInputTime)
        {
            return;
        }

        inputDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, inputDevices);

        for (int i = 0; i < inputDevices.Count; i++)
        {
            InputDevice device = inputDevices[i];
            if (!device.isValid)
            {
                continue;
            }

            if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                if (axis.x <= -stickThreshold)
                {
                    MoveSelection(-1);
                    ConsumeControllerInput();
                    return;
                }

                if (axis.x >= stickThreshold)
                {
                    MoveSelection(1);
                    ConsumeControllerInput();
                    return;
                }
            }

            if (IsPressed(device, CommonUsages.primaryButton)
                || IsPressed(device, CommonUsages.triggerButton)
                || IsPressed(device, CommonUsages.gripButton))
            {
                SelectCurrentMusic();
                ConsumeControllerInput();
                return;
            }
        }
    }

    private bool IsPressed(InputDevice device, InputFeatureUsage<bool> button)
    {
        return device.TryGetFeatureValue(button, out bool pressed) && pressed;
    }

    private void ConsumeControllerInput()
    {
        nextInputTime = Time.unscaledTime + inputRepeatDelay;
    }

    private void CreateArrowButton(Transform parent, string objectName, string symbol, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var buttonObj = new GameObject(objectName);
        buttonObj.transform.SetParent(parent, false);

        var buttonRt = buttonObj.AddComponent<RectTransform>();
        buttonRt.sizeDelta = new Vector2(96f, 96f);
        buttonRt.anchoredPosition = position;

        var image = buttonObj.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.type = Image.Type.Simple;
        image.color = new Color(1f, 0.24842751f, 0.2580632f, 0.85f);
        image.raycastTarget = true;

        CreateNeonBorder(
            buttonObj.transform,
            "ArrowNeonBorder",
            4f,
            12f,
            new Color(0.1f, 1f, 1f, 1f),
            new Color(1f, 0.2f, 0.85f, 0.32f));

        var button = buttonObj.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9685534f, 0.3807206f, 0.38710162f, 1f);
        colors.pressedColor = new Color(1f, 0.2f, 0.8f, 1f);
        colors.selectedColor = new Color(0.70440245f, 0.35220113f, 0.6326825f, 1f);
        colors.disabledColor = new Color(0.3f, 0.3f, 0.5f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        CreateArrowLabel(buttonObj.transform, symbol);
    }

    private void CreateButtonLabel(Transform parent, string title)
    {
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent, false);

        var labelRt = labelObj.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(18f, 0f);
        labelRt.offsetMax = new Vector2(-18f, 0f);

        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 40f;
        label.color = Color.white;
        label.text = title;
    }

    private void CreateArrowLabel(Transform parent, string symbol)
    {
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(parent, false);

        var labelRt = labelObj.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 52f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.text = symbol;
    }

    private void CreateNeonBorder(
        Transform parent,
        string objectName,
        float lineThickness,
        float glowThickness,
        Color lineColor,
        Color glowColor)
    {
        var borderObj = new GameObject(objectName);
        borderObj.transform.SetParent(parent, false);

        var borderRt = borderObj.AddComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = Vector2.zero;
        borderRt.offsetMax = Vector2.zero;

        CreateBorderLayer(borderObj.transform, "OuterGlow", glowThickness, 7f, glowColor);
        CreateBorderLayer(borderObj.transform, "InnerLine", lineThickness, 0f, lineColor);
    }

    private void CreateBorderLayer(Transform parent, string layerName, float thickness, float outset, Color color)
    {
        var layerObj = new GameObject(layerName);
        layerObj.transform.SetParent(parent, false);

        var layerRt = layerObj.AddComponent<RectTransform>();
        layerRt.anchorMin = Vector2.zero;
        layerRt.anchorMax = Vector2.one;
        layerRt.offsetMin = new Vector2(-outset, -outset);
        layerRt.offsetMax = new Vector2(outset, outset);

        CreateBorderLine(layerObj.transform, "Top", color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness));
        CreateBorderLine(layerObj.transform, "Bottom", color, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness));
        CreateBorderLine(layerObj.transform, "Left", color, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f));
        CreateBorderLine(layerObj.transform, "Right", color, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(thickness, 0f));
    }

    private void CreateBorderLine(
        Transform parent,
        string objectName,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta)
    {
        var lineObj = new GameObject(objectName);
        lineObj.transform.SetParent(parent, false);

        var lineRt = lineObj.AddComponent<RectTransform>();
        lineRt.anchorMin = anchorMin;
        lineRt.anchorMax = anchorMax;
        lineRt.pivot = pivot;
        lineRt.sizeDelta = sizeDelta;
        lineRt.anchoredPosition = Vector2.zero;

        var image = lineObj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void MoveSelection(int direction)
    {
        int count = GetValidOptionCount();
        if (count <= 0)
        {
            return;
        }

        selectedIndex = (selectedIndex + direction + count) % count;

        if (trackRoot != null && !Application.isPlaying)
        {
            trackRoot.anchoredPosition = GetTrackPosition();
        }
    }

    private Vector2 GetTrackPosition()
    {
        return new Vector2(-selectedIndex * CardSpacing, 0f);
    }

    private void ClampSelectedIndex()
    {
        int count = GetValidOptionCount();
        if (count <= 0)
        {
            selectedIndex = 0;
            return;
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, count - 1);
    }

    private int GetValidOptionCount()
    {
        return options == null ? 0 : options.Length;
    }

    private void SelectCurrentMusic()
    {
        if (options == null || options.Length == 0)
        {
            return;
        }

        ClampSelectedIndex();

        MusicOption option = options[selectedIndex];
        if (option == null || option.clip == null)
        {
            return;
        }

        string title = string.IsNullOrWhiteSpace(option.title) ? option.clip.name : option.title;
        SelectMusic(option.clip, title);
    }

    private void SelectMusic(AudioClip clip, string title)
    {
        MusicSelection.Select(clip, title);
        SceneManager.LoadScene(targetSceneName);
    }

    private void AddTrackedDeviceRaycaster(GameObject canvasObj)
    {
        EnsureCanvasRaycasters(canvasObj);
    }

    private GameObject CreateStandaloneCanvas()
    {
        Transform existing = transform.Find("MusicSelectionCanvas");
        if (existing != null)
        {
            return existing.gameObject;
        }

        var canvasObj = new GameObject("MusicSelectionCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.SetPositionAndRotation(canvasPosition, Quaternion.Euler(canvasEulerAngles));
        canvasObj.transform.localScale = Vector3.one;

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRt = canvasObj.GetComponent<RectTransform>();
        canvasRt.sizeDelta = canvasSize;

        canvasObj.AddComponent<CanvasScaler>();
        EnsureCanvasRaycasters(canvasObj);

        return canvasObj;
    }

    private void EnsureCanvasRaycasters(GameObject canvasObj)
    {
        if (canvasObj.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        System.Type raycasterType = System.Type.GetType(
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");

        if (raycasterType != null && canvasObj.GetComponent(raycasterType) == null)
        {
            canvasObj.AddComponent(raycasterType);
        }
    }

    private void DestroyGeneratedObject(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
