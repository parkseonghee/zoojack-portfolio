#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZooJack.Editor
{
    /// <summary>
    /// Resources의 <see cref="AudioLibrary"/> 자산을 만들고 소리를 끼워 넣는다.
    /// 임포트 설정도 용도에 맞게 손봐 준다.
    ///
    /// <b>파일 이름이 아니라 폴더를 본다.</b> 폴더에서 처음 찾은 AudioClip을 쓰므로,
    /// 나중에 다른 소리로 갈아 끼우고 싶으면 파일만 바꿔 넣고 이 메뉴를 다시 돌리면 된다.
    ///
    /// 크기(볼륨) 값은 건드리지 않는다 — 인스펙터에서 귀로 맞춘 값을 도구가 되돌리면 안 된다.
    /// </summary>
    public static class AudioLibraryBuilder
    {
        private const string ChipFolder  = "Assets/Sound/Chips Sound";
        private const string CoinFolder  = "Assets/Sound/UI Click Sound";
        private const string ClickFolder = "Assets/Sound/Click Sound";
        private const string CardFolder  = "Assets/Sound/Card Sound";
        private const string BookFolder  = "Assets/Sound/BookSound";
        private const string DiceFolder  = "Assets/Sound/Dice Sound";
        private const string ActionFolder = "Assets/Sound/Player Action Sound";
        private const string EmotionPopFolder =
            "Assets/ImportedAsset/DevsDaddy/Audio Libraries/UI/Pop/Mouth";
        private const string ChangeFolder = "Assets/Sound/Change Sound";
        private const string TimerFolder  = "Assets/Sound/Timer Sound";
        private const string MusicFolder  = "Assets/Sound/BackGround Music";
        // 폴더 이름의 공백 두 칸은 오타가 아니라 실제 폴더 이름이다.
        private const string WinFolder   = "Assets/Sound/Win  Sound";
        private const string OutputPath = "Assets/Resources/AudioLibrary.asset";

        // 카드·주사위·승리 폴더에는 소리가 여럿이라 이름으로 갈라야 한다.
        private const string DealHint   = "Deal";
        private const string FlipHint   = "cardPlace";
        private const string SlideHint  = "cardsound32562";
        private const string SpinHint   = "whoosh";
        private const string RevealHint = "correct";

        // 배경음악은 곡 이름으로 가른다.
        private const string LobbyMusicHint = "루비";
        private const string GameMusicHint  = "딜러";

        // 승리 소리 셋 중 둘은 파일 이름이 끝자리만 다르다. 앞부분으로는 갈리지 않으므로
        // 그 끝자리를 그대로 힌트로 쓴다.
        private const string ApparentWinHint = "358778";
        private const string FinalWinHint    = "358769";
        private const string MatchOverHint   = "peekaboolabcreative";

        [MenuItem("ZooJack/사운드/오디오 라이브러리 만들기")]
        public static void Build()
        {
            var clips = new AudioLibrary.EditorClips
            {
                LobbyMusic  = ClipIn(MusicFolder, LobbyMusicHint),
                GameMusic   = ClipIn(MusicFolder, GameMusicHint),
                Chip        = ClipIn(ChipFolder),
                Coin        = ClipIn(CoinFolder),
                Click       = ClipIn(ClickFolder),
                CardDeal    = ClipIn(CardFolder, DealHint),
                CardFlip    = ClipIn(CardFolder, FlipHint),
                CardSlide   = ClipIn(CardFolder, SlideHint),
                PageTurn    = ClipIn(BookFolder),
                DiceSpin    = ClipIn(DiceFolder, SpinHint),
                DiceReveal  = ClipIn(DiceFolder, RevealHint),
                PlayerAction = ClipIn(ActionFolder),
                EmotionPops = new[]
                {
                    ClipIn(EmotionPopFolder, "Pop_Mouth_Low_1"),
                    ClipIn(EmotionPopFolder, "Pop_Mouth_Low_2"),
                    ClipIn(EmotionPopFolder, "Pop_Mouth_Low_3")
                },
                Footsteps = new[]
                {
                    ClipIn(EmotionPopFolder, "Pop_Mouth_High_Sharp_2"),
                    ClipIn(EmotionPopFolder, "Pop_Mouth_High_Sharp_3")
                },
                Countdown   = ClipIn(TimerFolder),
                RoleReveal  = ClipIn(ChangeFolder),
                ApparentWin = ClipIn(WinFolder, ApparentWinHint),
                FinalWin    = ClipIn(WinFolder, FinalWinHint),
                MatchOver   = ClipIn(WinFolder, MatchOverHint),
            };

            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(OutputPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, OutputPath);
            }

            library.EditorSetClips(clips);
            EditorUtility.SetDirty(library);

            // 보고와 임포트 손질이 같은 목록을 본다. 소리를 하나 늘릴 때
            // 세 곳을 따로 고치다 보면 한 곳은 반드시 빠진다.
            //
            // 배경음악은 여기 넣지 않는다 — 짧은 효과음과 임포트 방식이 정반대다(아래 참고).
            var named = new (string Label, AudioClip Clip)[]
            {
                ("칩", clips.Chip),
                ("코인", clips.Coin),
                ("클릭", clips.Click),
                ("카드내림", clips.CardDeal),
                ("카드뒤집기", clips.CardFlip),
                ("카드자리바꿈", clips.CardSlide),
                ("책넘김", clips.PageTurn),
                ("주사위구름", clips.DiceSpin),
                ("주사위판정", clips.DiceReveal),
                ("행동알림", clips.PlayerAction),
                ("감정팝1", clips.EmotionPops[0]),
                ("감정팝2", clips.EmotionPops[1]),
                ("감정팝3", clips.EmotionPops[2]),
                ("발걸음2", clips.Footsteps[0]),
                ("발걸음3", clips.Footsteps[1]),
                ("초읽기", clips.Countdown),
                ("역할공개", clips.RoleReveal),
                ("겉보기승리", clips.ApparentWin),
                ("최종승리", clips.FinalWin),
                ("매치종료", clips.MatchOver),
            };

            var report = new StringBuilder("[AudioLibrary] ");
            report.Append(Describe("로비음악", clips.LobbyMusic));
            report.Append(" · ");
            report.Append(Describe("게임음악", clips.GameMusic));
            bool missing = clips.LobbyMusic == null || clips.GameMusic == null;
            foreach (var entry in named)
            {
                report.Append(" · ");
                report.Append(Describe(entry.Label, entry.Clip));
                if (entry.Clip == null) missing = true;
            }

            // 전부 짧고 자주 나는 소리다. 미리 풀어 두지 않으면 처음 날 때
            // 파일을 읽느라 소리가 한 박 늦는다.
            var fixedUp = new StringBuilder();
            foreach (var entry in named)
                if (SetLoadType(entry.Clip)) fixedUp.Append(' ').Append(entry.Label);

            // 배경음악은 반대로 간다. 몇 분짜리 곡을 통째로 풀어 올리면 메모리도 크고
            // 씬이 열릴 때 그만큼 멈춰 서므로, 읽으면서 트는(Streaming) 편이 맞다.
            if (SetMusicLoadType(clips.LobbyMusic)) fixedUp.Append(" 로비음악");
            if (SetMusicLoadType(clips.GameMusic))  fixedUp.Append(" 게임음악");

            if (fixedUp.Length > 0) report.Append($"  (임포트 손질:{fixedUp})");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (missing) Debug.LogWarning(report.ToString());
            else Debug.Log(report.ToString());

            Selection.activeObject = library;
        }

        private static string Describe(string label, AudioClip clip) =>
            clip != null ? $"{label}={clip.name}" : $"{label}=없음!";

        /// <summary>
        /// 폴더에서 소리 하나를 고른다.
        ///
        /// <paramref name="nameHint"/>가 없으면 폴더에 하나뿐이라는 뜻이라 그냥 첫 번째를 쓴다 —
        /// 그래야 파일만 바꿔 넣고 이 메뉴를 다시 돌리는 것으로 소리를 갈아 끼울 수 있다.
        /// 한 폴더에 여러 소리가 있으면 이름 조각으로 가른다. 못 찾으면 <b>조용히 아무거나
        /// 고르지 않고</b> 경고를 낸다 — 엉뚱한 소리가 붙는 것이 안 붙는 것보다 찾기 어렵다.
        /// </summary>
        private static AudioClip ClipIn(string folder, string nameHint = null)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[AudioLibrary] 폴더가 없다: {folder}");
                return null;
            }

            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
            if (guids.Length == 0)
            {
                Debug.LogWarning($"[AudioLibrary] 폴더에 소리가 없다: {folder}");
                return null;
            }

            if (string.IsNullOrEmpty(nameHint))
                return AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file.IndexOf(nameHint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            Debug.LogWarning(
                $"[AudioLibrary] '{folder}'에서 이름에 '{nameHint}'가 든 소리를 못 찾았다. " +
                "파일 이름을 바꿨다면 AudioLibraryBuilder의 힌트도 같이 고칠 것.");
            return null;
        }

        /// <summary>
        /// 긴 배경음악에 맞게 임포트한다(읽으면서 튼다). 짧은 효과음과 정반대다 —
        /// 몇 분짜리 곡을 통째로 풀어 두면 메모리를 크게 먹고 씬 열기가 그만큼 늦어진다.
        /// 이미 그렇게 돼 있으면 아무것도 하지 않고 false.
        /// </summary>
        private static bool SetMusicLoadType(AudioClip clip)
        {
            if (clip == null) return false;

            string path = AssetDatabase.GetAssetPath(clip);
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer) return false;

            var settings = importer.defaultSampleSettings;

            if (settings.loadType == AudioClipLoadType.Streaming &&
                !settings.preloadAudioData &&
                importer.loadInBackground)
                return false;

            settings.loadType = AudioClipLoadType.Streaming;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;

            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// 짧은 효과음에 맞게 임포트한다(통째로 풀어 미리 올려 둔다).
        /// 이미 그렇게 돼 있으면 아무것도 하지 않고 false.
        /// </summary>
        private static bool SetLoadType(AudioClip clip)
        {
            if (clip == null) return false;

            string path = AssetDatabase.GetAssetPath(clip);
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer) return false;

            var settings = importer.defaultSampleSettings;

            if (settings.loadType == AudioClipLoadType.DecompressOnLoad &&
                settings.preloadAudioData &&
                !importer.loadInBackground)
                return false;

            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;

            importer.SaveAndReimport();
            return true;
        }
    }
}
#endif
