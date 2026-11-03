using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.XR;

public class HealthManager : MonoBehaviour
{
    public static HealthManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateInstance()
    {
        if (Instance != null) return;
        new GameObject("HealthManager").AddComponent<HealthManager>();
    }

    public int maxHealth = 100;
    public int healAmount = 10;
    public int damageAmount = 20;

    int currentHealth;
    bool isGameOver;
    bool isResultOpen;
    bool isTransitioning;

    TextMeshPro hpText;
    Canvas resultCanvas;
    TextMeshProUGUI resultTitleText;
    TextMeshProUGUI bestComboText;
    TextMeshProUGUI currentComboText;
    Button restartButton;
    Button mainButton;
    Image restartButtonImage;
    Image mainButtonImage;
    Camera cachedCam;
    bool isGameplayScene;
    bool wasTriggerPressed;
    readonly List<InputDevice> inputDevices = new();

    static readonly WaitForSecondsRealtime Wait3sRealtime = new(3f);
    static readonly Color ColorHigh = Color.green;
    static readonly Color ColorMid = Color.yellow;
    static readonly Color ColorLow = Color.red;
    static readonly Color ButtonNormal = new(1f, 0.248f, 0.258f, 0.86f);
    static readonly Color ButtonHover = new(0.1f, 1f, 1f, 1f);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        currentHealth = maxHealth;
        isGameplayScene = SceneManager.GetActiveScene().name == "GameScene";
        CreateHPText();
        CreateResultCanvas();
        ControllerModeSwitcher.SetSwordMode(isGameplayScene);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResumeGameplay();
        cachedCam = null;
        isGameplayScene = scene.name == "GameScene";
        isGameOver = false;
        isResultOpen = false;
        isTransitioning = false;
        currentHealth = maxHealth;
        UpdateUI();
        ControllerModeSwitcher.SetSwordMode(isGameplayScene);

        if (resultCanvas != null)
        {
            resultCanvas.gameObject.SetActive(false);
        }
    }

    void CreateHPText()
    {
        var textObj = new GameObject("HPText");
        textObj.transform.SetParent(transform);
        hpText = textObj.AddComponent<TextMeshPro>();
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.fontSize = 4f;
        hpText.color = ColorHigh;
        UpdateUI();
    }

    void CreateResultCanvas()
    {
        var boldPixels = Resources.Load<TMP_FontAsset>("BoldPixels SDF");

        var canvasObj = new GameObject("ResultCanvas");
        canvasObj.transform.SetParent(transform);

        resultCanvas = canvasObj.AddComponent<Canvas>();
        resultCanvas.renderMode = RenderMode.WorldSpace;

        var canvasRt = canvasObj.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(620f, 420f);
        canvasRt.localScale = Vector3.one * 0.005f;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        AddTrackedDeviceRaycaster(canvasObj);

        var panelObj = new GameObject("Panel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        var panelRt = panelObj.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        var panelImg = panelObj.AddComponent<Image>();
        panelImg.color = new Color(0.02f, 0.02f, 0.06f, 0.84f);

        CreateNeonBorder(canvasObj.transform);

        resultTitleText = CreateText(canvasObj.transform, "ResultTitle", new Vector2(0f, 130f), new Vector2(580f, 90f), 66f, Color.red, boldPixels);
        bestComboText = CreateText(canvasObj.transform, "BestComboText", new Vector2(0f, 42f), new Vector2(560f, 60f), 42f, Color.white, boldPixels);
        currentComboText = CreateText(canvasObj.transform, "CurrentComboText", new Vector2(0f, -18f), new Vector2(560f, 60f), 42f, Color.white, boldPixels);

        restartButton = CreateButton(canvasObj.transform, "RestartButton", "RESTART", new Vector2(-145f, -128f), RestartGame, out restartButtonImage);
        mainButton = CreateButton(canvasObj.transform, "MainButton", "MAIN", new Vector2(145f, -128f), GoToIntro, out mainButtonImage);

        canvasObj.SetActive(false);
    }

    TextMeshProUGUI CreateText(Transform parent, string objectName, Vector2 position, Vector2 size, float fontSize, Color color, TMP_FontAsset font)
    {
        var textObj = new GameObject(objectName);
        textObj.transform.SetParent(parent, false);

        var rt = textObj.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = position;

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.color = color;
        if (font != null) text.font = font;
        return text;
    }

    Button CreateButton(Transform parent, string objectName, string label, Vector2 position, UnityEngine.Events.UnityAction action, out Image image)
    {
        var buttonObj = new GameObject(objectName);
        buttonObj.transform.SetParent(parent, false);

        var rt = buttonObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(230f, 72f);
        rt.anchoredPosition = position;

        image = buttonObj.AddComponent<Image>();
        image.color = ButtonNormal;

        var button = buttonObj.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(1f, 0.2f, 0.8f, 1f);
        colors.selectedColor = new Color(0.704f, 0.352f, 0.633f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        CreateSmallNeonBorder(buttonObj.transform);

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(buttonObj.transform, false);
        var labelRt = labelObj.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var text = labelObj.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 34f;
        text.color = Color.white;
        text.text = label;

        return button;
    }

    void CreateNeonBorder(Transform parent)
    {
        CreateBorder(parent, "ResultNeonBorder", 5f, 16f, new Color(0.1f, 1f, 1f, 1f), new Color(1f, 0.2f, 0.85f, 0.3f));
    }

    void CreateSmallNeonBorder(Transform parent)
    {
        CreateBorder(parent, "ButtonNeonBorder", 3f, 8f, new Color(0.1f, 1f, 1f, 1f), new Color(1f, 0.2f, 0.85f, 0.26f));
    }

    void CreateBorder(Transform parent, string objectName, float lineThickness, float glowThickness, Color lineColor, Color glowColor)
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

    void CreateBorderLayer(Transform parent, string layerName, float thickness, float outset, Color color)
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

    void CreateBorderLine(Transform parent, string objectName, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
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

    Camera GetCamera()
    {
        if (cachedCam != null) return cachedCam;
        cachedCam = Camera.main;
        if (cachedCam == null) cachedCam = FindAnyObjectByType<Camera>();
        return cachedCam;
    }

    void Update()
    {
        if (!isGameplayScene && !isResultOpen)
        {
            return;
        }

        Camera cam = GetCamera();
        if (cam == null) return;

        Vector3 basePos = cam.transform.position + cam.transform.forward * 3f;
        Quaternion rot = cam.transform.rotation;

        hpText.transform.SetPositionAndRotation(basePos + cam.transform.up * 1f, rot);
        resultCanvas.transform.SetPositionAndRotation(basePos, rot);
        resultCanvas.worldCamera = cam;

        if (isResultOpen)
        {
            HandleResultControllerSelection();
        }
    }

    public void Heal()
    {
        if (!isGameplayScene) return;
        if (isGameOver || isResultOpen) return;
        currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);
        UpdateUI();
    }

    public void TakeDamage()
    {
        if (!isGameplayScene) return;
        if (isGameOver || isResultOpen) return;
        currentHealth = Mathf.Max(currentHealth - damageAmount, 0);
        UpdateUI();
        if (currentHealth <= 0) StartCoroutine(GameOver());
    }

    public void ShowSongClear()
    {
        if (isGameOver || isResultOpen) return;
        ShowResult("SONG CLEAR", new Color(0.1f, 1f, 1f, 1f));
    }

    IEnumerator GameOver()
    {
        isGameOver = true;
        hpText.text = "";
        ShowResult("GAME OVER", Color.red);
        FreezeGameplay();

        // timeScale 이 0 이므로 실제 시간 기준으로 대기
        yield return Wait3sRealtime;

        if (isResultOpen)
        {
            yield break;
        }

        SceneManager.LoadScene("Intro");
    }

    // 게임 오버 시 노트 이동, 스폰, 음악, 배경 영상 등 모든 진행을 멈춘다
    void FreezeGameplay()
    {
        Time.timeScale = 0f;
        AudioListener.pause = true;

        foreach (var spawner in FindObjectsByType<Spawner>(FindObjectsSortMode.None))
        {
            spawner.enabled = false;
        }

        foreach (var video in FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None))
        {
            if (video.isPlaying) video.Pause();
        }
    }

    static void ResumeGameplay()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void ShowResult(string title, Color titleColor)
    {
        isResultOpen = true;
        wasTriggerPressed = IsAnyTriggerPressed();
        hpText.text = "";

        int current = ComboManager.Instance != null ? ComboManager.Instance.CurrentCombo : 0;
        int best = ComboManager.Instance != null ? ComboManager.Instance.BestCombo : 0;

        resultTitleText.text = title;
        resultTitleText.color = titleColor;
        bestComboText.text = $"Best Combo : {best}";
        currentComboText.text = $"Current Combo : {current}";
        resultCanvas.gameObject.SetActive(true);
        SetHoveredButton(null);
        ControllerModeSwitcher.SetSwordMode(false);
    }

    void RestartGame()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        isResultOpen = false;
        isGameOver = false;
        currentHealth = maxHealth;
        resultCanvas.gameObject.SetActive(false);
        SceneManager.LoadScene("GameScene");
    }

    void GoToIntro()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        isResultOpen = false;
        isGameOver = false;
        currentHealth = maxHealth;
        resultCanvas.gameObject.SetActive(false);
        MusicSelection.Clear();
        SceneManager.LoadScene("Intro");
    }

    void UpdateUI()
    {
        if (hpText == null) return;

        hpText.gameObject.SetActive(isGameplayScene && !isResultOpen && !isGameOver);
        if (!hpText.gameObject.activeSelf)
        {
            return;
        }

        float ratio = (float)currentHealth / maxHealth;
        int filled = Mathf.RoundToInt(ratio * 10);
        string bar = new string('|', filled) + new string('.', 10 - filled);

        hpText.color = ratio > 0.5f ? ColorHigh : ratio > 0.25f ? ColorMid : ColorLow;
        hpText.text = $"{bar}\n{currentHealth} / {maxHealth}";
    }

    void HandleResultControllerSelection()
    {
        Button hoveredButton = GetHoveredResultButton();
        SetHoveredButton(hoveredButton);

        bool isTriggerPressed = IsAnyTriggerPressed();
        if (hoveredButton != null && isTriggerPressed && !wasTriggerPressed)
        {
            hoveredButton.onClick.Invoke();
        }

        wasTriggerPressed = isTriggerPressed;
    }

    Button GetHoveredResultButton()
    {
        if (resultCanvas == null || !resultCanvas.gameObject.activeSelf)
        {
            return null;
        }

        inputDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, inputDevices);

        for (int i = 0; i < inputDevices.Count; i++)
        {
            InputDevice device = inputDevices[i];
            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position)
                || !device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                continue;
            }

            if (TryGetCanvasPoint(position, rotation * Vector3.forward, out Vector2 canvasPoint))
            {
                if (IsInsideButton(restartButton, canvasPoint)) return restartButton;
                if (IsInsideButton(mainButton, canvasPoint)) return mainButton;
            }
        }

        return null;
    }

    bool TryGetCanvasPoint(Vector3 origin, Vector3 direction, out Vector2 canvasPoint)
    {
        canvasPoint = Vector2.zero;
        if (direction.sqrMagnitude < 0.001f) return false;

        Plane plane = new(resultCanvas.transform.forward, resultCanvas.transform.position);
        Ray ray = new(origin, direction.normalized);
        if (!plane.Raycast(ray, out float distance) || distance < 0f || distance > 20f)
        {
            return false;
        }

        Vector3 localPoint = resultCanvas.transform.InverseTransformPoint(ray.GetPoint(distance));
        canvasPoint = new Vector2(localPoint.x, localPoint.y);
        return true;
    }

    bool IsInsideButton(Button button, Vector2 canvasPoint)
    {
        if (button == null || !button.interactable) return false;

        RectTransform rt = button.GetComponent<RectTransform>();
        Vector2 center = rt.anchoredPosition;
        Vector2 size = rt.sizeDelta;
        return canvasPoint.x >= center.x - size.x * 0.5f
            && canvasPoint.x <= center.x + size.x * 0.5f
            && canvasPoint.y >= center.y - size.y * 0.5f
            && canvasPoint.y <= center.y + size.y * 0.5f;
    }

    bool IsAnyTriggerPressed()
    {
        inputDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, inputDevices);

        for (int i = 0; i < inputDevices.Count; i++)
        {
            if (inputDevices[i].TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed)
            {
                return true;
            }
        }

        return false;
    }

    void SetHoveredButton(Button hoveredButton)
    {
        if (restartButtonImage != null)
        {
            restartButtonImage.color = hoveredButton == restartButton ? ButtonHover : ButtonNormal;
        }

        if (mainButtonImage != null)
        {
            mainButtonImage.color = hoveredButton == mainButton ? ButtonHover : ButtonNormal;
        }
    }

    void AddTrackedDeviceRaycaster(GameObject canvasObj)
    {
        System.Type raycasterType = System.Type.GetType(
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");

        if (raycasterType != null && canvasObj.GetComponent(raycasterType) == null)
        {
            canvasObj.AddComponent(raycasterType);
        }
    }
}
