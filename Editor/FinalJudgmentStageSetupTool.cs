#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// 최종 판정 화면에 초상화 큐브 무대를 세운다.
    ///
    /// 하는 일:
    ///  1. 예전 연출 잔재(FinalCandidateStage · FinalSpotlight)를 지운다.
    ///  2. PanelFinal 맨 앞에 어둠 판 + 큐브가 그려질 RawImage를 만들고 디렉터에 연결한다.
    ///  3. 큐브 전용 레이어를 만들고 메인 카메라 시야에서 뺀다 —
    ///     큐브는 전용 카메라가 렌더 텍스처에 담을 뿐, 화면에 직접 보이면 안 된다.
    ///
    /// 여러 번 눌러도 안전하다. 씬 구조를 한 번 바꾸는 도구이므로,
    /// 적용하고 씬을 저장한 뒤에는 지워도 된다.
    /// </summary>
    public static class FinalJudgmentStageSetupTool
    {
        private const string StageName = "FinalCubeStage";
        private const string ViewName = "CubeView";
        private const string CubeLayer = "ZJFinalCube";

        // 큐브가 놓일 자리. 위로는 딜러 판정문(y 310~358), 아래로는 최종 결과 카드
        // 사이에 들어간다.
        private static readonly Vector2 ViewPos = new Vector2(0f, 150f);
        private static readonly Vector2 ViewSize = new Vector2(300f, 300f);

        [MenuItem("ZooJack/최종 판정/초상화 큐브 무대 만들기")]
        public static void Setup()
        {
            var director = Object.FindFirstObjectByType<GameDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Fail("열려 있는 씬에서 GameDirector를 찾지 못했습니다. GameScene을 먼저 여세요.");
                return;
            }

            var so = new SerializedObject(director);
            var panelFinal = so.FindProperty("panelFinal").objectReferenceValue as GameObject;
            if (panelFinal == null)
            {
                Fail("GameDirector에 panelFinal이 연결돼 있지 않습니다.");
                return;
            }

            int layer = EnsureCubeLayer();
            if (layer < 0)
            {
                Fail("빈 레이어 슬롯이 없어 큐브 전용 레이어를 만들지 못했습니다.");
                return;
            }
            HideLayerFromSceneCameras(layer);

            var panel = (RectTransform)panelFinal.transform;
            RemoveLeftovers(panel);

            var cube = EnsureStage(panel);
            so.FindProperty("finalCube").objectReferenceValue = cube;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(director);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);

            Debug.Log("[FinalJudgmentStageSetupTool] 초상화 큐브 무대 완료. 씬을 저장하세요.");
            Selection.activeObject = cube.gameObject;
        }

        // 예전 연출이 쓰던 오브젝트. 남아 있으면 큐브 뒤에서 같이 그려진다.
        private static void RemoveLeftovers(RectTransform panel)
        {
            foreach (var name in new[] { "FinalCandidateStage", "FinalSpotlight" })
            {
                var old = panel.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }
        }

        private static FinalJudgmentCube EnsureStage(RectTransform panel)
        {
            var stage = panel.Find(StageName) as RectTransform;
            if (stage == null)
            {
                var go = new GameObject(StageName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "큐브 무대 생성");
                Undo.SetTransformParent(go.transform, panel, "큐브 무대 배치");
                stage = (RectTransform)go.transform;
            }

            stage.localScale = Vector3.one;
            stage.anchorMin = Vector2.zero;
            stage.anchorMax = Vector2.one;
            stage.offsetMin = Vector2.zero;
            stage.offsetMax = Vector2.zero;
            // 맨 앞에 둬야 판정 문구·버튼은 어둠 위에 밝게 남는다.
            stage.SetSiblingIndex(0);

            // 화면을 어둡게 깔지 않는다. 큐브는 밝은 테이블 위에서도 충분히 읽히고,
            // 어둠은 스포트라이트 연출에 딸린 것이었다.
            var leftoverDim = stage.GetComponent<Image>();
            if (leftoverDim != null) Undo.DestroyObjectImmediate(leftoverDim);

            var view = stage.Find(ViewName) as RectTransform;
            if (view == null)
            {
                var go = new GameObject(ViewName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "큐브 화면 생성");
                Undo.SetTransformParent(go.transform, stage, "큐브 화면 배치");
                view = (RectTransform)go.transform;
            }
            view.localScale = Vector3.one;
            view.anchorMin = view.anchorMax = view.pivot = new Vector2(0.5f, 0.5f);
            view.anchoredPosition = ViewPos;
            view.sizeDelta = ViewSize;

            var raw = view.GetComponent<RawImage>();
            if (raw == null) raw = Undo.AddComponent<RawImage>(view.gameObject);
            raw.color = Color.white;
            raw.raycastTarget = false;

            var cube = stage.GetComponent<FinalJudgmentCube>();
            if (cube == null) cube = Undo.AddComponent<FinalJudgmentCube>(stage.gameObject);

            var cubeSo = new SerializedObject(cube);
            cubeSo.FindProperty("view").objectReferenceValue = raw;
            // 머티리얼은 반드시 에셋으로 물린다. 코드에서 Shader.Find로 만들면
            // 빌드가 그 셰이더를 잘라내 큐브가 보이지 않는다.
            cubeSo.FindProperty("bodySource").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ZJ_CubeBody.mat");
            cubeSo.FindProperty("faceSource").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ZJ_CubeFace.mat");
            cubeSo.ApplyModifiedProperties();

            // 판정 단계에 들어갈 때만 켜진다.
            stage.gameObject.SetActive(false);
            EditorUtility.SetDirty(stage.gameObject);
            return cube;
        }

        // ── 레이어 ───────────────────────────────────────────────────

        /// <summary>큐브 전용 레이어를 찾거나 빈 슬롯에 만든다. 실패하면 -1.</summary>
        private static int EnsureCubeLayer()
        {
            int existing = LayerMask.NameToLayer(CubeLayer);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            // 0~7은 유니티 예약 구간이라 건드리지 않는다.
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = CubeLayer;
                tagManager.ApplyModifiedProperties();
                return i;
            }
            return -1;
        }

        // 큐브는 전용 카메라가 렌더 텍스처에 담는다. 씬의 다른 카메라까지 큐브를 보면
        // 화면 구석에 정체불명의 정육면체가 떠 있게 된다.
        private static void HideLayerFromSceneCameras(int layer)
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.targetTexture != null) continue; // 렌더 텍스처 전용 카메라는 그대로 둔다
                if ((cam.cullingMask & (1 << layer)) == 0) continue;

                Undo.RecordObject(cam, "큐브 레이어 제외");
                cam.cullingMask &= ~(1 << layer);
                EditorUtility.SetDirty(cam);
            }
        }

        private static void Fail(string message) =>
            EditorUtility.DisplayDialog("최종 판정 큐브", message, "확인");
    }
}
#endif
