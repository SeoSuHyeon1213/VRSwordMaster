using System;
using System.Collections.Generic;
using UnityEngine;

// Beat Sage(비트세이버 형식) 채보에서 가져온 노트 데이터.
// Tools > Beat Sage > Import Charts 메뉴가 Assets/Resources/Charts 에 생성한다.
[CreateAssetMenu(menuName = "Beat Sage/Beat Chart")]
public class BeatChart : ScriptableObject
{
    public const int CutUp = 0;
    public const int CutDown = 1;
    public const int CutLeft = 2;
    public const int CutRight = 3;
    public const int CutUpLeft = 4;
    public const int CutUpRight = 5;
    public const int CutDownLeft = 6;
    public const int CutDownRight = 7;
    public const int CutAny = 8;

    [Serializable]
    public struct Note
    {
        [Tooltip("곡 시작부터 노트를 쳐야 하는 시점 (초)")]
        public float time;
        [Tooltip("가로 위치 0~3 (플레이어 기준 왼쪽부터)")]
        public int lineIndex;
        [Tooltip("세로 위치 0~2 (아래부터)")]
        public int lineLayer;
        [Tooltip("0 = 빨강(왼손), 1 = 파랑(오른손)")]
        public int type;
        [Tooltip("베는 방향 0 위, 1 아래, 2 왼쪽, 3 오른쪽, 4 왼위, 5 오른위, 6 왼아래, 7 오른아래, 8 아무 방향")]
        public int cutDirection;
    }

    [Tooltip("이 채보와 함께 재생할 곡. 곡 선택 화면에서 같은 AudioClip 을 고르면 이 채보가 사용된다")]
    public AudioClip song;
    public string difficulty;
    public float bpm;
    public List<Note> notes = new List<Note>();

    static BeatChart[] cachedCharts;

    public static BeatChart FindForClip(AudioClip clip)
    {
        if (clip == null) return null;

        if (cachedCharts == null)
        {
            cachedCharts = Resources.LoadAll<BeatChart>("Charts");
        }

        foreach (var chart in cachedCharts)
        {
            if (chart != null && chart.song == clip) return chart;
        }

        // 곡 선택 화면이 다른 파일(예: Assets/Music 의 mp3)을 넘기는 경우 이름으로 찾음
        foreach (var chart in cachedCharts)
        {
            if (chart != null && string.Equals(chart.name, clip.name, StringComparison.OrdinalIgnoreCase)) return chart;
        }

        return null;
    }
}
