#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ZooJack.Editor
{
    /// <summary>ZooJack 커서 원본의 임포트 설정과 런타임 Resources 참조를 구성한다.</summary>
    public static class GameCursorSetupTool
    {
        private const string CursorPath = "Assets/ImportedAsset/UI Image/cursor.png";
        private const string SettingsPath = "Assets/Resources/GameCursorSettings.asset";
        // 캐주얼 게임 화면에서 조금 더 잘 보이게 기존 64px보다 한 단계만 키운다.
        // Hotspot은 정규화 좌표라 크기가 바뀌어도 실제 클릭 지점은 그대로 유지된다.
        private const int CursorSize = 72;

        // 원본 1280px 이미지에서 손가락 끝이 놓인 비율. 크기가 바뀌어도 같은 점을 가리킨다.
        private static readonly Vector2 HotspotNormalized = new Vector2(0.172f, 0.156f);

        [MenuItem("ZooJack/UI/게임 커서 설정")]
        public static void Setup()
        {
            if (AssetImporter.GetAtPath(CursorPath) is not TextureImporter importer)
            {
                Debug.LogError($"[GameCursorSetup] 커서 이미지를 찾지 못했습니다: {CursorPath}");
                return;
            }

            importer.textureType = TextureImporterType.Cursor;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = CursorSize;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorPath);
            if (texture == null)
            {
                Debug.LogError($"[GameCursorSetup] Texture2D로 불러오지 못했습니다: {CursorPath}");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<GameCursorSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<GameCursorSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.EditorConfigure(texture, HotspotNormalized);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GameCursorSetup] {texture.width}x{texture.height} 커서와 손가락 끝 Hotspot 설정 완료.");
        }
    }
}
#endif
