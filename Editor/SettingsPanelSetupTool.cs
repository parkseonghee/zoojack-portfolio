using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// 환경설정 화면(<see cref="SettingsPanel"/>)을 열려 있는 씬의 Canvas에 만든다.
    ///
    /// 왼쪽 위 상태바 아래에 '설정' 버튼 하나를 두고, 누르면 화면 가운데 카드가 뜬다.
    /// 카드에는 음량 셋과 해상도·화면 모드가 들어간다.
    ///
    /// <b>씬을 가리지 않는다.</b> Canvas만 있으면 되므로 게임 씬에서도 로비 씬에서도
    /// 같은 메뉴로 만들 수 있다. 이미 있으면 지우고 새로 만든다 — 값이 반쯤 바뀐
    /// 옛 화면이 남는 것보다 낫다.
    ///
    /// <b>해상도 목록은 여기에 없다.</b> 실행 중에 기기가 알려 주는 것을 훑으므로
    /// (<see cref="GameSettings.Sizes"/>) 이 도구는 화살표 두 개와 글자 한 칸만 만든다.
    /// </summary>
    public static class SettingsPanelSetupTool
    {
        private const string RootName = "ZJ_Settings";

        // ── 자리와 크기 ──────────────────────────────────────────────
        // 왼쪽 위 '설정' 버튼의 자리는 OpenButtonColumn이 정한다 — 셋이 한 열에 선다.

        // 이름 줄(RowHeight) 하나와 그 아래 사이(RowGap * 1.8)가 들어 있는 높이다.
        // 줄을 더 넣거나 뺄 때 이 값도 함께 움직여야 카드가 내용에 맞는다.
        private static readonly Vector2 CardSize = new Vector2(640f, 606f);

        private static readonly Vector2 CloseButtonSize = new Vector2(160f, 52f);
        private static readonly Vector2 SurrenderButtonSize = new Vector2(160f, 52f);
        private const float ButtonGap = 16f;
        private const float ButtonRowHeight = 52f;

        private const float CardPadding = 40f;   // 카드 안쪽 여백
        private const float RowHeight = 46f;
        private const float RowGap = 20f;
        private const float LabelWidth = 150f;
        private const float ValueWidth = 120f;

        /// <summary>이름 입력칸의 높이. 채팅 입력칸과 같은 손가락 크기다.</summary>
        private const float InputHeight = 40f;

        private const float DropdownWidth = 240f;
        private const float ItemHeight = 40f;

        /// <summary>펼쳐진 목록의 높이. 해상도는 스무 개가 넘으므로 다 보이지 않는 것이 정상이다.</summary>
        private const float TemplateHeight = 320f;
        private const float ScrollbarWidth = 14f;

        // ── 색 ───────────────────────────────────────────────────────
        // 결과 카드·순위표와 같은 계열이다. 환경설정만 다른 톤이면 게임에서 튀어나온
        // 창처럼 보인다.

        private static readonly Color CardFill = ZooJackPalette.Ink.Alpha(0.99f);
        private static readonly Color CardBorder = ZooJackPalette.Gold;
        private static readonly Color CardInner = ZooJackPalette.Steel.Alpha(0.5f);
        private static readonly Color Shadow = ZooJackPalette.Shadow;
        private static readonly Color ScrimColor = ZooJackPalette.Scrim;

        private static readonly Color TitleColor = ZooJackPalette.Cream;
        private static readonly Color LabelColor = ZooJackPalette.Sand;
        private static readonly Color ValueColor = Color.white;

        private static readonly Color ButtonFill = ZooJackPalette.Graphite.Alpha(0.98f);
        private static readonly Color ListFill = ZooJackPalette.Hex(0x14181B, 1f);
        private static readonly Color ItemHover = ZooJackPalette.Hex(0x3A4147, 1f);
        private static readonly Color ItemSelected = ZooJackPalette.Hex(0x4A5560, 1f);
        private static readonly Color ButtonBorder = ZooJackPalette.Gold;
        private static readonly Color CloseFill = ZooJackPalette.Forest.Alpha(0.98f);
        /// <summary>항복 버튼. 이 화면에서 되돌릴 수 없는 것은 이것 하나뿐이라 혼자 붉다.</summary>
        private static readonly Color SurrenderFill = ZooJackPalette.Hex(0x8E2A22, 0.98f);

        private static readonly Color SliderTrack = ZooJackPalette.Hex(0x11151800, 0f);
        private static readonly Color TrackFill = ZooJackPalette.Hex(0x33393D, 1f);
        private static readonly Color BarFill = ZooJackPalette.Hex(0xC9A959, 1f);
        private static readonly Color HandleFill = ZooJackPalette.Cream;

        private const float TitleSize = 30f;
        private const float LabelSize = 22f;
        private const float ValueSize = 22f;
        private const float ButtonSize = 22f;

        [MenuItem("ZooJack/게임/환경설정 화면 만들기")]
        public static void Setup()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("환경설정 화면",
                    "열려 있는 씬에 Canvas가 없습니다. 게임 씬이나 로비 씬을 먼저 여세요.", "확인");
                return;
            }

            var font = BorrowFont(canvas.transform);
            var host = PanelHost.For(canvas);

            var existing = host.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = Child(host, RootName);
            Stretch((RectTransform)root.transform);
            root.transform.SetAsLastSibling();   // 같은 판 안의 어떤 것 위에도 뜬다

            var panel = root.AddComponent<SettingsPanel>();

            var open = BuildOpenButton(root.transform, font);
            var window = BuildWindow(root.transform, font, out var wired);

            wired.Open = open;
            Wire(panel, window, wired);

            window.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[SettingsPanelSetupTool] '{EditorSceneManager.GetActiveScene().name}' 씬에 " +
                      "환경설정 화면을 만들었습니다. 해상도 변경은 빌드에서만 실제로 적용됩니다.");

            Selection.activeGameObject = root;
        }

        // ── 로비 ─────────────────────────────────────────────────────

        private const string LobbyPanelName = "PanelSettings";
        private const string LobbyCardName = "ZJ_SettingsCard";

        /// <summary>
        /// 로비의 메인 화면에서 지우는 위젯.
        ///
        /// 닉네임은 없어진 것이 아니라 <b>환경설정 카드 안으로 옮겨 왔다</b>(NicknameRow).
        /// 두 화면에 같은 칸이 있으면 어느 쪽에 친 이름이 남는지가 눌러 본 순서에 따라
        /// 달라진다. NetworkLobbyManager의 <c>inputNickname</c> 참조는 비게 되지만,
        /// 쓰는 두 곳이 모두 null을 확인하므로 그대로 두어도 문제가 없다.
        /// </summary>
        private static readonly string[] LobbyDropped = { "lblNickname", "inputNickname" };

        [MenuItem("ZooJack/로비/설정 화면 만들기")]
        public static void SetupLobby()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var panel = canvas != null ? canvas.transform.Find(LobbyPanelName) : null;
            if (panel == null)
            {
                EditorUtility.DisplayDialog("로비 설정 화면",
                    $"열려 있는 씬에서 {LobbyPanelName}을(를) 찾지 못했습니다. LobbyScene을 먼저 여세요.",
                    "확인");
                return;
            }

            var font = BorrowFont(canvas.transform);

            var old = panel.Find(LobbyCardName);
            if (old != null)
            {
                // 카드를 지우기 전에 빌려 온 위젯을 되돌려 놓는다. 함께 지우면
                // NetworkLobbyManager의 배선이 끊어져 로비가 조용히 망가진다.
                Rescue(old, panel, "txtSettingsTitle", "btnSettingsBack");
                Object.DestroyImmediate(old.gameObject);
            }

            // 이제 쓰지 않는 위젯은 치운다. 카드 안이든 패널 바로 아래든 어디 있어도 찾는다.
            int dropped = 0;
            foreach (var name in LobbyDropped)
            {
                var found = FindDeep(panel, name);
                if (found == null) continue;
                Object.DestroyImmediate(found.gameObject);
                dropped++;
            }

            // 패널이 스스로 그리던 배경. 카드가 제 배경을 갖고 있으므로 뒤에 한 겹 더
            // 깔리면 카드보다 넓고 짧은 판이 삐져나온다.
            //
            // <b>Image만 지워서는 부족하다.</b> 로비의 다른 패널들처럼 RoundedPanelGraphic으로
            // 그리는 경우가 있어(배경 아트가 바뀌며 그렇게 됐다) Graphic을 통째로 본다.
            var backdrop = panel.GetComponent<Graphic>();
            if (backdrop != null) Object.DestroyImmediate(backdrop);

            var card = BuildLobbyCard(panel, font, out var wired);

            // 카드 밖에 남은 것은 모두 치운다. 옛 화면의 장식(구분선 등)이 남아 있으면
            // 카드보다 크거나 어긋난 자리에서 삐져나온다. 쓸 위젯은 이미 카드 안으로
            // 들여왔으므로(제목·뒤로 버튼) 여기 남은 것은 정의상 쓰지 않는 것이다.
            var leftovers = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in panel)
                if (child.gameObject != card) leftovers.Add(child.gameObject);
            foreach (var leftover in leftovers)
            {
                dropped++;
                Debug.Log($"[SettingsPanelSetupTool] 카드 밖에 남은 '{leftover.name}'을(를) 치웠습니다.");
                Object.DestroyImmediate(leftover);
            }

            // 이 화면은 로비가 열고 닫는다. 그래서 window·scrim·여닫는 버튼이 없다 —
            // SettingsPanel은 window가 비어 있으면 붙박이로 동작한다.
            var settings = panel.GetComponent<SettingsPanel>();
            if (settings == null) settings = panel.gameObject.AddComponent<SettingsPanel>();
            Wire(settings, null, wired);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[SettingsPanelSetupTool] 로비 설정 화면에 닉네임·음량·해상도를 넣었습니다. " +
                      $"제목과 뒤로 버튼은 원래 것을 그대로 옮겨 왔고, 메인 화면에 남아 있던 " +
                      $"옛 위젯 {dropped}개를 지웠습니다(닉네임 칸은 이 카드 안으로 옮겨 왔습니다).");

            Selection.activeGameObject = card;
        }

        /// <summary>카드 안에 옮겨 두었던 위젯을 패널 바로 아래로 되돌린다.</summary>
        private static void Rescue(Transform card, Transform panel, params string[] names)
        {
            foreach (var name in names)
            {
                var found = card.Find(name);
                if (found != null) found.SetParent(panel, false);
            }
        }

        /// <summary>이름으로 자손까지 뒤진다. 카드를 만들기 전인지 후인지 신경 쓰지 않기 위해서다.</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject BuildLobbyCard(
            Transform panel, TMP_FontAsset font, out Wired wired)
        {
            wired = default;

            var card = Child(panel, LobbyCardName);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = CardSize;
            cardRect.anchoredPosition = Vector2.zero;

            var frame = card.AddComponent<RoundedPanelGraphic>();
            frame.Configure(CardFill, CardBorder, 20f, 2.4f, CardInner, 10f, 1f,
                Shadow, new Vector2(0f, -6f), 4f);

            float top = CardSize.y * 0.5f - CardPadding;

            // 제목도 카드 안으로 들인다. 화면 위쪽에 그대로 두면 카드가 그 위를 덮어
            // 글자가 반쯤 가려진다(실제로 그랬다).
            Adopt(panel, card.transform, "txtSettingsTitle", new Vector2(0f, top - 16f),
                new Vector2(CardSize.x - CardPadding * 2f, 44f));
            var lobbyTitle = FindDeep(card.transform, "txtSettingsTitle")
                ?.GetComponent<TextMeshProUGUI>();
            BindFixedText(lobbyTitle, "Settings.Title", "환경 설정");

            // 줄 배치는 게임 쪽 환경설정 카드와 같다(BuildWindow). 두 화면이 같은 것을
            // 다루므로 눈에 익은 자리도 같아야 한다.
            float y = top - 66f;
            wired.Nickname = NicknameRow(card.transform, font, y);
            y -= RowHeight + RowGap * 1.8f;
            wired.Master = SliderRow(card.transform, font, "전체 음량", y, out wired.MasterValue);
            y -= RowHeight + RowGap;
            wired.Music = SliderRow(card.transform, font, "배경음악", y, out wired.MusicValue);
            y -= RowHeight + RowGap;
            wired.Effects = SliderRow(card.transform, font, "효과음", y, out wired.EffectsValue);

            y -= RowHeight + RowGap * 1.8f;
            wired.Resolution = DropdownRow(card.transform, font, "해상도", y);
            y -= RowHeight + RowGap;
            wired.ScreenMode = DropdownRow(card.transform, font, "화면 모드", y);

            Adopt(panel, card.transform, "btnSettingsBack",
                new Vector2(0f, -CardSize.y * 0.5f + CardPadding + 4f),
                CloseButtonSize);
            var lobbyClose = FindDeep(card.transform, "btnSettingsBack");
            if (lobbyClose != null)
            {
                var closeFrame = lobbyClose.GetComponent<RoundedPanelGraphic>();
                if (closeFrame != null)
                    closeFrame.Configure(CloseFill, ButtonBorder, 8f, 2f,
                        Color.clear, 0f, 0f, Color.clear, Vector2.zero, 0f);
                BindFixedText(lobbyClose.GetComponentInChildren<TextMeshProUGUI>(true),
                    "Settings.Close", "닫기");
            }

            return card;
        }

        /// <summary>패널이 이미 갖고 있던 위젯을 카드 안으로 데려와 자리를 잡아 준다.</summary>
        private static void Adopt(
            Transform panel, Transform card, string name, Vector2 position, Vector2 size)
        {
            var found = panel.Find(name);
            if (found == null)
            {
                Debug.LogWarning($"[SettingsPanelSetupTool] {name}을(를) 찾지 못해 건너뜁니다.");
                return;
            }

            found.SetParent(card, false);
            Place((RectTransform)found, position, size);
        }

        /// <summary>도구가 만든 위젯을 한 번에 넘기기 위한 묶음. 이름으로 다시 찾지 않는다.</summary>
        private struct Wired
        {
            public Button Open, Close, Scrim, Surrender;
            public TMP_InputField Nickname;
            public Slider Master, Music, Effects;
            public TextMeshProUGUI MasterValue, MusicValue, EffectsValue;
            public TMP_Dropdown Resolution, ScreenMode;
        }

        // ── 여는 버튼 ────────────────────────────────────────────────

        private static Button BuildOpenButton(Transform parent, TMP_FontAsset font)
        {
            var go = Child(parent, "OpenButton");
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = OpenButtonColumn.Size;
            rect.anchoredPosition = OpenButtonColumn.PositionFor(
                OpenButtonColumn.Slot.Settings);

            var frame = Frame(go, ButtonFill, 10f, 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = frame;

            var label = Label(go.transform, "Label", ButtonSize, TitleColor,
                TextAlignmentOptions.Center, font);
            Stretch((RectTransform)label.transform);
            label.text = "설정";
            OpenButtonIconUtility.Apply(
                go.transform, label,
                "Assets/ImportedAsset/UI Image/settings.png",
                Vector2.zero);

            return button;
        }

        // ── 설정 창 ──────────────────────────────────────────────────

        private static GameObject BuildWindow(Transform parent, TMP_FontAsset font, out Wired wired)
        {
            wired = default;

            var window = Child(parent, "Window");
            Stretch((RectTransform)window.transform);

            // 뒤를 덮는 판. 카드 밖을 눌러도 닫히게 하려고 버튼을 겸한다.
            var scrimGo = Child(window.transform, "Scrim");
            Stretch((RectTransform)scrimGo.transform);
            var scrimImage = scrimGo.AddComponent<Image>();
            scrimImage.color = ScrimColor;
            wired.Scrim = scrimGo.AddComponent<Button>();
            wired.Scrim.targetGraphic = scrimImage;
            wired.Scrim.transition = Selectable.Transition.None;

            var card = Child(window.transform, "Card");
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = CardSize;
            cardRect.anchoredPosition = Vector2.zero;

            var frame = card.AddComponent<RoundedPanelGraphic>();
            frame.Configure(CardFill, CardBorder, 20f, 2.4f, CardInner, 10f, 1f,
                Shadow, new Vector2(0f, -6f), 4f);
            // 카드가 raycast를 먹어야 카드 위를 눌렀을 때 뒤의 판이 반응하지 않는다.
            frame.raycastTarget = true;

            float top = CardSize.y * 0.5f - CardPadding;

            var title = Label(card.transform, "Title", TitleSize, TitleColor,
                TextAlignmentOptions.Center, font);
            title.fontStyle = FontStyles.Bold;
            BindFixedText(title, "Settings.Title", "환경 설정");
            Place((RectTransform)title.transform, new Vector2(0f, top - 16f),
                new Vector2(CardSize.x - CardPadding * 2f, 40f));

            float y = top - 66f;

            // 이름이 맨 위에 온다. 이 창에서 남이 보는 것은 이것 하나뿐이라,
            // 소리·화면처럼 나만 겪는 설정과 섞어 두면 눈에 띄지 않는다.
            wired.Nickname = NicknameRow(card.transform, font, y);
            y -= RowHeight + RowGap * 1.8f;

            wired.Master = SliderRow(card.transform, font, "전체 음량", y, out wired.MasterValue);
            y -= RowHeight + RowGap;
            wired.Music = SliderRow(card.transform, font, "배경음악", y, out wired.MusicValue);
            y -= RowHeight + RowGap;
            wired.Effects = SliderRow(card.transform, font, "효과음", y, out wired.EffectsValue);

            y -= RowHeight + RowGap * 1.8f;
            wired.Resolution = DropdownRow(card.transform, font, "해상도", y);
            y -= RowHeight + RowGap;
            wired.ScreenMode = DropdownRow(card.transform, font, "화면 모드", y);

            BuildButtonRow(card.transform, font, ref wired);

            return window;
        }

        /// <summary>
        /// 카드 아래쪽의 버튼 줄.
        ///
        /// <b>자리를 손으로 잡지 않는다.</b> 항복 버튼은 매치가 없는 곳(로비의 설정)에서
        /// 스스로 숨는데, 두 버튼의 좌표를 박아 두면 숨는 순간 닫기 하나만 한쪽으로
        /// 치우쳐 남는다. 가로 배치를 <see cref="HorizontalLayoutGroup"/>에 맡기면 몇
        /// 개가 켜져 있든 줄 전체가 가운데에 선다.
        /// </summary>
        private static void BuildButtonRow(
            Transform card, TMP_FontAsset font, ref Wired wired)
        {
            var row = Child(card, "Buttons");
            Place((RectTransform)row.transform,
                new Vector2(0f, -CardSize.y * 0.5f + CardPadding + ButtonRowHeight * 0.5f),
                new Vector2(CardSize.x - CardPadding * 2f, ButtonRowHeight));

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = ButtonGap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            wired.Close = TextButton(row.transform, font, "CloseButton", "닫기", CloseFill,
                Vector2.zero, CloseButtonSize);
            wired.Surrender = TextButton(row.transform, font, "SurrenderButton", "항복",
                SurrenderFill, Vector2.zero, SurrenderButtonSize);
            BindFixedText(wired.Close.GetComponentInChildren<TextMeshProUGUI>(true),
                "Settings.Close", "닫기");
            BindFixedText(wired.Surrender.GetComponentInChildren<TextMeshProUGUI>(true),
                "Settings.Surrender", "항복");

            // 배치는 레이아웃이 잡으므로 좌표는 지운다. Place가 남겨 둔 값이 그대로 있으면
            // 에디터에서 레이아웃이 한 번 돌기 전까지 두 버튼이 겹쳐 보인다.
            foreach (var button in new[] { wired.Close, wired.Surrender })
                ((RectTransform)button.transform).anchoredPosition = Vector2.zero;

            wired.Surrender.gameObject.SetActive(false);
        }

        /// <summary>
        /// 이름표 · 입력칸 한 줄.
        ///
        /// 부품 구성은 채팅 입력칸(<c>ChatPanelSetupTool.BuildInput</c>)과 같다 —
        /// TMP_InputField는 자를 칸(TextArea)과 글·안내 라벨이 정해진 모양으로 물려 있어야
        /// 하고, 한글을 조합하는 동안 밑줄 서식을 쓰기 때문에 richText를 켜 두어야 한다.
        /// </summary>
        private static TMP_InputField NicknameRow(
            Transform card, TMP_FontAsset font, float y, Vector2? cardSize = null)
        {
            Vector2 size = cardSize ?? CardSize;
            var row = Row(card, "Nickname", y, size);
            BindFixedText(NameLabel(row, font, "닉네임", size), "Settings.Nickname", "닉네임");

            float width = size.x - CardPadding * 2f - LabelWidth;
            float center = -size.x * 0.5f + CardPadding + LabelWidth + width * 0.5f;

            var go = Child(row, "Input");
            Place((RectTransform)go.transform, new Vector2(center, 0f),
                new Vector2(width, InputHeight));

            var frame = Frame(go, ListFill, 8f, 1.6f);

            var area = Child(go.transform, "TextArea");
            var areaRect = (RectTransform)area.transform;
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(12f, 4f);
            areaRect.offsetMax = new Vector2(-12f, -4f);
            area.AddComponent<RectMask2D>();

            var text = Label(area.transform, "Text", ValueSize, ValueColor,
                TextAlignmentOptions.Left, font);
            Stretch((RectTransform)text.transform);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;

            var hint = Label(area.transform, "Placeholder", ValueSize, LabelColor,
                TextAlignmentOptions.Left, font);
            Stretch((RectTransform)hint.transform);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            BindFixedText(hint, "Settings.Nickname.Placeholder", "이름을 지어 주세요");

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = frame;
            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine;
            // 글자 수 상한은 게임 규칙 쪽에서 온다. 여기 숫자를 적어 두면 규칙이 바뀔 때
            // 칸만 옛 상한으로 남는다.
            input.characterLimit = PlayerNames.MaxLength;
            input.caretColor = ValueColor;
            input.customCaretColor = true;
            input.selectionColor = new Color(0.82f, 0.58f, 0.22f, 0.45f);
            return input;
        }

        /// <summary>이름 · 슬라이더 · 백분율 한 줄.</summary>
        private static Slider SliderRow(
            Transform card, TMP_FontAsset font, string name, float y, out TextMeshProUGUI value,
            Vector2? cardSize = null)
        {
            Vector2 size = cardSize ?? CardSize;
            var row = Row(card, name.Replace(" ", string.Empty), y, size);
            NameLabel(row, font, name, size);

            float trackX = -size.x * 0.5f + CardPadding + LabelWidth;
            float trackWidth = size.x - CardPadding * 2f - LabelWidth - ValueWidth;

            var sliderGo = Child(row, "Slider");
            var sliderRect = (RectTransform)sliderGo.transform;
            sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0f, 0.5f);
            sliderRect.sizeDelta = new Vector2(trackWidth, 20f);
            sliderRect.anchoredPosition = new Vector2(trackX, 0f);

            var background = sliderGo.AddComponent<Image>();
            background.color = SliderTrack;

            var slider = sliderGo.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            // 만들어 두는 값은 기본값(가득)이다. 화면을 열면 저장된 값으로 다시 맞춰지지만,
            // 그 전에 캔버스가 이 값을 한 번 알려 오므로 0으로 두면 안 된다.
            slider.value = 1f;

            // 홈 — 손잡이 반지름만큼 양옆을 비워야 손잡이가 끝에서 튀어나오지 않는다.
            var track = Child(sliderGo.transform, "Track");
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.offsetMin = new Vector2(12f, -5f);
            trackRect.offsetMax = new Vector2(-12f, 5f);
            var trackImage = track.AddComponent<Image>();
            trackImage.color = TrackFill;
            trackImage.raycastTarget = false;

            var fillArea = Child(sliderGo.transform, "Fill Area");
            var fillAreaRect = (RectTransform)fillArea.transform;
            fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRect.offsetMin = new Vector2(12f, -5f);
            fillAreaRect.offsetMax = new Vector2(-12f, 5f);

            var fill = Child(fillArea.transform, "Fill");
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = BarFill;
            fillImage.raycastTarget = false;

            var handleArea = Child(sliderGo.transform, "Handle Slide Area");
            var handleAreaRect = (RectTransform)handleArea.transform;
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(12f, 0f);
            handleAreaRect.offsetMax = new Vector2(-12f, 0f);

            var handle = Child(handleArea.transform, "Handle");
            var handleRect = (RectTransform)handle.transform;
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.sizeDelta = new Vector2(24f, 0f);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = HandleFill;

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            value = Label(row, "Value", ValueSize, ValueColor, TextAlignmentOptions.Right, font);
            Place((RectTransform)value.transform,
                new Vector2(size.x * 0.5f - CardPadding - ValueWidth * 0.5f, 0f),
                new Vector2(ValueWidth, RowHeight));

            return slider;
        }

        /// <summary>
        /// 이름 · 펼침 목록 한 줄.
        ///
        /// TMP_Dropdown은 부품이 정해진 모양으로 물려 있어야 한다 — 눌렀을 때 펼쳐지는
        /// 판(Template)은 <b>꺼진 채</b>로 들고 있다가 복제되므로, 그 안의 앵커가 어긋나면
        /// 목록이 엉뚱한 자리에 열리거나 아예 보이지 않는다. 그래서 유니티가 만드는 구조를
        /// 그대로 따라 짠다.
        /// </summary>
        private static TMP_Dropdown DropdownRow(
            Transform card, TMP_FontAsset font, string name, float y, Vector2? cardSize = null)
        {
            Vector2 size = cardSize ?? CardSize;
            var row = Row(card, name.Replace(" ", string.Empty), y, size);
            NameLabel(row, font, name, size);

            float right = size.x * 0.5f - CardPadding;
            float width = DropdownWidth;

            var go = Child(row, "Dropdown");
            Place((RectTransform)go.transform,
                new Vector2(right - width * 0.5f, 0f), new Vector2(width, RowHeight - 4f));

            var frame = Frame(go, ButtonFill, 8f, 2f);
            var drop = go.AddComponent<TMP_Dropdown>();
            drop.targetGraphic = frame;

            var caption = Label(go.transform, "Label", ValueSize, ValueColor,
                TextAlignmentOptions.Left, font);
            var captionRect = (RectTransform)caption.transform;
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(14f, 0f);
            captionRect.offsetMax = new Vector2(-34f, 0f);

            // 펼쳐진다는 표시. 글자로 두면 글꼴에 그 글자가 없을 때 네모가 뜬다 —
            // 작은 삼각형은 어느 글꼴에도 기대지 않는다.
            var arrow = Child(go.transform, "Arrow");
            var arrowRect = (RectTransform)arrow.transform;
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(14f, 8f);
            arrowRect.anchoredPosition = new Vector2(-18f, 0f);
            var arrowImage = arrow.AddComponent<Image>();
            arrowImage.color = LabelColor;
            arrowImage.raycastTarget = false;

            BuildTemplate(drop, go.transform, font);
            return drop;
        }

        /// <summary>눌렀을 때 아래로 펼쳐지는 판. 꺼진 채로 들고 있다가 복제된다.</summary>
        private static void BuildTemplate(TMP_Dropdown drop, Transform parent, TMP_FontAsset font)
        {
            var template = Child(parent, "Template");
            var templateRect = (RectTransform)template.transform;
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, 2f);
            templateRect.sizeDelta = new Vector2(0f, TemplateHeight);

            // TMP_Dropdown은 펼칠 때 이 판을 복제해 서서히 드러내는데, 그 투명도를
            // CanvasGroup에서 읽는다. 없으면 펼치는 순간 예외가 난다.
            template.AddComponent<CanvasGroup>();

            var templateFrame = template.AddComponent<RoundedPanelGraphic>();
            templateFrame.Configure(ListFill, ButtonBorder, 8f, 2f,
                new Color(0f, 0f, 0f, 0f), 0f, 0f, Shadow, new Vector2(0f, -4f), 3f);

            var scroll = template.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var viewport = Child(template.transform, "Viewport");
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0f, 1f);
            viewportRect.offsetMin = new Vector2(4f, 4f);
            viewportRect.offsetMax = new Vector2(-ScrollbarWidth - 4f, -4f);
            // 잘라내기. Mask는 이 그림의 <b>알파</b>로 스텐실을 찍으므로 색은 안 그려도
            // 알파는 1이어야 한다 — 반투명하게 두면 잘려 나가는 쪽이 항목 전체가 된다
            // (실제로 목록이 텅 빈 채로 열렸다). showMaskGraphic이 꺼져 있어 화면에는 안 그려진다.
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.white;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = Child(viewport.transform, "Content");
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, ItemHeight);

            var item = Child(content.transform, "Item");
            var itemRect = (RectTransform)item.transform;
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, ItemHeight);

            var toggle = item.AddComponent<Toggle>();
            toggle.transition = Selectable.Transition.ColorTint;

            var itemBackground = Child(item.transform, "Item Background");
            Stretch((RectTransform)itemBackground.transform);
            var backgroundImage = itemBackground.AddComponent<Image>();
            backgroundImage.color = new Color(1f, 1f, 1f, 0f);

            // 고른 줄에 깔리는 띠. 참고한 화면처럼 글자를 지우지 않고 뒤만 밝힌다.
            var checkmark = Child(item.transform, "Item Checkmark");
            Stretch((RectTransform)checkmark.transform);
            var checkImage = checkmark.AddComponent<Image>();
            checkImage.color = ItemSelected;

            var itemLabel = Label(item.transform, "Item Label", ValueSize, ValueColor,
                TextAlignmentOptions.Center, font);
            var itemLabelRect = (RectTransform)itemLabel.transform;
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(10f, 0f);
            itemLabelRect.offsetMax = new Vector2(-10f, 0f);

            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkImage;

            var colors = toggle.colors;
            colors.highlightedColor = ItemHover;
            colors.pressedColor = ItemHover;
            colors.selectedColor = new Color(1f, 1f, 1f, 0f);
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            toggle.colors = colors;

            var scrollbar = BuildScrollbar(template.transform);

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            drop.template = templateRect;
            drop.captionText = parent.Find("Label").GetComponent<TextMeshProUGUI>();
            drop.itemText = itemLabel;

            template.SetActive(false);
        }

        private static Scrollbar BuildScrollbar(Transform template)
        {
            var go = Child(template, "Scrollbar");
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(-ScrollbarWidth, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);

            var track = go.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.06f);

            var scrollbar = go.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var slidingArea = Child(go.transform, "Sliding Area");
            Stretch((RectTransform)slidingArea.transform);

            var handle = Child(slidingArea.transform, "Handle");
            var handleRect = (RectTransform)handle.transform;
            handleRect.sizeDelta = Vector2.zero;
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = BarFill;

            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleRect;
            return scrollbar;
        }

        private static Transform Row(Transform card, string name, float y, Vector2 cardSize)
        {
            var go = Child(card, "Row_" + name);
            Place((RectTransform)go.transform, new Vector2(0f, y),
                new Vector2(cardSize.x, RowHeight));
            return go.transform;
        }

        private static TextMeshProUGUI NameLabel(
            Transform row, TMP_FontAsset font, string text, Vector2 cardSize)
        {
            var label = Label(row, "Name", LabelSize, LabelColor, TextAlignmentOptions.Left, font);
            label.text = text;
            Place((RectTransform)label.transform,
                new Vector2(-cardSize.x * 0.5f + CardPadding + LabelWidth * 0.5f, 0f),
                new Vector2(LabelWidth, RowHeight));
            return label;
        }

        private static Button TextButton(
            Transform parent, TMP_FontAsset font, string name, string text, Color fill,
            Vector2 position, Vector2 size)
        {
            var go = Child(parent, name);
            Place((RectTransform)go.transform, position, size);

            var frame = Frame(go, fill, 8f, 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = frame;

            var label = Label(go.transform, "Label", ButtonSize, TitleColor,
                TextAlignmentOptions.Center, font);
            Stretch((RectTransform)label.transform);
            label.text = text;

            return button;
        }

        // ── 배선 ─────────────────────────────────────────────────────

        private static void Wire(SettingsPanel panel, GameObject window, Wired w)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("window").objectReferenceValue = window;
            so.FindProperty("scrim").objectReferenceValue = w.Scrim;
            so.FindProperty("btnOpen").objectReferenceValue = w.Open;
            so.FindProperty("btnClose").objectReferenceValue = w.Close;
            so.FindProperty("btnSurrender").objectReferenceValue = w.Surrender;

            so.FindProperty("sliderMaster").objectReferenceValue = w.Master;
            so.FindProperty("sliderMusic").objectReferenceValue = w.Music;
            so.FindProperty("sliderEffects").objectReferenceValue = w.Effects;
            so.FindProperty("txtMaster").objectReferenceValue = w.MasterValue;
            so.FindProperty("txtMusic").objectReferenceValue = w.MusicValue;
            so.FindProperty("txtEffects").objectReferenceValue = w.EffectsValue;

            so.FindProperty("inputNickname").objectReferenceValue = w.Nickname;

            so.FindProperty("dropResolution").objectReferenceValue = w.Resolution;
            so.FindProperty("dropScreenMode").objectReferenceValue = w.ScreenMode;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindFixedText(TMP_Text target, string key, string fallback) =>
            ZooJackTextBinding.Bind(target, key, fallback);

        // ── 잔손질 ───────────────────────────────────────────────────

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static RoundedPanelGraphic Frame(
            GameObject go, Color fill, float radius, float thickness)
        {
            var frame = go.AddComponent<RoundedPanelGraphic>();
            frame.Configure(fill, ButtonBorder, radius, thickness,
                new Color(0f, 0f, 0f, 0f), 0f, 0f,
                Shadow, new Vector2(0f, -3f), 2f);
            frame.raycastTarget = true;
            return frame;
        }

        private static TextMeshProUGUI Label(
            Transform parent, string name, float size, Color color,
            TextAlignmentOptions align, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 씬이 이미 쓰고 있는 글꼴을 그대로 빌린다. 경로로 지정해 두면 글꼴을 바꿀 때
        /// 이 화면만 옛 글꼴로 남는다.
        /// </summary>
        private static TMP_FontAsset BorrowFont(Transform canvas)
        {
            foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.font != null) return text.font;
            return null;
        }
    }
}
