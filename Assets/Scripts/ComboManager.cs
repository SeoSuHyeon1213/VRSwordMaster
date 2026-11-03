using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateInstance()
    {
        if (Instance != null) return;
        new GameObject("ComboManager").AddComponent<ComboManager>();
    }

    static readonly Color ColorNormal = Color.yellow;
    static readonly Color ColorHigh = new Color(1f, 0.5f, 0f);
    static readonly Color ColorMax = Color.red;

    [SerializeField] private Vector3 canvasOffsetFromCamera = new Vector3(0f, -0.15f, 3.2f);
    [SerializeField] private Vector2 canvasSize = new Vector2(520f, 160f);
    [SerializeField] private float canvasScale = 0.004f;

    Canvas comboCanvas;
    TextMeshProUGUI comboText;
    TextMeshProUGUI fallbackComboText;
    Text legacyComboText;
    RectTransform comboTargetTransform;
    bool usingSceneComboTarget;
    int combo;
    public int CurrentCombo => combo;
    public int BestCombo { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindComboTargetForScene(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void CreateComboCanvas()
    {
        var canvasObj = new GameObject("ComboCanvas");
        canvasObj.transform.SetParent(transform, false);

        comboCanvas = canvasObj.AddComponent<Canvas>();
        comboCanvas.renderMode = RenderMode.WorldSpace;

        var canvasRt = canvasObj.GetComponent<RectTransform>();
        canvasRt.sizeDelta = canvasSize;
        canvasRt.localScale = Vector3.one * canvasScale;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        var textObj = new GameObject("ComboText");
        textObj.transform.SetParent(canvasObj.transform, false);

        var textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        comboText = textObj.AddComponent<TextMeshProUGUI>();
        fallbackComboText = comboText;

        var font = Resources.Load<TMP_FontAsset>("ThaleahFat");
        if (font != null)
            comboText.font = font;

        comboText.alignment = TextAlignmentOptions.Center;
        comboText.fontSize = 72f;
        comboText.color = ColorNormal;
        comboText.text = "";

        PlaceCanvasInWorld();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        combo = 0;
        if (scene.name == "GameScene")
        {
            BestCombo = 0;
        }

        BindComboTargetForScene(scene);
        UpdateUI();
        PlaceCanvasInWorld();
    }

    void BindComboTargetForScene(Scene scene)
    {
        comboText = null;
        legacyComboText = null;
        comboTargetTransform = null;
        usingSceneComboTarget = false;

        if (scene.name == "GameScene" && TryFindSceneComboTarget(out TextMeshProUGUI tmpText, out Text legacyText, out RectTransform targetTransform))
        {
            comboText = tmpText;
            legacyComboText = legacyText;
            comboTargetTransform = targetTransform;
            usingSceneComboTarget = true;

            if (comboCanvas != null)
            {
                comboCanvas.gameObject.SetActive(false);
            }

            return;
        }

        if (comboCanvas == null)
        {
            CreateComboCanvas();
        }

        comboText = fallbackComboText;

        if (comboCanvas != null)
        {
            comboCanvas.gameObject.SetActive(true);
        }
    }

    bool TryFindSceneComboTarget(out TextMeshProUGUI tmpText, out Text legacyText, out RectTransform targetTransform)
    {
        tmpText = null;
        legacyText = null;
        targetTransform = null;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            TextMeshProUGUI buttonTmpText = button.GetComponentInChildren<TextMeshProUGUI>(true);
            Text buttonLegacyText = button.GetComponentInChildren<Text>(true);

            if (!HasComboName(button.gameObject)
                && !HasComboName(button.transform.parent != null ? button.transform.parent.gameObject : null)
                && !HasComboText(buttonTmpText)
                && !HasComboText(buttonLegacyText))
            {
                continue;
            }

            tmpText = buttonTmpText;
            legacyText = buttonLegacyText;
            targetTransform = button.GetComponent<RectTransform>();

            if (tmpText == null && legacyText == null)
            {
                tmpText = CreateComboTextOnButton(button.transform);
            }

            return true;
        }

        TextMeshProUGUI[] tmpTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            if (!HasComboName(tmpTexts[i].gameObject)
                && !HasComboName(tmpTexts[i].transform.parent != null ? tmpTexts[i].transform.parent.gameObject : null)
                && !HasComboText(tmpTexts[i]))
            {
                continue;
            }

            tmpText = tmpTexts[i];
            targetTransform = tmpText.rectTransform;
            return true;
        }

        Text[] legacyTexts = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < legacyTexts.Length; i++)
        {
            if (!HasComboName(legacyTexts[i].gameObject)
                && !HasComboName(legacyTexts[i].transform.parent != null ? legacyTexts[i].transform.parent.gameObject : null)
                && !HasComboText(legacyTexts[i]))
            {
                continue;
            }

            legacyText = legacyTexts[i];
            targetTransform = legacyText.rectTransform;
            return true;
        }

        return false;
    }

    bool HasComboName(GameObject obj)
    {
        return obj != null && obj.name.ToLowerInvariant().Contains("combo");
    }

    bool HasComboText(TMP_Text text)
    {
        return text != null && text.text.ToLowerInvariant().Contains("combo");
    }

    bool HasComboText(Text text)
    {
        return text != null && text.text.ToLowerInvariant().Contains("combo");
    }

    TextMeshProUGUI CreateComboTextOnButton(Transform buttonTransform)
    {
        var textObj = new GameObject("ComboText");
        textObj.transform.SetParent(buttonTransform, false);

        var textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var text = textObj.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 42f;
        text.color = ColorNormal;

        var font = Resources.Load<TMP_FontAsset>("ThaleahFat");
        if (font != null)
        {
            text.font = font;
        }

        return text;
    }

    void PlaceCanvasInWorld()
    {
        if (usingSceneComboTarget || comboCanvas == null) return;

        Camera cam = Camera.main;
        if (cam == null) cam = FindAnyObjectByType<Camera>();

        if (cam == null)
        {
            comboCanvas.transform.SetPositionAndRotation(
                new Vector3(0f, 1.6f, 3.2f),
                Quaternion.Euler(0f, 180f, 0f));
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 position = cam.transform.position
            + right * canvasOffsetFromCamera.x
            + Vector3.up * canvasOffsetFromCamera.y
            + forward * canvasOffsetFromCamera.z;

        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        comboCanvas.transform.SetPositionAndRotation(position, rotation);
    }

    public void AddCombo()
    {
        combo++;
        if (combo > BestCombo) BestCombo = combo;
        UpdateUI();
        StopAllCoroutines();
        StartCoroutine(PunchScale());
    }

    public void RegisterMiss()
    {
        combo = 0;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (comboText == null && legacyComboText == null) return;

        Color color = combo >= 10 ? ColorMax : combo >= 5 ? ColorHigh : ColorNormal;
        SetComboColor(color);
        SetComboText($"Combo : {combo}");
    }

    void SetComboText(string text)
    {
        if (comboText != null) comboText.text = text;
        if (legacyComboText != null) legacyComboText.text = text;
    }

    void SetComboColor(Color color)
    {
        if (comboText != null) comboText.color = color;
        if (legacyComboText != null) legacyComboText.color = color;
    }

    IEnumerator PunchScale()
    {
        RectTransform target = comboTargetTransform != null ? comboTargetTransform : comboText != null ? comboText.rectTransform : null;
        if (target == null) yield break;

        Vector3 originalScale = target.localScale;
        target.localScale = originalScale * 1.18f;
        yield return new WaitForSeconds(0.12f);
        target.localScale = originalScale;
    }
}
