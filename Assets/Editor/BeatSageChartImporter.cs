using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Assets/Charts/<곡이름>/ 에 풀어 둔 Beat Sage zip(Info.dat, 난이도 .dat, song.egg)을
// BeatChart 에셋(Assets/Resources/Charts/<곡이름>.asset)으로 변환한다.
public static class BeatSageChartImporter
{
    const string k_ChartsRoot = "Assets/Charts";
    const string k_OutputRoot = "Assets/Resources/Charts";

    static readonly string[] k_DifficultyOrder = { "ExpertPlus", "Expert", "Hard", "Normal", "Easy" };

    // ---- Info.dat (v2) ----
    [Serializable] class InfoDat
    {
        public float _beatsPerMinute;
        public float _songTimeOffset;
        public string _songFilename;
        public BeatmapSet[] _difficultyBeatmapSets;
    }

    [Serializable] class BeatmapSet
    {
        public string _beatmapCharacteristicName;
        public BeatmapInfo[] _difficultyBeatmaps;
    }

    [Serializable] class BeatmapInfo
    {
        public string _difficulty;
        public string _beatmapFilename;
    }

    // ---- 난이도 .dat (v2 와 v3 를 모두 읽기 위해 필드를 함께 둠) ----
    [Serializable] class BeatmapDat
    {
        public NoteV2[] _notes;
        public NoteV3[] colorNotes;
    }

    [Serializable] class NoteV2
    {
        public float _time;
        public int _lineIndex;
        public int _lineLayer;
        public int _type;
        public int _cutDirection;
    }

    [Serializable] class NoteV3
    {
        public float b;
        public int x;
        public int y;
        public int c;
        public int d;
    }

    [MenuItem("Tools/Beat Sage/Import Charts")]
    public static void ImportAll()
    {
        if (!AssetDatabase.IsValidFolder(k_ChartsRoot))
        {
            EditorUtility.DisplayDialog("Beat Sage", $"{k_ChartsRoot} 폴더가 없습니다.\nBeat Sage zip 을 {k_ChartsRoot}/<곡이름>/ 에 풀어 주세요.", "OK");
            return;
        }

        int imported = 0;
        foreach (string folder in AssetDatabase.GetSubFolders(k_ChartsRoot))
        {
            try
            {
                if (ImportFolder(folder)) imported++;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BeatSage] {folder} 가져오기 실패: {e}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[BeatSage] 채보 {imported}개를 가져왔습니다.");
    }

    static bool ImportFolder(string folder)
    {
        string infoPath = Directory.GetFiles(folder)
            .FirstOrDefault(p => Path.GetFileName(p).Equals("Info.dat", StringComparison.OrdinalIgnoreCase));
        if (infoPath == null)
        {
            Debug.LogWarning($"[BeatSage] Info.dat 이 없어 건너뜁니다: {folder}");
            return false;
        }

        var info = JsonUtility.FromJson<InfoDat>(File.ReadAllText(infoPath));
        if (info == null || info._beatsPerMinute <= 0f || info._difficultyBeatmapSets == null)
        {
            Debug.LogWarning($"[BeatSage] 지원하지 않는 Info.dat 형식입니다 (v2 만 지원): {infoPath}");
            return false;
        }

        List<BeatmapInfo> beatmaps = PickBeatmaps(info);
        if (beatmaps.Count == 0)
        {
            Debug.LogWarning($"[BeatSage] 난이도 정보가 없습니다: {infoPath}");
            return false;
        }

        var levels = new List<BeatChart.Level>();
        foreach (BeatmapInfo beatmap in beatmaps)
        {
            levels.Add(new BeatChart.Level
            {
                difficulty = beatmap._difficulty,
                notes = ReadNotes(Path.Combine(folder, beatmap._beatmapFilename), info),
            });
        }

        AudioClip song = ImportSong(folder, info._songFilename);

        string songName = Path.GetFileName(folder);
        EnsureFolder(k_OutputRoot);
        string outPath = $"{k_OutputRoot}/{songName}.asset";

        var chart = AssetDatabase.LoadAssetAtPath<BeatChart>(outPath);
        bool isNew = chart == null;
        if (isNew) chart = ScriptableObject.CreateInstance<BeatChart>();

        // 사용자가 곡을 다른 AudioClip(예: 원본 mp3)으로 바꿔 둔 경우 유지
        if (chart.song == null) chart.song = song;
        chart.bpm = info._beatsPerMinute;
        chart.levels = levels;

        if (isNew) AssetDatabase.CreateAsset(chart, outPath);
        EditorUtility.SetDirty(chart);

        string summary = string.Join(", ", levels.Select(l => $"{l.difficulty} {l.notes.Count}개"));
        Debug.Log($"[BeatSage] {songName}: {summary}, BPM {info._beatsPerMinute} -> {outPath}");
        return true;
    }

    static List<BeatChart.Note> ReadNotes(string beatmapPath, InfoDat info)
    {
        var data = JsonUtility.FromJson<BeatmapDat>(File.ReadAllText(beatmapPath));

        float secondsPerBeat = 60f / info._beatsPerMinute;
        var notes = new List<BeatChart.Note>();

        if (data._notes != null && data._notes.Length > 0)
        {
            foreach (var n in data._notes)
            {
                if (n._type != 0 && n._type != 1) continue; // 폭탄 등은 제외
                notes.Add(MakeNote(n._time, n._lineIndex, n._lineLayer, n._type, n._cutDirection, secondsPerBeat, info._songTimeOffset));
            }
        }
        else if (data.colorNotes != null)
        {
            foreach (var n in data.colorNotes)
            {
                notes.Add(MakeNote(n.b, n.x, n.y, n.c, n.d, secondsPerBeat, info._songTimeOffset));
            }
        }

        notes.Sort((a, b) => a.time.CompareTo(b.time));
        return notes;
    }

    static BeatChart.Note MakeNote(float beat, int x, int y, int type, int dir, float secondsPerBeat, float offset)
    {
        return new BeatChart.Note
        {
            time = beat * secondsPerBeat + offset,
            lineIndex = Mathf.Clamp(x, 0, 3),
            lineLayer = Mathf.Clamp(y, 0, 2),
            type = type,
            cutDirection = Mathf.Clamp(dir, 0, 8),
        };
    }

    // Standard 세트의 모든 난이도를 높은 난이도부터 돌려준다
    static List<BeatmapInfo> PickBeatmaps(InfoDat info)
    {
        var set = info._difficultyBeatmapSets.FirstOrDefault(s => s._beatmapCharacteristicName == "Standard")
                  ?? info._difficultyBeatmapSets.FirstOrDefault();
        if (set?._difficultyBeatmaps == null) return new List<BeatmapInfo>();

        return set._difficultyBeatmaps
            .OrderBy(b =>
            {
                int rank = Array.IndexOf(k_DifficultyOrder, b._difficulty);
                return rank < 0 ? k_DifficultyOrder.Length : rank;
            })
            .ToList();
    }

    // Unity 는 .egg 를 오디오로 인식하지 못하므로 .ogg 로 바꿔서 가져온다
    static AudioClip ImportSong(string folder, string songFilename)
    {
        if (string.IsNullOrEmpty(songFilename)) return null;

        string songPath = $"{folder}/{songFilename}";
        if (songPath.EndsWith(".egg", StringComparison.OrdinalIgnoreCase))
        {
            string oggPath = Path.ChangeExtension(songPath, ".ogg");
            if (File.Exists(songPath) && !File.Exists(oggPath))
            {
                File.Move(songPath, oggPath);
                if (File.Exists(songPath + ".meta")) File.Delete(songPath + ".meta");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            songPath = oggPath;
        }

        return AssetDatabase.LoadAssetAtPath<AudioClip>(songPath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
