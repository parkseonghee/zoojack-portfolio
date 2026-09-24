#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>설정·기록·설명서 여는 버튼에 공통 아이콘을 배치한다.</summary>
    internal static class OpenButtonIconUtility
    {
        private static readonly Vector2 IconSize = new Vector2(80f, 80f);

        public static void Apply(
            Transform button, TextMeshProUGUI label, string spritePath,
            Vector2 anchoredPosition)
        {
            if (button == null || label == null) return;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[OpenButtonIconUtility] 아이콘을 찾지 못했습니다: {spritePath}");
                return;
            }

            var iconTransform = button.Find("Icon") as RectTransform;
            if (iconTransform == null)
            {
                var iconObject = new GameObject(
                    "Icon", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                iconTransform = iconObject.GetComponent<RectTransform>();
                iconTransform.SetParent(button, false);
            }

            iconTransform.anchorMin = iconTransform.anchorMax =
                new Vector2(0.5f, 0.5f);
            iconTransform.pivot = new Vector2(0.5f, 0.5f);
            iconTransform.anchoredPosition = anchoredPosition;
            iconTransform.sizeDelta = IconSize;
            iconTransform.localScale = Vector3.one;

            var image = iconTransform.GetComponent<Image>();
            if (image == null)
                image = iconTransform.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            // 기존 판 모양과 텍스트는 제거한다. 투명 Image는 보이지 않지만
            // 108x52 전체를 클릭할 수 있게 해 아이콘보다 클릭 영역이 작아지지 않는다.
            foreach (var frame in button.GetComponents<RoundedPanelGraphic>())
                Object.DestroyImmediate(frame);
            var hitArea = button.GetComponent<Image>();
            if (hitArea == null) hitArea = button.gameObject.AddComponent<Image>();
            hitArea.sprite = null;
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            var buttonComponent = button.GetComponent<Button>();
            if (buttonComponent != null) buttonComponent.targetGraphic = image;

            Object.DestroyImmediate(label.gameObject);

            iconTransform.SetAsLastSibling();
        }
    }
}
#endif
