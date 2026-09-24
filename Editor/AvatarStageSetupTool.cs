#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// 열려 있는 씬에 테이블 위 캐릭터(AvatarStage + 동물 3마리)를 만들고 배선한다.
    /// 이미 있으면 새로 만들지 않고 스프라이트·이름표만 다시 맞춘다(여러 번 눌러도 안전).
    ///
    /// 캐릭터는 TableArea 아래에 두므로 중앙 패널보다 뒤에 그려진다. 테이블 주변을
    /// 돌아다니는 동안에는 보이고, 패널 뒤로 지나갈 때만 가려진다.
    /// </summary>
    public static class AvatarStageSetupTool
    {
        // ── 행동 알약("고민하는 중...") 치수 ────────────────────────
        // 절대값이 아니라 명패에 대한 비율이다. 명패를 키우거나 줄이면 알약도 따라간다.
        private const float PlateActionGap = 4f;             // 명패 윗변과의 틈(px)
        private const float PlateActionPaddingScale = 0.7f;  // 명패 여백 대비
        private const float PlateActionFontScale = 0.8f;     // 명패 이름 글자 대비
        private const float PlateActionTextAlpha = 0.85f;    // 이름보다 한 걸음 뒤로

        private const string IconFolder = "Assets/ImportedAsset/Characters";
        private const string NormalizedIdleFolder =
            "Assets/ImportedAsset/Characters/Animations/Normalized";
        private const float LegacyDealerPlateY = 108f;
        private const float UnifiedPlateY = 92f;
        private static readonly Vector2 UnifiedAvatarSize = new Vector2(220f, 145f);

        private static readonly (PlayerRole Role, string Object, string Icon, string Label)[] Cast =
        {
            (PlayerRole.PlayerA, "AvatarPlayerA", "rabbit_idle", "플레이어 A"),
            (PlayerRole.PlayerB, "AvatarPlayerB", "fox_idle",    "플레이어 B"),
            (PlayerRole.Dealer,  "AvatarDealer",  "croc_idle",   "딜러")
        };

        [MenuItem("ZooJack/Avatars/씬에 캐릭터 만들기")]
        public static void Setup()
        {
            var director = Object.FindFirstObjectByType<GameDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                EditorUtility.DisplayDialog("캐릭터 설정",
                    "열려 있는 씬에서 GameDirector를 찾지 못했습니다. GameScene을 먼저 여세요.", "확인");
                return;
            }

            var table = FindTableArea(director);
            if (table == null)
            {
                EditorUtility.DisplayDialog("캐릭터 설정",
                    "캐릭터를 담을 TableArea를 찾지 못했습니다.", "확인");
                return;
            }

            var directorSo = new SerializedObject(director);
            var stageProp = directorSo.FindProperty("avatarStage");
            var stage = stageProp.objectReferenceValue as AvatarStage ?? CreateStage(table);
            stageProp.objectReferenceValue = stage;
            directorSo.ApplyModifiedProperties();

            EnsureStage(stage);

            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(stage);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);

            Debug.Log("[AvatarStageSetupTool] 캐릭터 3마리 설정 완료. " +
                      "플레이 모드에서 WASD 또는 방향키로 움직입니다. 씬을 저장하세요.");
            Selection.activeObject = stage.gameObject;
        }

        /// <summary>
        /// GameScene과 LobbyScene이 같은 정규화 Sprite, Animator, 크기와 감정 표시 구조를
        /// 사용하도록 한 곳에서 갱신한다. 로비는 기존 168x112 값을 보존하지 않고 게임의
        /// 공통 크기를 강제해야 하므로 <paramref name="forceUnifiedSize"/>를 사용한다.
        /// </summary>
        internal static void EnsureStage(AvatarStage stage, bool forceUnifiedSize = false)
        {
            if (stage == null) return;

            var stageSo = new SerializedObject(stage);
            foreach (var entry in Cast)
                EnsureAvatar(stageSo, stage, entry, forceUnifiedSize);
            EnsureEmotionBubbleLayer(stageSo, stage);
            stageSo.ApplyModifiedProperties();

            EditorUtility.SetDirty(stage);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        }

        // GameDirector가 들고 있는 손패 영역의 부모가 곧 TableArea다.
        // 이름으로 찾으면 이름을 바꿨을 때 조용히 실패하므로 실제 참조를 타고 올라간다.
        private static RectTransform FindTableArea(GameDirector director)
        {
            var so = new SerializedObject(director);
            var hand = so.FindProperty("handLayoutA").objectReferenceValue as Component;
            if (hand != null) return hand.transform.parent as RectTransform;

            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            return canvas == null ? null : canvas.transform.Find("TableArea") as RectTransform;
        }

        private static AvatarStage CreateStage(RectTransform table)
        {
            var go = new GameObject("AvatarStage", typeof(RectTransform), typeof(AvatarStage));
            Undo.RegisterCreatedObjectUndo(go, "Create Avatar Stage");

            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(table, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.SetAsLastSibling(); // 카드·칩 위에 서도록
            return go.GetComponent<AvatarStage>();
        }

        private static void EnsureAvatar(
            SerializedObject stageSo, AvatarStage stage,
            (PlayerRole Role, string Object, string Icon, string Label) entry,
            bool forceUnifiedSize)
        {
            string field = entry.Role switch
            {
                PlayerRole.PlayerA => "avatarPlayerA",
                PlayerRole.PlayerB => "avatarPlayerB",
                _                  => "avatarDealer"
            };

            var prop = stageSo.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[AvatarStageSetupTool] AvatarStage에 '{field}' 필드가 없습니다.");
                return;
            }

            var view = prop.objectReferenceValue as PlayerAvatarView ?? CreateAvatar(stage, entry);
            prop.objectReferenceValue = view;

            EnsureCanvasGroup(view.gameObject);

            var viewSo = new SerializedObject(view);
            EnsureUnifiedAvatarSize(entry.Role, viewSo, forceUnifiedSize);
            var imageProperty = viewSo.FindProperty("image");
            var image = EnsureCharacterVisual(
                view, imageProperty.objectReferenceValue as Image);
            imageProperty.objectReferenceValue = image;
            viewSo.FindProperty("characterVisual").objectReferenceValue = image.rectTransform;
            viewSo.FindProperty("characterAnimator").objectReferenceValue =
                EnsureCharacterAnimator(image.rectTransform);
            viewSo.FindProperty("walkAnimationCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CharacterWalkAnimationCatalog>(
                    CharacterWalkAnimationSetupTool.CatalogPath);
            if (image != null)
            {
                // 실행 직후 Animator의 Idle 상태가 사용하는 512x512 정규화본을
                // 씬 뷰에서도 그대로 보여 준다. 원본 Idle을 두면 플레이 전/후의
                // 투명 여백이 달라 캐릭터 크기가 바뀐 것처럼 보인다.
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"{NormalizedIdleFolder}/{entry.Icon}_normalized.png")
                    ?? LoadIcon(entry.Icon);
                if (sprite != null) image.sprite = sprite;
                else Debug.LogWarning($"[AvatarStageSetupTool] 스프라이트를 찾지 못했습니다: {entry.Icon}.png");
                image.preserveAspect = true;
                image.raycastTarget = false;
                EditorUtility.SetDirty(image);
            }

            // 발밑 이름표는 실행 중에 '✓ 준비' 하나만 띄운다 — 누구인지는 머리 위 명패가
            // 이미 말하고 있어서, 여기까지 이름을 적으면 같은 말이 위아래로 두 번 뜼다.
            // 색도 로비의 준비 표시와 맞춰 둔다 — 같은 뜻이니 같은 색이어야 한다.
            var label = viewSo.FindProperty("nameLabel").objectReferenceValue as TextMeshProUGUI;
            if (label != null)
            {
                label.text = string.Empty;
                label.color = AvatarStage.ReadyBadgeColor;
                label.font = BorrowFont(label.transform) ?? label.font;
                label.gameObject.SetActive(false);
                EditorUtility.SetDirty(label);
            }

            var plate = viewSo.FindProperty("plateRoot")?.objectReferenceValue as RectTransform;
            EnsureDealerPlateClearance(entry.Role, plate);
            var anchorProperty = viewSo.FindProperty("emotionAnchor");
            if (plate != null && anchorProperty != null)
                anchorProperty.objectReferenceValue = EnsureEmotionAnchor(plate);

            if (plate != null)
            {
                var actionPill = EnsurePlateAction(plate);
                var rootProperty = viewSo.FindProperty("plateActionRoot");
                var labelProperty = viewSo.FindProperty("plateAction");
                if (rootProperty != null) rootProperty.objectReferenceValue = actionPill;
                if (labelProperty != null)
                    labelProperty.objectReferenceValue =
                        actionPill != null ? actionPill.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            }

            viewSo.ApplyModifiedProperties();

            var rect = view.GetComponent<RectTransform>();
            rect.anchoredPosition = AvatarMovement.SpawnPositionFor(entry.Role);
            EditorUtility.SetDirty(view);
        }

        private static void EnsureUnifiedAvatarSize(
            PlayerRole role, SerializedObject viewSo, bool force)
        {
            var sizeProperty = viewSo.FindProperty("size");
            if (sizeProperty == null) return;

            if (force)
            {
                sizeProperty.vector2Value = UnifiedAvatarSize;
                return;
            }

            // 이전 씬의 역할별 기본 크기만 새 공통 크기로 마이그레이션한다.
            // 이후 인스펙터에서 사용자가 조정한 값은 Setup 재실행 시 보존한다.
            Vector2 legacySize = role switch
            {
                PlayerRole.PlayerA => new Vector2(235f, 155f),
                PlayerRole.PlayerB => new Vector2(225f, 155f),
                _                  => new Vector2(265f, 170f)
            };
            if (sizeProperty.vector2Value != legacySize) return;
            sizeProperty.vector2Value = UnifiedAvatarSize;
        }

        /// <summary>
        /// 같은 씬이 이미 쓰고 있는 글꼴을 빌린다.
        ///
        /// <b>기본 글꼴로 두면 체크 표시가 두부가 된다.</b> 이름표는 만들 때 글꼴을 주지
        /// 않아 LiberationSans로 남아 있었는데, 그 글꼴과 폴백에는 '✓'(U+2713)가 없어서
        /// <see cref="AvatarStage.ReadyBadge"/>가 빈 네모로 찍혔다. 이 게임이 쓰는 글꼴에는
        /// 있으므로, 경로를 적어 두는 대신 옆에서 쓰고 있는 것을 그대로 가져온다.
        /// </summary>
        private static TMP_FontAsset BorrowFont(Transform near)
        {
            var canvas = near.GetComponentInParent<Canvas>();
            var root = canvas != null ? canvas.transform : near.root;
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.font != null && text.font.name != "LiberationSans SDF") return text.font;
            return null;
        }

        private static void EnsureDealerPlateClearance(
            PlayerRole role, RectTransform plate)
        {
            if (role != PlayerRole.Dealer || plate == null) return;

            // 이제 세 Avatar 높이가 같으므로 Dealer Plate도 같은 높이로 되돌린다.
            // 이전 자동 보정값일 때만 바꿔 다른 수동 위치는 보존한다.
            var position = plate.anchoredPosition;
            if (!Mathf.Approximately(position.y, LegacyDealerPlateY)) return;
            position.y = UnifiedPlateY;
            plate.anchoredPosition = position;
            EditorUtility.SetDirty(plate);
        }

        private static PlayerAvatarView CreateAvatar(
            AvatarStage stage, (PlayerRole Role, string Object, string Icon, string Label) entry)
        {
            var go = new GameObject(entry.Object,
                typeof(RectTransform), typeof(PlayerAvatarView));
            Undo.RegisterCreatedObjectUndo(go, "Create Avatar");

            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(stage.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);

            // 이름표는 캐릭터 발밑에.
            var labelGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, -72f);
            labelRect.sizeDelta = new Vector2(200f, 28f);

            // 발밑 이름표는 실행 중에 '✓ 준비' 하나만 띄운다 — 누구인지는 머리 위 명패가
            // 이미 말하고 있어서, 여기까지 이름을 적으면 같은 말이 위아래로 두 번 뜬다.
            // 그래서 색도 로비의 준비 표시와 맞춘다.
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            label.font = BorrowFont(labelRect) ?? label.font;
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = AvatarStage.ReadyBadgeColor;
            label.raycastTarget = false;
            labelGo.SetActive(false);

            var view = go.GetComponent<PlayerAvatarView>();
            var image = EnsureCharacterVisual(view, null);
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("image").objectReferenceValue = image;
            viewSo.FindProperty("characterVisual").objectReferenceValue = image.rectTransform;
            viewSo.FindProperty("characterAnimator").objectReferenceValue =
                EnsureCharacterAnimator(image.rectTransform);
            viewSo.FindProperty("walkAnimationCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CharacterWalkAnimationCatalog>(
                    CharacterWalkAnimationSetupTool.CatalogPath);
            viewSo.FindProperty("nameLabel").objectReferenceValue = label;
            viewSo.ApplyModifiedProperties();
            return view;
        }

        private static Image EnsureCharacterVisual(PlayerAvatarView view, Image previousImage)
        {
            var root = view.GetComponent<RectTransform>();
            var visual = root.Find("CharacterVisual") as RectTransform;
            if (visual == null)
            {
                var go = new GameObject("CharacterVisual",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(go, "Create Character Visual");
                visual = go.GetComponent<RectTransform>();
                visual.SetParent(root, false);
                visual.SetAsFirstSibling();
            }

            visual.anchorMin = Vector2.zero;
            visual.anchorMax = Vector2.one;
            visual.pivot = new Vector2(0.5f, 0.5f);
            visual.offsetMin = Vector2.zero;
            visual.offsetMax = Vector2.zero;
            visual.localScale = Vector3.one;

            var image = visual.GetComponent<Image>() ?? Undo.AddComponent<Image>(visual.gameObject);
            if (previousImage != null && previousImage != image)
            {
                image.sprite = previousImage.sprite;
                image.color = previousImage.color;
                image.material = previousImage.material;
                image.enabled = previousImage.enabled;
                previousImage.enabled = false;
                previousImage.raycastTarget = false;
                EditorUtility.SetDirty(previousImage);
            }

            image.preserveAspect = true;
            image.raycastTarget = false;

            var animator = view.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController == null)
                Undo.DestroyObjectImmediate(animator);

            EditorUtility.SetDirty(visual);
            EditorUtility.SetDirty(image);
            return image;
        }

        private static Animator EnsureCharacterAnimator(RectTransform visual)
        {
            if (visual.GetComponent<AvatarFootstepEvents>() == null)
                Undo.AddComponent<AvatarFootstepEvents>(visual.gameObject);

            var animator = visual.GetComponent<Animator>();
            // UnityEngine.Object의 '파괴된 객체'는 ??에서 null로 취급되지 않는다.
            if (animator == null)
                animator = Undo.AddComponent<Animator>(visual.gameObject);
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(animator);
            return animator;
        }

        private static RectTransform EnsureEmotionAnchor(RectTransform plate)
        {
            var anchor = plate.Find("EmotionAnchor") as RectTransform;
            bool created = anchor == null;
            if (anchor == null)
            {
                var go = new GameObject("EmotionAnchor", typeof(RectTransform), typeof(LayoutElement));
                Undo.RegisterCreatedObjectUndo(go, "Create Emotion Anchor");
                anchor = go.GetComponent<RectTransform>();
                anchor.SetParent(plate, false);
            }
            RemoveDuplicateNamedChildren(plate, anchor, "EmotionAnchor");

            var layout = anchor.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(anchor.gameObject);
            layout.ignoreLayout = true;

            anchor.anchorMin = anchor.anchorMax = new Vector2(0.5f, 1f);
            anchor.pivot = new Vector2(0.5f, 0f);
            // 사용자가 인스펙터에서 조정한 위치는 재실행해도 유지한다.
            // GameScene에서 사용자가 확정한 세 앵커의 값과 동일하다.
            //
            // 다만 높이(y)는 실행 중에 EmotionBubbleView가 오르내린다 — 말풍선이 뜨면
            // 행동 알약 위로 올라갔다가 사라지면 돌아온다. 그 두 높이를 바꾸려면
            // 여기가 아니라 말풍선 쪽 restingAnchorY / raisedAnchorY를 본다.
            if (created) anchor.anchoredPosition = new Vector2(0f, 10f);
            anchor.sizeDelta = Vector2.zero;
            anchor.localScale = Vector3.one;
            anchor.SetAsLastSibling();
            EditorUtility.SetDirty(layout);
            EditorUtility.SetDirty(anchor);
            return anchor;
        }

        /// <summary>
        /// 명패 바로 위에 얹히는 행동 알약("고민하는 중..."). 없으면 만들고, 있으면
        /// 자리와 모양만 다시 맞춘다.
        ///
        /// <b>모양은 명패에서 그대로 베낀다.</b> 둥근 모서리·테두리·그림자 값을 여기에
        /// 다시 적으면 명패 쪽을 손볼 때마다 두 상자가 갈라진다. 알약은 명패보다 작아야
        /// 하므로 여백과 글자 크기만 줄인다.
        ///
        /// 명패의 <b>자식</b>으로 두되 배치에서는 빼 둔다(<c>ignoreLayout</c>). 명패는
        /// 글자 길이에 따라 가로세로가 변하는데, 자식으로 매달아 두면 그 위쪽 변에
        /// 저절로 따라붙어 얼마나 커지든 항상 명패 바로 위에 앉는다 —
        /// 감정표현 앵커가 쓰는 것과 같은 방법이다.
        /// </summary>
        private static RectTransform EnsurePlateAction(RectTransform plate)
        {
            var pill = plate.Find("PlateAction") as RectTransform;
            if (pill == null)
            {
                var go = new GameObject("PlateAction",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(RoundedPanelGraphic),
                    typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
                Undo.RegisterCreatedObjectUndo(go, "Create Plate Action");
                pill = go.GetComponent<RectTransform>();
                pill.SetParent(plate, false);
            }
            RemoveDuplicateNamedChildren(plate, pill, "PlateAction");

            var ignore = pill.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(pill.gameObject);
            ignore.ignoreLayout = true;

            pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 1f);
            pill.pivot = new Vector2(0.5f, 0f);
            pill.anchoredPosition = new Vector2(0f, PlateActionGap);
            pill.localScale = Vector3.one;
            pill.SetAsLastSibling();

            // 배경: 명패의 것을 통째로 복사한 뒤 클릭만 막지 않게 한다.
            var plateGraphic = plate.GetComponent<RoundedPanelGraphic>();
            var pillGraphic = pill.GetComponent<RoundedPanelGraphic>()
                              ?? Undo.AddComponent<RoundedPanelGraphic>(pill.gameObject);
            if (plateGraphic != null) EditorUtility.CopySerialized(plateGraphic, pillGraphic);
            pillGraphic.raycastTarget = false;

            // 여백: 명패의 것을 베끼되 좁힌다. 같은 여백을 쓰면 두 상자가 같은 크기로
            // 보여 어느 쪽이 이름이고 어느 쪽이 상태인지 구분되지 않는다.
            var plateLayout = plate.GetComponent<HorizontalLayoutGroup>();
            var pillLayout = pill.GetComponent<HorizontalLayoutGroup>()
                             ?? Undo.AddComponent<HorizontalLayoutGroup>(pill.gameObject);
            if (plateLayout != null)
            {
                EditorUtility.CopySerialized(plateLayout, pillLayout);
                var pad = pillLayout.padding;
                pillLayout.padding = new RectOffset(
                    Mathf.RoundToInt(pad.left * PlateActionPaddingScale),
                    Mathf.RoundToInt(pad.right * PlateActionPaddingScale),
                    Mathf.RoundToInt(pad.top * PlateActionPaddingScale),
                    Mathf.RoundToInt(pad.bottom * PlateActionPaddingScale));
            }

            // 글자 길이에 맞춰 알약이 줄고 늘어야 "고발하는 중..."과 "배분하는 중..."이
            // 같은 상자에서 잘리지 않는다.
            var plateFitter = plate.GetComponent<ContentSizeFitter>();
            var pillFitter = pill.GetComponent<ContentSizeFitter>()
                             ?? Undo.AddComponent<ContentSizeFitter>(pill.gameObject);
            if (plateFitter != null) EditorUtility.CopySerialized(plateFitter, pillFitter);

            EnsurePlateActionLabel(pill, plate.Find("PlateName")?.GetComponent<TextMeshProUGUI>());

            // 씀 알약은 씁지 않는다. 켜 둔 채로 저장하면 씨을 열자마자 글자 없는 상자가
            // 명패 위에 한 순간 떠있는다 — 첫 명패 갱신이 돌기 전까지.
            pill.gameObject.SetActive(false);

            EditorUtility.SetDirty(pill);
            return pill;
        }

        /// <summary>
        /// 알약 안의 글자. 글꼴·머티리얼·정렬은 명패 이름에서 베껴 오고, 크기와 진하기만
        /// 낮춘다 — 이름보다 커 보이면 무엇이 이 사람의 정체인지 흐려진다.
        /// </summary>
        private static void EnsurePlateActionLabel(RectTransform pill, TextMeshProUGUI plateName)
        {
            var label = pill.Find("Label") as RectTransform;
            if (label == null)
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(go, "Create Plate Action Label");
                label = go.GetComponent<RectTransform>();
                label.SetParent(pill, false);
            }
            RemoveDuplicateNamedChildren(pill, label, "Label");

            var text = label.GetComponent<TextMeshProUGUI>();
            if (plateName != null) EditorUtility.CopySerialized(plateName, text);

            text.text = string.Empty;
            text.fontSize = (plateName != null ? plateName.fontSize : 19f) * PlateActionFontScale;
            text.color = new Color(text.color.r, text.color.g, text.color.b, PlateActionTextAlpha);
            text.raycastTarget = false;
            // 줄바꿈 설정은 명패 이름을 복사할 때 같이 따라온다(줄바꿈 없음).

            label.localScale = Vector3.one;
            EditorUtility.SetDirty(text);
        }

        private static void EnsureEmotionBubbleLayer(SerializedObject stageSo, AvatarStage stage)
        {
            var property = stageSo.FindProperty("emotionBubbleLayer");
            if (property == null) return;

            var layer = property.objectReferenceValue as RectTransform
                        ?? stage.transform.Find("EmotionBubbleLayer") as RectTransform
                        ?? stage.transform.Find("EmotionDisplayLayer") as RectTransform;
            if (layer == null)
            {
                var go = new GameObject("EmotionBubbleLayer", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create Emotion Bubble Layer");
                layer = go.GetComponent<RectTransform>();
                layer.SetParent(stage.transform, false);
            }
            RemoveDuplicateNamedChildren(stage.transform, layer,
                "EmotionBubbleLayer", "EmotionDisplayLayer");

            layer.name = "EmotionBubbleLayer";
            layer.anchorMin = layer.anchorMax = layer.pivot = new Vector2(0.5f, 0.5f);
            layer.anchoredPosition = Vector2.zero;
            layer.sizeDelta = Vector2.zero;
            layer.localScale = Vector3.one;
            layer.SetAsLastSibling();
            property.objectReferenceValue = layer;
            // 못 찾았다고 이미 이어 둔 것을 지우지 않는다. 빈손을 써 넣으면 그 화면의
            // 감정표현이 통째로 사라지는데, 씬 파일에는 0 하나만 남아 아무도 못 찾는다.
            var catalogProperty = stageSo.FindProperty("emotionCatalog");
            var catalog = EmotionCatalogAsset.Find();
            if (catalog != null)
                catalogProperty.objectReferenceValue = catalog;
            else if (catalogProperty.objectReferenceValue == null)
                Debug.LogWarning("[AvatarStageSetupTool] EmotionCatalog 에셋을 찾지 못했습니다. " +
                                 "이 씬에서는 감정표현 그림이 뜨지 않습니다.");

            var playerA = stageSo.FindProperty("avatarPlayerA").objectReferenceValue as PlayerAvatarView;
            var playerB = stageSo.FindProperty("avatarPlayerB").objectReferenceValue as PlayerAvatarView;
            var dealer = stageSo.FindProperty("avatarDealer").objectReferenceValue as PlayerAvatarView;

            stageSo.FindProperty("bubblePlayerA").objectReferenceValue =
                EnsureEmotionBubble(layer, "BubblePlayerA", playerA?.EmotionAnchor);
            stageSo.FindProperty("bubblePlayerB").objectReferenceValue =
                EnsureEmotionBubble(layer, "BubblePlayerB", playerB?.EmotionAnchor);
            stageSo.FindProperty("bubbleDealer").objectReferenceValue =
                EnsureEmotionBubble(layer, "BubbleDealer", dealer?.EmotionAnchor);
            EditorUtility.SetDirty(layer);
        }

        private static EmotionBubbleView EnsureEmotionBubble(
            RectTransform layer, string name, RectTransform anchor)
        {
            var rect = layer.Find(name) as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(name,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                    typeof(CanvasGroup), typeof(EmotionBubbleView));
                Undo.RegisterCreatedObjectUndo(go, "Create Emotion Bubble");
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(layer, false);
            }
            RemoveDuplicateNamedChildren(layer, rect, name);

            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            // 앵커는 Plate 상단보다 26px 위에 있는 'Bubble 바닥'이다.
            // 가운데 피벗을 쓰면 104px 이미지의 절반이 아래로 내려가 Plate를 덮는다.
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(104f, 104f);
            rect.localScale = Vector3.one;

            var image = rect.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            var group = rect.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var bubble = rect.GetComponent<EmotionBubbleView>();
            var bubbleSo = new SerializedObject(bubble);
            bubbleSo.FindProperty("anchor").objectReferenceValue = anchor;
            bubbleSo.FindProperty("image").objectReferenceValue = image;
            bubbleSo.FindProperty("canvasGroup").objectReferenceValue = group;
            bubbleSo.ApplyModifiedPropertiesWithoutUndo();

            rect.gameObject.SetActive(false);
            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(bubble);
            return bubble;
        }

        private static void RemoveDuplicateNamedChildren(
            Transform parent, Transform keep, params string[] names)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child == keep) continue;

                for (int j = 0; j < names.Length; j++)
                {
                    if (child.name != names[j]) continue;
                    Undo.DestroyObjectImmediate(child.gameObject);
                    break;
                }
            }
        }

        // 카드를 가릴 때 반투명해지는 데 쓴다. 런타임에도 알아서 붙지만,
        // 미리 붙여둬야 인스펙터에서 투명도를 눈으로 확인할 수 있다.
        private static void EnsureCanvasGroup(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = Undo.AddComponent<CanvasGroup>(go);
                group.alpha = 1f;
            }

            group.blocksRaycasts = false;
            group.interactable = false;
            EditorUtility.SetDirty(group);
        }

        private static Sprite LoadIcon(string name)
        {
            string path = $"{IconFolder}/{name}.png";

            // 여러 조각으로 잘린 파일은 가장 큰 조각이 실제 캐릭터다.
            Sprite best = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not Sprite sprite) continue;
                if (best == null || sprite.rect.width * sprite.rect.height
                                  > best.rect.width * best.rect.height)
                    best = sprite;
            }
            return best;
        }

    }
}
#endif
