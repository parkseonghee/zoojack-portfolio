using UnityEngine;

namespace ZooJack
{
    /// <summary>그림 줄에 놓이는 것 한 개.</summary>
    public enum HowToPlayItemKind
    {
        /// <summary>실제 카드 앞면. <see cref="CardSpriteRegistry"/>에서 가져온다.</summary>
        Card,

        /// <summary>카드 뒷면. 아직 안 보이는 패를 말할 때.</summary>
        CardBack,

        /// <summary>아무 그림(초상화·칩·버튼 잘라낸 것 등).</summary>
        Image,

        /// <summary>짧은 기호. <c>→</c> <c>+</c> <c>?</c> 처럼 줄을 잇는 글자.</summary>
        Symbol
    }

    /// <summary>
    /// 그림 줄에 가로로 늘어서는 것 하나.
    ///
    /// 카드를 <see cref="Sprite"/>로 직접 넣지 않는 이유: 카드 그림이 바뀌면 게임과
    /// 설명서가 어긋난다. 끗수와 무늬만 적어 두면 언제나 게임이 쓰는 그림이 나온다.
    /// </summary>
    [System.Serializable]
    public class HowToPlayItem
    {
        public HowToPlayItemKind Kind = HowToPlayItemKind.Card;

        [Tooltip("Kind가 Card일 때만 쓴다.")]
        public BlackjackCardRank Rank = BlackjackCardRank.Ace;

        public BlackjackCardSuit Suit = BlackjackCardSuit.Spades;

        [Tooltip("Kind가 Image일 때만 쓴다.")]
        public Sprite Image;

        [Tooltip("Kind가 Symbol일 때만 쓴다. 화살표·물음표처럼 한두 글자.")]
        public string Symbol = "→";

        [Tooltip("Symbol이 사용자 문구일 때 ZooJack String Table에서 찾을 ID. 비면 Symbol을 그대로 쓴다.")]
        public string TextKey;

        [Range(0.3f, 2f)]
        [Tooltip("줄 높이에 곱한다. 강조하고 싶은 것만 키운다.")]
        public float Scale = 1f;

        [Tooltip("적어도 이만큼은 차지한다. 제 높이에 곱하며, 0이면 제 길이에 맞춘다. "
               + "여러 줄에서 같은 값을 주면 글 길이가 달라도 세로줄이 맞는다.")]
        public float MinWidth;
    }

    public enum HowToPlayBlockKind
    {
        /// <summary>큰 글자 한 줄. 쪽마다 하나면 충분하다.</summary>
        Heading,

        /// <summary>그림 줄. 설명의 <b>본체</b>다.</summary>
        Row,

        /// <summary>짧은 글. 그림이 말하지 못하는 것만 적는다.</summary>
        Caption
    }

    /// <summary>쪽을 이루는 한 덩어리. 위에서 아래로 쌓인다.</summary>
    [System.Serializable]
    public class HowToPlayBlock
    {
        public HowToPlayBlockKind Kind = HowToPlayBlockKind.Row;

        [TextArea(1, 4)]
        [Tooltip("Heading·Caption의 글, 그리고 Row 아래 붙는 한 줄 설명.")]
        public string Text;

        [Tooltip("Text를 ZooJack String Table에서 찾을 ID. 비면 기존 Text를 그대로 쓴다.")]
        public string TextKey;

        public string LocalizedText => string.IsNullOrWhiteSpace(TextKey)
            ? Text ?? string.Empty
            : ZooJackText.Get(TextKey, Text ?? string.Empty);

        [Tooltip("Kind가 Row일 때 가로로 늘어설 것들.")]
        public HowToPlayItem[] Items;

        [Tooltip("그림 줄의 높이(px). 0이면 기본값을 쓴다.")]
        public float Height;
    }

    /// <summary>
    /// 설명서 한 쪽. <b>화면이 아니라 값이다</b> — 그리는 것은
    /// <see cref="HowToPlayPageView"/>가 하고, 여기는 인스펙터에서 채우는 자리다.
    ///
    /// <b>그림으로 설명한다.</b> 쪽은 제목 하나에 긴 글 한 덩어리가 아니라 덩어리
    /// 여러 개로 이루어진다. 그중 본체는 실제 카드 그림이 늘어선 <see cref="HowToPlayBlockKind.Row"/>이고,
    /// 글은 그림이 말하지 못하는 것만 짧게 받친다.
    ///
    /// 덩어리를 하나도 넣지 않으면 쪽수만 있는 빈 쪽이 된다.
    /// </summary>
    [System.Serializable]
    public class HowToPlayPage
    {
        [Tooltip("위에서 아래로 쌓이는 순서 그대로.")]
        public HowToPlayBlock[] Blocks;

        public bool IsBlank => Blocks == null || Blocks.Length == 0;
    }
}
