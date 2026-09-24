using System.Collections.Generic;
using UnityEngine;

namespace ZooJack
{
    [CreateAssetMenu(fileName = "CardSpriteRegistry", menuName = "ZooJack/Card Sprite Registry")]
    public class CardSpriteRegistry : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public BlackjackCardSuit Suit;
            public BlackjackCardRank Rank;
            public Sprite Sprite;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private Sprite cardBackDefault;
        [SerializeField] private Sprite cardBackCandidate;

        private Dictionary<(BlackjackCardSuit, BlackjackCardRank), Sprite> lookup;

        private void OnEnable() => BuildLookup();

        private void BuildLookup()
        {
            lookup = new Dictionary<(BlackjackCardSuit, BlackjackCardRank), Sprite>();
            foreach (var e in entries)
                if (e.Sprite != null)
                    lookup[(e.Suit, e.Rank)] = e.Sprite;
        }

        public Sprite GetSprite(BlackjackCard card)
        {
            if (card == null) return cardBackDefault;
            if (lookup == null) BuildLookup();
            return lookup.TryGetValue((card.Suit, card.Rank), out var s) ? s : cardBackDefault;
        }

        public Sprite CardBack => cardBackDefault;
        public Sprite CandidateBack => cardBackCandidate != null ? cardBackCandidate : cardBackDefault;

#if UNITY_EDITOR
        public List<Entry> EditorEntries => entries;
        public void EditorSetCardBack(Sprite s) => cardBackDefault = s;
        public void EditorSetCandidateBack(Sprite s) => cardBackCandidate = s;
#endif
    }
}
