using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 캐릭터의 Idle Sprite와 UI Image용 Animator Controller를 연결한다.
    /// 실제 프레임 순서와 위치/크기 보정은 AnimationClip 안에 저장된다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterWalkAnimationCatalog",
        menuName = "ZooJack/Character Walk Animation Catalog")]
    public sealed class CharacterWalkAnimationCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private Sprite idleSprite;
            [SerializeField] private RuntimeAnimatorController animatorController;

            public Sprite IdleSprite => idleSprite;
            public RuntimeAnimatorController AnimatorController => animatorController;
            public bool CanAnimate => idleSprite != null && animatorController != null;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public Entry FindByIdleSprite(Sprite idleSprite)
        {
            if (idleSprite == null) return null;
            foreach (var entry in entries)
                if (entry != null && entry.IdleSprite == idleSprite)
                    return entry;
            return null;
        }
    }
}
