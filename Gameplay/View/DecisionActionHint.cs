using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 결정 패널에서 히트·스탠드·더블다운·다이 버튼에 마우스를 올리면, 안내 문구 자리를
    /// 그 행동의 설명으로 바꾼다. 벗어나면 원래 문구로 돌아온다.
    ///
    /// 이 게임은 행동마다 값이 다르게 움직인다 — 다이는 절반만 잃고, 더블다운은 판돈을
    /// 2배로 올리는 대신 한 장만 더 받는다. 버튼 이름만 봐서는 알 수 없는 규칙이라
    /// 처음 앉은 사람이 다이와 스탠드를 구별하지 못한다.
    ///
    /// 되돌릴 문구는 <b>올리는 순간</b>의 것을 기억한다. 디렉터가 패널을 열 때마다
    /// "누구의 차례"를 다시 써 넣으므로, 미리 붙잡아 두면 차례가 바뀌었을 때 옛 이름으로
    /// 되돌아간다. 대신 이미 올라와 있는 동안에는 다시 붙잡지 않는다 — 버튼에서 버튼으로
    /// 곧장 옮겨 갈 때 설명 자체를 "원래 문구"로 기억해 버리기 때문이다.
    /// </summary>
    public class DecisionActionHint : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public Button button;
            public GameObject icon;
            public PlayerActionType action;
        }

        [SerializeField] private TextMeshProUGUI target;
        [SerializeField] private Entry[] entries;

        [Tooltip("설명이 뜨는 동안 글자를 오른쪽으로 밀어 아이콘 자리를 비운다.")]
        [SerializeField] private float iconMargin = 62f;

        [Tooltip("설명 둘째 줄의 크기와 색. 기본 문구와 같은 모양을 쓴다.")]
        [SerializeField] private int bodyFontSize = 17;
        [SerializeField] private Color bodyColor = new Color(0.72f, 0.66f, 0.55f);

        // null이면 지금 아무 버튼에도 올라와 있지 않다는 뜻이다.
        private string restoreText;
        private Vector4 restoreMargin;

        private void Awake()
        {
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry?.button == null) continue;

                // 리스너를 코드에서 단다. 직렬화된 이벤트로 묶으면 씬에 배선이 하나 더 늘고,
                // 그 배선이 끊어졌을 때 조용히 설명만 안 뜨는 상태가 된다.
                var trigger = entry.button.gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = entry.button.gameObject.AddComponent<EventTrigger>();

                Entry captured = entry;
                Add(trigger, EventTriggerType.PointerEnter, () => Show(captured));
                Add(trigger, EventTriggerType.PointerExit, Hide);
            }

            HideAllIcons();
        }

        private static void Add(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        // 설명이 뜬 채로 패널이 내려가면 다음에 열렸을 때 남의 설명이 먼저 보인다.
        private void OnDisable() => Hide();

        private void Show(Entry entry)
        {
            if (target == null || entry == null) return;

            // 첫 진입에서만 붙잡는다. 버튼 사이를 곧장 옮겨 다닐 때 Enter가 Exit보다
            // 먼저 오면, 여기서 다시 붙잡을 경우 설명이 "원래 문구"가 되어 버린다.
            if (restoreText == null)
            {
                restoreText = target.text;
                restoreMargin = target.margin;
            }

            HideAllIcons();
            if (entry.icon != null) entry.icon.SetActive(true);

            target.margin = new Vector4(iconMargin, restoreMargin.y, restoreMargin.z, restoreMargin.w);
            target.text = ZooJackText.Get(
                "Game.Decision.HintFormat",
                "<b>{0}</b>\n<size={1}><color=#{2}>{3}</color></size>",
                RoundSettlement.ActionTitle(entry.action), bodyFontSize,
                ColorUtility.ToHtmlStringRGB(bodyColor), RoundSettlement.ActionBody(entry.action));
        }

        private void Hide()
        {
            HideAllIcons();
            if (restoreText == null) return;

            if (target != null)
            {
                target.text = restoreText;
                target.margin = restoreMargin;
            }
            restoreText = null;
        }

        private void HideAllIcons()
        {
            if (entries == null) return;
            foreach (var entry in entries)
                if (entry?.icon != null) entry.icon.SetActive(false);
        }
    }
}
