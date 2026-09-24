using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 캐릭터 이동 규칙(순수 계산). MonoBehaviour·Fusion에 의존하지 않으므로
    /// 클라이언트의 예측 이동과 호스트의 검증이 정확히 같은 규칙을 쓴다.
    ///
    /// 좌표계는 캔버스 기준 anchoredPosition(px)이고 원점은 화면 중앙이다.
    /// 캔버스가 1920x1080 ScaleWithScreenSize라 해상도가 바뀌어도 값은 그대로 통한다.
    /// </summary>
public static class AvatarMovement
    {
        /// <summary>이동 속도(캔버스 px/초). 화면 가로를 약 5초에 가로지른다.</summary>
        public const float Speed = 380f;

        /// <summary>위치를 호스트에 보고하는 간격(초). 초당 20회.</summary>
        public const float SendInterval = 0.05f;

        /// <summary>이 거리(px) 이상 움직였을 때만 보고한다. 가만히 있으면 트래픽 0.</summary>
        public const float SendThreshold = 1.5f;

        /// <summary>원격 캐릭터가 목표 위치를 따라잡는 속도. 클수록 즉각적이고 덜 부드럽다.</summary>
        public const float RemoteFollowSpeed = 14f;

        /// <summary>원격 캐릭터가 이 거리(px)를 넘게 벌어지면 보간하지 않고 순간이동한다.</summary>
        public const float RemoteSnapDistance = 400f;

        private static readonly Vector2 LegacyCenter = new Vector2(0f, -100f);
        private static readonly Vector2 LegacyRadius = new Vector2(840f, 360f);

        /// <summary>
        /// 씬의 TableArea/TableMovementBounds 오브젝트를 읽는다.
        /// 이 오브젝트의 Move/Scale 조작이 로컬 이동과 호스트 검증에 함께 반영된다.
        /// </summary>
        private static TableMovementBounds Bounds => TableMovementBounds.Current;

        private static Vector2 Center => Bounds != null ? Bounds.Center : LegacyCenter;
        private static Vector2 Radius => Bounds != null ? Bounds.Radius : LegacyRadius;

        /// <summary>입력 방향을 길이 1 이하로 정규화한다. 대각선이 더 빠르지 않게 한다.</summary>
        public static Vector2 NormalizeInput(float x, float y)
        {
            var input = new Vector2(x, y);
            float magnitude = input.magnitude;
            if (magnitude <= 0.0001f) return Vector2.zero;
            return magnitude > 1f ? input / magnitude : input;
        }

        /// <summary>테이블 타원 안(테두리 포함)인지 검사한다.</summary>
        public static bool IsInsideTable(Vector2 position)
        {
            if (Bounds != null) return Bounds.IsInside(position);

            Vector2 center = Center;
            Vector2 radius = Radius;
            float dx = (position.x - center.x) / radius.x;
            float dy = (position.y - center.y) / radius.y;
            return dx * dx + dy * dy <= 1f + 1e-3f;
        }

        /// <summary>테이블 타원 안으로 잘라낸다. 호스트가 받은 좌표도 이 경로를 사용한다.</summary>
        public static Vector2 Clamp(Vector2 position)
        {
            if (Bounds != null) return Bounds.Clamp(position);

            Vector2 center = Center;
            Vector2 radius = Radius;
            float dx = (position.x - center.x) / radius.x;
            float dy = (position.y - center.y) / radius.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance <= 1f) return position;

            return new Vector2(
                center.x + dx / distance * radius.x,
                center.y + dy / distance * radius.y);
        }

        /// <summary>한 프레임 이동. 입력은 정규화되어 들어온다고 가정하지 않는다.</summary>
        public static Vector2 Step(Vector2 position, Vector2 input, float deltaTime)
        {
            Vector2 direction = NormalizeInput(input.x, input.y);
            if (direction == Vector2.zero) return Clamp(position);
            return Clamp(position + direction * (Speed * deltaTime));
        }

        /// <summary>원격 캐릭터를 목표 위치로 부드럽게 당긴다. 너무 멀면 순간이동.</summary>
        public static Vector2 Follow(Vector2 current, Vector2 target, float deltaTime)
        {
            if ((target - current).sqrMagnitude > RemoteSnapDistance * RemoteSnapDistance)
                return target;

            float t = 1f - Mathf.Exp(-RemoteFollowSpeed * deltaTime);
            return Vector2.Lerp(current, target, t);
        }

        /// <summary>
        /// 역할별 시작 위치. 실제 값은 씬의 TableMovementBounds에 연결된
        /// Spawn_PlayerA / Spawn_PlayerB / Spawn_Dealer 오브젝트에서만 가져온다.
        /// </summary>
        public static Vector2 SpawnPositionFor(PlayerRole role)
        {
            if (Bounds != null) return Bounds.SpawnPositionFor(role);
            return Vector2.zero;
        }
    }
}
