using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 슬라이더를 놓는 순간 칩 소리를 낸다.
    ///
    /// <b>왜 값 변화가 아니라 '놓을 때'인가.</b> <see cref="Slider.onValueChanged"/>는 손잡이를
    /// 끄는 동안 매 프레임 불린다. 거기에 소리를 걸면 한 번 끌 때마다 수십 번이 쏟아져
    /// 기관총이 된다. 금액이 정해지는 순간은 손을 뗄 때 한 번뿐이므로 그때만 낸다.
    ///
    /// 손잡이를 잡았다 값이 그대로인 채 놓으면 울리지 않는다 — 아무것도 정하지 않았는데
    /// 정한 것 같은 소리가 나면 안 된다.
    ///
    /// 이 컴포넌트는 <see cref="Attach"/>로 런타임에 붙는다. 씬에 손으로 달아 둘 필요가 없고,
    /// 핫시트·네트워크 두 디렉터가 같은 곳에서 붙이므로 어느 쪽으로 들어와도 동작한다.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    [DisallowMultipleComponent]
    public class SliderSettleSound : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IEndDragHandler
    {
        private Slider cached;
        private float valueAtGrab;
        private bool grabbed;

        // 지연 초기화: 이 컴포넌트는 판돈 패널이 <b>꺼져 있는</b> 동안 붙는다(디렉터가 시작할 때
        // 패널은 아직 안 뜬다). 꺼진 오브젝트에 AddComponent를 하면 Awake가 그 자리에서 돌지 않고
        // 오브젝트가 처음 켜질 때로 미뤄지므로, Awake에서 참조를 잡아 두면 그 사이에 오는 호출이
        // 전부 헛돈다. 쓸 때 확보하면 그런 창이 없다.
        private Slider Target => cached != null ? cached : (cached = GetComponent<Slider>());

        /// <summary>이 슬라이더에 소리를 붙인다. 이미 붙어 있으면 아무것도 하지 않는다.</summary>
        public static void Attach(Slider target)
        {
            if (target == null) return;
            if (target.GetComponent<SliderSettleSound>() != null) return;
            target.gameObject.AddComponent<SliderSettleSound>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            var slider = Target;
            if (slider == null) return;
            valueAtGrab = slider.value;
            grabbed = true;
        }

        // 손을 떼는 한 번에 두 신호가 모두 올 수 있다(끌었다면 EndDrag, 그리고 PointerUp).
        // grabbed를 먼저 내려 두 번째는 그냥 빠져나가게 한다 — 같은 소리가 두 번 겹치면
        // 두 배로 크고 위상이 뭉갠다.
        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnEndDrag(PointerEventData eventData) => Release();

        private void Release()
        {
            if (!grabbed) return;
            grabbed = false;

            var slider = Target;
            if (slider == null) return;

            // 잡았다가 그대로 놓았다면 정해진 것이 없다.
            if (Mathf.Approximately(slider.value, valueAtGrab)) return;

            GameAudio.PlayChip();
        }
    }
}
