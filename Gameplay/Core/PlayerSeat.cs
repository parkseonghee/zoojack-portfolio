using System;

namespace ZooJack
{
    [Serializable]
    public class PlayerSeat
    {
        public string PlayerId;
        public string DisplayName;
        public PlayerRole Role;
        public bool IsConnected;
        public bool IsReady;

        /// <summary>
        /// 이 사람의 동물 캐릭터. <see cref="Role"/>과 달리 매치 내내 바뀌지 않는다 —
        /// 역할은 3라운드마다 돌지만 사람은 그대로이기 때문이다.
        /// </summary>
        public CharacterId Character;

        public PlayerSeat Clone()
        {
            return new PlayerSeat
            {
                PlayerId = PlayerId,
                DisplayName = DisplayName,
                Role = Role,
                IsConnected = IsConnected,
                IsReady = IsReady,
                Character = Character
            };
        }
    }
}
