using TMPro;
using UnityEngine;

namespace ZooJack
{
    public class CardSlotView : MonoBehaviour
    {
        [SerializeField] private Color redSuitColor = new Color(0.75f, 0.1f, 0.1f);
        [SerializeField] private Color blackSuitColor = new Color(0.05f, 0.05f, 0.05f);
        [SerializeField] private Color fairCandidateColor = new Color(0.1f, 0.6f, 0.1f);
        [SerializeField] private Color manipulatedCandidateColor = new Color(0.9f, 0.35f, 0.0f);

        private TextMeshPro label;

        private void Awake()
        {
            label = GetComponentInChildren<TextMeshPro>();
        }

        public void ShowCard(BlackjackCard card)
        {
            if (label == null) return;
            label.text = FormatCard(card);
            label.color = IsRed(card) ? redSuitColor : blackSuitColor;
        }

        public void ShowCandidate(BlackjackCard card, int index)
        {
            if (label == null) return;
            string mark = index == 0 ? "✓" : "✗";
            label.text = mark + "\n" + FormatCard(card);
            label.color = index == 0 ? fairCandidateColor : manipulatedCandidateColor;
        }

        public void Clear()
        {
            if (label != null) label.text = "";
        }

        private static string FormatCard(BlackjackCard card)
        {
            if (card == null) return "?";
            string rank;
            switch ((int)card.Rank)
            {
                case  1: rank = "A"; break;
                case 11: rank = "J"; break;
                case 12: rank = "Q"; break;
                case 13: rank = "K"; break;
                default: rank = ((int)card.Rank).ToString(); break;
            }
            string suit;
            switch (card.Suit)
            {
                case BlackjackCardSuit.Hearts:   suit = "♥"; break;
                case BlackjackCardSuit.Diamonds: suit = "♦"; break;
                case BlackjackCardSuit.Clubs:    suit = "♣"; break;
                default:                         suit = "♠"; break;
            }
            return rank + "\n" + suit;
        }

        private static bool IsRed(BlackjackCard card) =>
            card != null && (card.Suit == BlackjackCardSuit.Hearts || card.Suit == BlackjackCardSuit.Diamonds);
    }
}
