#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ZooJack.Editor
{
    /// <summary>
    /// 열려 있는 씬의 GameDirector에 베팅 칩 자리(ChipsA/ChipsB)를 만들고 배선한다.
    /// 이미 있으면 새로 만들지 않고 스프라이트·참조만 다시 맞춘다(여러 번 눌러도 안전).
    ///
    /// 자리 위치는 손패 영역 바로 아래로 잡아두므로, 테이블 그림의 원형 마킹에
    /// 정확히 맞추려면 씬 뷰에서 ChipsA/ChipsB를 조금씩 옮겨 마무리한다.
    /// </summary>
    public static class BetChipSetupTool
    {
        private const string ChipFolder = "Assets/ImportedAsset/kenney_boardgame-pack/PNG/Chips";

        // 손패 영역 중심에서 아래로 얼마나 내릴지(캔버스 px).
        // 테이블 배경의 원형 마킹 중심을 화면에서 재서 맞춘 값이다.
        private const float DropBelowHand = 181f;

        [MenuItem("ZooJack/Bet Chips/씬에 베팅 칩 자리 만들기")]
        public static void Setup()
        {
            var director = Object.FindFirstObjectByType<GameDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                EditorUtility.DisplayDialog("베팅 칩 설정",
                    "열려 있는 씬에서 GameDirector를 찾지 못했습니다. GameScene을 먼저 여세요.", "확인");
                return;
            }

            // GameDirector의 필드는 internal이라 에디터 어셈블리에서 직접 못 읽는다.
            // 전부 SerializedObject를 거쳐 접근한다.
            var so = new SerializedObject(director);
            var chipsA = EnsureStack(so, "chipsA", "ChipsA", "handLayoutA");
            var chipsB = EnsureStack(so, "chipsB", "ChipsB", "handLayoutB");
            so.ApplyModifiedProperties();

            int wired = 0;
            if (chipsA != null && ApplyTemplates(chipsA)) wired++;
            if (chipsB != null && ApplyTemplates(chipsB)) wired++;

            EditorUtility.SetDirty(director);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);

            Debug.Log($"[BetChipSetupTool] 베팅 칩 자리 {wired}곳 설정 완료. " +
                      "씬 뷰에서 ChipsA/ChipsB를 테이블 원형 마킹에 맞춘 뒤 씬을 저장하세요.");
            Selection.activeObject = chipsA != null ? chipsA.gameObject : director.gameObject;
        }

        // GameDirector의 필드에 이미 붙어 있으면 그대로 쓰고, 없으면 손패 옆에 새로 만든다.
        private static BetChipStackView EnsureStack(
            SerializedObject so, string fieldName, string objectName, string handFieldName)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[BetChipSetupTool] GameDirector에 '{fieldName}' 필드가 없습니다.");
                return null;
            }

            if (prop.objectReferenceValue is BetChipStackView existing) return existing;

            var handProp = so.FindProperty(handFieldName);
            var nearHand = handProp?.objectReferenceValue as Component;
            if (nearHand == null)
            {
                Debug.LogWarning(
                    $"[BetChipSetupTool] {objectName}: 기준이 될 손패 영역이 비어 있어 자리를 만들지 못했습니다. " +
                    $"GameDirector의 {handFieldName}를 먼저 연결하세요.");
                return null;
            }

            var handRect = nearHand.GetComponent<RectTransform>();
            var go = new GameObject(objectName, typeof(RectTransform), typeof(BetChipStackView));
            Undo.RegisterCreatedObjectUndo(go, "Create Bet Chip Stack");

            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(handRect.parent, false);
            // 손패 영역은 피벗이 좌/우 끝에 있을 수 있으므로 anchoredPosition을 그대로 베끼면
            // 안 된다. 월드 기준 '중심'을 구해 거기서 아래로 내린 뒤 position으로 되돌린다.
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(240f, 120f);
            rect.localScale = Vector3.one;

            var corners = new Vector3[4];
            handRect.GetWorldCorners(corners);
            Vector3 handCenter = (corners[0] + corners[2]) * 0.5f;
            rect.position = handCenter - new Vector3(0f, DropBelowHand * handRect.lossyScale.y, 0f);

            // 카드보다 뒤에 깔리도록 손패 영역 앞 순서에 둔다.
            rect.SetSiblingIndex(handRect.GetSiblingIndex());

            var view = go.GetComponent<BetChipStackView>();
            prop.objectReferenceValue = view;
            return view;
        }

        // 칩 색. 뇌물 프리셋 버튼(btnBribePreset0~3)과 똑같이 맞춘다. 같은 색이
        // 뇌물 패널과 테이블에서 다른 값을 뜻하면 플레이어가 값을 못 읽는다.
        // 세워 놓은 기둥으로 보여야 하므로 옆에서 본 _side 스프라이트를 쓴다.
        private const string Black = "chipBlackWhite_side"; // 100
        private const string Red   = "chipRedWhite_side";   // 50
        private const string Blue  = "chipBlueWhite_side";  // 25
        private const string Green = "chipGreenWhite_side"; // 10

        // 금액 구간별 칩 구성. 왼쪽 기둥부터 차례로 선다(비싼 색이 앞).
        private static readonly (string Label, int MinAmount, (string Sprite, int Count)[] Groups)[] Templates =
        {
            ("Green (1~200)",    0,   new[] { (Green, 5) }),
            ("Blue (201~400)",   201, new[] { (Green, 5), (Blue, 3) }),
            ("Red (401~600)",    401, new[] { (Red, 5), (Blue, 3), (Green, 2) }),
            ("Black (601~1000)", 601, new[] { (Black, 5), (Red, 4), (Blue, 3) })
        };

        private static bool ApplyTemplates(BetChipStackView view)
        {
            var so = new SerializedObject(view);
            var list = so.FindProperty("templates");
            if (list == null || !list.isArray) return false;

            list.arraySize = Templates.Length;
            for (int i = 0; i < Templates.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Label").stringValue = Templates[i].Label;
                element.FindPropertyRelative("MinAmount").intValue = Templates[i].MinAmount;

                var groups = element.FindPropertyRelative("Groups");
                var source = Templates[i].Groups;
                groups.arraySize = source.Length;
                for (int g = 0; g < source.Length; g++)
                {
                    var slot = groups.GetArrayElementAtIndex(g);
                    slot.FindPropertyRelative("Count").intValue = source[g].Count;

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ChipFolder}/{source[g].Sprite}.png");
                    if (sprite == null)
                    {
                        Debug.LogWarning($"[BetChipSetupTool] 칩 스프라이트를 찾지 못했습니다: {source[g].Sprite}.png");
                        continue;
                    }
                    slot.FindPropertyRelative("Chip").objectReferenceValue = sprite;
                }
            }

            ApplyLayout(so);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);
            return true;
        }

        // 옆에서 본 칩(_side, 64x38)에 맞는 배치값. 위에서 본 원형 칩 기준으로 잡혀 있던
        // 값이 남아 있으면 스프라이트가 찌그러지거나 기둥이 어긋나 보이므로 여기서 맞춘다.
        // 세부 조정은 이 툴을 다시 돌리지 말고 인스펙터에서 하면 된다.
        private static void ApplyLayout(SerializedObject so)
        {
            so.FindProperty("chipSize").vector2Value = new Vector2(64f, 38f);
            so.FindProperty("stackOffsetY").floatValue = 10f;  // 옆면 두께만큼만 올린다
            so.FindProperty("chipsPerStack").intValue = 10;    // 템플릿 최대 5장이라 안 쪼개짐
            // 기둥이 가장 많은 구간이 3개다. 52px면 총 168px라 베팅 원(약 250px) 안에
            // 여유 있게 들어간다. 살짝 겹치는 편이 칩을 늘어놓은 느낌도 난다.
            so.FindProperty("stackSpacingX").floatValue = 52f;
            so.FindProperty("jitter").floatValue = 1.5f;
        }
    }
}
#endif
