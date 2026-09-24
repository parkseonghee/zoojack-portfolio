using System;
using System.Collections.Generic;
using System.Linq;

namespace ZooJack
{
    public class RoleAssignmentService : IRoleAssignmentService
    {
        private readonly List<PlayerSeat> players = new List<PlayerSeat>();
        private int dealerIndex = -1;

        public void AssignRoles(IReadOnlyList<PlayerSeat> sourcePlayers)
        {
            if (sourcePlayers == null)
            {
                throw new ArgumentNullException(nameof(sourcePlayers));
            }

            var activePlayers = sourcePlayers.Where(player => player != null && player.IsConnected).Take(3).ToList();
            if (activePlayers.Count != 3)
            {
                throw new InvalidOperationException("Role assignment requires exactly three connected players.");
            }

            players.Clear();
            players.AddRange(activePlayers);
            dealerIndex = dealerIndex < 0 ? 0 : dealerIndex % players.Count;
            ApplyRoles();
        }

        public PlayerSeat GetDealer()
        {
            return GetByRole(PlayerRole.Dealer);
        }

        public PlayerSeat GetPlayerA()
        {
            return GetByRole(PlayerRole.PlayerA);
        }

        public PlayerSeat GetPlayerB()
        {
            return GetByRole(PlayerRole.PlayerB);
        }

        /// <summary>
        /// 역할을 한 칸 돌린다: Dealer→PlayerA→PlayerB→Dealer.
        /// dealerIndex를 뒤로 물리는 이유는 이 방향을 맞추기 위해서다. 앞으로 당기면
        /// Dealer→PlayerB→PlayerA가 되어 네트워크 모드(FusionGameState.HostRotateRoles)와
        /// 좌석 순서가 어긋난다. 두 모드는 같은 규칙이어야 한다.
        /// </summary>
        public void RotateDealer()
        {
            if (players.Count != 3)
            {
                throw new InvalidOperationException("Roles must be assigned before rotating the dealer.");
            }

            dealerIndex = (dealerIndex - 1 + players.Count) % players.Count;
            ApplyRoles();
        }

        private void ApplyRoles()
        {
            for (var index = 0; index < players.Count; index++)
            {
                var offset = (index - dealerIndex + players.Count) % players.Count;
                players[index].Role = offset == 0
                    ? PlayerRole.Dealer
                    : offset == 1 ? PlayerRole.PlayerA : PlayerRole.PlayerB;
            }
        }

        private PlayerSeat GetByRole(PlayerRole role)
        {
            return players.FirstOrDefault(player => player.Role == role);
        }
    }
}
