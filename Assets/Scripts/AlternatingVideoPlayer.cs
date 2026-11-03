using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class AlternatingVideoPlayer : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private VideoClip[] clips;
    [SerializeField] private float switchInterval = 60f;

    [Header("Fever Time")]
    [SerializeField] private VideoClip feverClip;
    [SerializeField] private int feverComboThreshold = 10;

    private float elapsed;
    private int currentIndex;
    private bool isFever;
    private double resumeTime;

    private void Awake()
    {
        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }
    }

    private void OnEnable()
    {
        elapsed = 0f;
        isFever = false;

        if (videoPlayer != null && clips != null && clips.Length > 0 && clips[0] != null)
        {
            currentIndex = 0;
            PlayClip(clips[currentIndex], 0);
        }
    }

    private void Update()
    {
        // 게임 오버로 일시정지된 동안에는 배경을 바꾸지 않음
        if (videoPlayer == null || Time.timeScale == 0f)
        {
            return;
        }

        UpdateFever();
        if (isFever)
        {
            return;
        }

        if (clips == null || clips.Length < 2 || switchInterval <= 0f)
        {
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed < switchInterval)
        {
            return;
        }

        elapsed = 0f;
        currentIndex = GetNextPlayableIndex(currentIndex);
        PlayClip(clips[currentIndex], 0);
    }

    private void UpdateFever()
    {
        if (feverClip == null || ComboManager.Instance == null)
        {
            return;
        }

        bool shouldFever = ComboManager.Instance.CurrentCombo >= feverComboThreshold;
        if (shouldFever == isFever)
        {
            return;
        }

        isFever = shouldFever;
        if (isFever)
        {
            // 피버 종료 후 원래 배경을 이어서 재생하기 위해 위치 저장
            resumeTime = videoPlayer.time;
            PlayClip(feverClip, 0);
        }
        else if (clips != null && currentIndex < clips.Length)
        {
            PlayClip(clips[currentIndex], resumeTime);
        }
    }

    private int GetNextPlayableIndex(int startIndex)
    {
        for (int offset = 1; offset <= clips.Length; offset++)
        {
            int index = (startIndex + offset) % clips.Length;
            if (clips[index] != null)
            {
                return index;
            }
        }

        return startIndex;
    }

    private void PlayClip(VideoClip clip, double startTime)
    {
        if (clip == null)
        {
            return;
        }

        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.isLooping = true;
        videoPlayer.time = startTime;
        videoPlayer.Play();
    }
}
