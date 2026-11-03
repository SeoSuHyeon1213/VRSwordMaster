using UnityEngine;

public static class MusicSelection
{
    public static AudioClip SelectedClip { get; private set; }
    public static string SelectedTitle { get; private set; }

    public static void Select(AudioClip clip, string title)
    {
        SelectedClip = clip;
        SelectedTitle = string.IsNullOrWhiteSpace(title) && clip != null ? clip.name : title;
    }

    public static void Clear()
    {
        SelectedClip = null;
        SelectedTitle = null;
    }
}
