using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 캔버스 좌표계에서 플레이어가 움직일 수 있는 테이블 타원과 역할별 시작 위치.
    /// GameScene의 TableArea와 LobbyScene의 PanelRoom 아래 경계 오브젝트를
    /// 클라이언트 이동과 호스트 검증이 함께 읽는다.
    /// </summary>
    public sealed class TableMovementBounds : MonoBehaviour
    {
        private const int GizmoSegments = 64;

        [Header("Role Spawn Points (Scene Objects)")]
        [Tooltip("TableArea 아래 Spawn_PlayerA RectTransform. 씬에서 직접 옮겨 시작 위치를 조정합니다.")]
        [SerializeField] private RectTransform playerASpawnPoint;
        [Tooltip("TableArea 아래 Spawn_PlayerB RectTransform. 씬에서 직접 옮겨 시작 위치를 조정합니다.")]
        [SerializeField] private RectTransform playerBSpawnPoint;
        [Tooltip("TableArea 아래 Spawn_Dealer RectTransform. 씬에서 직접 옮겨 시작 위치를 조정합니다.")]
        [SerializeField] private RectTransform dealerSpawnPoint;

        private static TableMovementBounds current;

        /// <summary>
        /// 현재 씬의 테이블 영역 아래에 있는 유일한 경계 오브젝트.
        /// Transform의 위치와 Scale을 조절하면 모든 이동 판정에 즉시 반영된다.
        /// </summary>
        public static TableMovementBounds Current
        {
            get
            {
                if (current == null)
                    current = FindFirstObjectByType<TableMovementBounds>(
                        FindObjectsInactive.Include);
                return current;
            }
        }

        private RectTransform RectTransform => transform as RectTransform;

        /// <summary>타원 중심. Move 도구로 직접 옮길 수 있다.</summary>
        public Vector2 Center
        {
            get
            {
                RectTransform rect = RectTransform;
                return rect != null
                    ? rect.anchoredPosition
                    : (Vector2)transform.localPosition;
            }
        }

        /// <summary>
        /// 타원 반경. 기본 RectTransform 크기의 절반에 Transform Scale을 적용한다.
        /// Scale 도구로 조절하는 값이다.
        /// </summary>
        public Vector2 Radius
        {
            get
            {
                RectTransform rect = RectTransform;
                Vector2 size = rect != null ? rect.rect.size : Vector2.one;
                Vector3 scale = transform.localScale;
                return new Vector2(
                    Mathf.Max(1f, size.x * 0.5f * Mathf.Abs(scale.x)),
                    Mathf.Max(1f, size.y * 0.5f * Mathf.Abs(scale.y)));
            }
        }

        public bool IsInside(Vector2 position)
        {
            Vector2 center = Center;
            Vector2 radius = Radius;
            float dx = (position.x - center.x) / radius.x;
            float dy = (position.y - center.y) / radius.y;
            return dx * dx + dy * dy <= 1f + 1e-3f;
        }

        public Vector2 Clamp(Vector2 position)
        {
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

        public Vector2 SpawnPositionFor(PlayerRole role)
        {
            RectTransform point = role switch
            {
                PlayerRole.PlayerA => playerASpawnPoint,
                PlayerRole.PlayerB => playerBSpawnPoint,
                PlayerRole.Dealer => dealerSpawnPoint,
                _ => null
            };

            if (point == null) return Center;

            // AvatarStage와 TableMovementBounds는 모두 TableArea 좌표를 사용한다.
            // 포인트가 중간 정리 오브젝트 아래 있어도 월드 위치를 TableArea 로컬 좌표로
            // 바꾸므로 계층 구조에 영향을 받지 않는다.
            Transform coordinateRoot = transform.parent;
            Vector2 position = coordinateRoot != null
                ? (Vector2)coordinateRoot.InverseTransformPoint(point.position)
                : point.anchoredPosition;
            return Clamp(position);
        }

        private void OnEnable()
        {
            current = this;
        }

        private void OnDisable()
        {
            if (current == this) current = null;
        }

        private void OnDrawGizmosSelected()
        {
            RectTransform rect = RectTransform;
            if (rect == null) return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);

            Vector2 halfSize = rect.rect.size * 0.5f;
            Vector3 previous = new Vector3(halfSize.x, 0f, 0f);
            for (int i = 1; i <= GizmoSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / GizmoSegments;
                Vector3 next = new Vector3(
                    Mathf.Cos(angle) * halfSize.x,
                    Mathf.Sin(angle) * halfSize.y,
                    0f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;

            DrawSpawnPoint(playerASpawnPoint, new Color(0.2f, 0.65f, 1f, 1f));
            DrawSpawnPoint(playerBSpawnPoint, new Color(1f, 0.55f, 0.2f, 1f));
            DrawSpawnPoint(dealerSpawnPoint, new Color(0.8f, 0.25f, 1f, 1f));
        }

        private static void DrawSpawnPoint(RectTransform point, Color color)
        {
            if (point == null) return;

            Color previousColor = Gizmos.color;
            Gizmos.color = color;
            float radius = 22f * Mathf.Max(0.01f, point.lossyScale.x);
            Gizmos.DrawWireSphere(point.position, radius);
            Gizmos.DrawLine(point.position + Vector3.left * radius, point.position + Vector3.right * radius);
            Gizmos.DrawLine(point.position + Vector3.down * radius, point.position + Vector3.up * radius);
            Gizmos.color = previousColor;
        }
    }
}
