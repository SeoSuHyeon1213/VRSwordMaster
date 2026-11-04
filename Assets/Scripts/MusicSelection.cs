using UnityEngine;

public static class MusicSelection
{
    public static AudioClip SelectedClip { get; private set; }
    public static string SelectedTitle { get; private set; }

    // 인트로 화면에 위에서부터 표시되는 순서
    public static readonly string[] Difficulties = { "Expert", "Hard" };

    private const string DifficultyPrefKey = "SelectedDifficulty";
    private static string selectedDifficulty;

    // 게임 오버 후 Clear() 를 거쳐도, 앱을 다시 켜도 고른 난이도를 유지한다
    public static string SelectedDifficulty
    {
        get
        {
            if (selectedDifficulty == null)
            {
                selectedDifficulty = PlayerPrefs.GetString(DifficultyPrefKey, Difficulties[0]);
                if (System.Array.IndexOf(Difficulties, selectedDifficulty) < 0)
                {
                    selectedDifficulty = Difficulties[0];
                }
            }

            return selectedDifficulty;
        }
        set
        {
            selectedDifficulty = value;
            PlayerPrefs.SetString(DifficultyPrefKey, value);
            PlayerPrefs.Save();
        }
    }

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
