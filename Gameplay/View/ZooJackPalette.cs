using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// ZooJack 화면의 색 단일 원천.
    ///
    /// <b>왜 필요했나.</b> 예전에는 같은 색이 세 겹으로 흩어져 있었다.
    /// <list type="number">
    /// <item>에디터 셋업 툴 여덟 개가 <c>Hex()</c> 헬퍼를 <b>각자 한 벌씩</b> 정의했다.
    ///       구현은 글자 하나까지 같았다.</item>
    /// <item>같은 색이 툴마다 다른 이름으로 다시 적혔다 — <c>0x1B1F22</c>는 어디선
    ///       <c>CardFill</c>, 어디선 <c>BaseFill</c>, 어디선 <c>TabActive</c>였다.
    ///       카드 색을 바꾸려면 네 파일을 찾아야 했고, 하나를 놓치면 그 화면만 옛 색으로 남았다.</item>
    /// <item>본문 글 안의 강조색은 <c>"&lt;color=#A98B5F&gt;뇌물&lt;/color&gt;"</c>처럼
    ///       <b>문구 자체에</b> 박혀 있었다. 그래서 코드·카탈로그 툴·String Table 세 곳을
    ///       모두 고쳐야 색 하나가 바뀌었다.</item>
    /// </list>
    ///
    /// <b>이름을 색조로 붙인 이유.</b> 같은 크림색이 어떤 화면에선 제목이고 어떤 화면에선
    /// 슬라이더 손잡이다. 쓰임으로 이름을 붙이면(<c>TitleColor</c>) 손잡이에 제목 색을 쓰는
    /// 이상한 코드가 되므로, 색조로 이름을 붙이고 쓰임은 주석에 적는다.
    ///
    /// <b>여기 없는 색도 있다.</b> 한 화면에서만 쓰는 색(설명서의 종이색, 패널 걷기 버튼의
    /// 청록)은 그 툴에 그대로 둔다. 공용 팔레트에 올리면 이름만 늘고 무엇이 진짜 공용
    /// 토큰인지 흐려진다. 대신 그런 색도 <see cref="Hex"/>는 여기 것을 쓴다.
    /// </summary>
    public static class ZooJackPalette
    {
        // ── 면(배경·버튼 바탕) ────────────────────────────────────────
        /// <summary>카드·패널 바탕, 활성 탭.</summary>
        public const int InkRgb = 0x1B1F22;
        /// <summary>버튼 바탕, 배지 바탕.</summary>
        public const int GraphiteRgb = 0x2A2F33;
        /// <summary>카드 안쪽 구분선·가는 테두리.</summary>
        public const int SteelRgb = 0x6E7378;
        /// <summary>확인·닫기·다시하기 버튼.</summary>
        public const int ForestRgb = 0x06401F;
        /// <summary>나가기·항복 등 되돌릴 수 없는 버튼.</summary>
        public const int MaroonRgb = 0x400B0B;

        // ── 글자·테두리 ──────────────────────────────────────────────
        /// <summary>제목, 슬라이더 손잡이.</summary>
        public const int CreamRgb = 0xF2E8CC;
        /// <summary>항목 라벨.</summary>
        public const int SandRgb = 0xCFC4AC;
        /// <summary>카드·버튼 테두리.</summary>
        public const int GoldRgb = 0x8A7A5A;
        /// <summary>본문 안 강조(뇌물·판돈 라벨).</summary>
        public const int BronzeRgb = 0xA98B5F;
        /// <summary>부제·안내 문구.</summary>
        public const int TaupeRgb = 0xB8A98C;
        /// <summary>흐린 글자, 카운트다운 평시.</summary>
        public const int StoneRgb = 0x9A9384;
        /// <summary>가장 흐린 글자(덮인 상대 패 등).</summary>
        public const int SlateRgb = 0x8E846F;

        // ── 좌석(로비에서 자리마다 다른 색) ──────────────────────────
        public const int SeatNavyRgb = 0x0A2A40;   // 플레이어 A
        public const int SeatMaroonRgb = 0x3A1115; // 플레이어 B
        public const int SeatBrownRgb = 0x61380E;  // 딜러

        // ── 상태 ─────────────────────────────────────────────────────
        /// <summary>버스트한 점수.</summary>
        public const int BustRgb = 0xD75A52;
        /// <summary>에이스를 11로 센 소프트 점수.</summary>
        public const int SoftRgb = 0x79C866;
        /// <summary>확정된 하드 점수.</summary>
        public const int HardRgb = 0xD3A33C;
        /// <summary>남은 시간이 얼마 없을 때.</summary>
        public const int UrgentRgb = 0xE86B61;

        // ── Color 값 ─────────────────────────────────────────────────
        public static readonly Color Ink = Hex(InkRgb);
        public static readonly Color Graphite = Hex(GraphiteRgb);
        public static readonly Color Steel = Hex(SteelRgb);
        public static readonly Color Forest = Hex(ForestRgb);
        public static readonly Color Maroon = Hex(MaroonRgb);

        public static readonly Color Cream = Hex(CreamRgb);
        public static readonly Color Sand = Hex(SandRgb);
        public static readonly Color Gold = Hex(GoldRgb);
        public static readonly Color Bronze = Hex(BronzeRgb);
        public static readonly Color Taupe = Hex(TaupeRgb);
        public static readonly Color Stone = Hex(StoneRgb);
        public static readonly Color Slate = Hex(SlateRgb);

        public static readonly Color SeatNavy = Hex(SeatNavyRgb);
        public static readonly Color SeatMaroon = Hex(SeatMaroonRgb);
        public static readonly Color SeatBrown = Hex(SeatBrownRgb);

        public static readonly Color Bust = Hex(BustRgb);
        public static readonly Color Soft = Hex(SoftRgb);
        public static readonly Color Hard = Hex(HardRgb);
        public static readonly Color Urgent = Hex(UrgentRgb);

        /// <summary>카드 뒤에 까는 그림자. 알파는 <see cref="Alpha"/>로 조절한다.</summary>
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);
        /// <summary>모달 뒤에 까는 어둠막.</summary>
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.72f);

        /// <summary>잔액이 늘 때의 초록. 옛 값을 그대로 유지하려고 실수로 적는다.</summary>
        public static readonly Color MoneyGain = new Color(0.42f, 0.82f, 0.44f, 1f);
        /// <summary>잔액이 줄 때의 빨강.</summary>
        public static readonly Color MoneyLoss = new Color(0.91f, 0.38f, 0.33f, 1f);

        // ── 리치텍스트 태그 ──────────────────────────────────────────
        // TMP의 <color=...>에 넣을 문자열. 정수에서 바로 만들므로 float 왕복이 없고,
        // 팔레트를 고치면 문구 안의 색까지 함께 따라온다.
        public static readonly string BronzeTag = Tag(BronzeRgb);
        public static readonly string TaupeTag = Tag(TaupeRgb);
        public static readonly string SlateTag = Tag(SlateRgb);
        public static readonly string StoneTag = Tag(StoneRgb);
        public static readonly string UrgentTag = Tag(UrgentRgb);
        public static readonly string BustTag = Tag(BustRgb);
        public static readonly string SoftTag = Tag(SoftRgb);
        public static readonly string HardTag = Tag(HardRgb);

        // ── 캐릭터 강조색 ───────────────────────────────────

        /// <summary>
        /// 사람마다의 고유색. 역할은 라운드마다 교대하지만 사람·캐릭터는 매치 내내
        /// 그대로이므로, 이 색은 <b>그 사람을 가리키는 표시</b>로 쓴다 — 머니바 직군 라벨,
        /// 로컬 카드 외곽선, 역할 공개 강조, 채팅 말풍선 이름, 기록 카드의 강조색이 모두 이 하나를
        /// 따른다. 한 곳이 어긋나면 같은 사람이 화면마다 다른 색으로 보인다.
        ///
        /// <c>GameDirector.MoneyCardColor</c>였다. 머니바 전용이 아니라 여기로 옮겼다.
        /// </summary>
        public static Color CharacterAccent(CharacterId owner) => owner switch
        {
            CharacterId.Rabbit => new Color(0.28f, 0.72f, 1f, 1f),
            CharacterId.Croc   => new Color(0.39f, 0.84f, 0.55f, 1f),
            CharacterId.Fox    => new Color(1f, 0.42f, 0.48f, 1f),
            _                  => new Color(0.85f, 0.82f, 0.72f, 1f)
        };

        // ── 헬퍼 ─────────────────────────────────────────────────────

        /// <summary>0xRRGGBB 정수를 Color로. 셋업 툴 여덟 개에 흩어져 있던 구현이다.</summary>
        public static Color Hex(int rgb, float alpha = 1f) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        /// <summary>같은 색을 다른 투명도로. <c>Ink.Alpha(0.99f)</c>처럼 쓴다.</summary>
        public static Color Alpha(this Color color, float alpha) =>
            new Color(color.r, color.g, color.b, alpha);

        /// <summary>0xRRGGBB → TMP <c>&lt;color&gt;</c>에 넣을 "#RRGGBB".</summary>
        public static string Tag(int rgb) => "#" + rgb.ToString("X6");
    }
}
