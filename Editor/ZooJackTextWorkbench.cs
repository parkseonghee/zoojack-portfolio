using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

namespace ZooJack.Editor
{
    /// <summary>문구, 상황, 실제 미리보기를 한 화면에서 검색하고 수정하는 ZooJack 전용 편집 창.</summary>
    public sealed class ZooJackTextWorkbench : EditorWindow
    {
        private string search = string.Empty;
        private string category = "전체";
        private Vector2 scroll;
        private bool showContext = true;
        private string newKey = string.Empty;
        private string newValue = string.Empty;

        [MenuItem("ZooJack/텍스트/텍스트 관리 창")]
        public static void Open()
        {
            var window = GetWindow<ZooJackTextWorkbench>("ZooJack 텍스트");
            window.minSize = new Vector2(820f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(ZooJackText.TableName);
            if (collection == null)
            {
                EditorGUILayout.HelpBox("ZooJack String Table이 없습니다. 먼저 카탈로그를 만드세요.", MessageType.Info);
                if (GUILayout.Button("카탈로그 만들기"))
                {
                    ZooJackTextCatalogSetupTool.Build();
                    GUIUtility.ExitGUI();
                }
                return;
            }

            var table = collection.GetTable("ko") as StringTable;
            if (table == null)
            {
                EditorGUILayout.HelpBox("한국어(ko) 테이블을 찾지 못했습니다.", MessageType.Error);
                return;
            }

            DrawToolbar(collection);
            DrawAddRow(table, collection.SharedData);

            List<string> categories = collection.SharedData.Entries
                .Select(e => e.Metadata.GetMetadata<ZooJackTextContext>()?.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct().OrderBy(c => c).ToList();
            categories.Insert(0, "전체");
            int selected = Mathf.Max(0, categories.IndexOf(category));
            selected = EditorGUILayout.Popup("분류", selected, categories.ToArray());
            category = categories[selected];

            scroll = EditorGUILayout.BeginScrollView(scroll);
            int visible = 0;
            foreach (var shared in collection.SharedData.Entries.OrderBy(e => e.Key))
            {
                StringTableEntry entry = table.GetEntry(shared.Id);
                if (entry == null) continue;
                var context = shared.Metadata.GetMetadata<ZooJackTextContext>();
                if (category != "전체" && context?.Category != category) continue;
                if (!Matches(shared.Key, entry.Value, context, search)) continue;

                visible++;
                DrawEntry(table, collection.SharedData, shared, entry, context);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.LabelField($"표시 {visible}개 / 전체 {collection.SharedData.Entries.Count}개", EditorStyles.miniLabel);
        }

        private void DrawToolbar(StringTableCollection collection)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.MinWidth(220f));
            if (GUILayout.Button("지우기", EditorStyles.toolbarButton, GUILayout.Width(45f))) search = string.Empty;
            showContext = GUILayout.Toggle(showContext, "상황 상세", EditorStyles.toolbarButton, GUILayout.Width(75f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("기본 항목 보충", EditorStyles.toolbarButton, GUILayout.Width(95f)))
                ZooJackTextCatalogSetupTool.Build();
            if (GUILayout.Button("전체 연결·보충", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                ZooJackAllTextSetupTool.BuildAll();
            if (GUILayout.Button("Unity 테이블", EditorStyles.toolbarButton, GUILayout.Width(85f)))
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Localization Tables");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "검색은 ID·문구·씬·대상·등장 조건·변수·미리보기·메모 전체를 대상으로 합니다. " +
                "여기서 수정한 한국어 값은 String Table을 사용하는 게임 UI에 적용됩니다.", MessageType.None);
        }

        private void DrawAddRow(StringTable table, SharedTableData sharedData)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("새 문구 추가", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            newKey = EditorGUILayout.TextField("ID", newKey);
            newValue = EditorGUILayout.TextField("한국어", newValue);
            GUI.enabled = !string.IsNullOrWhiteSpace(newKey) && table.GetEntry(newKey.Trim()) == null;
            if (GUILayout.Button("추가", GUILayout.Width(55f)))
            {
                Undo.RecordObjects(new UnityEngine.Object[] { table, sharedData }, "ZooJack 문구 추가");
                table.AddEntry(newKey.Trim(), newValue);
                var shared = sharedData.GetEntry(newKey.Trim());
                shared?.Metadata.AddMetadata(new ZooJackTextContext());
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(sharedData);
                newKey = newValue = string.Empty;
                GUI.FocusControl(null);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawEntry(StringTable table, SharedTableData sharedData,
            SharedTableData.SharedTableEntry shared, StringTableEntry entry, ZooJackTextContext context)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.SelectableLabel(shared.Key, EditorStyles.boldLabel, GUILayout.Height(18f));
            if (GUILayout.Button("ID 복사", GUILayout.Width(60f))) EditorGUIUtility.systemCopyBuffer = shared.Key;
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            string value = EditorGUILayout.TextArea(entry.Value ?? string.Empty, GUILayout.MinHeight(42f));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(table, "ZooJack 문구 수정");
                entry.Value = value;
                EditorUtility.SetDirty(table);
            }

            if (context == null)
            {
                if (GUILayout.Button("상황 정보 추가"))
                {
                    Undo.RecordObject(sharedData, "ZooJack 문구 상황 추가");
                    shared.Metadata.AddMetadata(new ZooJackTextContext());
                    EditorUtility.SetDirty(sharedData);
                }
                EditorGUILayout.EndVertical();
                return;
            }

            string preview = string.IsNullOrWhiteSpace(context.Preview) ? entry.Value : context.Preview;
            EditorGUILayout.LabelField("미리보기", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(RemoveUnsupportedPreviewTags(preview), MessageType.None);

            if (showContext)
            {
                EditorGUI.BeginChangeCheck();
                string newCategory = EditorGUILayout.TextField("분류", context.Category);
                string newScene = EditorGUILayout.TextField("씬", context.Scene);
                string newTarget = EditorGUILayout.TextField("UI 대상", context.Target);
                string newTrigger = EditorGUILayout.TextField("등장 조건", context.Trigger);
                string newVariables = EditorGUILayout.TextField("변수", context.Variables);
                string newPreview = EditorGUILayout.TextField("출력 예시", context.Preview);
                int newMaxLines = EditorGUILayout.IntField("최대 줄 수", context.MaxLines);
                string newNotes = EditorGUILayout.TextField("메모", context.Notes);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(sharedData, "ZooJack 문구 상황 수정");
                    context.Category = newCategory;
                    context.Scene = newScene;
                    context.Target = newTarget;
                    context.Trigger = newTrigger;
                    context.Variables = newVariables;
                    context.Preview = newPreview;
                    context.MaxLines = newMaxLines;
                    context.Notes = newNotes;
                    EditorUtility.SetDirty(sharedData);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static bool Matches(string key, string value, ZooJackTextContext context, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            return Contains(key, query) || Contains(value, query) || (context?.Matches(query) ?? false);
        }

        private static bool Contains(string value, string query) =>
            !string.IsNullOrEmpty(value)
            && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string RemoveUnsupportedPreviewTags(string value) =>
            string.IsNullOrEmpty(value) ? "(빈 문구)" : value.Replace("<sprite index=0>", "◉");
    }
}
