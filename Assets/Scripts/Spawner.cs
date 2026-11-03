using UnityEngine;
using VRBeats;

public class Spawner : MonoBehaviour
{
    [Header("Cubes")]
    public GameObject[] redCubes;
    public GameObject[] blueCubes;
    public Transform[] point;
    public AudioSource audioSource;
    public AudioClip[] playlist;
    public bool useSelectedMusic = true;

    private int currentTrackIndex = 0;

    [Header("Beat Detection")]
    [Tooltip("평균 에너지 대비 몇 배 이상일 때 비트로 감지할지 (값이 클수록 강한 비트만 감지)")]
    public float sensitivity = 3.5f;
    [Tooltip("비트 사이 최소 간격 (초). 너무 빠른 연속 스폰 방지")]
    public float minBeatInterval = 4.5f;
    [Tooltip("분석할 베이스 주파수 범위 상한 인덱스 (0~1023). DnB 킥드럼: 약 20)")]
    public int bassRangeEnd = 20;

    [Header("Beat Sage Chart")]
    [Tooltip("노트가 날아오는 속도 (m/s)")]
    public float noteSpeed = 2f;
    [Tooltip("플레이어 앞 몇 m 지점에서 노트를 치게 할지")]
    public float hitDistance = 1.2f;
    [Tooltip("4x3 격자의 가로 간격 (m)")]
    public float gridSpacingX = 0.45f;
    [Tooltip("4x3 격자의 세로 간격 (m)")]
    public float gridSpacingY = 0.4f;
    [Tooltip("격자 가장 아랫줄의 높이 (m)")]
    public float gridBottomHeight = 0.9f;

    private BeatChart currentChart;
    private int nextNoteIndex;
    private Vector3 hitCenter;
    private Vector3 towardPlayer;
    private Vector3 playerRight;
    private float appearDistance;

    private const int SpectrumSize = 1024;
    private const int HistorySize = 43;

    private float[] spectrum = new float[SpectrumSize];
    private float[] energyHistory = new float[HistorySize];
    private int historyIndex = 0;
    private float lastBeatTime = -999f;
    private float lastSpawnTime = -999f;
    private float nextEndCheckTime;

    private int layerR;
    private int layerB;
    private bool hasStartedTrack;

    void Start()
    {
        layerR = LayerMask.NameToLayer("R");
        layerB = LayerMask.NameToLayer("B");

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            Debug.LogWarning("Spawner needs an AudioSource.");
            return;
        }

        audioSource.Stop();

        AudioClip clipToPlay = null;
        if (useSelectedMusic && MusicSelection.SelectedClip != null)
        {
            clipToPlay = MusicSelection.SelectedClip;
            currentTrackIndex = FindPlaylistIndex(clipToPlay);
        }
        else if (playlist != null && playlist.Length > 0)
        {
            if (useSelectedMusic)
            {
                Debug.LogWarning("Selected music is empty. Playing the first playlist track instead.");
            }

            currentTrackIndex = Mathf.Clamp(currentTrackIndex, 0, playlist.Length - 1);
            clipToPlay = playlist[currentTrackIndex];
        }

        if (clipToPlay == null)
        {
            Debug.LogWarning("Spawner has no AudioClip to play. Assign a selected clip or playlist.");
            return;
        }

        PlayClip(clipToPlay);
    }

    void Update()
    {
        if (audioSource == null) return;

        if (hasStartedTrack && audioSource.clip != null && !audioSource.isPlaying && Time.time >= nextEndCheckTime)
        {
            bool reachedEnd = audioSource.time >= audioSource.clip.length - 0.15f;
            if (reachedEnd)
            {
                PlayNextPlaylistTrack();
                return;
            }
        }

        if (!audioSource.isPlaying)
            return;

        if (currentChart != null)
        {
            UpdateChart();
            return;
        }

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        float currentEnergy = 0f;
        for (int i = 0; i < bassRangeEnd; i++)
            currentEnergy += spectrum[i];

        float avgEnergy = 0f;
        for (int i = 0; i < HistorySize; i++)
            avgEnergy += energyHistory[i];
        avgEnergy /= HistorySize;

        energyHistory[historyIndex] = currentEnergy;
        historyIndex = (historyIndex + 1) % HistorySize;

        bool beatDetected = currentEnergy > avgEnergy * sensitivity
                            && Time.time - lastBeatTime > minBeatInterval;

        if (!beatDetected && Time.time - lastSpawnTime > Mathf.Max(minBeatInterval, 1f) && currentEnergy > 0.00001f)
        {
            beatDetected = true;
        }

        if (beatDetected)
        {
            SpawnCube();
            lastBeatTime = Time.time;
            lastSpawnTime = Time.time;
        }
    }

    int FindPlaylistIndex(AudioClip clip)
    {
        if (clip == null || playlist == null) return -1;

        for (int i = 0; i < playlist.Length; i++)
        {
            if (playlist[i] == clip)
            {
                return i;
            }
        }

        return -1;
    }

    void PlayNextPlaylistTrack()
    {
        if (playlist == null || playlist.Length == 0)
        {
            hasStartedTrack = false;
            Debug.LogWarning("Playlist is empty. No next track to play.");
            return;
        }

        if (currentTrackIndex < 0 || currentTrackIndex >= playlist.Length)
        {
            currentTrackIndex = 0;
        }
        else
        {
            currentTrackIndex = (currentTrackIndex + 1) % playlist.Length;
        }

        PlayClip(playlist[currentTrackIndex]);
    }

    void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            hasStartedTrack = false;
            return;
        }

        audioSource.clip = clip;
        audioSource.time = 0f;
        audioSource.Play();
        hasStartedTrack = true;
        nextEndCheckTime = Time.time + 0.5f;
        ResetBeatHistory();
        SetupChart(clip);
    }

    // 선택한 곡에 Beat Sage 채보가 있으면 채보 모드로 전환 (없으면 비트 감지 사용)
    void SetupChart(AudioClip clip)
    {
        currentChart = BeatChart.FindForClip(clip);
        nextNoteIndex = 0;
        if (currentChart == null) return;

        // 스포너가 바라보는 방향(-forward 반대)이 플레이어 쪽
        towardPlayer = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (towardPlayer.sqrMagnitude < 0.001f) towardPlayer = Vector3.back;
        playerRight = Vector3.Cross(Vector3.up, -towardPlayer).normalized;

        Vector3 playerPos = GetPlayerPosition();
        float playerToSpawner = Vector3.Dot(transform.position - playerPos, -towardPlayer);

        // XR Origin 위치를 바닥으로 보고 격자 높이를 계산
        hitCenter = playerPos - towardPlayer * hitDistance;
        appearDistance = Mathf.Max(playerToSpawner - hitDistance, 1f);

        Debug.Log($"[Spawner] Beat Sage chart: {currentChart.name} ({currentChart.difficulty}, {currentChart.notes.Count} notes)");
    }

    Vector3 GetPlayerPosition()
    {
        var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null) return origin.transform.position;

        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    void UpdateChart()
    {
        float songTime = (float)audioSource.timeSamples / audioSource.clip.frequency;
        float lookAhead = appearDistance / Mathf.Max(noteSpeed, 0.01f);

        while (nextNoteIndex < currentChart.notes.Count)
        {
            BeatChart.Note note = currentChart.notes[nextNoteIndex];
            float timeUntilHit = note.time - songTime;
            if (timeUntilHit > lookAhead) break;

            nextNoteIndex++;
            // 너무 늦은 노트(이미 지나간 시점)는 건너뜀
            if (timeUntilHit < -0.1f) continue;

            SpawnChartNote(note, timeUntilHit);
        }
    }

    void SpawnChartNote(BeatChart.Note note, float timeUntilHit)
    {
        bool red = note.type == 0;
        GameObject[] pool = red ? redCubes : blueCubes;
        if (pool == null || pool.Length == 0) return;

        Vector3 hitPos = hitCenter
            + playerRight * ((note.lineIndex - 1.5f) * gridSpacingX)
            + Vector3.up * (gridBottomHeight + note.lineLayer * gridSpacingY);

        // 노트가 타격 지점에 note.time 에 도착하도록 현재 거리만큼 뒤에 생성
        Vector3 spawnPos = hitPos - towardPlayer * (timeUntilHit * noteSpeed);

        // Saber 는 transform.up 의 반대 방향으로 벨 때 인정하므로 up = -베는 방향
        bool anyDirection = note.cutDirection == BeatChart.CutAny;
        Vector2 cut = anyDirection ? Vector2.down : CutDirectionToVector(note.cutDirection);
        Vector3 cutWorld = (playerRight * cut.x + Vector3.up * cut.y).normalized;
        Quaternion rotation = Quaternion.LookRotation(-towardPlayer, -cutWorld);

        GameObject obj = Instantiate(pool[Random.Range(0, pool.Length)], spawnPos, rotation, transform);
        ApplyNoteColor(obj, red);

        var cube = obj.GetComponentInChildren<Cube>();
        if (cube != null)
        {
            cube.speed = noteSpeed;
            cube.SetAnyDirection(anyDirection);
        }
    }

    static Vector2 CutDirectionToVector(int cutDirection)
    {
        switch (cutDirection)
        {
            case BeatChart.CutUp: return Vector2.up;
            case BeatChart.CutDown: return Vector2.down;
            case BeatChart.CutLeft: return Vector2.left;
            case BeatChart.CutRight: return Vector2.right;
            case BeatChart.CutUpLeft: return new Vector2(-1f, 1f);
            case BeatChart.CutUpRight: return new Vector2(1f, 1f);
            case BeatChart.CutDownLeft: return new Vector2(-1f, -1f);
            case BeatChart.CutDownRight: return new Vector2(1f, -1f);
            default: return Vector2.down;
        }
    }

    void ApplyNoteColor(GameObject obj, bool red)
    {
        SetLayerRecursively(obj, red ? layerR : layerB);

        var bindings = obj.GetComponentInChildren<MaterialBindings>();
        if (bindings != null)
        {
            Color color = red ? Color.red : Color.blue;
            bindings.SetBaseColor(color);
            bindings.SetEmmisiveColor(color);
        }
    }

    void ResetBeatHistory()
    {
        for (int i = 0; i < energyHistory.Length; i++)
        {
            energyHistory[i] = 0f;
        }

        historyIndex = 0;
        lastBeatTime = Time.time - minBeatInterval;
        lastSpawnTime = Time.time - Mathf.Max(minBeatInterval, 1f);
    }

    void SpawnCube()
    {
        bool spawnRed = Random.value > 0.5f;
        GameObject[] pool = spawnRed ? redCubes : blueCubes;

        if (pool == null || pool.Length == 0) return;
        if (point == null || point.Length == 0) return;

        int i = Random.Range(0, pool.Length);
        int p = Random.Range(0, point.Length);
        GameObject obj = Instantiate(pool[i], point[p]);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.Rotate(transform.forward, 90 * Random.Range(0, 4));

        ApplyNoteColor(obj, spawnRed);
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
