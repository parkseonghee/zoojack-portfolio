using UnityEngine;

namespace ZooJack
{
    /// <summary>빌드에서도 ImportedAsset의 커서 Texture를 찾을 수 있게 Resources가 보관하는 참조.</summary>
    public sealed class GameCursorSettings : ScriptableObject
    {
        [SerializeField] private Texture2D cursorTexture;
        [SerializeField] private Vector2 hotspotNormalized = new Vector2(0.172f, 0.156f);

        public Texture2D CursorTexture => cursorTexture;

        public Vector2 Hotspot => cursorTexture == null
            ? Vector2.zero
            : new Vector2(
                cursorTexture.width * Mathf.Clamp01(hotspotNormalized.x),
                cursorTexture.height * Mathf.Clamp01(hotspotNormalized.y));

#if UNITY_EDITOR
        public void EditorConfigure(Texture2D texture, Vector2 normalizedHotspot)
        {
            cursorTexture = texture;
            hotspotNormalized = normalizedHotspot;
        }
#endif
    }

    /// <summary>어느 씬에서 시작해도 ZooJack 전용 마우스 커서를 적용한다.</summary>
    public static class GameCursor
    {
        private const string ResourceName = "GameCursorSettings";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            var settings = Resources.Load<GameCursorSettings>(ResourceName);
            if (settings == null || settings.CursorTexture == null) return;

            Cursor.SetCursor(settings.CursorTexture, settings.Hotspot, CursorMode.Auto);
        }
    }
}
