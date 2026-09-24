#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    public static class EmotionWheelSetupTool
    {
        private const float WheelRadius = 220f;
        private const float IconRadius = 154f;

        [MenuItem("ZooJack/감정표현/선택 휠 만들기")]
        public static void Build()
        {
            Canvas canvas = null;
            var canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var candidate in canvases)
            {
                if (!candidate.isRootCanvas || !candidate.gameObject.scene.IsValid()) continue;
                canvas = candidate;
                if (candidate.name == "Canvas") break;
            }

            var catalog = EmotionCatalogAsset.Find();
            if (canvas == null || catalog == null)
            {
                Debug.LogError("[EmotionWheelSetup] 루트 Canvas 또는 EmotionCatalog를 찾지 못했습니다.");
                return;
            }

            var root = Ensure("ZJ_EmotionWheel", canvas.transform,
                typeof(CanvasGroup), typeof(EmotionWheelController));
            Stretch(root);
            root.SetAsLastSibling();
            RemoveNamedChildren(root, "DimBackground");

            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var wheel = Ensure("WheelRoot", root);
            Center(wheel, Vector2.one * WheelRadius * 2f);

            var ring = Ensure("RingGraphic", wheel, typeof(EmotionWheelArcGraphic));
            Center(ring, Vector2.one * WheelRadius * 2f);
            ring.SetSiblingIndex(0);
            var ringGraphic = ring.GetComponent<EmotionWheelArcGraphic>();
            // 다른 모달 카드에 쓰는 RoundedPanelGraphic의 기본 검정 반투명 배경과
            // 같은 색으로 맞춘다. 휠만 남색으로 떠 보이지 않게 한다.
            ringGraphic.color = new Color(0.025f, 0.032f, 0.03f, 0.985f);
            ringGraphic.Configure(84f, WheelRadius, 0f, 360f, 96);

            var highlight = Ensure("SelectionHighlight", wheel, typeof(EmotionWheelArcGraphic));
            Center(highlight, Vector2.one * WheelRadius * 2f);
            highlight.SetSiblingIndex(1);
            var highlightGraphic = highlight.GetComponent<EmotionWheelArcGraphic>();
            highlightGraphic.color = new Color(1f, 0.67f, 0.18f, 0.58f);
            highlightGraphic.Configure(84f, WheelRadius, 67.5f, 112.5f, 24);
            highlight.gameObject.SetActive(false);

            var iconsRoot = Ensure("EmotionIcons", wheel);
            Stretch(iconsRoot);
            iconsRoot.SetSiblingIndex(2);
            var icons = new Image[EmotionCatalog.WheelSlotCount];
            for (int i = 0; i < icons.Length; i++)
            {
                var icon = Ensure($"Emotion_{i}", iconsRoot, typeof(Image));
                Center(icon, new Vector2(88f, 88f));
                icon.SetSiblingIndex(i);

                float angle = (90f - i * 45f) * Mathf.Deg2Rad;
                icon.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * IconRadius;

                icons[i] = icon.GetComponent<Image>();
                icons[i].sprite = catalog.EntryAt(i)?.Sprite;
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;
                icons[i].color = new Color(0.78f, 0.82f, 0.82f, 1f);
            }

            var controller = root.GetComponent<EmotionWheelController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.FindProperty("canvas").objectReferenceValue = canvas;
            serialized.FindProperty("overlayRoot").objectReferenceValue = root;
            serialized.FindProperty("overlayGroup").objectReferenceValue = group;
            serialized.FindProperty("wheelRoot").objectReferenceValue = wheel;
            serialized.FindProperty("selectionHighlight").objectReferenceValue = highlightGraphic;

            var iconProperty = serialized.FindProperty("icons");
            iconProperty.arraySize = icons.Length;
            for (int i = 0; i < icons.Length; i++)
                iconProperty.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var director = Object.FindFirstObjectByType<GameDirector>(FindObjectsInactive.Include);
            if (director != null)
            {
                var directorSo = new SerializedObject(director);
                var wheelProperty = directorSo.FindProperty("emotionWheel");
                if (wheelProperty != null) wheelProperty.objectReferenceValue = controller;
                directorSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(director);
            }

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            EditorSceneManager.SaveScene(root.gameObject.scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("[EmotionWheelSetup] ZJ_EmotionWheel 생성/갱신 및 배선 완료.");
        }

        private static RectTransform Ensure(string name, Transform parent, params System.Type[] components)
        {
            RectTransform rect = null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name && child is RectTransform candidate)
                {
                    rect = candidate;
                    break;
                }
            }

            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create Emotion Wheel");
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
            }

            for (int i = 0; i < components.Length; i++)
                if (rect.GetComponent(components[i]) == null)
                    Undo.AddComponent(rect.gameObject, components[i]);

            // 동일한 예약 이름의 직접 자식은 하나만 유지한다.
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != rect && child.name == name)
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
            return rect;
        }

        private static void RemoveNamedChildren(Transform parent, string name)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                if (parent.GetChild(i).name == name)
                    Undo.DestroyObjectImmediate(parent.GetChild(i).gameObject);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }
    }
}
#endif
