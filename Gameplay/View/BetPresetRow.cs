using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 판돈 프리셋 칩 한 줄. 누르면 슬라이더를 그 금액으로 옮긴다.
    /// 핫시트(<see cref="GameDirector"/>)와 네트워크(<see cref="NetworkGameDirector"/>) 디렉터가 공유한다.
    ///
    /// <b>왜 클래스로 뺐나.</b> 예전에는 금액이 <c>btnBetPreset100</c>처럼 필드 이름에까지 박혀
    /// 있었고, 리스너 연결·활성화 판정이 두 디렉터에 똑같이 한 벌씩 있었다(합쳐 열여섯 줄).
    /// 금액을 바꾸려면 여덟 곳을 고쳐야 했고, 한쪽만 고치면 핫시트와 빌드가 달라졌다.
    /// 지금 금액은 <see cref="RoundSettlement.BetPresets"/> 한 곳에만 있고,
    /// 버튼 배열의 순서가 그 배열의 순서다.
    ///
    /// 뇌물 쪽 <see cref="BribeSelector"/>와 같은 짜임이다. 다른 점은 뇌물은 자기 값을
    /// 직접 들고 있고, 판돈은 슬라이더가 값의 주인이라 여기는 슬라이더를 밀어 주기만 한다는 것.
    /// </summary>
    internal class BetPresetRow
    {
        private readonly Button[] buttons;
        private readonly Slider slider;

        public BetPresetRow(Button[] buttons, Slider slider)
        {
            this.buttons = buttons;
            this.slider = slider;
            if (buttons == null) return;

            for (int i = 0; i < buttons.Length && i < RoundSettlement.BetPresets.Length; i++)
            {
                int amount = RoundSettlement.BetPresets[i];
                if (buttons[i] == null) continue;
                buttons[i].onClick.AddListener(() => Select(amount));
                PresetChipLabel.Write(buttons[i], amount);
            }
        }

        private void Select(int amount)
        {
            // 칩을 집는 소리. 잔액을 넘는 칩은 버튼 자체가 꺼져 있으므로
            // (RefreshInteractable) 누를 수 없는 칩에서는 울리지 않는다.
            GameAudio.PlayChip();

            if (slider == null) return;
            slider.value = Mathf.Clamp(
                RoundSettlement.BetToSteps(amount),
                Mathf.RoundToInt(slider.minValue), Mathf.RoundToInt(slider.maxValue));
        }

        /// <summary>잔액 상한을 넘는 칩은 누를 수 없게 한다.</summary>
        public void RefreshInteractable(int maxBet)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length && i < RoundSettlement.BetPresets.Length; i++)
                if (buttons[i] != null)
                    buttons[i].interactable = maxBet >= RoundSettlement.BetPresets[i];
        }
    }
}
