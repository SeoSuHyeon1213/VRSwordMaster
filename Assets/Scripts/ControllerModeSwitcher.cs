using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// 플레이 중에는 검, 그 외(인트로/결과창)에는 일반 XR 컨트롤러 모델과 레이를 보여준다.
public static class ControllerModeSwitcher
{
    public static void SetSwordMode(bool useSword)
    {
        var origin = Object.FindAnyObjectByType<XROrigin>();
        if (origin == null) return;

        // Saber는 컨트롤러 자체에 붙어 있으므로 컴포넌트만 끄고, 자식의 검 모델을 켜고 끈다
        foreach (var saber in origin.GetComponentsInChildren<Saber>(true))
        {
            saber.enabled = useSword;
            foreach (Transform child in saber.transform)
            {
                if (child.name.Contains("Sword"))
                {
                    child.gameObject.SetActive(useSword);
                }
            }
        }

        foreach (var t in origin.GetComponentsInChildren<Transform>(true))
        {
            // 템플릿의 "Left/Right Controller Visual" 모델
            if (t.name.EndsWith("Controller Visual"))
            {
                t.gameObject.SetActive(!useSword);
            }
        }

        // 메뉴용 레이(Near-Far Interactor)는 컨트롤러 모드에서만 사용
        foreach (var interactor in origin.GetComponentsInChildren<NearFarInteractor>(true))
        {
            interactor.gameObject.SetActive(!useSword);
        }
    }
}
