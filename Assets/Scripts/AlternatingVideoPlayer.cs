using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class AlternatingVideoPlayer : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private VideoClip[] clips;
    [SerializeField] private float switchInterval = 60f;

    private float elapsed;
    private int currentIndex;

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

        if (videoPlayer != null && clips != null && clips.Length > 0 && clips[0] != null)
        {
            currentIndex = 0;
            PlayClip(currentIndex);
        }
    }

    private void Update()
    {
        if (videoPlayer == null || clips == null || clips.Length < 2 || switchInterval <= 0f)
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
        PlayClip(currentIndex);
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

    private void PlayClip(int index)
    {
        VideoClip clip = clips[index];
        if (clip == null)
        {
            return;
        }

        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.isLooping = true;
        videoPlayer.time = 0;
        videoPlayer.Play();
    }
}
