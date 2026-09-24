using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

namespace ZooJack.Editor
{
    /// <summary>
    /// 두 실제 씬의 고정 TMP와 설명서 데이터까지 ZooJack String Table에 연결한다.
    /// 기존 테이블 값은 절대 덮어쓰지 않고, 재실행하면 새 오브젝트/문구만 보충한다.
    /// </summary>
    public static class ZooJackAllTextSetupTool
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/GameScene.unity",
            "Assets/Scenes/LobbyScene.unity"
        };

        [MenuItem("ZooJack/텍스트/전체 문구 연결·보충")]
        public static void BuildAll()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isDirty)
            {
                Debug.LogWarning("[ZooJackText] 저장되지 않은 씬 변경이 있어 전체 문구 연결을 중단했습니다. 씬을 저장한 뒤 다시 실행하세요.");
                return;
            }

            string restorePath = active.IsValid() ? active.path : ScenePaths[0];
            ZooJackTextCatalogSetupTool.Build();
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(ZooJackText.TableName);
            StringTable table = collection?.GetTable("ko") as StringTable;
            if (collection == null || table == null)
            {
                Debug.LogError("[ZooJackText] ZooJack 한국어 String Table을 찾지 못했습니다.");
                return;
            }

            int added = 0;
            int bound = 0;
            int removedDynamicBindings = 0;
            try
            {
                foreach (string scenePath in ScenePaths)
                {
                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    // Scene을 Single로 열면 사용 중이 아니던 에디터 Asset 객체가 언로드될 수
                    // 있으므로, 테이블 참조는 씬마다 AssetDatabase에서 다시 받는다.
                    collection = LocalizationEditorSettings.GetStringTableCollection(ZooJackText.TableName);
                    table = collection?.GetTable("ko") as StringTable;
                    if (collection == null || table == null)
                        throw new InvalidOperationException("ZooJack 한국어 String Table을 다시 불러오지 못했습니다.");

                    // 설명서는 두 씬이 같은 ID와 최신 기본 구조를 공유한다.
                    var howTo = UnityEngine.Object.FindFirstObjectByType<HowToPlayPanel>(
                        FindObjectsInactive.Include);
                    if (howTo != null)
                    {
                        HowToPlayContentTool.Apply(howTo);
                        RegisterHowToPlay(howTo, collection, table, ref added);
                    }

                    HashSet<int> runtimeOwnedTextIds = FindRuntimeOwnedTextIds();

                    foreach (TMP_Text text in UnityEngine.Object.FindObjectsByType<TMP_Text>(
                                 FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (text == null || !IsUserFacingText(text.text)) continue;
                        if (IsHowToPlayGeneratedText(text.transform)) continue;

                        ZooJackLocalizedText binding = text.GetComponent<ZooJackLocalizedText>();
                        if (runtimeOwnedTextIds.Contains(text.GetInstanceID()))
                        {
                            // 역할 공개, 토스트, 점수처럼 실행 중 코드가 내용을 정하는 TMP에
                            // 고정 Scene 키가 붙으면 OnEnable이 방금 계산한 문구를 덮어쓴다.
                            // 자동 생성된 잘못된 연결과 그 전용 항목만 정리한다.
                            if (binding != null && IsGeneratedSceneKey(binding.Key))
                            {
                                string obsoleteKey = binding.Key;
                                Undo.DestroyObjectImmediate(binding);
                                collection.RemoveEntry(obsoleteKey);
                                removedDynamicBindings++;
                            }
                            continue;
                        }

                        string key = binding != null && !string.IsNullOrWhiteSpace(binding.Key)
                            ? binding.Key
                            : SceneKey(scene.name, text);
                        string path = HierarchyPath(text.transform);
                        string fallback = text.text;

                        StringTableEntry entry = AddIfMissing(
                            collection, table, key, fallback, CategoryFor(path), scene.name,
                            path, "씬에 고정되어 표시되는 텍스트", ref added);

                        if (binding == null)
                        {
                            binding = Undo.AddComponent<ZooJackLocalizedText>(text.gameObject);
                            bound++;
                        }

                        // 테이블을 원본으로 삼는다. 재실행해도 사용자가 고친 값이 씬에 반영된다.
                        string authored = entry?.Value ?? fallback;
                        var serialized = new SerializedObject(binding);
                        serialized.FindProperty("key").stringValue = key;
                        serialized.FindProperty("fallback").stringValue = authored;
                        serialized.FindProperty("target").objectReferenceValue = text;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        text.text = authored;
                        EditorUtility.SetDirty(text);
                        EditorUtility.SetDirty(binding);
                    }

                    // 다음 씬을 열면 테이블 Asset 인스턴스가 언로드될 수 있다. 씬별로
                    // 즉시 저장해야 앞 씬에서 추가한 ID가 사라지지 않는다.
                    EditorUtility.SetDirty(table);
                    EditorUtility.SetDirty(collection.SharedData);
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(restorePath))
                    EditorSceneManager.OpenScene(restorePath, OpenSceneMode.Single);
            }

            collection = LocalizationEditorSettings.GetStringTableCollection(ZooJackText.TableName);
            table = collection?.GetTable("ko") as StringTable;
            if (collection == null || table == null) return;
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZooJackText] 전체 문구 연결 완료. 새 테이블 항목 {added}개, 새 씬 연결 {bound}개, 동적 문구 오연결 제거 {removedDynamicBindings}개, 전체 {collection.SharedData.Entries.Count}개.");
        }

        /// <summary>
        /// 씬 스크립트가 직렬화 참조로 보유한 TMP는 런타임 상태를 표시하는 대상으로 본다.
        /// ZooJackLocalizedText 자체는 제외해야 기존 고정 연결이 전부 동적으로 오인되지 않는다.
        /// </summary>
        private static HashSet<int> FindRuntimeOwnedTextIds()
        {
            var result = new HashSet<int>();
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour is ZooJackLocalizedText) continue;

                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.GetIterator();
                // 배열/중첩 설정 안의 TMP 참조(기록 행, 결과 행 등)도 빠뜨리지 않는다.
                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue is TMP_Text text)
                        result.Add(text.GetInstanceID());
                }
            }
            return result;
        }

        private static bool IsGeneratedSceneKey(string key) =>
            !string.IsNullOrEmpty(key) && key.StartsWith("Scene.", StringComparison.Ordinal);

        private static void RegisterHowToPlay(HowToPlayPanel panel,
            StringTableCollection collection, StringTable table, ref int added)
        {
            var pages = typeof(HowToPlayPanel)
                .GetField("pages", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(panel) as HowToPlayPage[];
            if (pages == null) return;

            for (int pageIndex = 0; pageIndex < pages.Length; pageIndex++)
            {
                var blocks = pages[pageIndex]?.Blocks;
                if (blocks == null) continue;
                for (int blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
                {
                    HowToPlayBlock block = blocks[blockIndex];
                    if (block == null) continue;
                    string target = $"ZJ_HowToPlay/{pageIndex + 1}쪽/{blockIndex + 1}번째 블록";
                    if (!string.IsNullOrWhiteSpace(block.TextKey))
                        AddIfMissing(collection, table, block.TextKey, block.Text,
                            "설명서", "GameScene/LobbyScene", target,
                            "설명서 페이지가 열릴 때", ref added);

                    if (block.Items == null) continue;
                    for (int itemIndex = 0; itemIndex < block.Items.Length; itemIndex++)
                    {
                        HowToPlayItem item = block.Items[itemIndex];
                        if (item == null || string.IsNullOrWhiteSpace(item.TextKey)) continue;
                        AddIfMissing(collection, table, item.TextKey, item.Symbol,
                            "설명서", "GameScene/LobbyScene", target + $"/{itemIndex + 1}번째 글자",
                            "설명서 역할 이동 그림", ref added);
                    }
                }
            }
        }

        private static StringTableEntry AddIfMissing(
            StringTableCollection collection, StringTable table, string key, string value,
            string category, string scene, string target, string trigger, ref int added)
        {
            StringTableEntry entry = table.GetEntry(key);
            if (entry == null)
            {
                entry = table.AddEntry(key, value ?? string.Empty);
                added++;
            }

            var shared = collection.SharedData.GetEntry(key);
            if (shared == null) return entry;
            var context = shared.Metadata.GetMetadata<ZooJackTextContext>();
            if (context == null)
            {
                context = new ZooJackTextContext();
                shared.Metadata.AddMetadata(context);
            }
            if (string.IsNullOrWhiteSpace(context.Category)) context.Category = category;
            if (string.IsNullOrWhiteSpace(context.Scene)) context.Scene = scene;
            if (string.IsNullOrWhiteSpace(context.Target)) context.Target = target;
            if (string.IsNullOrWhiteSpace(context.Trigger)) context.Trigger = trigger;

            var comment = shared.Metadata.GetMetadata<Comment>();
            if (comment == null)
            {
                comment = new Comment();
                shared.Metadata.AddMetadata(comment);
            }
            if (string.IsNullOrWhiteSpace(comment.CommentText))
                comment.CommentText = $"{scene} / {target} — {trigger}";
            return entry;
        }

        private static bool IsUserFacingText(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "\u200B") return false;
            foreach (char c in value)
                if ((c >= '\uac00' && c <= '\ud7a3') || (c >= 'A' && c <= 'Z')
                    || (c >= 'a' && c <= 'z')) return true;
            return false;
        }

        // 설명서 본문은 HowToPlayBlock의 ID로 연결한다. 복제용 TMP까지 씬 키를 붙이면
        // 같은 문장이 두 번 나타나 어느 쪽을 고쳐야 하는지 알 수 없어진다.
        private static bool IsHowToPlayGeneratedText(Transform transform)
        {
            for (Transform t = transform; t != null; t = t.parent)
                if (t.GetComponent<HowToPlayPageView>() != null
                    || t.GetComponent<HowToPlayRowView>() != null
                    || t.GetComponent<HowToPlayItemView>() != null) return true;
            return false;
        }

        private static string SceneKey(string sceneName, TMP_Text text)
        {
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(text);
            return $"Scene.{Safe(sceneName)}.{Safe(text.gameObject.name)}.{id.targetObjectId}";
        }

        private static string CategoryFor(string path)
        {
            if (path.Contains("ZJ_History")) return "기록";
            if (path.Contains("ZJ_HowToPlay")) return "설명서";
            if (path.IndexOf("Settings", StringComparison.OrdinalIgnoreCase) >= 0) return "환경설정";
            if (path.IndexOf("Chat", StringComparison.OrdinalIgnoreCase) >= 0) return "채팅";
            return "씬 고정 문구";
        }

        private static string HierarchyPath(Transform transform)
        {
            var builder = new StringBuilder(transform.name);
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                builder.Insert(0, parent.name + "/");
            return builder.ToString();
        }

        private static string Safe(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Text";
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
                builder.Append(char.IsLetterOrDigit(c) ? c : '_');
            return builder.ToString().Trim('_');
        }
    }
}
