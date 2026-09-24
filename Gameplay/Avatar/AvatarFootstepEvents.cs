using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// CharacterVisual에서 부모 캐릭터의 실제 이동 상태를 보고 두 발소리를 번갈아 낸다.
    /// 캐릭터마다 이 컴포넌트를 하나씩 가지므로 세 캐릭터의 순서는 서로 섞이지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarFootstepEvents : MonoBehaviour
    {
        private const float WalkAnimationSpeed = 1.5f;

        [Tooltip("발소리 사이의 시간(초). 1.5배 걷기 애니메이션에 맞춘 값이다.")]
        [SerializeField, Min(0.1f)] private float interval = 0.4f / WalkAnimationSpeed;

        private PlayerAvatarView avatar;
        private bool useSecondClip;
        private float remaining;

        private void Awake() => avatar = GetComponentInParent<PlayerAvatarView>();

        private void LateUpdate()
        {
            if (avatar == null) avatar = GetComponentInParent<PlayerAvatarView>();
            if (avatar == null || !avatar.IsWalking)
            {
                remaining = 0f;
                return;
            }

            remaining -= Time.deltaTime;
            if (remaining > 0f) return;

            PlayFootstep();
            remaining = Mathf.Max(0.1f, interval);
        }

        private void PlayFootstep()
        {
            GameAudio.PlayFootstep(useSecondClip);
            useSecondClip = !useSecondClip;
        }

        private void OnDisable()
        {
            useSecondClip = false;
            remaining = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate() => interval = Mathf.Max(0.1f, interval);
#endif
    }
}
