using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 테이블 위 베팅 자리에 금액대별로 정해진 칩 구성을 세워 보여준다.
    ///
    /// 금액을 액면가로 쪼개는 게 아니라, 금액 구간마다 "이 구간이면 이 칩들"이라는
    /// 템플릿을 미리 정해 둔다. 판돈이 커질수록 더 비싼 색이 등장하고 기둥도 늘어나므로
    /// 플레이어는 숫자를 읽지 않고 색과 부피만으로 판이 얼마나 큰지 알 수 있다.
    ///
    /// 칩 Image는 풀링해서 재사용한다(라운드마다 생성·파괴하지 않는다).
    /// </summary>
    public class BetChipStackView : MonoBehaviour
    {
        /// <summary>같은 색 칩 한 무더기. 화면에는 기둥 하나로 선다.</summary>
        [System.Serializable]
        public struct ChipGroup
        {
            [Tooltip("옆에서 본(_side) 칩 스프라이트.")]
            public Sprite Chip;

            [Min(0)] public int Count;
        }

        /// <summary>금액 구간 하나의 칩 구성. MinAmount 오름차순으로 넣어야 한다.</summary>
        [System.Serializable]
        public struct Template
        {
            [Tooltip("인스펙터 가독용 이름. 로직에는 쓰이지 않는다.")]
            public string Label;

            [Tooltip("이 템플릿이 적용되기 시작하는 금액(포함).")]
            public int MinAmount;

            [Tooltip("왼쪽 기둥부터 차례로. 비싼 색을 앞에 두면 왼쪽에 선다.")]
            public ChipGroup[] Groups;
        }

        [Header("금액 구간별 칩 구성")]
        [SerializeField]
        private Template[] templates =
        {
            new Template { Label = "Green (1~200)",       MinAmount = 0   },
            new Template { Label = "Blue (201~400)",      MinAmount = 201 },
            new Template { Label = "Red (401~600)",       MinAmount = 401 },
            new Template { Label = "Black (601~1000)",    MinAmount = 601 }
        };

        [Header("배치")]
        [Tooltip("_side 스프라이트는 64x38 비율이다.")]
        [SerializeField] private Vector2 chipSize = new Vector2(64f, 38f);

        [Tooltip("한 장 위에 얹을 때의 높이. 옆면 두께만큼 올려야 기둥처럼 보인다.")]
        [SerializeField] private float stackOffsetY = 10f;

        [Tooltip("한 기둥에 쌓을 최대 개수. 넘으면 같은 색을 여러 기둥으로 나눈다.")]
        [SerializeField, Min(1)] private int chipsPerStack = 10;

        [SerializeField] private float stackSpacingX = 52f;

        [Tooltip("칩마다 살짝 어긋나게 두는 흔들림(px). 0이면 자로 잰 듯 정렬된다.")]
        [SerializeField, Min(0f)] private float jitter = 1.5f;

        [Header("연출")]
        [SerializeField] private bool animateDrop = true;
        [SerializeField] private float dropHeight = 90f;
        [SerializeField] private float dropSeconds = 0.18f;
        [SerializeField] private float dropStagger = 0.04f;

        // 칩 한 장이 놓일 자리. Height는 바닥부터 몇 번째인지로, 떨어지는 순서에 쓴다.
        private struct Slot
        {
            public Vector2 Position;
            public int Height;
            public Sprite Chip;
        }

        private readonly List<Image> pool = new List<Image>();
        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<int> order = new List<int>();
        private Sequence dropSequence;
        private int currentSeed;

        /// <summary>현재 표시 중인 금액. 0이면 칩이 없다.</summary>
        public int CurrentAmount { get; private set; }

        /// <summary>
        /// 실제로 쌓여 있는 칩이 차지하는 월드 사각형. 칩이 없으면 크기 0.
        ///
        /// 자리 사각형(240x120)이 아니라 칩 자체를 재는 이유는, 자리가 실제 칩 더미보다
        /// 훨씬 크기 때문이다. 자리로 판정하면 칩 근처에 서기만 해도 캐릭터가 흐려진다.
        /// </summary>
        public Rect WorldChipBounds()
        {
            bool any = false;
            float minX = 0f, minY = 0f, maxX = 0f, maxY = 0f;

            for (int i = 0; i < pool.Count; i++)
            {
                Image chip = pool[i];
                if (chip == null || !chip.gameObject.activeInHierarchy || !chip.enabled) continue;

                Rect r = AvatarOcclusion.WorldRect(chip.rectTransform, Vector2.one);
                if (!any) { minX = r.xMin; minY = r.yMin; maxX = r.xMax; maxY = r.yMax; any = true; }
                else
                {
                    if (r.xMin < minX) minX = r.xMin;
                    if (r.yMin < minY) minY = r.yMin;
                    if (r.xMax > maxX) maxX = r.xMax;
                    if (r.yMax > maxY) maxY = r.yMax;
                }
            }

            return any ? new Rect(minX, minY, maxX - minX, maxY - minY) : Rect.zero;
        }

        /// <summary>
        /// 칩이 다 내려앉았을 때 차지하는 영역(이 컴포넌트의 로컬 좌표). 칩이 없으면 크기 0.
        ///
        /// <see cref="WorldChipBounds"/>와 달리 <b>낙하 연출 중에도 최종 자리</b>를 돌려준다.
        /// 커서 감지 영역처럼 "떨어지는 동안에도 크기가 흔들리면 안 되는" 용도에 쓴다.
        /// </summary>
        public Rect RestingChipBounds()
        {
            if (CurrentAmount <= 0 || slots.Count == 0) return Rect.zero;

            // 칩은 앵커 (0.5,0.5) 자식이므로 기준점은 이 사각형의 중심이다.
            // 피벗이 어디에 있든 rect.center를 더하면 로컬 좌표가 맞는다.
            Vector2 origin = ((RectTransform)transform).rect.center;
            Vector2 half = chipSize * 0.5f;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < slots.Count; i++)
            {
                Vector2 c = origin + slots[i].Position;
                if (c.x - half.x < minX) minX = c.x - half.x;
                if (c.y - half.y < minY) minY = c.y - half.y;
                if (c.x + half.x > maxX) maxX = c.x + half.x;
                if (c.y + half.y > maxY) maxY = c.y + half.y;
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        // 연출 도중 꺼지면 최종 상태로 스냅한다. 그냥 죽이면 칩이 알파 0·공중에 뜬 채로
        // 남고, 같은 금액으로 다시 켜질 때 Show()의 중복 가드에 걸려 영영 안 보인다.
        private void OnDisable()
        {
            if (dropSequence == null) return;
            dropSequence.Kill(true);
            dropSequence = null;
        }

        private void OnDestroy() => KillSequence();

        // ── 표시 ─────────────────────────────────────────────────────

        /// <summary>
        /// 이 자리에 amount 구간의 칩 구성을 세운다. 0 이하면 비운다.
        /// </summary>
        /// <param name="seed">
        /// 기둥 순서를 섞는 씨앗. 보통 라운드 번호를 넘긴다. 같은 씨앗·같은 금액이면
        /// 항상 같은 배열이 나오므로, 모든 피어가 서로의 칩을 똑같은 모양으로 본다.
        /// </param>
        public void Show(int amount, int seed = 0)
        {
            if (amount <= 0)
            {
                Clear();
                return;
            }

            // 같은 금액·같은 씨앗으로 다시 부르면 무시한다. 네트워크 디렉터는 상태가
            // 바뀔 때마다 화면 전체를 다시 그리므로, 이 가드가 없으면 카드 한 장 받을
            // 때마다 칩이 다시 떨어지고 순서까지 뒤바뀐다.
            if (amount == CurrentAmount && seed == currentSeed) return;

            CurrentAmount = amount;
            currentSeed = seed;
            BuildSlots(amount, seed);

            // 칩이 실제로 새로 놓일 때만 소리를 낸다.
            //
            // 소리를 부르는 쪽(디렉터)이 아니라 여기에 둔 이유는 위쪽 중복 가드다. 네트워크
            // 디렉터는 상태가 바뀔 때마다 화면을 통째로 다시 그리므로, 부르는 쪽에 두면 카드
            // 한 장 받을 때마다 칩 소리가 난다. 가드를 지난 이 지점이 곧 "칩 더미가 실제로
            // 달라졌다"는 뜻이라, 첫 베팅 공개와 더블다운 때만 정확히 한 번씩 울린다.
            //
            // 두 자리가 같은 프레임에 놓여도 GameAudio가 한 번으로 합친다.
            if (slots.Count > 0 && isActiveAndEnabled) GameAudio.PlayChip();

            KillSequence();
            if (animateDrop) dropSequence = DOTween.Sequence();

            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                Image chip = Rent(i);
                chip.sprite = slot.Chip;
                chip.enabled = slot.Chip != null;

                var rect = chip.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = chipSize;
                rect.localScale = Vector3.one;

                if (animateDrop)
                {
                    // 바닥 칩부터 떨어진다(높이가 곧 순번).
                    float delay = slot.Height * dropStagger;
                    rect.anchoredPosition = slot.Position + new Vector2(0f, dropHeight);
                    SetAlpha(chip, 0f);
                    dropSequence
                        .Insert(delay, rect.DOAnchorPos(slot.Position, dropSeconds).SetEase(Ease.OutQuad))
                        .Insert(delay, chip.DOFade(1f, dropSeconds * 0.6f));
                }
                else
                {
                    rect.anchoredPosition = slot.Position;
                    SetAlpha(chip, 1f);
                }
            }

            for (int i = slots.Count; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);
        }

        /// <summary>칩을 모두 치운다.</summary>
        public void Clear()
        {
            KillSequence();
            CurrentAmount = 0;
            for (int i = 0; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);
        }

        // ── 템플릿 선택(순수 계산) ────────────────────────────────────

        /// <summary>
        /// 금액이 속한 템플릿의 인덱스. MinAmount가 금액 이하인 것 중 마지막을 고르므로,
        /// 배열을 오름차순으로만 유지하면 구간 경계는 자동으로 맞는다.
        /// 템플릿이 없으면 -1.
        /// </summary>
        public int ResolveTemplateIndex(int amount)
        {
            if (templates == null || templates.Length == 0) return -1;

            int index = -1;
            for (int i = 0; i < templates.Length; i++)
                if (amount >= templates[i].MinAmount) index = i;

            // 첫 템플릿의 MinAmount가 금액보다 높으면(구간 밖) 가장 낮은 구간으로 떨어뜨린다.
            return index < 0 ? 0 : index;
        }

        /// <summary>금액이 속한 템플릿.</summary>
        public Template ResolveTemplate(int amount)
        {
            int index = ResolveTemplateIndex(amount);
            return index < 0 ? default : templates[index];
        }

        /// <summary>이 금액에 세워질 칩 총 장수.</summary>
        public int ChipCountFor(int amount)
        {
            if (amount <= 0) return 0;
            var groups = ResolveTemplate(amount).Groups;
            if (groups == null) return 0;

            int total = 0;
            for (int i = 0; i < groups.Length; i++) total += Mathf.Max(0, groups[i].Count);
            return total;
        }

        // ── 배치 ─────────────────────────────────────────────────────

        /// <summary>
        /// 칩 자리를 미리 계산해 <see cref="slots"/>에 채운다.
        ///
        /// 기둥이 서는 좌우 순서는 씨앗에 따라 섞는다. 템플릿 순서 그대로 두면 매 라운드
        /// 똑같이 "큰 액수부터 왼쪽" 그림이라 금방 눈에 밟힌다. 섞어도 각 기둥의 색과
        /// 장수는 그대로이므로 금액을 읽는 데는 지장이 없다.
        ///
        /// 담는 순서는 아래에서 위로다. 나중에 넣은 자리가 뒤쪽 형제 = 위에 그려지므로,
        /// 위에 얹힌 칩이 아래 칩의 윗면을 가려 진짜 기둥처럼 보인다. 옆면(_side)
        /// 스프라이트에서는 이 순서가 뒤집히면 대번에 어색해진다.
        /// </summary>
        private void BuildSlots(int amount, int seed)
        {
            slots.Clear();

            var groups = ResolveTemplate(amount).Groups;
            if (groups == null || groups.Length == 0) return;

            // 그룹(색)을 섞은 뒤 기둥으로 펼친다. 기둥이 아니라 그룹 단위로 섞어야
            // 같은 색이 여러 기둥으로 쪼개졌을 때 서로 흩어지지 않는다.
            order.Clear();
            for (int g = 0; g < groups.Length; g++)
                if (Mathf.Max(0, groups[g].Count) > 0) order.Add(g);
            Shuffle(order, Hash(seed, amount));

            // 먼저 기둥 구성을 정해 전체 폭을 알아야 가운데 정렬을 할 수 있다.
            var stackHeights = new List<int>();
            var stackChips = new List<Sprite>();
            for (int i = 0; i < order.Count; i++)
            {
                int count = groups[order[i]].Count;
                int stackCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)chipsPerStack));
                int even = count / stackCount;
                int extra = count % stackCount; // 앞쪽 기둥부터 한 장씩 더 얹는다
                for (int s = 0; s < stackCount; s++)
                {
                    stackHeights.Add(even + (s < extra ? 1 : 0));
                    stackChips.Add(groups[order[i]].Chip);
                }
            }
            if (stackHeights.Count == 0) return;

            for (int s = 0; s < stackHeights.Count; s++)
            {
                float x = (s - (stackHeights.Count - 1) * 0.5f) * stackSpacingX;
                for (int h = 0; h < stackHeights[s]; h++)
                {
                    float px = x;
                    float py = h * stackOffsetY;
                    if (jitter > 0f)
                    {
                        // 씨앗·금액·기둥·높이로만 결정되는 흔들림이라 다시 그려도 칩이
                        // 튀지 않고, 라운드가 바뀌면 쌓인 모양도 미세하게 달라진다.
                        px += Wobble(seed * 7919 + amount * 31 + s * 101 + h * 7 + 1) * jitter;
                        py += Wobble(seed * 6271 + amount * 17 + s * 61 + h * 3 + 2) * jitter * 0.3f;
                    }
                    slots.Add(new Slot
                    {
                        Position = new Vector2(px, py),
                        Height = h,
                        Chip = stackChips[s]
                    });
                }
            }
        }

        /// <summary>
        /// 씨앗과 금액을 섞어 0이 아닌 난수 상태를 만든다. xorshift는 상태가 0이면
        /// 영원히 0을 뱉으므로 0을 걸러낸다.
        /// </summary>
        private static uint Hash(int seed, int amount)
        {
            unchecked
            {
                uint h = (uint)(seed * 73856093) ^ (uint)(amount * 19349663);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return h == 0u ? 2463534242u : h;
            }
        }

        // 결정적 xorshift32. 같은 상태에서 시작하면 모든 피어가 같은 순서를 얻는다.
        private static int NextRandom(ref uint state)
        {
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (int)(state & 0x7FFFFFFF);
            }
        }

        // 피셔-예이츠 셔플. 모든 순열이 같은 확률로 나온다.
        private static void Shuffle(List<int> list, uint state)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextRandom(ref state) % (i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // -1..1 사이의 결정적 의사난수.
        private static float Wobble(int seed)
        {
            unchecked
            {
                int h = seed * 1103515245 + 12345;
                h ^= h >> 13;
                return (h % 1000) / 500f - 1f;
            }
        }

        private Image Rent(int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject($"Chip{pool.Count}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                pool.Add(image);
            }

            Image chip = pool[index];
            chip.gameObject.SetActive(true);
            chip.transform.SetSiblingIndex(index);
            return chip;
        }

        private static void SetAlpha(Image image, float alpha)
        {
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        private void KillSequence()
        {
            if (dropSequence == null) return;
            dropSequence.Kill();
            dropSequence = null;
        }
    }
}
