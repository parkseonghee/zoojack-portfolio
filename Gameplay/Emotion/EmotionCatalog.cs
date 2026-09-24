using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 감정표현 그림과 휠 배치 순서를 보관한다.
    /// 목록의 첫 칸이 12시이며 이후 항목은 시계 방향으로 배치된다.
    /// </summary>
    [CreateAssetMenu(fileName = "EmotionCatalog", menuName = "ZooJack/Emotion Catalog")]
    public class EmotionCatalog : ScriptableObject
    {
        public const int WheelSlotCount = 8;

        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private EmotionId id;
            [SerializeField] private Sprite sprite;

            public EmotionId Id => id;
            public Sprite Sprite => sprite;

            public Entry(EmotionId id) => this.id = id;
        }

        [Tooltip("첫 칸은 12시, 이후 칸은 시계 방향입니다.")]
        [SerializeField] private List<Entry> entries = new List<Entry>
        {
            new Entry(EmotionId.Angry),
            new Entry(EmotionId.GG),
            new Entry(EmotionId.Laugh),
            new Entry(EmotionId.Sad),
            new Entry(EmotionId.Surprised),
            new Entry(EmotionId.Suspicious),
            new Entry(EmotionId.Taunt),
            new Entry(EmotionId.ThumbsUp)
        };

        public IReadOnlyList<Entry> Entries => entries;
        public int Count => entries.Count;

        public Entry EntryAt(int clockwiseIndex) =>
            clockwiseIndex >= 0 && clockwiseIndex < entries.Count
                ? entries[clockwiseIndex]
                : null;

        public Sprite SpriteFor(EmotionId id)
        {
            foreach (var entry in entries)
                if (entry != null && entry.Id == id)
                    return entry.Sprite;

            return null;
        }
    }
}
