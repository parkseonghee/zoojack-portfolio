#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZooJack.Editor
{
    /// <summary>감정 카탈로그, 휠, Plate 앵커와 Bubble을 한 번에 생성/갱신한다.</summary>
    public static class EmotionSystemSetupTool
    {
        private const string EmotionFolder = "Assets/ImportedAsset/Emotions";
        private const string CatalogPath = EmotionFolder + "/EmotionCatalog.asset";

        private static readonly EmotionId[] DefaultOrder =
        {
            EmotionId.Angry,
            EmotionId.GG,
            EmotionId.Laugh,
            EmotionId.Sad,
            EmotionId.Surprised,
            EmotionId.Suspicious,
            EmotionId.Taunt,
            EmotionId.ThumbsUp
        };

        [MenuItem("ZooJack/감정표현/전체 시스템 설정")]
        public static void Setup()
        {
            if (!EditorSceneManager.GetActiveScene().IsValid())
            {
                Debug.LogError("[EmotionSystemSetup] 열린 씬이 없습니다.");
                return;
            }

            EnsureCatalog();
            AvatarStageSetupTool.Setup();
            EmotionWheelSetupTool.Build();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[EmotionSystemSetup] 감정표현 시스템 생성/갱신 완료.");
        }

        private static EmotionCatalog EnsureCatalog()
        {
            // 있는 것을 먼저 찾는다. 경로로만 물으면 에셋이 옮겨 갔을 때 못 알아보고
            // 똑같은 카탈로그를 하나 더 만들어, 씬마다 다른 것을 가리키게 된다.
            var catalog = EmotionCatalogAsset.Find();
            bool created = catalog == null;
            if (created)
            {
                EnsureFolder("Assets", "ImportedAsset");
                EnsureFolder("Assets/ImportedAsset", "Emotions");
                catalog = ScriptableObject.CreateInstance<EmotionCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            var order = ReadValidOrder(entries);
            foreach (var id in DefaultOrder)
                if (!order.Contains(id)) order.Add(id);

            entries.arraySize = EmotionCatalog.WheelSlotCount;
            for (int i = 0; i < EmotionCatalog.WheelSlotCount; i++)
            {
                var id = order[i];
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").intValue = (int)id;
                entry.FindPropertyRelative("sprite").objectReferenceValue = LoadSprite(id.ToString());
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            if (created) Debug.Log("[EmotionSystemSetup] EmotionCatalog.asset 생성 및 이미지 연결 완료.");
            return catalog;
        }

        private static List<EmotionId> ReadValidOrder(SerializedProperty entries)
        {
            var result = new List<EmotionId>(EmotionCatalog.WheelSlotCount);
            for (int i = 0; i < entries.arraySize; i++)
            {
                int value = entries.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("id").intValue;
                if (value < 0 || value >= EmotionCatalog.WheelSlotCount) continue;

                var id = (EmotionId)value;
                if (!result.Contains(id)) result.Add(id);
            }
            return result;
        }

        private static Sprite LoadSprite(string fileName)
        {
            string path = $"{EmotionFolder}/{fileName}.png";
            Sprite best = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not Sprite sprite) continue;
                if (best == null || sprite.rect.width * sprite.rect.height
                                 > best.rect.width * best.rect.height)
                    best = sprite;
            }

            if (best == null)
                Debug.LogError($"[EmotionSystemSetup] 감정 이미지를 찾지 못했습니다: {path}");
            return best;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
