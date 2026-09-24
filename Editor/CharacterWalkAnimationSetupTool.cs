#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// 걷기 시트를 동일한 셀/피벗으로 다시 Slice하고 UI Image용
    /// AnimationClip, AnimatorController, Catalog를 생성한다.
    /// </summary>
    public static class CharacterWalkAnimationSetupTool
    {
        public const string CatalogPath =
            "Assets/ImportedAsset/Characters/Animations/CharacterWalkAnimationCatalog.asset";

        private const string CharacterFolder = "Assets/ImportedAsset/Characters";
        private const string AnimationFolder = CharacterFolder + "/Animations";
        private const string NormalizedFolder = AnimationFolder + "/Normalized";
        private const float FrameRate = 6f;
        private const int SourceCellHeight = 423;
        private const int NormalizedCellSize = 512;
        private const int NormalizedArtSize = 508;

        private sealed class Profile
        {
            public readonly string Key;
            public readonly string Idle;
            public readonly int[] FrameCells;
            public readonly int HorizontalOffset;

            public Profile(
                string key, string idle, int[] frameCells,
                int horizontalOffset = 0)
            {
                Key = key;
                Idle = idle;
                FrameCells = frameCells;
                HorizontalOffset = horizontalOffset;
            }

            public string SheetPath => $"{CharacterFolder}/{Key}_walk.png";
            public string NormalizedIdlePath =>
                $"{NormalizedFolder}/{Key}_idle_normalized.png";
            public string NormalizedSheetPath =>
                $"{NormalizedFolder}/{Key}_walk_normalized.png";
            public string IdlePath => $"{CharacterFolder}/{Idle}.png";
            public string IdleClipPath => $"{AnimationFolder}/{Key}_idle_ui.anim";
            public string WalkClipPath => Key == "croc"
                ? $"{AnimationFolder}/croco_walk.anim"
                : $"{AnimationFolder}/{Key}_walk.anim";
            public string ControllerPath => Key switch
            {
                "rabbit" => $"{AnimationFolder}/rabbit_walk_1.controller",
                "fox" => $"{AnimationFolder}/fox_walk_3.controller",
                _ => $"{AnimationFolder}/croc_walk_5.controller"
            };
        }

        private sealed class OpaqueRegion
        {
            public readonly RectInt Bounds;
            public readonly int CellX;
            public readonly int CellY;
            public readonly int CellWidth;
            public readonly int CellHeight;
            public readonly bool[] Mask;

            public OpaqueRegion(
                RectInt bounds,
                int cellX, int cellY, int cellWidth, int cellHeight,
                bool[] mask)
            {
                Bounds = bounds;
                CellX = cellX;
                CellY = cellY;
                CellWidth = cellWidth;
                CellHeight = cellHeight;
                Mask = mask;
            }

            public bool Contains(int globalX, int globalY)
            {
                int x = globalX - CellX;
                int y = globalY - CellY;
                return x >= 0 && x < CellWidth &&
                       y >= 0 && y < CellHeight &&
                       Mask[y * CellWidth + x];
            }
        }

        // 기존 AnimationClip에서 사용하던 포즈 순서를 그대로 유지한다.
        // 원본의 자동 Slice 번호와 고정 4x2 셀 번호가 달라 셀 번호로 기록한다.
        private static readonly Profile[] Profiles =
        {
            new Profile("rabbit", "rabbit_idle", new[] { 1, 2, 6, 2 }),
            new Profile("fox", "fox_idle", new[] { 3, 1, 5, 1 }),
            // 꼬리까지 포함한 외곽선은 중앙이지만 몸통의 시각적 중심은 오른쪽이다.
            // Idle/Walk 모두 같은 양만큼 왼쪽으로 옮겨 Plate 아래에 몸통을 맞춘다.
            new Profile("croc", "croc_idle", new[] { 4, 1, 6, 1 }, -24)
        };

        [MenuItem("ZooJack/Avatars/걷기 애니메이션 설정")]
        public static void Setup()
        {
            foreach (var profile in Profiles)
            {
                GenerateNormalizedIdle(profile);
                GenerateNormalizedSheet(profile);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var profile in Profiles)
            {
                SliceSheet(profile);
                SliceNormalizedIdle(profile);
                SliceNormalizedSheet(profile);
            }

            var controllers = new RuntimeAnimatorController[Profiles.Length];
            for (int i = 0; i < Profiles.Length; i++)
                controllers[i] = BuildAnimator(Profiles[i]);

            EnsureCatalog(controllers);
            AvatarStageSetupTool.Setup();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid()) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterWalkAnimationSetup] 정규화 시트, UI AnimationClip, Animator 배선 완료.");
        }

        public static CharacterWalkAnimationCatalog EnsureCatalog()
        {
            var controllers = Profiles
                .Select(profile =>
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        profile.ControllerPath))
                .ToArray();
            return EnsureCatalog(controllers);
        }

        private static CharacterWalkAnimationCatalog EnsureCatalog(
            IReadOnlyList<RuntimeAnimatorController> controllers)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterWalkAnimationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterWalkAnimationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            entries.arraySize = Profiles.Length;
            for (int i = 0; i < Profiles.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("idleSprite").objectReferenceValue =
                    LoadLargestSprite(Profiles[i].IdlePath);
                entry.FindPropertyRelative("animatorController").objectReferenceValue =
                    i < controllers.Count ? controllers[i] : null;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void GenerateNormalizedIdle(Profile profile)
        {
            string sourceAbsolutePath = ToAbsolutePath(profile.IdlePath);
            if (!File.Exists(sourceAbsolutePath))
            {
                Debug.LogError(
                    $"[CharacterWalkAnimationSetup] Idle 원본을 찾지 못했습니다: {profile.IdlePath}");
                return;
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var normalized = new Texture2D(
                NormalizedCellSize, NormalizedCellSize,
                TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(sourceAbsolutePath), false))
                {
                    Debug.LogError(
                        $"[CharacterWalkAnimationSetup] Idle PNG를 읽지 못했습니다: " +
                        profile.IdlePath);
                    return;
                }

                var sourcePixels = source.GetPixels32();
                var outputPixels = new Color32[
                    NormalizedCellSize * NormalizedCellSize];
                // Idle 원본의 약한 배경 글로우는 캐릭터 크기 계산에서 제외한다.
                var sourceRegion = FindMainOpaqueRegion(
                    sourcePixels, source.width,
                    0, 0, source.width, source.height, 64);
                BlitNormalizedFrame(
                    sourcePixels, source.width, source.height,
                    sourceRegion,
                    outputPixels, NormalizedCellSize, 0,
                    profile.HorizontalOffset);

                normalized.SetPixels32(outputPixels);
                normalized.Apply(false, false);
                string outputAbsolutePath =
                    ToAbsolutePath(profile.NormalizedIdlePath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputAbsolutePath));
                File.WriteAllBytes(outputAbsolutePath, normalized.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(normalized);
            }
        }

        private static void GenerateNormalizedSheet(Profile profile)
        {
            string sourceAbsolutePath = ToAbsolutePath(profile.SheetPath);
            if (!File.Exists(sourceAbsolutePath))
            {
                Debug.LogError(
                    $"[CharacterWalkAnimationSetup] 원본 시트를 찾지 못했습니다: {profile.SheetPath}");
                return;
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var normalized = new Texture2D(
                NormalizedCellSize * profile.FrameCells.Length,
                NormalizedCellSize,
                TextureFormat.RGBA32,
                false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(sourceAbsolutePath), false))
                {
                    Debug.LogError(
                        $"[CharacterWalkAnimationSetup] PNG를 읽지 못했습니다: {profile.SheetPath}");
                    return;
                }

                var sourcePixels = source.GetPixels32();
                var outputPixels = new Color32[
                    normalized.width * normalized.height];
                int sourceCellWidth = source.width / 4;
                int topY = source.height - SourceCellHeight;

                for (int outputIndex = 0;
                     outputIndex < profile.FrameCells.Length;
                     outputIndex++)
                {
                    int sourceCell = profile.FrameCells[outputIndex];
                    int cellX = (sourceCell % 4) * sourceCellWidth;
                    int cellY = sourceCell < 4 ? topY : 0;
                    var sourceRegion = FindMainOpaqueRegion(
                        sourcePixels, source.width,
                        cellX, cellY, sourceCellWidth, SourceCellHeight, 16);
                    BlitNormalizedFrame(
                        sourcePixels, source.width, source.height,
                        sourceRegion,
                        outputPixels, normalized.width,
                        outputIndex * NormalizedCellSize,
                        profile.HorizontalOffset);
                }

                normalized.SetPixels32(outputPixels);
                normalized.Apply(false, false);

                string outputAbsolutePath =
                    ToAbsolutePath(profile.NormalizedSheetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputAbsolutePath));
                File.WriteAllBytes(outputAbsolutePath, normalized.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(normalized);
            }
        }

        private static OpaqueRegion FindMainOpaqueRegion(
            Color32[] pixels, int textureWidth,
            int cellX, int cellY, int cellWidth, int cellHeight,
            byte alphaThreshold)
        {
            var visited = new bool[cellWidth * cellHeight];
            var queue = new Queue<int>();
            var component = new List<int>();
            int bestCount = 0;
            var best = new RectInt(cellX, cellY, cellWidth, cellHeight);
            bool[] bestMask = null;

            for (int localY = 0; localY < cellHeight; localY++)
            for (int localX = 0; localX < cellWidth; localX++)
            {
                int localIndex = localY * cellWidth + localX;
                if (visited[localIndex]) continue;
                visited[localIndex] = true;
                if (pixels[(cellY + localY) * textureWidth + cellX + localX].a <
                    alphaThreshold)
                    continue;

                queue.Clear();
                component.Clear();
                queue.Enqueue(localIndex);
                int count = 0;
                int minX = localX, minY = localY;
                int maxX = localX, maxY = localY;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    int x = current % cellWidth;
                    int y = current / cellWidth;
                    count++;
                    component.Add(current);
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);

                    Visit(x - 1, y);
                    Visit(x + 1, y);
                    Visit(x, y - 1);
                    Visit(x, y + 1);
                }

                if (count <= bestCount) continue;
                bestCount = count;
                const int padding = 2;
                int expandedMinX = Mathf.Max(0, minX - padding);
                int expandedMinY = Mathf.Max(0, minY - padding);
                int expandedMaxX = Mathf.Min(cellWidth - 1, maxX + padding);
                int expandedMaxY = Mathf.Min(cellHeight - 1, maxY + padding);
                best = new RectInt(
                    cellX + expandedMinX,
                    cellY + expandedMinY,
                    expandedMaxX - expandedMinX + 1,
                    expandedMaxY - expandedMinY + 1);
                bestMask = new bool[cellWidth * cellHeight];
                foreach (int index in component)
                    bestMask[index] = true;

                void Visit(int x, int y)
                {
                    if (x < 0 || x >= cellWidth || y < 0 || y >= cellHeight)
                        return;
                    int index = y * cellWidth + x;
                    if (visited[index]) return;
                    visited[index] = true;
                    if (pixels[(cellY + y) * textureWidth + cellX + x].a >=
                        alphaThreshold)
                        queue.Enqueue(index);
                }
            }

            if (bestMask == null)
            {
                bestMask = new bool[cellWidth * cellHeight];
                Array.Fill(bestMask, true);
            }
            else
            {
                // 임계값 아래의 안티앨리어싱 테두리는 살리고, 떨어져 있는 흰색
                // 배경 제거 찌꺼기는 제외한다.
                for (int pass = 0; pass < 2; pass++)
                {
                    var expanded = (bool[])bestMask.Clone();
                    for (int y = 0; y < cellHeight; y++)
                    for (int x = 0; x < cellWidth; x++)
                    {
                        if (!bestMask[y * cellWidth + x]) continue;
                        for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = x + ox;
                            int ny = y + oy;
                            if (nx >= 0 && nx < cellWidth &&
                                ny >= 0 && ny < cellHeight)
                                expanded[ny * cellWidth + nx] = true;
                        }
                    }
                    bestMask = expanded;
                }
            }

            return new OpaqueRegion(
                best, cellX, cellY, cellWidth, cellHeight, bestMask);
        }

        private static void BlitNormalizedFrame(
            Color32[] source, int sourceWidth, int sourceHeight,
            OpaqueRegion sourceRegion,
            Color32[] destination, int destinationWidth, int destinationCellX,
            int horizontalOffset)
        {
            RectInt sourceBounds = sourceRegion.Bounds;
            float scale = NormalizedArtSize /
                          (float)Mathf.Max(sourceBounds.width, sourceBounds.height);
            int targetWidth = Mathf.Max(
                1, Mathf.RoundToInt(sourceBounds.width * scale));
            int targetHeight = Mathf.Max(
                1, Mathf.RoundToInt(sourceBounds.height * scale));
            int targetX = destinationCellX +
                          (NormalizedCellSize - targetWidth) / 2 +
                          horizontalOffset;
            int targetY = (NormalizedCellSize - targetHeight) / 2;

            for (int y = 0; y < targetHeight; y++)
            for (int x = 0; x < targetWidth; x++)
            {
                float sourceX = sourceBounds.xMin +
                    ((x + 0.5f) / targetWidth) * sourceBounds.width - 0.5f;
                float sourceY = sourceBounds.yMin +
                    ((y + 0.5f) / targetHeight) * sourceBounds.height - 0.5f;
                destination[(targetY + y) * destinationWidth + targetX + x] =
                    SamplePremultiplied(
                        source, sourceWidth, sourceHeight,
                        sourceRegion, sourceX, sourceY);
            }
        }

        private static Color32 SamplePremultiplied(
            Color32[] pixels, int width, int height,
            OpaqueRegion region, float x, float y)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, height - 1);
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float tx = Mathf.Clamp01(x - x0);
            float ty = Mathf.Clamp01(y - y0);

            Color32 c00 = MaskedPixel(x0, y0);
            Color32 c10 = MaskedPixel(x1, y0);
            Color32 c01 = MaskedPixel(x0, y1);
            Color32 c11 = MaskedPixel(x1, y1);
            float w00 = (1f - tx) * (1f - ty);
            float w10 = tx * (1f - ty);
            float w01 = (1f - tx) * ty;
            float w11 = tx * ty;

            float alpha =
                c00.a / 255f * w00 + c10.a / 255f * w10 +
                c01.a / 255f * w01 + c11.a / 255f * w11;
            if (alpha <= 0.0001f) return new Color32(0, 0, 0, 0);

            float red =
                c00.r / 255f * (c00.a / 255f) * w00 +
                c10.r / 255f * (c10.a / 255f) * w10 +
                c01.r / 255f * (c01.a / 255f) * w01 +
                c11.r / 255f * (c11.a / 255f) * w11;
            float green =
                c00.g / 255f * (c00.a / 255f) * w00 +
                c10.g / 255f * (c10.a / 255f) * w10 +
                c01.g / 255f * (c01.a / 255f) * w01 +
                c11.g / 255f * (c11.a / 255f) * w11;
            float blue =
                c00.b / 255f * (c00.a / 255f) * w00 +
                c10.b / 255f * (c10.a / 255f) * w10 +
                c01.b / 255f * (c01.a / 255f) * w01 +
                c11.b / 255f * (c11.a / 255f) * w11;

            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(red / alpha * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(green / alpha * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(blue / alpha * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));

            Color32 MaskedPixel(int sampleX, int sampleY)
            {
                return region.Contains(sampleX, sampleY)
                    ? pixels[sampleY * width + sampleX]
                    : new Color32(0, 0, 0, 0);
            }
        }

        private static void SliceNormalizedIdle(Profile profile)
        {
            AssetDatabase.ImportAsset(
                profile.NormalizedIdlePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            var importer =
                AssetImporter.GetAtPath(profile.NormalizedIdlePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError(
                    $"[CharacterWalkAnimationSetup] 정규화 Idle을 가져오지 못했습니다: " +
                    profile.NormalizedIdlePath);
                return;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.textureType = TextureImporterType.Sprite;
            importer.SetTextureSettings(settings);
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = NormalizedCellSize;
            importer.SaveAndReimport();
        }

        private static void SliceNormalizedSheet(Profile profile)
        {
            AssetDatabase.ImportAsset(
                profile.NormalizedSheetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            var importer =
                AssetImporter.GetAtPath(profile.NormalizedSheetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError(
                    $"[CharacterWalkAnimationSetup] 정규화 시트를 가져오지 못했습니다: " +
                    profile.NormalizedSheetPath);
                return;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = NormalizedCellSize * profile.FrameCells.Length;
            importer.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previousIds = provider.GetSpriteRects()
                .Where(rect => rect != null)
                .ToDictionary(rect => rect.name, rect => rect.spriteID);

            var rects = new SpriteRect[profile.FrameCells.Length];
            for (int i = 0; i < rects.Length; i++)
            {
                string spriteName = $"{profile.Key}_walk_normalized_{i}";
                rects[i] = new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(
                        i * NormalizedCellSize, 0,
                        NormalizedCellSize, NormalizedCellSize),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                    spriteID = previousIds.TryGetValue(spriteName, out var id)
                        ? id
                        : GUID.Generate()
                };
            }

            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void SliceSheet(Profile profile)
        {
            var importer = AssetImporter.GetAtPath(profile.SheetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[CharacterWalkAnimationSetup] 시트를 찾지 못했습니다: {profile.SheetPath}");
                return;
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            bool importSettingsChanged =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Multiple ||
                textureSettings.spriteMeshType != SpriteMeshType.FullRect;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            importer.alphaIsTransparency = true;
            if (importSettingsChanged) importer.SaveAndReimport();

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            var previousIds = provider.GetSpriteRects()
                .Where(rect => rect != null)
                .ToDictionary(rect => rect.name, rect => rect.spriteID);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(profile.SheetPath);
            int cellWidth = texture.width / 4;
            int topY = texture.height - SourceCellHeight;
            var rects = new SpriteRect[8];
            for (int i = 0; i < rects.Length; i++)
            {
                string spriteName = $"{profile.Key}_walk_frame_{i}";
                rects[i] = new SpriteRect
                {
                    name = spriteName,
                    rect = new Rect(
                        (i % 4) * cellWidth,
                        i < 4 ? topY : 0,
                        cellWidth,
                        SourceCellHeight),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                    spriteID = previousIds.TryGetValue(spriteName, out var id)
                        ? id
                        : GUID.Generate()
                };
            }

            provider.SetSpriteRects(rects);
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static RuntimeAnimatorController BuildAnimator(Profile profile)
        {
            var idleSprite = LoadLargestSprite(profile.NormalizedIdlePath);
            var walkSprites = Enumerable.Range(0, profile.FrameCells.Length)
                .Select(index => LoadSprite(
                    profile.NormalizedSheetPath,
                    $"{profile.Key}_walk_normalized_{index}"))
                .ToArray();

            var idleClip = LoadOrCreateClip(profile.IdleClipPath);
            ConfigureIdleClip(idleClip, idleSprite);

            var walkClip = LoadOrCreateClip(profile.WalkClipPath);
            ConfigureWalkClip(walkClip, walkSprites);

            var controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(profile.ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(profile.ControllerPath);
            ConfigureController(controller, idleClip, walkClip);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip LoadOrCreateClip(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null)
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    AnimationUtility.SetEditorCurve(clip, binding, null);
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                return clip;
            }

            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void ConfigureIdleClip(AnimationClip clip, Sprite idleSprite)
        {
            clip.frameRate = FrameRate;
            SetSpriteCurve(clip, new[]
            {
                new ObjectReferenceKeyframe { time = 0f, value = idleSprite },
                new ObjectReferenceKeyframe { time = 1f, value = idleSprite }
            });
            SetConstantCurve(clip, "m_LocalScale.x", 1f, 1f);
            SetConstantCurve(clip, "m_LocalScale.y", 1f, 1f);
            SetConstantCurve(clip, "m_LocalScale.z", 1f, 1f);
            SetConstantCurve(clip, "m_AnchoredPosition.y", 0f, 1f);
            SetLoopTime(clip, true);
            EditorUtility.SetDirty(clip);
        }

        private static void ConfigureWalkClip(
            AnimationClip clip, IReadOnlyList<Sprite> sprites)
        {
            clip.frameRate = FrameRate;
            float step = 1f / FrameRate;
            var spriteKeys = new ObjectReferenceKeyframe[sprites.Count + 1];
            for (int i = 0; i < sprites.Count; i++)
                spriteKeys[i] = new ObjectReferenceKeyframe
                    { time = i * step, value = sprites[i] };
            spriteKeys[^1] = new ObjectReferenceKeyframe
                { time = sprites.Count * step, value = sprites[0] };
            SetSpriteCurve(clip, spriteKeys);

            float length = sprites.Count * step;
            SetConstantCurve(clip, "m_LocalScale.x", 1f, length);
            SetConstantCurve(clip, "m_LocalScale.y", 1f, length);
            SetConstantCurve(clip, "m_LocalScale.z", 1f, length);
            SetConstantCurve(clip, "m_AnchoredPosition.y", 0f, length);

            SetLoopTime(clip, true);
            EditorUtility.SetDirty(clip);
        }

        private static void SetSpriteCurve(
            AnimationClip clip, ObjectReferenceKeyframe[] keys)
        {
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    string.Empty, typeof(Image), "m_Sprite"),
                keys);
        }

        private static void SetConstantCurve(
            AnimationClip clip, string property, float value, float length)
        {
            var curve = AnimationCurve.Constant(0f, Mathf.Max(length, 1f / 60f), value);
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    string.Empty, typeof(RectTransform), property),
                curve);
        }

        private static void SetLoopTime(AnimationClip clip, bool loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void ConfigureController(
            AnimatorController controller, AnimationClip idle, AnimationClip walk)
        {
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);

            var stateMachine = controller.layers[0].stateMachine;
            foreach (var child in stateMachine.states.ToArray())
                stateMachine.RemoveState(child.state);
            foreach (var transition in stateMachine.anyStateTransitions.ToArray())
                stateMachine.RemoveAnyStateTransition(transition);

            var idleState = stateMachine.AddState("Idle");
            idleState.motion = idle;
            var walkState = stateMachine.AddState("Walk");
            walkState.motion = walk;
            stateMachine.defaultState = idleState;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0f;
            toWalk.AddCondition(
                AnimatorConditionMode.If, 0f, "IsWalking");

            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;
            toIdle.AddCondition(
                AnimatorConditionMode.IfNot, 0f, "IsWalking");
        }

        private static Sprite LoadLargestSprite(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderByDescending(sprite => sprite.rect.width * sprite.rect.height)
                .FirstOrDefault();
        }

        private static Sprite LoadSprite(string path, string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .FirstOrDefault(candidate => candidate.name == spriteName);
            if (sprite == null)
                Debug.LogError(
                    $"[CharacterWalkAnimationSetup] Sprite를 찾지 못했습니다: {spriteName} ({path})");
            return sprite;
        }
    }
}
#endif
