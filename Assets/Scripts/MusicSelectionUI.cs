using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] private float stickThreshold = 0.55f;
    [SerializeField] private float inputRepeatDelay = 0.32f;

    // 레이가 버튼을 가리키는 중인지 추적한다. 가리키는 동안의 트리거는 해당 버튼의 클릭으로 처리한다
    private class HoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public static int Count;
        private int pointers;

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointers++;
            Count++;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (pointers > 0)
            {
                pointers--;
                Count--;
            }
        }

        private void OnDisable()
        {
            Count -= pointers;
            pointers = 0;
        }
    }

    private enum Step
    {
        Start,
        Song,
        Difficulty,
    }

    private class DifficultyRow
    {
        public string difficulty;
        public Image image;
        public TextMeshProUGUI label;
    }

    private const string PanelName = "MusicSelectionPanel";
    private const string StartGroupName = "START";
    private const float CardSpacing = 430f;
    private static readonly Color CardColor = new Color(1f, 0.24842751f, 0.2580632f, 0.85f);
    private static readonly Color DimCardColor = new Color(1f, 0.24842751f, 0.2580632f, 0.3f);

    private RectTransform trackRoot;
    private readonly List<InputDevice> inputDevices = new();
    private readonly List<DifficultyRow> difficultyRows = new();
    private Step step;
    private Transform startGroup;
    private TMP_FontAsset uiFont;
    private float nextInputTime;
    private bool wasTriggerPressed;
    private bool wasPrimaryPressed;
    private bool wasBackPressed;
    private bool isLoading;

    private void Awake()
    {
        // Intro 에도 Spawner 가 있으므로, 이전 판의 선택이 남아 있으면 인트로에서 곡이 재생된다
        MusicSelection.Clear();
    }

    private void Start()
    {
        if (canvasRoot != null)
        {
            startGroup = canvasRoot.Find(StartGroupName);

            var sceneText = canvasRoot.GetComponentInChildren<TMP_Text>(true);
            if (sceneText != null)
            {
                uiFont = sceneText.font;
            }
        }

        // 이전 화면에서 누르고 있던 버튼이 바로 입력되지 않게 현재 상태로 시작
        ReadButtons(out wasTriggerPressed, out wasPrimaryPressed, out wasBackPressed);

        if (startGroup == null)
        {
            ShowSongStep();
            return;
        }

        // 씬의 START 버튼은 GameScene 을 바로 불러오므로 곡 선택으로 연결을 바꾼다
        var startButton = startGroup.GetComponentInChildren<Button>(true);
        if (startButton != null)
        {
            startButton.onClick = new Button.ButtonClickedEvent();
            startButton.onClick.AddListener(ShowSongStep);
            startButton.gameObject.AddComponent<HoverTarget>();
        }

        ShowStartStep();
    }

    private void ShowStartStep()
    {
        step = Step.Start;
        RemovePanels();
        trackRoot = null;
        startGroup.gameObject.SetActive(true);
    }

    private void ShowSongStep()
    {
        step = Step.Song;
        if (startGroup != null)
        {
            startGroup.gameObject.SetActive(false);
        }

        RebuildUI();
    }

    private void ShowDifficultyStep()
    {
        if (GetCurrentOption() == null)
        {
            return;
        }

        step = Step.Difficulty;
        BuildDifficultyPanel();
    }

    private void BuildDifficultyPanel()
    {
        RemovePanels();
        trackRoot = null;
        difficultyRows.Clear();

        Transform panelRoot = CreatePanelRoot();
        CreateTitle(panelRoot, $"<size=60%>{GetTitle(GetCurrentOption())}</size>\nSELECT DIFFICULTY");

        string[] difficulties = MusicSelection.Difficulties;
        const float rowSpacing = 112f;
        float top = -22f + (difficulties.Length - 1) * rowSpacing * 0.5f;

        for (int i = 0; i < difficulties.Length; i++)
        {
            string difficulty = difficulties[i];
            Image image = CreateCard(
                panelRoot,
                $"Difficulty_{difficulty}",
                new Vector2(380f, 96f),
                new Vector2(0f, top - i * rowSpacing),
                difficulty.ToUpperInvariant(),
                () =>
                {
                    MusicSelection.SelectedDifficulty = difficulty;
                    StartGame();
                });

            difficultyRows.Add(new DifficultyRow
            {
                difficulty = difficulty,
                image = image,
                label = image.GetComponentInChildren<TextMeshProUGUI>(),
            });
        }

        CreateHint(panelRoot, "STICK UP/DOWN : MOVE    TRIGGER : START    B/Y : BACK");
        RefreshDifficultyRows();
    }

    private void RefreshDifficultyRows()
    {
        foreach (DifficultyRow row in difficultyRows)
        {
            bool selected = row.difficulty == MusicSelection.SelectedDifficulty;
            string upper = row.difficulty.ToUpperInvariant();
            row.image.color = selected ? CardColor : DimCardColor;
            row.label.text = selected ? $"> {upper} <" : upper;
        }
    }

    // direction: -1 = 목록 위쪽, 1 = 목록 아래쪽. 끝에서 더 밀어도 순환하지 않는다
    private void MoveDifficulty(int direction)
    {
        string[] difficulties = MusicSelection.Difficulties;
        int current = System.Array.IndexOf(difficulties, MusicSelection.SelectedDifficulty);
        int next = Mathf.Clamp(current + direction, 0, difficulties.Length - 1);
        if (next == current)
        {
            return;
        }

        MusicSelection.SelectedDifficulty = difficulties[next];
        RefreshDifficultyRows();
    }

    private void Confirm()
    {
        switch (step)
        {
            case Step.Start:
                ShowSongStep();
                break;
            case Step.Song:
                ShowDifficultyStep();
                break;
            case Step.Difficulty:
                StartGame();
                break;
        }
    }

    private void GoBack()
    {
        if (step == Step.Difficulty)
        {
            ShowSongStep();
        }
        else if (step == Step.Song && startGroup != null)
        {
            ShowStartStep();
        }
    }

    private void StartGame()
    {
        MusicOption option = GetCurrentOption();
        if (isLoading || option == null)
        {
            return;
        }

        isLoading = true;
        MusicSelection.Select(option.clip, GetTitle(option));
        SceneManager.LoadScene(targetSceneName);
    }

    private MusicOption GetCurrentOption()
    {
        ClampSelectedIndex();
        if (options == null || options.Length == 0)
        {
            return null;
        }

        MusicOption option = options[selectedIndex];
        return option != null && option.clip != null ? option : null;
    }

    private static string GetTitle(MusicOption option)
    {
        return string.IsNullOrWhiteSpace(option.title) ? option.clip.name : option.title;
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
        ClampSelectedIndex();
        RemovePanels();

        Transform panelRoot = CreatePanelRoot();
        CreateTitle(panelRoot, "SELECT MUSIC");
        CreateCarousel(panelRoot);
        CreateHint(panelRoot, "STICK LEFT/RIGHT : MOVE    TRIGGER : SELECT    B/Y : BACK");
    }

    private Transform GetPanelParent()
    {
        if (canvasRoot == null)
        {
            return CreateStandaloneCanvas().transform;
        }

        EnsureCanvasRaycasters(canvasRoot.gameObject);
        return canvasRoot;
    }

    private void RemovePanels()
    {
        RemoveExistingPanels(GetPanelParent());
    }

    private Transform CreatePanelRoot()
    {
        var panelRoot = new GameObject(PanelName);
        panelRoot.transform.SetParent(GetPanelParent(), false);

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

        return panelRoot.transform;
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
                // Destroy 는 프레임 끝에 처리되므로 먼저 꺼서 바로 사라지게 한다
                child.gameObject.SetActive(false);
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

    private void CreateHint(Transform parent, string text)
    {
        var hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(parent, false);

        var hintRt = hintObj.AddComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, 18f);
        hintRt.sizeDelta = new Vector2(-32f, 40f);

        var hint = hintObj.AddComponent<TextMeshProUGUI>();
        hint.alignment = TextAlignmentOptions.Center;
        hint.fontSize = 20f;
        hint.color = new Color(1f, 1f, 1f, 0.75f);
        hint.raycastTarget = false;
        hint.text = text;
        ApplyFont(hint);
    }

    private void ApplyFont(TMP_Text text)
    {
        if (uiFont != null)
        {
            text.font = uiFont;
        }
    }

    private void CreateTitle(Transform parent, string text)
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
        titleText.raycastTarget = false;
        titleText.text = text;
        ApplyFont(titleText);
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

            int index = i;
            CreateCard(
                trackRoot,
                $"MusicCard_{i + 1}",
                new Vector2(380f, 160f),
                new Vector2(i * CardSpacing, 0f),
                GetTitle(option),
                () =>
                {
                    selectedIndex = index;
                    ShowDifficultyStep();
                });
        }
    }

    private Image CreateCard(
        Transform parent,
        string objectName,
        Vector2 size,
        Vector2 position,
        string title,
        UnityEngine.Events.UnityAction action)
    {
        var buttonObj = new GameObject(objectName);
        buttonObj.transform.SetParent(parent, false);

        var buttonRt = buttonObj.AddComponent<RectTransform>();
        buttonRt.sizeDelta = size;
        buttonRt.anchoredPosition = position;

        var image = buttonObj.AddComponent<Image>();
        image.sprite = buttonSprite;
        image.type = Image.Type.Simple;
        image.color = CardColor;
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

        button.onClick.AddListener(action);
        buttonObj.AddComponent<HoverTarget>();

        CreateButtonLabel(buttonObj.transform, title);
        return image;
    }

    private void HandleControllerInput()
    {
        if (!enableControllerInput || isLoading)
        {
            return;
        }

        ReadButtons(out bool trigger, out bool primary, out bool back);
        bool triggerDown = trigger && !wasTriggerPressed;
        bool primaryDown = primary && !wasPrimaryPressed;
        bool backDown = back && !wasBackPressed;
        wasTriggerPressed = trigger;
        wasPrimaryPressed = primary;
        wasBackPressed = back;

        // 레이로 버튼을 가리키고 있으면 트리거는 그 버튼의 클릭으로 처리된다
        if (primaryDown || (triggerDown && HoverTarget.Count == 0))
        {
            Confirm();
            return;
        }

        if (backDown)
        {
            GoBack();
            return;
        }

        if (Time.unscaledTime < nextInputTime)
        {
            return;
        }

        Vector2 stick = ReadStick();
        if (step == Step.Song && Mathf.Abs(stick.x) >= stickThreshold)
        {
            MoveSelection(stick.x > 0f ? 1 : -1);
            ConsumeControllerInput();
        }
        else if (step == Step.Difficulty && Mathf.Abs(stick.y) >= stickThreshold)
        {
            MoveDifficulty(stick.y > 0f ? -1 : 1);
            ConsumeControllerInput();
        }
    }

    private void ReadButtons(out bool trigger, out bool primary, out bool back)
    {
        trigger = false;
        primary = false;
        back = false;

        inputDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, inputDevices);

        for (int i = 0; i < inputDevices.Count; i++)
        {
            InputDevice device = inputDevices[i];
            trigger |= IsPressed(device, CommonUsages.triggerButton);
            primary |= IsPressed(device, CommonUsages.primaryButton);
            back |= IsPressed(device, CommonUsages.secondaryButton);
        }
    }

    // ReadButtons 가 같은 프레임에 채운 컨트롤러 목록에서 가장 크게 기울인 스틱 값을 읽는다
    private Vector2 ReadStick()
    {
        Vector2 stick = Vector2.zero;
        for (int i = 0; i < inputDevices.Count; i++)
        {
            if (inputDevices[i].TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis)
                && axis.sqrMagnitude > stick.sqrMagnitude)
            {
                stick = axis;
            }
        }

        return stick;
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
        buttonObj.AddComponent<HoverTarget>();

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
        label.raycastTarget = false;
        label.text = title;
        ApplyFont(label);
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
        label.raycastTarget = false;
        label.text = symbol;
        ApplyFont(label);
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
