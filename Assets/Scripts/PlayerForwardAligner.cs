using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

// Quest 의 바닥(Floor) 트래킹은 앱 실행 시 바라보는 방향이 아니라 가디언 기준 방향을 정면으로 쓰기 때문에
// 빌드에서 플레이어가 반대 방향(180도)을 보고 시작할 수 있다.
// 씬 로드와 재중심(Recenter) 시 카메라 시선을 씬에 배치된 XR Origin 의 정면으로 맞춘다.
public class PlayerForwardAligner : MonoBehaviour
{
    public static PlayerForwardAligner Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateInstance()
    {
        if (Instance != null) return;
        new GameObject("PlayerForwardAligner").AddComponent<PlayerForwardAligner>();
    }

    XROrigin origin;
    Vector3 targetForward;
    Vector3 targetPosition;
    readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();

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
        BindOrigin();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeRecenter();
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindOrigin();
    }

    void BindOrigin()
    {
        origin = FindAnyObjectByType<XROrigin>();
        if (origin == null) return;

        // 씬에 배치된 XR Origin 의 방향과 위치를 게임 기준 정면으로 사용
        targetForward = Vector3.ProjectOnPlane(origin.transform.forward, Vector3.up).normalized;
        if (targetForward.sqrMagnitude < 0.001f) targetForward = Vector3.forward;
        targetPosition = origin.transform.position;

        StopAllCoroutines();
        StartCoroutine(AlignWhenTracking());
        SubscribeRecenter();
    }

    IEnumerator AlignWhenTracking()
    {
        // 헤드셋 포즈가 들어올 때까지 대기 (최대 약 3초)
        for (int i = 0; i < 180; i++)
        {
            if (origin == null) yield break;
            if (origin.Camera != null && origin.Camera.transform.localPosition != Vector3.zero) break;
            yield return null;
        }

        // 트래킹 시작 직후 포즈가 흔들리는 것을 피하기 위해 한 프레임 더 대기
        yield return null;
        Align();
    }

    void Align()
    {
        if (origin == null || origin.Camera == null) return;

        origin.MatchOriginUpCameraForward(Vector3.up, targetForward);

        // 플레이어 위치도 씬에 배치된 위치로 맞춤 (높이는 트래킹 값 유지)
        Vector3 cameraPos = origin.Camera.transform.position;
        origin.MoveCameraToWorldLocation(new Vector3(targetPosition.x, cameraPos.y, targetPosition.z));
    }

    void SubscribeRecenter()
    {
        UnsubscribeRecenter();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var subsystem in subsystems)
        {
            subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
        }
    }

    void UnsubscribeRecenter()
    {
        foreach (var subsystem in subsystems)
        {
            if (subsystem != null) subsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
        }
        subsystems.Clear();
    }

    void OnTrackingOriginUpdated(XRInputSubsystem subsystem)
    {
        StopAllCoroutines();
        StartCoroutine(AlignWhenTracking());
    }
}
