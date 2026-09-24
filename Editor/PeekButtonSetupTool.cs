using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// 화면을 덮는 패널을 잠시 걷어내는 버튼(<see cref="PanelPeekToggle"/>)을 만들어
    /// 딜러 배분 화면과 고발 화면에 하나씩 붙인다.
    ///
    /// 생김새는 리그 오브 레전드의 증강 숨기기 버튼을 따랐다 — 가로로 누운 청록 알약에
    /// 은색 테두리, 가운데에 카드 아이콘만 있는 형태다. 글자가 없어야 패널을 걷어낸
    /// 뒤에도 화면에 남은 이 하나가 시선을 끌지 않는다.
    ///
    /// 여러 번 눌러도 안전하다 — 기존 버튼은 지우고 다시 만든다. 자리나 색을 바꿀 일이
    /// 생기면 씬이 아니라 여기를 고치는 편이 낫다. 씬에서 직접 고치면 다음에 이 도구를
    /// 돌렸을 때 조용히 사라진다.
    /// </summary>
    public static class PeekButtonSetupTool
    {
        private const string PrefabPath = "Assets/Prefabs/ZJ_PeekButton.prefab";
        private const string CardSpritePath =
            "Assets/ImportedAsset/kenney_boardgame-pack/PNG/Cards/cardBack_blue1.png";

        private const string ButtonName = "ZJ_PeekButton";

        // 버튼을 붙일 패널들. 둘 다 화면을 가득 덮으면서, 그 밑의 카드를 봐야 고를 수 있는 자리다.
        private static readonly string[] TargetPanels = { "PanelDealer", "PanelAccusation" };

        private const float ButtonWidth = 180f;
        private const float ButtonHeight = 56f;

        /// <summary>
        /// 패널 아래쪽 가운데. 딜러 화면의 확정 버튼(y 114~166)과 점수 카드(y 117~243)
        /// 아래로 빠지는 자리라 두 패널 어디에도 겹치지 않는다.
        /// </summary>
        private static readonly Vector2 ButtonPosition = new Vector2(0f, 76f);

        // 색은 참고한 버튼을 그대로 옮기지 않고 이 게임의 톤에 맞춰 어둡게 눌렀다.
        // 다만 테두리는 눌러서는 안 된다 — 패널을 걷어내면 화면에 이것 하나만 남으므로
        // 어두운 테이블 위에서도 경계가 보여야 다시 누를 자리를 찾는다.
        private static readonly Color Frame = ZooJackPalette.Hex(0x0A2730, 1f);      // 테두리 안쪽 띠(짙은 청록)
        private static readonly Color Fill = ZooJackPalette.Hex(0x1C6579, 1f);       // 안쪽 밝은 면
        private static readonly Color Border = ZooJackPalette.Hex(0xC9DDE3, 1f);     // 은색 테두리
        private static readonly Color PeekBorder = ZooJackPalette.Hex(0xEAF9FF, 1f); // 걷어낸 동안(더 밝게)
        private static readonly Color InnerLine = ZooJackPalette.Hex(0x3E93A6, 0.85f);
        private static readonly Color Shadow = ZooJackPalette.Shadow.Alpha(0.55f);
        private static readonly Color Icon = ZooJackPalette.Hex(0xDCF3F9, 1f);


        [MenuItem("ZooJack/게임/패널 숨기기 버튼 만들기")]
        public static void Setup()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("패널 숨기기 버튼",
                    "열려 있는 씬에 Canvas가 없습니다. GameScene을 먼저 여세요.", "확인");
                return;
            }

            var prefab = BuildPrefab();
            if (prefab == null) return;

            int placed = 0;
            foreach (var panelName in TargetPanels)
            {
                var panel = canvas.transform.Find(panelName);
                if (panel == null)
                {
                    Debug.LogWarning($"[PeekButtonSetupTool] {panelName}을(를) 찾지 못해 건너뜁니다.");
                    continue;
                }

                Place(prefab, panel);
                placed++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[PeekButtonSetupTool] 숨기기 버튼을 패널 {placed}곳에 붙였습니다.");
        }

        private static void Place(GameObject prefab, Transform panel)
        {
            // 이미 있으면 지우고 새로 만든다. 값이 반쯤 바뀐 옛 버튼이 남는 것보다 낫다.
            var existing = panel.Find(ButtonName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel);
            instance.name = ButtonName;

            var rect = (RectTransform)instance.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rect.anchoredPosition = ButtonPosition;

            // 패널의 맨 뒤로 보낸다. 걷어내는 동안 이것만 남으므로 가장 나중에 그려져야 한다.
            instance.transform.SetAsLastSibling();
        }

        // ── 프리팹 만들기 ────────────────────────────────────────────

        private static GameObject BuildPrefab()
        {
            var sprite = LoadSprite(CardSpritePath);
            if (sprite == null)
            {
                EditorUtility.DisplayDialog("패널 숨기기 버튼",
                    $"카드 뒷면 스프라이트를 찾지 못했습니다:\n{CardSpritePath}", "확인");
                return null;
            }

            var root = new GameObject(ButtonName, typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            var frame = root.AddComponent<RoundedPanelGraphic>();
            frame.Configure(
                background: Frame,
                outerBorder: Border,
                radius: 9f,
                outerThickness: 2.5f,
                innerBorder: InnerLine,
                innerInset: 3f,
                innerThickness: 1f,
                shadow: Shadow,
                shadowPosition: new Vector2(0f, -3f),
                shadowSize: 2f);
            frame.raycastTarget = true;

            var button = root.AddComponent<Button>();
            button.targetGraphic = frame;

            // 안쪽 밝은 면. 참고한 버튼의 그라데이션을 두 겹으로 흉내 낸 것이다 —
            // RoundedPanelGraphic은 단색만 칠하므로 밝은 면을 한 겹 더 얹어야 입체가 산다.
            var fill = CreateChild(root.transform, "Fill");
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(6f, 6f);
            fillRect.offsetMax = new Vector2(-6f, -6f);
            var fillGraphic = fill.AddComponent<RoundedPanelGraphic>();
            fillGraphic.Configure(
                background: Fill,
                outerBorder: new Color(0f, 0f, 0f, 0f),
                radius: 5f,
                outerThickness: 0f,
                innerBorder: new Color(0f, 0f, 0f, 0f),
                innerInset: 0f,
                innerThickness: 0f,
                shadow: new Color(0f, 0f, 0f, 0f),
                shadowPosition: Vector2.zero,
                shadowSize: 0f);
            fillGraphic.raycastTarget = false;

            // 가운데 카드 아이콘. 부채꼴로 펼친 석 장이라 글자 없이도 "카드를 본다"로 읽힌다.
            var icon = CreateChild(root.transform, "Icon");
            var iconRect = (RectTransform)icon.transform;
            iconRect.sizeDelta = new Vector2(52f, 32f);
            iconRect.anchoredPosition = new Vector2(0f, -1f);

            // 가운데 장을 마지막에 만들어 맨 위에 오게 한다.
            // 옆 장을 아래로 많이 내리면 부채가 버튼 바닥까지 처져 답답해 보인다.
            var left = CreateCard(icon.transform, sprite, "Card_L", new Vector2(-12f, -2f), 20f);
            var right = CreateCard(icon.transform, sprite, "Card_R", new Vector2(12f, -2f), -20f);
            var center = CreateCard(icon.transform, sprite, "Card_C", new Vector2(0f, 3f), 0f);

            var toggle = root.AddComponent<PanelPeekToggle>();
            var so = new SerializedObject(toggle);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("frame").objectReferenceValue = frame;
            var parts = so.FindProperty("iconParts");
            parts.arraySize = 3;
            parts.GetArrayElementAtIndex(0).objectReferenceValue = left;
            parts.GetArrayElementAtIndex(1).objectReferenceValue = right;
            parts.GetArrayElementAtIndex(2).objectReferenceValue = center;

            // 눌리지 않은 상태의 색을 여기서 함께 박아 둔다. PanelPeekToggle은 Awake에서
            // 이 값으로 테두리를 다시 칠하므로, 위에서 Configure한 색과 어긋나면
            // 게임을 켜는 순간 버튼 색이 한 번 바뀐다.
            so.FindProperty("idleBorder").colorValue = Border;
            so.FindProperty("peekBorder").colorValue = PeekBorder;
            so.FindProperty("idleIcon").colorValue = Icon;
            so.FindProperty("peekIcon").colorValue = Color.white;
            so.FindProperty("borderThickness").floatValue = 2.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreateCard(
            Transform parent, Sprite sprite, string name, Vector2 position, float angle)
        {
            var go = CreateChild(parent, name);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(19f, 25f);
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        // 카드 텍스처는 스프라이트가 하위 에셋으로 들어 있을 수 있어 둘 다 본다.
        private static Sprite LoadSprite(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (direct != null) return direct;

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) return sprite;
            return null;
        }
    }
}
