using System.Collections.Generic;

namespace ZooJack
{
    public interface IRoleAssignmentService
    {
        void AssignRoles(IReadOnlyList<PlayerSeat> players);
        PlayerSeat GetDealer();
        PlayerSeat GetPlayerA();
        PlayerSeat GetPlayerB();
        void RotateDealer();
    }
}
