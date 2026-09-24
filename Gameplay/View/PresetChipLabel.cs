using TMPro;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 프리셋 칩 버튼에 적힌 금액을 코드 쪽 상수에 맞춰 쓴다.
    ///
    /// <b>왜 필요한가.</b> 금액을 <see cref="RoundSettlement.BetPresets"/> ·
    /// <see cref="BribeSelector.Presets"/> 한 곳으로 모아도, 버튼에 <i>보이는</i> 숫자가
    /// 프리팹에 글자로 박혀 있으면 절반만 고친 것이다. 상수를 300에서 200으로 바꾸면
    /// 눌렀을 때는 200이 걸리는데 칩에는 여전히 300이 적혀 있고, 이건 컴파일도 통과하고
    /// 로그도 남지 않아 화면을 눈으로 볼 때까지 모른다.
    ///
    /// 그래서 화면을 켤 때 한 번 덮어쓴다. 프리팹의 글자는 에디터에서 배치를 보기 위한
    /// 자리표시자가 되고, 실제로 보이는 값의 출처는 언제나 상수 하나다.
    /// </summary>
    internal static class PresetChipLabel
    {
        /// <summary>버튼 안의 첫 번째 글자 칸에 금액을 적는다. 글자 칸이 없으면 넘어간다.</summary>
        public static void Write(Button button, int amount)
        {
            if (button == null) return;

            // 비활성 상태로 시작하는 패널이 대부분이라 includeInactive를 켜야 찾는다.
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null) return;

            string text = amount.ToString();
            if (label.text != text) label.text = text;
        }
    }
}
