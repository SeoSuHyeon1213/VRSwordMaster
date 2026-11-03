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
        Color color = spawnRed ? Color.red : Color.blue;
        int layer = spawnRed ? layerR : layerB;

        if (pool == null || pool.Length == 0) return;
        if (point == null || point.Length == 0) return;

        int i = Random.Range(0, pool.Length);
        int p = Random.Range(0, point.Length);
        GameObject obj = Instantiate(pool[i], point[p]);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.Rotate(transform.forward, 90 * Random.Range(0, 4));

        SetLayerRecursively(obj, layer);

        var bindings = obj.GetComponentInChildren<MaterialBindings>();
        if (bindings != null)
        {
            bindings.SetBaseColor(color);
            bindings.SetEmmisiveColor(color);
        }
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
