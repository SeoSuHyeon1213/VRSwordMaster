using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

[InitializeOnLoad]
public static class NeonFontSetup
{
    const string k_DoneKey  = "NeonFontSetup_Done";
    const string k_FontPath = "Assets/Fonts/Orbitron-Bold.ttf";
    const string k_TmpPath  = "Assets/Fonts/Orbitron-Bold SDF.asset";
    const string k_ScenePath = "Assets/Scenes/Intro.unity";

    static NeonFontSetup()
    {
        if (EditorPrefs.GetBool(k_DoneKey, false)) return;
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        var srcFont = AssetDatabase.LoadAssetAtPath<Font>(k_FontPath);
        if (srcFont == null)
        {
            Debug.LogError("[NeonFontSetup] Orbitron-Bold.ttf not found at " + k_FontPath);
            return;
        }

        // TMP Font Asset 생성 (없거나 아틀라스 텍스처가 빠진 경우)
        var tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(k_TmpPath);
        if (tmpFont != null && (tmpFont.atlasTextures == null || tmpFont.atlasTextures.Length == 0
                                || tmpFont.atlasTextures[0] == null || tmpFont.material == null))
        {
            Debug.LogWarning("[NeonFontSetup] Broken TMP Font Asset (missing atlas/material), recreating: " + k_TmpPath);
            AssetDatabase.DeleteAsset(k_TmpPath);
            tmpFont = null;
        }
        if (tmpFont == null)
        {
            tmpFont = TMP_FontAsset.CreateFontAsset(srcFont);
            AssetDatabase.CreateAsset(tmpFont, k_TmpPath);

            // 아틀라스 텍스처와 머티리얼을 서브 에셋으로 저장해야 빌드 시 null 이 되지 않음
            var atlas = tmpFont.atlasTextures[0];
            atlas.name = tmpFont.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, tmpFont);
            tmpFont.material.name = tmpFont.name + " Material";
            AssetDatabase.AddObjectToAsset(tmpFont.material, tmpFont);

            EditorUtility.SetDirty(tmpFont);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NeonFontSetup] TMP Font Asset created: " + k_TmpPath);
        }

        // Intro 씬을 열어 Game Start 버튼 텍스트에 폰트 적용
        bool sceneWasOpen = false;
        foreach (var s in UnityEngine.SceneManagement.SceneManager.GetAllScenes())
            if (s.path == k_ScenePath) { sceneWasOpen = true; break; }

        var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Additive);
        bool changed = false;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.text == "Game Start")
                {
                    tmp.font = tmpFont;
                    EditorUtility.SetDirty(tmp);
                    changed = true;
                    Debug.Log("[NeonFontSetup] Font applied to: " + tmp.gameObject.name);
                }
            }
        }

        if (changed) EditorSceneManager.SaveScene(scene);
        if (!sceneWasOpen) EditorSceneManager.CloseScene(scene, true);

        EditorPrefs.SetBool(k_DoneKey, true);
        Debug.Log("[NeonFontSetup] Done. Orbitron-Bold applied to Game Start button.");
    }

    // Unity 메뉴에서 수동으로 재실행 가능
    [MenuItem("Tools/Neon Font/재적용")]
    static void ReApply()
    {
        EditorPrefs.DeleteKey(k_DoneKey);
        Run();
    }

    [MenuItem("Tools/Neon Font/초기화")]
    static void Reset() => EditorPrefs.DeleteKey(k_DoneKey);
}
