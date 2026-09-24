using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// 결정 패널의 행동 설명(<see cref="DecisionActionHint"/>)을 붙이고, 행동마다
    /// 실제 카드 그림으로 만든 아이콘을 만든다.
    ///
    /// 아이콘을 그림 파일로 따로 그리지 않고 카드 스프라이트를 겹쳐 조립한다.
    /// 이 게임의 모든 아이콘이 그렇듯 새 에셋을 늘리지 않는 편이 낫고, 무엇보다
    /// 테이블 위에 놓이는 것과 같은 카드라 무엇을 말하는지가 바로 읽힌다.
    ///
    /// 행동을 색과 배치로 함께 구분한다. 넷 다 카드 그림이라 모양만으로는 헷갈리는데,
    /// 색을 버튼과 맞춰 두면 "지금 보고 있는 버튼의 설명"이라는 것이 한눈에 들어온다.
    ///
    /// 여러 번 눌러도 안전하다 — 아이콘 뭉치는 통째로 다시 만든다.
    /// </summary>
    public static class DecisionHintSetupTool
    {
        private const string CardFolder = "Assets/ImportedAsset/kenney_boardgame-pack/PNG/Cards";
        private const string CardRootPath = "PanelDecision/ZJ_DecisionActionCard";
        private const string IconsName = "ActionIcons";

        /// <summary>아이콘 한 벌이 놓이는 상자. 안내 문구 왼쪽 끝에 겹쳐 둔다.</summary>
        private static readonly Vector2 IconBoxSize = new Vector2(58f, 52f);

        /// <summary>카드 한 장 크기. 두세 장을 겹쳐도 상자를 넘지 않는 값이다.</summary>
        private static readonly Vector2 CardSize = new Vector2(27f, 35f);

        // 색은 버튼 배경의 색조를 그대로 쓰되 밝기만 올린다. 버튼 색(#09401F 등)은
        // 어두운 카드 위에 얹으면 그림이 뭉개질 만큼 짙어서 그대로 쓸 수 없다.
        // 색조까지 바꾸면 지금 보고 있는 버튼과 아이콘이 남남으로 보인다.
        private static readonly Color HitTint = ZooJackPalette.Hex(0x7FE0A2);    // btnHit    #09401F
        private static readonly Color StandTint = ZooJackPalette.Hex(0xE0B072);  // btnStand  #61380E
        private static readonly Color DoubleTint = ZooJackPalette.Hex(0x7FB4F0); // btnDouble #0E3260
        private static readonly Color DieTint = ZooJackPalette.Hex(0xE8877F);    // btnDie    #400B0B


        // 카드 한 장의 배치: 스프라이트 이름, 위치, 기울기.
        private readonly struct Card
        {
            public readonly string Sprite;
            public readonly Vector2 Position;
            public readonly float Angle;
            public Card(string sprite, Vector2 position, float angle)
            {
                Sprite = sprite; Position = position; Angle = angle;
            }
        }

        // 행동 하나의 아이콘 = 카드 여러 장. 뒤에 오는 장이 위에 그려진다.
        private readonly struct IconSpec
        {
            public readonly PlayerActionType Action;
            public readonly string Name;
            public readonly Color Tint;
            public readonly Card[] Cards;
            public IconSpec(PlayerActionType action, string name, Color tint, Card[] cards)
            {
                Action = action; Name = name; Tint = tint; Cards = cards;
            }
        }

        private static readonly IconSpec[] Icons =
        {
            // 히트 — 깔린 두 장 위로 한 장이 오른쪽 위에서 들어온다. 움직임이 있어야
            // "더 받는다"로 읽힌다.
            new IconSpec(PlayerActionType.RequestHit, "Icon_Hit", HitTint, new[]
            {
                new Card("cardClubs2",  new Vector2(-9f, -6f), 14f),
                new Card("cardClubs3",  new Vector2(1f, -8f), -6f),
                new Card("cardClubsA",  new Vector2(11f, 8f), -26f)
            }),

            // 스탠드 — 두 장을 반듯하게 세워 나란히. 기울기가 없어 멈춰 있는 인상을 준다.
            // 겹침을 적게 둬야 한 장으로 뭉쳐 보이지 않는다.
            new IconSpec(PlayerActionType.RequestStand, "Icon_Stand", StandTint, new[]
            {
                new Card("cardSpades10", new Vector2(-11f, 2f), 0f),
                new Card("cardClubs3",   new Vector2(11f, -2f), 0f)
            }),

            // 더블다운 — 같은 카드 두 장을 나란히. 판돈이 2배가 된다는 뜻을
            // 숫자가 아니라 "똑같은 것이 둘"로 보여 준다.
            new IconSpec(PlayerActionType.RequestDoubleDown, "Icon_DoubleDown", DoubleTint, new[]
            {
                new Card("cardClubs3", new Vector2(-11f, -2f), -10f),
                new Card("cardClubs3", new Vector2(11f, 2f), 10f)
            }),

            // 다이 — 뒷면 두 장을 아래로 눕힌다. 앞면을 덮는 것이 곧 패를 접는 일이다.
            new IconSpec(PlayerActionType.RequestDie, "Icon_Die", DieTint, new[]
            {
                new Card("cardBack_red1", new Vector2(-8f, 3f), -22f),
                new Card("cardBack_red1", new Vector2(8f, -3f), -38f)
            })
        };

        [MenuItem("ZooJack/게임/행동 설명 만들기")]
        public static void Setup()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("행동 설명",
                    "열려 있는 씬에 Canvas가 없습니다. GameScene을 먼저 여세요.", "확인");
                return;
            }

            var card = canvas.transform.Find(CardRootPath);
            if (card == null)
            {
                EditorUtility.DisplayDialog("행동 설명",
                    $"{CardRootPath}을(를) 찾지 못했습니다.", "확인");
                return;
            }

            var text = card.Find("txtDecisionHand")?.GetComponent<TMPro.TextMeshProUGUI>();
            if (text == null)
            {
                EditorUtility.DisplayDialog("행동 설명", "txtDecisionHand를 찾지 못했습니다.", "확인");
                return;
            }

            var icons = BuildIcons(card, (RectTransform)text.transform);
            if (icons == null) return;

            Wire(card, text, icons);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[DecisionHintSetupTool] 행동 설명과 아이콘 {Icons.Length}벌을 붙였습니다.");
        }

        // ── 아이콘 ────────────────────────────────────────────────────

        private static GameObject[] BuildIcons(Transform card, RectTransform textRect)
        {
            var old = card.Find(IconsName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(IconsName, typeof(RectTransform));
            root.transform.SetParent(card, false);
            var rootRect = (RectTransform)root.transform;

            // 문구 상자의 왼쪽 끝에 맞춘다. DecisionActionHint가 같은 폭만큼 글자를
            // 오른쪽으로 밀어 주므로 겹치지 않는다.
            rootRect.anchorMin = rootRect.anchorMax = textRect.anchorMin;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = IconBoxSize;
            rootRect.anchoredPosition = new Vector2(
                textRect.anchoredPosition.x - textRect.sizeDelta.x * 0.5f + IconBoxSize.x * 0.5f + 4f,
                textRect.anchoredPosition.y);

            var built = new GameObject[Icons.Length];
            for (int i = 0; i < Icons.Length; i++)
            {
                var spec = Icons[i];
                var go = new GameObject(spec.Name, typeof(RectTransform));
                go.transform.SetParent(root.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = IconBoxSize;
                rect.anchoredPosition = Vector2.zero;

                for (int j = 0; j < spec.Cards.Length; j++)
                {
                    var c = spec.Cards[j];
                    var sprite = LoadSprite($"{CardFolder}/{c.Sprite}.png");
                    if (sprite == null)
                    {
                        EditorUtility.DisplayDialog("행동 설명",
                            $"카드 스프라이트를 찾지 못했습니다:\n{CardFolder}/{c.Sprite}.png", "확인");
                        Object.DestroyImmediate(root);
                        return null;
                    }

                    var cardGo = new GameObject(c.Sprite, typeof(RectTransform));
                    cardGo.transform.SetParent(go.transform, false);
                    var cardRect = (RectTransform)cardGo.transform;
                    cardRect.sizeDelta = CardSize;
                    cardRect.anchoredPosition = c.Position;
                    cardRect.localRotation = Quaternion.Euler(0f, 0f, c.Angle);

                    var image = cardGo.AddComponent<Image>();
                    image.sprite = sprite;
                    image.color = Shade(spec.Tint, j, spec.Cards.Length);
                    image.preserveAspect = true;
                    image.raycastTarget = false;   // 버튼 위를 덮지 않는다
                }

                go.SetActive(false);   // 마우스를 올려야 켜진다
                built[i] = go;
            }
            return built;
        }

        // ── 배선 ──────────────────────────────────────────────────────

        private static void Wire(Transform card, TMPro.TextMeshProUGUI text, GameObject[] icons)
        {
            var hint = card.GetComponent<DecisionActionHint>();
            if (hint == null) hint = card.gameObject.AddComponent<DecisionActionHint>();

            var so = new SerializedObject(hint);
            so.FindProperty("target").objectReferenceValue = text;

            var array = so.FindProperty("entries");
            array.arraySize = Icons.Length;
            for (int i = 0; i < Icons.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("button").objectReferenceValue =
                    FindButton(card, Icons[i].Action);
                element.FindPropertyRelative("icon").objectReferenceValue = icons[i];
                element.FindPropertyRelative("action").enumValueIndex = (int)Icons[i].Action;
            }

            // 글자를 미는 폭은 아이콘 상자 폭에서 나온다. 두 값이 갈라지면 아이콘이
            // 글자에 깔리거나 쓸데없이 빈 자리가 생긴다.
            so.FindProperty("iconMargin").floatValue = IconBoxSize.x + 8f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button FindButton(Transform card, PlayerActionType action)
        {
            string name = action switch
            {
                PlayerActionType.RequestHit        => "btnHit",
                PlayerActionType.RequestStand      => "btnStand",
                PlayerActionType.RequestDoubleDown => "btnDoubleDown",
                PlayerActionType.RequestDie        => "btnDie",
                _                                  => null
            };
            if (name == null) return null;

            var found = card.Find(name);
            if (found == null)
            {
                Debug.LogWarning($"[DecisionHintSetupTool] {name}을(를) 찾지 못했습니다.");
                return null;
            }
            return found.GetComponent<Button>();
        }

        /// <summary>
        /// 뒤에 깔린 장일수록 어둡게 칠한다.
        ///
        /// 카드 스프라이트는 흰 테두리로 서로를 구분하는데, 한 색으로 물들이면 그 테두리가
        /// 면과 같은 색이 되어 겹친 두 장이 한 장처럼 뭉친다(스탠드·더블다운이 실제로 그랬다).
        /// 장마다 밝기를 달리하면 경계가 다시 드러나고 겹친 순서도 함께 읽힌다.
        /// 곱셈이 알파까지 건드리므로 원래 알파는 되돌려 놓는다.
        /// </summary>
        private static Color Shade(Color tint, int index, int count)
        {
            if (count <= 1) return tint;
            var shaded = tint * Mathf.Lerp(0.58f, 1f, index / (float)(count - 1));
            shaded.a = tint.a;
            return shaded;
        }

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
