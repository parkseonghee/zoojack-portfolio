using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 뇌물 금액 선택 UI 로직(프리셋 칩 버튼 + 직접입력 스테퍼).
    /// 핫시트(GameDirector)와 네트워크(NetworkGameDirector) 디렉터가 공유한다.
    /// 슬라이더를 대체하며, 선택값은 <see cref="Value"/> 로 읽는다.
    /// </summary>
    internal class BribeSelector
    {
        // 프리셋 칩 금액(게임 규칙: 뇌물 상한 100). 상한/잔액을 넘는 프리셋 버튼은 자동 비활성화된다.
        public static readonly int[] Presets = { 10, 25, 50, 100 };
        public const int Step = 5; // 직접입력 스테퍼 증감 단위

        private readonly Button[] presetButtons;
        private readonly TextMeshProUGUI amountText;
        private int value;
        private int max;

        /// <summary>현재 선택된 뇌물 금액.</summary>
        public int Value => value;

        public BribeSelector(Button[] presetButtons, Button minus, Button plus,
            TextMeshProUGUI amountText, Action onChanged = null)
        {
            this.presetButtons = presetButtons;
            this.amountText = amountText;

            if (presetButtons != null)
            {
                for (int i = 0; i < presetButtons.Length && i < Presets.Length; i++)
                {
                    int preset = Presets[i];
                    if (presetButtons[i] == null) continue;
                    presetButtons[i].onClick.AddListener(() =>
                    {
                        // 코인을 집는 소리. 상한을 넘는 코인은 버튼 자체가 꺼져 있으므로
                        // (RefreshPresetInteractable) 누를 수 없는 코인에서는 울리지 않는다.
                        GameAudio.PlayCoin();
                        SetValue(preset);
                        onChanged?.Invoke();
                    });
                    PresetChipLabel.Write(presetButtons[i], preset);
                }
            }
            if (minus != null) minus.onClick.AddListener(() => { SetValue(value - Step); onChanged?.Invoke(); });
            if (plus  != null) plus.onClick.AddListener(() => { SetValue(value + Step); onChanged?.Invoke(); });
        }

        /// <summary>
        /// 패널 표시 시 호출. 상한(뇌물 상한과 잔액 중 작은 값)을 정한다.
        ///
        /// <paramref name="keepValue"/>가 참이면 <b>지금 올려 둔 금액을 그대로 둔다</b>.
        /// 이미 떠 있는 판을 무언가 때문에 다시 그릴 때 쓴다 — 상한만 새로 잡으면 되는데
        /// 0으로 되돌리면 고르던 사람이 처음부터 다시 골라야 한다. 값은 새 상한에 맞춰
        /// 다시 갇히므로, 잔액이 줄어 상한이 내려가도 넘치는 액수가 남지 않는다.
        /// </summary>
        public void Begin(int maxBribe, bool keepValue = false)
        {
            max = Mathf.Max(0, maxBribe);
            SetValue(keepValue ? value : 0);
            RefreshPresetInteractable();
        }

        private void SetValue(int v)
        {
            value = Mathf.Clamp(v, 0, max);
            if (amountText != null) amountText.text = value + " <sprite index=0>";
        }

        // 상한/잔액을 넘는 프리셋 버튼은 누를 수 없게 한다.
        private void RefreshPresetInteractable()
        {
            if (presetButtons == null) return;
            for (int i = 0; i < presetButtons.Length && i < Presets.Length; i++)
                if (presetButtons[i] != null)
                    presetButtons[i].interactable = Presets[i] <= max;
        }
    }
}
