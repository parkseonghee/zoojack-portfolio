
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace ZooJack.Editor
{
    public static class CardSpriteRegistryBuilder
    {
        private const string CardFolder  = "Assets/Assest/kenney_boardgame-pack/PNG/Cards";
        private const string OutputPath  = "Assets/Assest/kenney_boardgame-pack/CardSpriteRegistry.asset";

        [MenuItem("ZooJack/Build Card Sprite Registry")]
        public static void Build()
        {
            var registry = AssetDatabase.LoadAssetAtPath<CardSpriteRegistry>(OutputPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<CardSpriteRegistry>();
                AssetDatabase.CreateAsset(registry, OutputPath);
            }

            registry.EditorEntries.Clear();
            int count = 0;

            foreach (BlackjackCardSuit suit in System.Enum.GetValues(typeof(BlackjackCardSuit)))
            {
                foreach (BlackjackCardRank rank in System.Enum.GetValues(typeof(BlackjackCardRank)))
                {
                    string path   = $"{CardFolder}/card{suit}{RankStr(rank)}.png";
                    var    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null) continue;

                    registry.EditorEntries.Add(new CardSpriteRegistry.Entry
                    {
                        Suit   = suit,
                        Rank   = rank,
                        Sprite = sprite
                    });
                    count++;
                }
            }

            registry.EditorSetCardBack(Load("cardBack_red2"));
            registry.EditorSetCandidateBack(Load("cardBack_blue2"));

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CardSpriteRegistryBuilder] Done — {count} card sprites registered. Asset: {OutputPath}");
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = registry;
        }

        private static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{CardFolder}/{name}.png");

        private static string RankStr(BlackjackCardRank rank)
        {
            switch (rank)
            {
                case BlackjackCardRank.Ace:   return "A";
                case BlackjackCardRank.Jack:  return "J";
                case BlackjackCardRank.Queen: return "Q";
                case BlackjackCardRank.King:  return "K";
                default:                      return ((int)rank).ToString();
            }
        }
    }
}
#endif
