using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>E를 누르는 동안 마우스 방향으로 감정표현을 고르는 화면 컨트롤러.</summary>
    [DisallowMultipleComponent]
    public sealed class EmotionWheelController : MonoBehaviour
    {
        [SerializeField] private EmotionCatalog catalog;
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform overlayRoot;
        [SerializeField] private CanvasGroup overlayGroup;
        [SerializeField] private RectTransform wheelRoot;
        [SerializeField] private EmotionWheelArcGraphic selectionHighlight;
        [SerializeField] private Image[] icons = new Image[EmotionCatalog.WheelSlotCount];

        [Header("배치")]
        [SerializeField, Min(1f)] private float wheelRadius = 220f;
        [SerializeField, Min(0f)] private float edgePadding = 16f;
        [SerializeField, Min(0f)] private float deadZoneRadius = 72f;

        [Header("선택 강조")]
        [SerializeField, Range(1f, 1.5f)] private float selectedScale = 1.16f;
        [SerializeField] private Color normalIconColor = new Color(0.78f, 0.82f, 0.82f, 1f);
        [SerializeField] private Color selectedIconColor = Color.white;

        /// <summary>
        /// 씬에 있는 감정 휠(비활성 포함). 인스펙터 연결이 비었을 때의 폴백이다.
        ///
        /// 예전에는 이 <c>FindFirstObjectByType</c> 한 줄이 게임 디렉터 둘과 로비 매니저에
        /// 다섯 번 흩어져 있었다. 탐색 조건(<c>FindObjectsInactive.Include</c>)을 한 곳에서만
        /// 빠뜨려도 "로비에서는 되는데 게임에서는 안 되는" 차이가 생긴다.
        /// </summary>
        public static EmotionWheelController FindInScene() =>
            FindFirstObjectByType<EmotionWheelController>(FindObjectsInactive.Include);

        public event Action<EmotionId> EmotionSelected;

        public bool IsOpen { get; private set; }
        public int SelectedIndex { get; private set; } = -1;

        private Vector2 wheelCenterScreen;
        private bool inputEnabled = true;
        private Func<bool> canOpen;

        private Camera EventCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        private void Awake()
        {
            PopulateIcons();
            HideImmediate();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (!inputEnabled || keyboard == null || mouse == null) return;

            if (IsOpen && !CanOpenNow())
            {
                Cancel();
                return;
            }

            if (!IsOpen)
            {
                if (keyboard.eKey.wasPressedThisFrame && CanOpenNow())
                    Open(mouse.position.ReadValue());
                return;
            }

            UpdateSelection(mouse.position.ReadValue());

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cancel();
                return;
            }

            if (keyboard.eKey.wasReleasedThisFrame)
                ConfirmOrCancel();
        }

        private void OnDisable() => HideImmediate();

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled) Cancel();
        }

        /// <summary>게임 상태에 따라 휠을 열 수 있는지 즉시 판단하는 조건을 연결한다.</summary>
        public void SetOpenGuard(Func<bool> guard)
        {
            canOpen = guard;
            if (IsOpen && !CanOpenNow()) Cancel();
        }

        public void CancelSelection() => Cancel();

        // 채팅에 글을 치는 동안에는 키보드가 저쪽 것이다. 'e'를 칠 때마다 휠이 열리면
        // 글을 끝까지 쓸 수가 없다.
        private bool CanOpenNow() => !ChatFocus.IsTyping && (canOpen == null || canOpen());

        private void Open(Vector2 mouseScreenPosition)
        {
            if (catalog == null || catalog.Count != EmotionCatalog.WheelSlotCount ||
                canvas == null || overlayRoot == null || overlayGroup == null || wheelRoot == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    overlayRoot, mouseScreenPosition, EventCamera, out Vector2 localMouse))
                return;

            float safeRadius = wheelRadius + edgePadding;
            wheelRoot.anchoredPosition = EmotionWheelSelection.ClampCenter(
                localMouse, overlayRoot.rect, safeRadius);

            Canvas.ForceUpdateCanvases();
            wheelCenterScreen = RectTransformUtility.WorldToScreenPoint(EventCamera, wheelRoot.position);

            IsOpen = true;
            overlayGroup.alpha = 1f;
            SelectedIndex = -1;
            UpdateSelection(mouseScreenPosition);
        }

        private void UpdateSelection(Vector2 mouseScreenPosition)
        {
            float screenDeadZone = deadZoneRadius * Mathf.Max(0.0001f, canvas.scaleFactor);
            bool hasSelection = EmotionWheelSelection.TryGetIndex(
                mouseScreenPosition, wheelCenterScreen, screenDeadZone, out int index);

            SetSelectedIndex(hasSelection ? index : -1);
        }

        private void ConfirmOrCancel()
        {
            var entry = catalog != null ? catalog.EntryAt(SelectedIndex) : null;
            HideImmediate();
            if (entry != null) EmotionSelected?.Invoke(entry.Id);
        }

        private void Cancel() => HideImmediate();

        private void SetSelectedIndex(int index)
        {
            if (SelectedIndex == index) return;
            SelectedIndex = index;

            if (selectionHighlight != null)
            {
                selectionHighlight.gameObject.SetActive(index >= 0);
                if (index >= 0)
                    selectionHighlight.rectTransform.localEulerAngles = new Vector3(0f, 0f, -index * 45f);
            }

            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                bool selected = i == index;
                icons[i].color = selected ? selectedIconColor : normalIconColor;
                icons[i].rectTransform.localScale = Vector3.one * (selected ? selectedScale : 1f);
            }
        }

        private void PopulateIcons()
        {
            if (catalog == null) return;
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;
                icons[i].sprite = catalog.EntryAt(i)?.Sprite;
                icons[i].preserveAspect = true;
                icons[i].raycastTarget = false;
            }
        }

        private void HideImmediate()
        {
            IsOpen = false;
            if (overlayGroup != null)
            {
                overlayGroup.alpha = 0f;
                overlayGroup.interactable = false;
                overlayGroup.blocksRaycasts = false;
            }

            SetSelectedIndex(-1);
        }
    }
}
