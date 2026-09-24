using System;
using System.Collections.Generic;
using System.Text;

namespace ZooJack
{
    /// <summary>
    /// 오간 말을 담아 두는 곳.
    ///
    /// <b>왜 씬 밖에 두는가.</b> 대화는 방 로비에서 시작해 게임 씬으로 이어진다. 말을
    /// 채팅창이 들고 있으면 씬이 바뀌는 순간 지금까지 나눈 이야기가 통째로 사라진다.
    /// <see cref="MatchHistory"/>·<see cref="GameAudio"/>와 같은 이유로 정적이다.
    ///
    /// <b>화면을 모른다.</b> 말이 들어오면 <see cref="Changed"/>로 알릴 뿐이라, 채팅창은
    /// 씬에 있든 없든 상관없다 — 게임 씬으로 넘어가는 동안 도착한 말도 그대로 쌓인다.
    /// </summary>
    public static class ChatLog
    {
        /// <summary>들고 있는 최대 줄 수. 넘으면 오래된 것부터 밀려난다.</summary>
        public const int MaxLines = 120;

        /// <summary>한 줄에 담을 수 있는 글자 수. 보내는 쪽과 호스트가 같은 값으로 자른다.</summary>
        public const int MaxTextLength = 80;

        public readonly struct Line
        {
            /// <summary>말한 사람의 동물. 화면에서 이름 색을 정하는 데 쓴다.</summary>
            public readonly CharacterId Speaker;

            /// <summary>화면에 적히는 이름.</summary>
            public readonly string Who;

            public readonly string Text;

            public Line(CharacterId speaker, string who, string text)
            {
                Speaker = speaker;
                Who = who;
                Text = text;
            }
        }

        private static readonly List<Line> lines = new List<Line>(MaxLines);

        public static IReadOnlyList<Line> Lines => lines;

        /// <summary>줄이 늘거나 지워졌다. 채팅창이 이 신호를 보고 다시 그린다.</summary>
        public static event Action Changed;

        public static void Add(CharacterId speaker, string who, string text)
        {
            text = Sanitize(text);
            if (text.Length == 0) return;

            if (lines.Count >= MaxLines) lines.RemoveAt(0);
            lines.Add(new Line(speaker, string.IsNullOrEmpty(who)
                ? ZooJackText.Get("Chat.Speaker.Unknown", "누군가") : who, text));
            Changed?.Invoke();
        }

        /// <summary>
        /// 이 클라이언트에게만 보이는 시스템 안내를 남긴다.
        /// 네트워크 RPC를 거치지 않으므로 방에 늦게 들어온 사람도 자신의 입장 시점에
        /// 안내를 받을 수 있고, 다른 사람의 채팅창에는 같은 안내가 중복되지 않는다.
        /// </summary>
        public static void AddSystem(string text) => Add(
            CharacterId.None,
            ZooJackText.Get("Chat.Speaker.System", "[시스템]"),
            text);

        public static void Clear()
        {
            if (lines.Count == 0) return;
            lines.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// 보낼 수 있는 꼴로 다듬는다. <b>보내는 쪽과 호스트가 같은 함수를 지난다</b> —
        /// 규칙이 두 벌이면 한쪽만 통과한 글이 다른 쪽에서 다르게 보인다.
        ///
        /// 줄바꿈을 빈칸으로 눌러 두는 이유는 한 사람이 한 번에 채팅창을 다 차지하지
        /// 못하게 하기 위해서다.
        /// </summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;

            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (c == '\n' || c == '\r' || c == '\t') { sb.Append(' '); continue; }
                if (char.IsControl(c)) continue;

                // '<'를 그대로 두면 사람이 친 글이 TMP 서식으로 읽힌다. <size=400>을 한 번
                // 치면 채팅창 하나가 화면을 덮는다. 비슷하게 생긴 글자로 바꿔 둔다.
                sb.Append(c == '<' ? '＜' : c);
            }

            string text = sb.ToString().Trim();
            return text.Length > MaxTextLength ? text.Substring(0, MaxTextLength) : text;
        }
    }
}
