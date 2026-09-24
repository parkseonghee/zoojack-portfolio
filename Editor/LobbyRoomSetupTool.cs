#if UNITY_EDITOR && (FUSION2 || ZOOJACK_PHOTON_FUSION)
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// 로비의 "방 안" 화면(PanelRoom)을 만든다. 목록 형태였던 옛 PanelLobby를 지우고,
    /// 게임 테이블과 같은 그림 위에서 캐릭터를 고르고 돌아다니는 화면으로 바꾼다.
    ///
    /// 여러 번 눌러도 안전하다 — PanelRoom은 통째로 다시 만든다. 손으로 고친 값이
    /// 있으면 날아가므로, 자리·크기를 바꿀 일이 생기면 씬이 아니라 여기를 고치는 게 낫다.
    /// </summary>
    public static class LobbyRoomSetupTool
    {
        private const string RoomPanelName = "PanelRoom";
        private const string LegacyPanelName = "PanelLobby";

        private const string FontPath = "Assets/ImportedAsset/Pretendard-1.3.9/Pretendard_SDF.asset";
        private const string TablePath = "Assets/ImportedAsset/Table/zj_table_background.png";
        private const string AvatarFolder = "Assets/ImportedAsset/Characters";
        private const string IconFolder = "Assets/ImportedAsset/CharacterIcons";

        // 로비 화면은 게임 화면에서 구운 프리팹 세 개로 조립한다. 값을 코드에 옮겨 적으면
        // 한쪽만 고쳤을 때 조용히 갈라진다 — 명패에서 실제로 한 번 그랬다.
        /// <summary>머리 위 명패. 게임 씬 <c>AvatarPlayerA/Plate</c>를 구운 것.</summary>
        private const string PlatePrefabPath = "Assets/Prefabs/ZJ_AvatarPlate.prefab";

        /// <summary>히트·스탠드와 같은 결정 버튼. 게임 씬 <c>btnHit</c>을 구운 것.</summary>
        private const string ButtonPrefabPath = "Assets/Prefabs/ZJ_ActionButton.prefab";

        /// <summary>잔액 카드와 같은 초상화 카드. 게임 씬 <c>ZJ_BalanceCard_A</c>를 구운 것.</summary>
        private const string CardPrefabPath = "Assets/Prefabs/ZJ_CharacterCard.prefab";

        // 게임 씬 결정 버튼의 배경색. 준비는 히트(초록), 나가기는 다이(적갈)를 따른다.
        private static readonly Color ButtonGreen = ZooJackPalette.Hex(0x09401F, 0.98f);
        private static readonly Color ButtonMaroon = ZooJackPalette.Maroon.Alpha(0.98f);

        // 게임 씬 잔액 카드의 자리 색. 로비에서도 같은 색을 써야 자리가 이어져 보인다.
        private static readonly Color SeatNavy = ZooJackPalette.SeatNavy.Alpha(0.99f);   // 플레이어 A
        private static readonly Color SeatMaroon = ZooJackPalette.SeatMaroon.Alpha(0.98f); // 플레이어 B
        private static readonly Color SeatBrown = ZooJackPalette.SeatBrown;     // 딜러

        /// <summary>명패가 캐릭터 중심에서 떠 있는 높이(px). 게임 씬과 같은 값.</summary>
        private const float PlateHeight = 78f;

        private const float ButtonWidth = 170f;   // 게임은 146. "준비 취소"가 들어갈 만큼만 넓혔다.
        private const float ButtonHeight = 76f;   // 게임과 같은 높이

        // GameScene/Canvas/TableArea/TableMovementBounds와 시작점의 실제 값.
        // 로비에서도 같은 이동 판정을 쓰기 위해 PanelRoom 좌표계에 그대로 둔다.
        private static readonly Vector2 MovementBoundsPosition = new Vector2(0f, -17f);
        private static readonly Vector2 MovementBoundsSize = new Vector2(1600f, 900f);
        private static readonly Vector3 MovementBoundsScale = new Vector3(1.08f, 0.88f, 1f);
        private static readonly Vector2 SpawnPlayerA = new Vector2(-280f, -162f);
        private static readonly Vector2 SpawnPlayerB = new Vector2(280f, -162f);
        private static readonly Vector2 SpawnDealer = new Vector2(0f, 152f);

        // 색은 게임 화면의 나무·펠트 톤에 맞춘다.
        private static readonly Color Gold = new Color(0.92f, 0.76f, 0.30f);
        private static readonly Color Cream = new Color(0.93f, 0.90f, 0.80f);
        private static readonly Color Muted = new Color(0.72f, 0.69f, 0.60f);
        private static readonly Color BarBackground = new Color(0.05f, 0.07f, 0.06f, 0.86f);
        private static readonly Color BarBorder = new Color(0.55f, 0.40f, 0.16f, 0.85f);

        private static readonly (CharacterId Id, string Avatar, string Icon, Color Seat)[] Cast =
        {
            (CharacterId.Rabbit, "rabbit_idle", "rabbit_icon", SeatNavy),
            (CharacterId.Fox,    "fox_idle",    "fox_icon",    SeatMaroon),
            (CharacterId.Croc,   "croc_idle",   "croc_icon",   SeatBrown)
        };


        private static TMP_FontAsset font;

        [MenuItem("ZooJack/로비/방 화면 만들기")]
        public static void Setup()
        {
            var manager = Object.FindFirstObjectByType<NetworkLobbyManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                EditorUtility.DisplayDialog("방 화면 만들기",
                    "열려 있는 씬에서 NetworkLobbyManager를 찾지 못했습니다. LobbyScene을 먼저 여세요.", "확인");
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("방 화면 만들기", "씬에 Canvas가 없습니다.", "확인");
                return;
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                Debug.LogWarning($"[LobbyRoomSetupTool] 폰트를 찾지 못했습니다: {FontPath} — 한글이 깨질 수 있습니다.");

            var canvasRect = (RectTransform)canvas.transform;
            DestroyChild(canvasRect, LegacyPanelName);
            DestroyChild(canvasRect, RoomPanelName);

            var room = BuildRoom(canvasRect, manager);
            EnsureRoomAvatarParity(manager, room);
            room.gameObject.SetActive(false); // 시작은 메인 메뉴. 실행 중 ShowOnly가 켠다.

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Selection.activeObject = room.gameObject;

            Debug.Log("[LobbyRoomSetupTool] PanelRoom을 만들고 NetworkLobbyManager에 배선했습니다. " +
                      "씬을 저장하세요.");
        }

        // ── 캐릭터 선택 안내 ─────────────────────────────────────────

        private const string HintName = "Hint";
        private const string PromptName = "SelectPrompt";

        /// <summary>카드 위를 가리키는 화살표. 글꼴 글자라 그림 파일이 필요 없다.</summary>
        private const string HintGlyph = "▼";
        private static readonly Vector2 HintSize = new Vector2(72f, 62f);
        private const float HintFontSize = 48f;
        /// <summary>
        /// 화살표가 쉬는 자리(카드 윗변에서 px). 여기서 <c>LobbyCharacterCard.HintBobDistance</c>
        /// 만큼 올라갔다 돌아오므로, 실제로 오가는 구간은 7~15px이다.
        /// </summary>
        private const float HintGap = 7f;

        private static readonly Vector2 PromptSize = new Vector2(760f, 56f);
        private const float PromptFontSize = 44f;
        private const float PromptTop = -34f;  // TopBar 윗변에서 내려온 거리(px)

        /// <summary>
        /// "어디를 눌러야 하는지 몰랐다"는 말에 답하는 두 가지를 붙인다 —
        /// 빈 카드 위의 화살표와 화면 위쪽의 안내 문구.
        ///
        /// <b>방 화면을 다시 만들지 않는다.</b> <see cref="Setup"/>는 PanelRoom을 지우고
        /// 새로 짓기 때문에 그 뒤에 손으로 맞춰 둔 배치까지 함께 사라진다. 이 메뉴는
        /// 있는 화면에 없는 것만 더한다. 여러 번 눌러도 같은 결과다.
        /// </summary>
        [MenuItem("ZooJack/로비/캐릭터 선택 안내 만들기")]
        public static void SetupSelectionGuide()
        {
            var manager = Object.FindFirstObjectByType<NetworkLobbyManager>(FindObjectsInactive.Include);
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var room = canvas != null ? canvas.transform.Find(RoomPanelName) as RectTransform : null;
            if (manager == null || room == null)
            {
                EditorUtility.DisplayDialog("캐릭터 선택 안내",
                    "열려 있는 씬에서 NetworkLobbyManager나 PanelRoom을 찾지 못했습니다. " +
                    "LobbyScene을 먼저 여세요.", "확인");
                return;
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            int arrows = EnsureCardHintArrows(room);
            var prompt = EnsureSelectPrompt(room);

            var so = new SerializedObject(manager);
            Assign(so, "selectPrompt", prompt != null ? prompt.gameObject : null);
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Debug.Log($"[LobbyRoomSetupTool] 캐릭터 선택 안내를 붙였습니다. " +
                      $"카드 화살표 {arrows}개, 안내 문구 {PromptName}. 씬을 저장하세요.");
        }

        /// <summary>
        /// 화살표를 카드마다 세우고 <see cref="LobbyCharacterCard"/>에 물린다.
        ///
        /// <b>모양은 프리팹에, 참조는 씬에.</b> 세 카드는 같은 프리팹의 인스턴스라 화살표
        /// 자체는 프리팹에 한 번만 넣으면 셋에 함께 선다. 그런데 <c>LobbyCharacterCard</c>는
        /// 프리팹이 아니라 <b>씬의 인스턴스마다 따로 붙어</b> 있어서(프리팹 루트에는 없다),
        /// 어느 화살표가 자기 것인지는 카드마다 씬에서 물려 줘야 한다.
        /// </summary>
        private static int EnsureCardHintArrows(RectTransform room)
        {
            if (!EnsureHintInPrefab()) return 0;

            int wired = 0;
            foreach (var card in room.GetComponentsInChildren<LobbyCharacterCard>(true))
            {
                var hint = card.transform.Find(HintName) as RectTransform;
                if (hint == null)
                {
                    Debug.LogWarning($"[LobbyRoomSetupTool] {card.name}에서 {HintName}을(를) " +
                                     "찾지 못했습니다. 카드가 프리팹 인스턴스가 아닐 수 있습니다.");
                    continue;
                }

                var so = new SerializedObject(card);
                so.FindProperty("hintArrow").objectReferenceValue = hint;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(card);
                wired++;
            }
            return wired;
        }

        /// <summary>카드 프리팹에 화살표 오브젝트를 넣는다(이미 있으면 새로 굽는다).</summary>
        private static bool EnsureHintInPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath) == null)
            {
                Debug.LogWarning($"[LobbyRoomSetupTool] 카드 프리팹을 찾지 못했습니다: {CardPrefabPath}");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
            try
            {
                var rect = (RectTransform)root.transform;
                DestroyChild(rect, HintName);

                var label = Label(rect, HintName, HintGlyph, HintFontSize, Gold,
                    TextAlignmentOptions.Center);
                var hint = (RectTransform)label.transform;

                // 카드 윗변 바깥에 선다. 카드 안에 넣으면 이름·상태 줄과 자리를 다투고,
                // 위아래로 오갈 때 카드 경계에서 잘린다.
                hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 1f);
                hint.pivot = new Vector2(0.5f, 0f);
                hint.sizeDelta = HintSize;
                hint.anchoredPosition = new Vector2(0f, HintGap);

                // 꺼진 채로 굽는다. 카드가 자기 상태를 보고 켠다(LobbyCharacterCard.Render).
                label.gameObject.SetActive(false);

                PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 화면 위쪽 가운데의 안내 문구. 방 번호와 조작 안내가 오른쪽에 몰려 있어
        /// 가운데가 비어 있고, 테이블 위라 아무것도 가리지 않는다.
        /// </summary>
        private static RectTransform EnsureSelectPrompt(RectTransform room)
        {
            var bar = room.Find("TopBar") as RectTransform ?? room;
            DestroyChild(bar, PromptName);

            var label = Label(bar, PromptName,
                ZooJackText.Get("Lobby.SelectPrompt", "역할을 선택하세요"),
                PromptFontSize, Gold, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;

            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = PromptSize;
            rect.anchoredPosition = new Vector2(0f, PromptTop);

            ZooJackTextBinding.Bind(label, "Lobby.SelectPrompt", label.text);
            return rect;
        }

        // ── 방 인원 표시 ─────────────────────────────────────────────

        private const string CountName = "RoomCount";
        private const string PersonnelIconPath = "Assets/ImportedAsset/UI Image/personnel.png";

        // 방 번호와 조작 안내 왼쪽의 빈자리. 안내 문구(가운데)와 방 번호(오른쪽) 사이다.
        private static readonly Vector2 CountPosition = new Vector2(-280f, -60f);
        private static readonly Vector2 CountSize = new Vector2(120f, 44f);
        private static readonly Vector2 CountIconSize = new Vector2(30f, 24f);
        private const float CountPadding = 14f;   // 판 안쪽 여백(px)
        private const float CountFontSize = 22f;

        /// <summary>
        /// 방에 몇 명이 들어왔는지 알려 주는 작은 판. 사람 아이콘 하나와 숫자뿐이다.
        ///
        /// <b>방 화면을 다시 만들지 않는다.</b> 있는 TopBar에 없는 것만 더한다 —
        /// <see cref="SetupSelectionGuide"/>와 같은 이유다. 여러 번 눌러도 결과가 같다.
        /// </summary>
        [MenuItem("ZooJack/로비/방 인원 표시 만들기")]
        public static void SetupRoomCount()
        {
            var manager = Object.FindFirstObjectByType<NetworkLobbyManager>(FindObjectsInactive.Include);
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var room = canvas != null ? canvas.transform.Find(RoomPanelName) as RectTransform : null;
            if (manager == null || room == null)
            {
                EditorUtility.DisplayDialog("방 인원 표시",
                    "열려 있는 씬에서 NetworkLobbyManager나 PanelRoom을 찾지 못했습니다. " +
                    "LobbyScene을 먼저 여세요.", "확인");
                return;
            }

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var count = BuildRoomCount(room);
            var so = new SerializedObject(manager);
            Assign(so, "txtRoomCount", count);
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Debug.Log($"[LobbyRoomSetupTool] 방 인원 표시({CountName})를 붙였습니다. 씬을 저장하세요.");
        }

        private static TextMeshProUGUI BuildRoomCount(RectTransform room)
        {
            var bar = room.Find("TopBar") as RectTransform ?? room;
            DestroyChild(bar, CountName);

            var plate = Create(bar, CountName, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 0.5f));
            plate.sizeDelta = CountSize;
            plate.anchoredPosition = CountPosition;

            // 아래 바와 같은 판이다. 위아래에 같은 것이 서 있어야 한 화면으로 읽힌다.
            var frame = plate.gameObject.AddComponent<RoundedPanelGraphic>();
            frame.Configure(BarBackground, BarBorder, 12f, 2f,
                new Color(0f, 0f, 0f, 0f), 0f, 0f,
                new Color(0f, 0f, 0f, 0f), Vector2.zero, 0f);
            frame.raycastTarget = false;   // 표시일 뿐이라 누를 것이 없다

            var iconRect = Create(plate, "Icon", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f));
            iconRect.sizeDelta = CountIconSize;
            iconRect.anchoredPosition = new Vector2(CountPadding, 0f);

            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PersonnelIconPath);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (icon.sprite == null)
                Debug.LogWarning($"[LobbyRoomSetupTool] 아이콘을 찾지 못했습니다: {PersonnelIconPath}");

            // 숫자는 아이콘 오른쪽 남은 자리를 채운다. 정원까지 두 자리라 넓힐 일이 없다.
            float textLeft = CountPadding + CountIconSize.x + 8f;
            var label = Label(plate, "Count", "0 / 3", CountFontSize, Cream,
                TextAlignmentOptions.Left);
            label.fontStyle = FontStyles.Bold;

            var textRect = (RectTransform)label.transform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(textLeft, 0f);
            textRect.offsetMax = new Vector2(-CountPadding, 0f);

            return label;
        }

        // ── 화면 조립 ────────────────────────────────────────────────

        private static RectTransform BuildRoom(RectTransform canvas, NetworkLobbyManager manager)
        {
            var room = CreateStretched(canvas, RoomPanelName);

            AddTable(room);
            EnsureMovementBounds(room);
            AddTopBar(room, out var roomInfo);
            var bottom = AddBottomBar(room, out var cards, out var ready, out var readyLabel, out var leave);

            // 캐릭터는 맨 마지막에 그린다 — 테이블을 돌아다니는 내내 UI 위로 보여야 한다.
            // 대신 아래 바를 가리면 반투명해지도록 가림 대상으로 넘겨 준다. 위쪽 바는
            // 이동 범위(타원 위끝 y=+260)에 닿지 않으므로 넘기지 않는다.
            var stage = AddAvatarStage(room, new[] { bottom });

            var so = new SerializedObject(manager);
            Assign(so, "panelRoom", room.gameObject);
            Assign(so, "txtRoomInfo", roomInfo);
            Assign(so, "avatarStage", stage);
            Assign(so, "btnReady", ready);
            Assign(so, "txtReadyLabel", readyLabel);
            Assign(so, "btnLeave", leave);
            AssignRuntimeAvatarSprites(so);

            var array = so.FindProperty("characterCards");
            array.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];

            so.ApplyModifiedProperties();
            return room;
        }

        /// <summary>기존 PanelRoom을 지우지 않고 GameScene과 같은 아바타 기능으로 갱신한다.</summary>
        [MenuItem("ZooJack/로비/GameScene 아바타 동작 동기화")]
        public static void SyncExistingRoomAvatarSystem()
        {
            var manager = Object.FindFirstObjectByType<NetworkLobbyManager>(FindObjectsInactive.Include);
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var room = canvas != null ? canvas.transform.Find(RoomPanelName) as RectTransform : null;
            if (manager == null || room == null)
            {
                EditorUtility.DisplayDialog("로비 아바타 동기화",
                    "LobbyScene의 NetworkLobbyManager 또는 PanelRoom을 찾지 못했습니다.", "확인");
                return;
            }

            EnsureRoomAvatarParity(manager, room);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            EditorSceneManager.SaveScene(manager.gameObject.scene);
            Debug.Log("[LobbyRoomSetupTool] PanelRoom 아바타를 GameScene과 같은 크기·애니메이션·감정표현 구성으로 갱신했습니다.");
        }

        internal static void EnsureRoomAvatarParity(NetworkLobbyManager manager, RectTransform room)
        {
            if (manager == null || room == null) return;

            EnsureMovementBounds(room);
            var managerSo = new SerializedObject(manager);
            var stage = managerSo.FindProperty("avatarStage")?.objectReferenceValue as AvatarStage
                        ?? room.Find("AvatarStage")?.GetComponent<AvatarStage>();
            if (stage != null)
            {
                AvatarStageSetupTool.EnsureStage(stage, true);
                Assign(managerSo, "avatarStage", stage);
            }

            AssignRuntimeAvatarSprites(managerSo);
            EmotionWheelSetupTool.Build();
            Assign(managerSo, "emotionWheel",
                EmotionWheelController.FindInScene());
            managerSo.ApplyModifiedProperties();
        }

        private static void AssignRuntimeAvatarSprites(SerializedObject managerSo)
        {
            // Catalog는 원본 Idle을 캐릭터 식별 키로 사용한다. 실행 중 SetSprite로 원본을
            // 넘기면 Animator가 즉시 512x512 정규화 Idle/Walk 클립을 재생한다.
            // 여기까지 정규화 Sprite로 바꾸면 Catalog 조회가 실패해 걷기 애니메이션이 꺼진다.
            Assign(managerSo, "avatarRabbit", LoadLargest($"{AvatarFolder}/rabbit_idle.png"));
            Assign(managerSo, "avatarFox", LoadLargest($"{AvatarFolder}/fox_idle.png"));
            Assign(managerSo, "avatarCroc", LoadLargest($"{AvatarFolder}/croc_idle.png"));
        }

        /// <summary>
        /// 기존 PanelRoom을 지우지 않고 GameScene과 같은 이동 타원·시작점을 추가하거나 갱신한다.
        /// LobbyPanelSetupTool에서도 호출하므로 상단 아이콘만 다시 만들 때도 경계가 보존된다.
        /// </summary>
        internal static TableMovementBounds EnsureMovementBounds(RectTransform room)
        {
            if (room == null) return null;

            var spawnRoot = EnsureRect(room, "AvatarSpawnPoints");
            CenterAt(spawnRoot, Vector2.zero, Vector2.zero, Vector3.one);
            var spawnA = EnsureRect(spawnRoot, "Spawn_PlayerA");
            var spawnB = EnsureRect(spawnRoot, "Spawn_PlayerB");
            var spawnD = EnsureRect(spawnRoot, "Spawn_Dealer");
            CenterAt(spawnA, SpawnPlayerA, Vector2.zero, Vector3.one);
            CenterAt(spawnB, SpawnPlayerB, Vector2.zero, Vector3.one);
            CenterAt(spawnD, SpawnDealer, Vector2.zero, Vector3.one);

            var boundsRect = EnsureRect(room, "TableMovementBounds");
            CenterAt(boundsRect, MovementBoundsPosition, MovementBoundsSize, MovementBoundsScale);
            var bounds = boundsRect.GetComponent<TableMovementBounds>()
                ?? boundsRect.gameObject.AddComponent<TableMovementBounds>();
            var so = new SerializedObject(bounds);
            Assign(so, "playerASpawnPoint", spawnA);
            Assign(so, "playerBSpawnPoint", spawnB);
            Assign(so, "dealerSpawnPoint", spawnD);
            so.ApplyModifiedProperties();

            // AvatarStage보다 먼저 활성화돼 Awake의 ResetPositions가 이 경계를 읽게 한다.
            spawnRoot.SetSiblingIndex(Mathf.Min(1, room.childCount - 1));
            boundsRect.SetSiblingIndex(Mathf.Min(2, room.childCount - 1));

            SetExistingAvatarPosition(room, "AvatarPlayerA", SpawnPlayerA);
            SetExistingAvatarPosition(room, "AvatarPlayerB", SpawnPlayerB);
            SetExistingAvatarPosition(room, "AvatarDealer", SpawnDealer);
            EditorUtility.SetDirty(bounds);
            return bounds;
        }

        private static RectTransform EnsureRect(RectTransform parent, string name)
        {
            var rect = parent.Find(name) as RectTransform;
            return rect != null ? rect : Create(parent, name,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }

        private static void CenterAt(
            RectTransform rect, Vector2 position, Vector2 size, Vector3 scale)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = scale;
        }

        private static void SetExistingAvatarPosition(
            RectTransform room, string avatarName, Vector2 position)
        {
            var avatar = room.Find("AvatarStage/" + avatarName) as RectTransform;
            if (avatar != null) avatar.anchoredPosition = position;
        }

        private static void AddTable(RectTransform room)
        {
            var table = CreateStretched(room, "Table");
            var image = table.gameObject.AddComponent<Image>();
            image.sprite = LoadLargest(TablePath);
            image.raycastTarget = false;
            if (image.sprite == null)
                Debug.LogWarning($"[LobbyRoomSetupTool] 테이블 그림을 찾지 못했습니다: {TablePath}");
        }

        private static RectTransform AddTopBar(RectTransform room, out TextMeshProUGUI roomInfo)
        {
            var bar = Create(room, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            bar.sizeDelta = new Vector2(0f, 120f);
            bar.anchoredPosition = Vector2.zero;

            var title = Label(bar, "Title", "ZOO JACK", 44f, Gold, TextAlignmentOptions.Left);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 52f), new Vector2(48f, -30f));

            var subtitle = Label(bar, "Subtitle", "방 로비", 22f, Muted, TextAlignmentOptions.Left);
            Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(320f, 30f), new Vector2(52f, -78f));

            roomInfo = Label(bar, "txtRoomInfo", "방: -", 24f, Cream, TextAlignmentOptions.Right);
            Place(roomInfo.rectTransform, new Vector2(1f, 1f), new Vector2(620f, 40f), new Vector2(-48f, -34f));

            // 조작 안내는 위쪽에 둔다. 아래 바에 넣으면 카드가 들어갈 높이를 깎아야 하는데,
            // 그러면 바가 캐릭터 시작 위치까지 올라와 서로를 가린다.
            var hint = Label(bar, "Hint", "WASD · 방향키로 이동", 18f, Muted, TextAlignmentOptions.Right);
            Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(400f, 28f), new Vector2(-48f, -76f));

            return bar;
        }

        private static RectTransform AddBottomBar(
            RectTransform room,
            out LobbyCharacterCard[] cards,
            out Button ready,
            out TextMeshProUGUI readyLabel,
            out Button leave)
        {
            // 높이와 위치는 캐릭터 시작 위치에서 거꾸로 잡았다. A·B는 y = -330에 서고
            // 몸이 112px이므로 발끝이 -386이다. 바의 윗변이 그보다 아래(-420)여야
            // 방에 들어서자마자 캐릭터와 카드가 서로를 가리지 않는다.
            // (일부러 아래로 걸어 내려간 경우에만 겹치고, 그때는 반투명 처리가 받는다.)
            var bar = Create(room, "BottomBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            bar.sizeDelta = new Vector2(1480f, 110f);
            bar.anchoredPosition = new Vector2(0f, 10f);
            Panel(bar, BarBackground, BarBorder, 20f);

            // 카드(250)와 버튼(170)은 게임 화면과 같은 높이(76)로 한 줄에 늘어놓고,
            // 전체 덩어리를 바 가운데에 맞춘다 — 좌우 여백이 145로 같아진다.
            cards = new LobbyCharacterCard[Cast.Length];
            float x = -470f;
            for (int i = 0; i < Cast.Length; i++)
            {
                cards[i] = CreateCard(bar, Cast[i], new Vector2(x, 0f));
                x += 260f;
            }

            ready = CreateButton(bar, "btnReady", "준비", new Vector2(320f, 0f), ButtonGreen, out readyLabel);
            leave = CreateButton(bar, "btnLeave", "나가기", new Vector2(510f, 0f), ButtonMaroon, out _);

            return bar;
        }

        /// <summary>
        /// 게임 화면의 잔액 카드를 구운 프리팹으로 캐릭터 카드를 만든다.
        /// 자리 색(파랑·갈색·자주)만 인스턴스에서 갈아 끼우고 나머지는 프리팹을 따른다.
        /// </summary>
        private static LobbyCharacterCard CreateCard(
            RectTransform parent, (CharacterId Id, string Avatar, string Icon, Color Seat) entry, Vector2 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[LobbyRoomSetupTool] 카드 프리팹을 찾지 못했습니다: {CardPrefabPath}");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = $"Card{entry.Id}";
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;

            var panel = go.GetComponent<RoundedPanelGraphic>();
            var iconImage = go.transform.Find("PortraitSlot/Portrait")?.GetComponent<Image>();
            var nameLabel = go.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            var stateLabel = go.transform.Find("State")?.GetComponent<TextMeshProUGUI>();
            var button = go.GetComponent<Button>();
            if (panel == null || iconImage == null || nameLabel == null || stateLabel == null || button == null)
            {
                Debug.LogError($"[LobbyRoomSetupTool] 카드 프리팹 구조가 예상과 다릅니다: {CardPrefabPath}");
                return null;
            }

            // 자리 색만 인스턴스에서 바꾼다(프리팹 오버라이드). 나머지는 프리팹이 정한다.
            var pso = new SerializedObject(panel);
            pso.FindProperty("backgroundColor").colorValue = entry.Seat;
            pso.ApplyModifiedProperties();

            // 원본은 게임 화면의 잔액 카드라 보여주기 전용이고 raycastTarget이 꺼져 있다.
            // 로비에서는 이 카드가 곧 버튼이므로 켜 준다. 이게 없으면 클릭이 카드를 그냥
            // 통과해서 캐릭터를 고를 수 없다.
            panel.raycastTarget = true;

            iconImage.sprite = LoadLargest($"{IconFolder}/{entry.Icon}.png");
            if (iconImage.sprite == null)
                Debug.LogWarning($"[LobbyRoomSetupTool] 아이콘을 찾지 못했습니다: {entry.Icon}.png");

            // 실행 중에도 LobbyCharacterCard가 다시 넣지만, 여기서 채워야
            // 에디터에서 씬을 열었을 때 카드가 무엇인지 눈으로 확인할 수 있다.
            nameLabel.text = LobbyCharacterCard.HeadlineFor(entry.Id);
            nameLabel.fontSize = 16f;
            stateLabel.text = "빈자리";

            var view = go.AddComponent<LobbyCharacterCard>();
            var so = new SerializedObject(view);
            so.FindProperty("character").enumValueIndex = (int)entry.Id;
            Assign(so, "button", button);
            Assign(so, "panel", panel);
            Assign(so, "icon", iconImage);
            Assign(so, "nameLabel", nameLabel);
            Assign(so, "stateLabel", stateLabel);
            so.ApplyModifiedProperties();

            return view;
        }

        private static AvatarStage AddAvatarStage(RectTransform room, RectTransform[] occluders)
        {
            var stageRect = CreateStretched(room, "AvatarStage");
            var stage = stageRect.gameObject.AddComponent<AvatarStage>();
            var so = new SerializedObject(stage);

            foreach (var (role, objectName, field) in new[]
                     {
                         (PlayerRole.PlayerA, "AvatarPlayerA", "avatarPlayerA"),
                         (PlayerRole.PlayerB, "AvatarPlayerB", "avatarPlayerB"),
                         (PlayerRole.Dealer,  "AvatarDealer",  "avatarDealer")
                     })
            {
                Assign(so, field, CreateAvatar(stageRect, role, objectName));
            }

            var extra = so.FindProperty("extraOccluders");
            extra.arraySize = occluders.Length;
            for (int i = 0; i < occluders.Length; i++)
                extra.GetArrayElementAtIndex(i).objectReferenceValue = occluders[i];

            so.ApplyModifiedProperties();
            return stage;
        }

        private static PlayerAvatarView CreateAvatar(RectTransform stage, PlayerRole role, string objectName)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(stage, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = AvatarMovement.SpawnPositionFor(role);
            // PlayerAvatarView가 실행 중에 자기 size 필드로 다시 맞추지만, 여기서도 넣어야
            // 에디터에서 씬을 열었을 때 100x100 기본값으로 보이지 않는다.
            rect.sizeDelta = new Vector2(168f, 112f);

            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            // 머리 위 명패는 게임 화면과 <b>같은 프리팹</b>을 쓴다. 값을 베껴 적으면
            // 한쪽만 고쳤을 때 조용히 갈라진다 — 실제로 한 번 그랬다(테두리 색·안쪽 테두리·
            // 그림자·타이머 위치가 전부 달랐다).
            var plate = InstantiatePlate(rect);
            if (plate == null) return null;

            var plateName = plate.Find("PlateName")?.GetComponent<TextMeshProUGUI>();
            var plateTimer = plate.Find("PlateTimer")?.GetComponent<TextMeshProUGUI>();
            if (plateName == null || plateTimer == null)
            {
                Debug.LogError($"[LobbyRoomSetupTool] 명패 프리팹에 PlateName/PlateTimer가 없습니다: {PlatePrefabPath}");
                return null;
            }
            // 게임 씬처럼 역할 이름을 넣어 둔다. 비워 두면 에디터에서 명패가 찌그러져
            // 보여 크기를 가늠할 수 없다. 실행 중에는 SetPlate가 다시 채운다.
            plateName.text = RoleKo(role);
            plateTimer.text = string.Empty;
            plateTimer.gameObject.SetActive(false);   // 로비에는 제한시간이 없다

            // 준비 표시는 명패 위에 붙인다. 게임 화면처럼 발밑(-72)에 두면 A·B가 서 있는
            // 자리에서는 그 글자만 아래 바 안으로 들어가 선택 카드를 덮는다.
            var nameLabel = Label(rect, "Name", "", 18f, AvatarStage.ReadyBadgeColor,
                TextAlignmentOptions.Center);
            Place(nameLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(200f, 28f), new Vector2(0f, 112f));

            var view = go.AddComponent<PlayerAvatarView>();
            var so = new SerializedObject(view);
            Assign(so, "image", image);
            Assign(so, "nameLabel", nameLabel);
            Assign(so, "plateRoot", plate);
            Assign(so, "plateName", plateName);
            Assign(so, "plateTimer", plateTimer);
            so.ApplyModifiedProperties();

            go.SetActive(false); // 자리가 찰 때 켜진다
            return view;
        }

        // ── 작은 도구들 ──────────────────────────────────────────────

        /// <summary>
        /// 게임 화면의 결정 버튼(히트·스탠드·다이)을 구운 프리팹으로 버튼을 만든다.
        /// 배경색만 인스턴스에서 갈아 끼운다 — 게임에서도 버튼마다 색만 다르다.
        /// </summary>
        private static Button CreateButton(
            RectTransform parent, string name, string text,
            Vector2 position, Color background, out TextMeshProUGUI label)
        {
            label = null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[LobbyRoomSetupTool] 버튼 프리팹을 찾지 못했습니다: {ButtonPrefabPath}");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            // 게임 버튼은 146 폭이지만 "준비 취소"가 들어가야 해서 조금 넓힌다. 높이는 같다.
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            var panel = go.GetComponent<RoundedPanelGraphic>();
            if (panel != null)
            {
                var pso = new SerializedObject(panel);
                pso.FindProperty("backgroundColor").colorValue = background;
                pso.ApplyModifiedProperties();
            }

            label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = text;
            else Debug.LogError($"[LobbyRoomSetupTool] 버튼 프리팹에 라벨이 없습니다: {ButtonPrefabPath}");

            return go.GetComponent<Button>();
        }

        private static TextMeshProUGUI Label(
            RectTransform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);

            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 게임 화면과 공유하는 명패를 붙인다. 프리팹 인스턴스로 놓으므로 프리팹을 고치면
        /// 로비도 따라온다 — 이것이 "게임 씬과 통일"을 유지하는 유일한 장치다.
        /// </summary>
        private static RectTransform InstantiatePlate(RectTransform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlatePrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[LobbyRoomSetupTool] 명패 프리팹을 찾지 못했습니다: {PlatePrefabPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            // 게임 씬과 같은 이름을 쓴다. 프리팹 인스턴스의 루트 이름은 바꿔도 연결이 끊기지 않는다.
            instance.name = "Plate";

            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, PlateHeight);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void Panel(RectTransform rect, Color background, Color border, float radius)
        {
            // RoundedPanelGraphic은 CanvasRenderer가 없으면 아무것도 그리지 않는다.
            // [RequireComponent]에 기대지 않고 직접 붙인다 — 전에 이걸로 배경이 통째로 사라진 적이 있다.
            if (rect.GetComponent<CanvasRenderer>() == null)
                rect.gameObject.AddComponent<CanvasRenderer>();

            var panel = rect.gameObject.AddComponent<RoundedPanelGraphic>();
            panel.raycastTarget = false;
            panel.Configure(
                background, border, radius, 2f,
                new Color(border.r, border.g, border.b, 0.35f), 7f, 1f,
                new Color(0f, 0f, 0f, 0.45f), new Vector2(0f, -5f), 3f);
        }

        private static RectTransform CreateStretched(RectTransform parent, string name)
        {
            var rect = Create(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static RectTransform Create(
            RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static string RoleKo(PlayerRole role) => role switch
        {
            PlayerRole.PlayerA => "플레이어 A",
            PlayerRole.PlayerB => "플레이어 B",
            PlayerRole.Dealer  => "딜러",
            _                  => string.Empty
        };

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Assign(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[LobbyRoomSetupTool] '{so.targetObject.GetType().Name}'에 " +
                               $"'{field}' 필드가 없습니다.");
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static void DestroyChild(RectTransform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        /// <summary>
        /// 여러 조각으로 잘린 텍스처에서 실제 그림 한 장을 고른다.
        /// 자동 슬라이스가 남긴 부스러기 조각이 섞여 있어 가장 큰 것을 쓴다.
        /// </summary>
        private static Sprite LoadLargest(string path)
        {
            Sprite best = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not Sprite sprite) continue;
                if (best == null
                    || sprite.rect.width * sprite.rect.height > best.rect.width * best.rect.height)
                    best = sprite;
            }
            return best;
        }
    }
}
#endif
