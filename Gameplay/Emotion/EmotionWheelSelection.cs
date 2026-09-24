using UnityEngine;

namespace ZooJack
{
    /// <summary>화면 좌표 두 점으로 12시부터 시계 방향인 8방향 선택을 계산한다.</summary>
    public static class EmotionWheelSelection
    {
        public const int SectorCount = EmotionCatalog.WheelSlotCount;
        public const float SectorAngle = 360f / SectorCount;

        private const float HalfSectorAngle = SectorAngle * 0.5f;
        private const float BoundaryBiasDegrees = 0.0001f;

        /// <summary>
        /// 데드존 밖이면 0(12시)부터 시계 방향인 구역 번호를 반환한다.
        /// 데드존 경계는 선택하지 않으며, 구역 경계는 시계 방향 쪽 구역에 포함한다.
        /// </summary>
        public static bool TryGetIndex(
            Vector2 mousePosition,
            Vector2 wheelCenter,
            float deadZoneRadius,
            out int clockwiseIndex)
        {
            Vector2 direction = mousePosition - wheelCenter;
            float radius = Mathf.Max(0f, deadZoneRadius);

            if (direction.sqrMagnitude <= radius * radius)
            {
                clockwiseIndex = -1;
                return false;
            }

            // Atan2(x, y)를 쓰면 12시가 0도이고 각도가 시계 방향으로 증가한다.
            float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            angle = Mathf.Repeat(angle + HalfSectorAngle + BoundaryBiasDegrees, 360f);

            clockwiseIndex = Mathf.FloorToInt(angle / SectorAngle) % SectorCount;
            return true;
        }

        /// <summary>반지름을 포함한 휠 전체가 영역 안에 남도록 중심을 제한한다.</summary>
        public static Vector2 ClampCenter(Vector2 desiredCenter, Rect area, float wheelRadius)
        {
            float radius = Mathf.Max(0f, wheelRadius);
            float minX = area.xMin + radius;
            float maxX = area.xMax - radius;
            float minY = area.yMin + radius;
            float maxY = area.yMax - radius;

            float x = minX <= maxX ? Mathf.Clamp(desiredCenter.x, minX, maxX) : area.center.x;
            float y = minY <= maxY ? Mathf.Clamp(desiredCenter.y, minY, maxY) : area.center.y;
            return new Vector2(x, y);
        }
    }
}
