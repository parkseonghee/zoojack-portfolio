using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 최종 판정 큐브. 세 사람의 초상화를 붙인 정육면체가 빠르게 구르다가
    /// 승자의 면을 정면으로 세우며 딱 멈춘다.
    ///
    /// 화면 캔버스가 ScreenSpaceOverlay라 3D를 그 위에 직접 그릴 수 없다. 그래서
    /// 전용 카메라가 큐브만 렌더 텍스처에 담고, UI는 그 텍스처를 <see cref="RawImage"/>로
    /// 붙인다. 큐브 무대(카메라·조명·큐브)는 씬 본체에서 아주 멀리 떨어진 곳에
    /// 런타임으로 세우므로 씬에 배선할 것이 없고, 저장되지도 않는다.
    ///
    /// 여섯 면에 초상화 셋을 두 번씩 붙인다. 어느 방향으로 굴러도 빈 면이 없고,
    /// 승자 면은 마주 보는 짝이 있어 뒤통수가 비지 않는다.
    /// </summary>
    public class FinalJudgmentCube : MonoBehaviour
    {
        // 씬 본체와 겹치지 않게 무대를 멀리 세운다. 전용 레이어까지 있으면 이중으로 안전하고,
        // 레이어가 없어도 메인 카메라(직교, size 5)의 시야 밖이라 화면에 새지 않는다.
        private static readonly Vector3 RigOrigin = new Vector3(0f, -5000f, 0f);
        private const string CubeLayerName = "ZJFinalCube";

        [Header("연결")]
        [Tooltip("큐브가 그려질 자리. 이 RawImage에 렌더 텍스처가 붙는다.")]
        [SerializeField] private RawImage view;

        // 머티리얼을 코드에서 Shader.Find로 만들면 에디터에서만 보인다.
        // 빌드는 '어떤 에셋도 참조하지 않는 셰이더'를 통째로 잘라내기 때문이다.
        // 여기 에셋으로 물려 두어야 셰이더와 필요한 변형이 빌드에 남는다.
        [Tooltip("큐브 몸통 머티리얼(URP/Lit). 비우면 빌드에서 큐브가 보이지 않는다.")]
        [SerializeField] private Material bodySource;

        [Tooltip("초상화 면 머티리얼(URP/Lit + 알파 잘라내기). 초상화마다 복제해서 쓴다.")]
        [SerializeField] private Material faceSource;

        [Header("회전")]
        [Tooltip("구르기 시작 속도(도/초).")]
        [SerializeField] private float spinSpeed = 620f;

        [Tooltip("두구두구가 한 번 칠 때마다 더해지는 속도. 후반으로 갈수록 빨라진다.")]
        [SerializeField] private float flashKick = 95f;
        [SerializeField] private float maxSpinSpeed = 1500f;

        [Tooltip("회전축. 세로축(0,1,0)이면 왼쪽에서 오른쪽으로 곧게 돈다 — 초상화가 늘 " +
                 "똑바로 서 있어 누구 얼굴인지 읽힌다. 옆면 네 개(A·딜러·B·딜러)가 차례로 " +
                 "지나가므로 세 사람 모두 보인다. 비스듬한 축으로 두면 여섯 면을 다 보여 주는 " +
                 "대신 얼굴이 기울어 굴러 알아보기 어렵다.")]
        [SerializeField] private Vector3 spinAxis = Vector3.up;

        [Header("멈춤")]
        [SerializeField] private float settleSpinDuration = 0.55f;
        [SerializeField] private float snapDuration = 0.42f;

        [Header("무대")]
        [SerializeField] private int textureSize = 512;
        [SerializeField] private float cameraDistance = 3.4f;

        [Tooltip("카메라 높이. 0이면 큐브 정면을 똑바로 본다 — 멈춘 순간 승자의 면 하나만 남아 " +
                 "그림 한 장처럼 보인다. 0이 아니면 윗면이 걸쳐 보여 결과가 지저분해진다. " +
                 "구르는 동안에는 큐브가 기울어 있어 이 값이 0이어도 입체로 읽힌다.")]
        [SerializeField] private float cameraHeight;

        [SerializeField] private float cameraFov = 32f;

        // 초상화를 붙일 여섯 면. 이름 순서는 A · 딜러 · B이고, 뒤쪽 세 면이 같은 셋을 되풀이한다.
        private static readonly (Vector3 Normal, Vector3 Up, int Portrait)[] Faces =
        {
            (new Vector3( 0f,  0f, -1f), Vector3.up,       0), // 정면 = A
            (new Vector3( 1f,  0f,  0f), Vector3.up,       1), // 오른쪽 = 딜러
            (new Vector3(-1f,  0f,  0f), Vector3.up,       2), // 왼쪽 = B
            (new Vector3( 0f,  0f,  1f), Vector3.up,       1), // 뒤 = 딜러
            (new Vector3( 0f,  1f,  0f), Vector3.forward,  2), // 위 = B
            (new Vector3( 0f, -1f,  0f), Vector3.back,     0)  // 아래 = A
        };

        private Transform rig;
        private Transform cube;
        private Camera rigCamera;
        private RenderTexture target;
        private Material bodyMaterial;
        private readonly Material[] faceMaterials = new Material[3];
        private readonly Transform[] faceQuads = new Transform[Faces.Length];

        private Sequence settle;
        private float currentSpin;
        private bool active;

        /// <summary>
        /// <see cref="Settle"/>을 부른 뒤 승자의 면이 완전히 서기까지 걸리는 시간(초).
        /// 결과 문구를 언제 띄울지 정하는 쪽이 이 값을 기준으로 삼는다 —
        /// 큐브가 아직 구르는데 승자 이름이 먼저 뜨면 연출이 통째로 새어 버린다.
        /// </summary>
        public float SettleDuration => settleSpinDuration + snapDuration;

        /// <summary>면을 정확히 세우는 마지막 접기 시간(초). 이 구간은 늘리지 않는다 —
        /// '딱' 하고 서는 맛이 여기서 나오므로 길어지면 흐물거린다.</summary>
        public float SnapDuration => snapDuration;

        /// <summary>기본 감속 시간(초). 부르는 쪽이 늘려 잡을 때 기준으로 삼는다.</summary>
        public float DefaultSpinDownDuration => settleSpinDuration;

        /// <summary>큐브를 올린다. 초상화는 지금 그 역할을 맡은 사람의 것으로 갈아 끼운다.</summary>
        public void Begin(Sprite playerA, Sprite dealer, Sprite playerB)
        {
            gameObject.SetActive(true);
            BuildRig();
            ApplyCameraPose();
            ApplyPortraits(playerA, dealer, playerB);

            if (active) return;
            active = true;

            KillTweens();
            currentSpin = spinSpeed;
            if (cube != null) cube.localRotation = Quaternion.identity;
            if (rigCamera != null) rigCamera.enabled = true;
        }

        /// <summary>큐브를 내린다.</summary>
        public void End()
        {
            KillTweens();
            active = false;
            currentSpin = 0f;
            if (rigCamera != null) rigCamera.enabled = false;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>아직 승자가 없는 상태. 그냥 계속 구른다.</summary>
        public void FocusNone()
        {
            if (!active) return;
            KillSettle();
            currentSpin = spinSpeed;
        }

        /// <summary>
        /// 두구두구 한 박. 큐브를 한 번 더 밀어 준다. 박자가 점점 빨라지므로
        /// 큐브도 함께 빨라지고, 그게 그대로 긴장감이 된다.
        /// </summary>
        public void Flash(int index)
        {
            if (!active) return;
            currentSpin = Mathf.Min(currentSpin + flashKick, maxSpinSpeed);
        }

        /// <summary>
        /// 승자의 면을 정면으로 세우고 멈춘다.
        /// </summary>
        /// <param name="spinDownSeconds">
        /// 속도를 0까지 떨어뜨리는 데 쓸 시간(초). 0 이하면 인스펙터 값을 쓴다.
        /// 구르는 소리의 잦아드는 길이에 맞추려고 부르는 쪽이 늘려 잡는다.
        /// </param>
        public void Settle(FinalWinner winner, float spinDownSeconds = 0f)
        {
            if (!active || cube == null) return;

            KillSettle();
            Quaternion facing = TargetRotation(winner);
            float spinDown = spinDownSeconds > 0f ? spinDownSeconds : settleSpinDuration;

            settle = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            // 1) 구르는 속도를 떨어뜨린다. 이 동안에도 Update가 계속 돌린다.
            //    OutQuad는 처음에 빨리 줄고 뒤로 갈수록 천천히 줄어 — 소리가 잦아드는
            //    모양(지수 감쇠)과 같아서, 길게 잡아도 그림과 소리가 함께 사그라든다.
            settle.Append(DOTween.To(() => currentSpin, v => currentSpin = v, 0f, spinDown)
                .SetEase(Ease.OutQuad));
            // 2) 남은 각도를 짧게 접어 면을 정확히 세운다. OutBack이 '딱' 소리 나는 맛을 낸다.
            settle.Append(cube.DOLocalRotateQuaternion(facing, snapDuration).SetEase(Ease.OutBack));
            // 3) 멈추는 순간의 반동.
            settle.Append(cube.DOPunchScale(Vector3.one * 0.14f, 0.34f, 8, 0.7f));
        }

        private void Update()
        {
            if (!active || cube == null || currentSpin <= 0f) return;
            cube.localRotation *= Quaternion.AngleAxis(
                currentSpin * Time.unscaledDeltaTime, spinAxis.normalized);
        }

        // 승자 면이 카메라를 똑바로 보게 만드는 큐브 회전.
        // 카메라는 -Z 쪽에서 +Z를 보므로, 정면이 되는 면의 법선은 (0,0,-1)이다.
        private static Quaternion TargetRotation(FinalWinner winner) => winner switch
        {
            FinalWinner.PlayerA => Quaternion.identity,              // (0,0,-1) 면
            FinalWinner.Dealer  => Quaternion.Euler(0f,  90f, 0f),   // (1,0,0) 면을 앞으로
            FinalWinner.PlayerB => Quaternion.Euler(0f, -90f, 0f),   // (-1,0,0) 면을 앞으로
            _                   => Quaternion.identity
        };

        // ── 무대 만들기 ──────────────────────────────────────────────

        private void BuildRig()
        {
            if (rig != null) return;

            int layer = LayerMask.NameToLayer(CubeLayerName);
            if (layer < 0) layer = gameObject.layer;

            var root = new GameObject("[ZJ Final Cube Rig]") { hideFlags = HideFlags.DontSave };
            root.transform.position = RigOrigin;
            rig = root.transform;

            target = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
            {
                name = "ZJ Final Cube RT",
                antiAliasing = 4,
                hideFlags = HideFlags.DontSave
            };
            target.Create();
            if (view != null) view.texture = target;

            var camGo = new GameObject("Camera") { hideFlags = HideFlags.DontSave };
            camGo.transform.SetParent(rig, false);
            rigCamera = camGo.AddComponent<Camera>();
            rigCamera.clearFlags = CameraClearFlags.SolidColor;
            rigCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 투명 — UI 위에 얹혀야 한다
            rigCamera.cullingMask = 1 << layer;
            rigCamera.fieldOfView = cameraFov;
            rigCamera.nearClipPlane = 0.1f;
            rigCamera.farClipPlane = 20f;
            rigCamera.targetTexture = target;
            rigCamera.enabled = false;

            var lightGo = new GameObject("Light") { hideFlags = HideFlags.DontSave };
            lightGo.transform.SetParent(rig, false);
            lightGo.transform.localRotation = Quaternion.Euler(38f, -32f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.intensity = 1.15f;
            light.cullingMask = 1 << layer;
            light.shadows = LightShadows.None;

            BuildCube(layer);
        }

        /// <summary>
        /// 카메라를 인스펙터 값대로 다시 세운다. 무대는 한 번 만들고 계속 쓰기 때문에,
        /// 만들 때 한 번만 적용하면 값을 고쳐도 옛 자세가 그대로 남는다.
        ///
        /// 높이가 0이면 큐브 정면을 똑바로 본다 — 멈춘 순간 승자의 면 하나만 남아
        /// 그림 한 장처럼 보이고, 윗면이 걸쳐 보이지 않는다.
        /// </summary>
        private void ApplyCameraPose()
        {
            if (rigCamera == null) return;

            var t = rigCamera.transform;
            t.localPosition = new Vector3(0f, cameraHeight, -cameraDistance);
            t.localRotation = Quaternion.LookRotation(
                new Vector3(0f, -cameraHeight, cameraDistance).normalized, Vector3.up);
            rigCamera.fieldOfView = cameraFov;
        }

        private void BuildCube(int layer)
        {
            var cubeGo = new GameObject("Cube") { hideFlags = HideFlags.DontSave, layer = layer };
            cubeGo.transform.SetParent(rig, false);
            cube = cubeGo.transform;

            bodyMaterial = CloneSource(bodySource, "몸통");

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.hideFlags = HideFlags.DontSave;
            body.layer = layer;
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(cube, false);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;

            var quadMesh = BuildFaceMesh();
            for (int i = 0; i < Faces.Length; i++)
            {
                var f = Faces[i];
                var go = new GameObject("Face" + i) { hideFlags = HideFlags.DontSave, layer = layer };
                go.transform.SetParent(cube, false);
                // 몸통 표면보다 아주 살짝 띄워야 Z-파이팅으로 지글거리지 않는다.
                go.transform.localPosition = f.Normal * 0.501f;
                go.transform.localRotation = Quaternion.LookRotation(f.Normal, f.Up);

                go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = FaceMaterial(f.Portrait);
                faceQuads[i] = go.transform;
            }
        }

        private Material FaceMaterial(int portrait)
        {
            if (faceMaterials[portrait] != null) return faceMaterials[portrait];

            // 초상화 바깥은 투명하다. 반투명 대신 잘라내기(에셋에 이미 설정돼 있다)를 쓰면
            // 정렬 문제가 없고 몸통과 같은 Lit 셰이딩을 받아 면이 돌 때 함께 어두워진다.
            // 초상화마다 다른 텍스처를 넣어야 하므로 에셋을 그대로 쓰지 않고 복제한다.
            var mat = CloneSource(faceSource, "초상화 면");
            faceMaterials[portrait] = mat;
            return mat;
        }

        private void ApplyPortraits(Sprite playerA, Sprite dealer, Sprite playerB)
        {
            ApplyPortrait(0, playerA);
            ApplyPortrait(1, dealer);
            ApplyPortrait(2, playerB);

            // 초상화마다 가로세로 비율이 다르다. 정사각 면에 그대로 늘리면 찌그러지므로
            // 짧은 쪽을 기준으로 판을 줄여 원래 비율을 지킨다.
            for (int i = 0; i < faceQuads.Length; i++)
            {
                if (faceQuads[i] == null) continue;
                Sprite s = Faces[i].Portrait == 0 ? playerA : Faces[i].Portrait == 1 ? dealer : playerB;
                faceQuads[i].localScale = FaceScale(s);
            }
        }

        private void ApplyPortrait(int index, Sprite sprite)
        {
            var mat = faceMaterials[index];
            if (mat == null || sprite == null || sprite.texture == null) return;

            var tex = sprite.texture;
            var r = sprite.textureRect;
            var scale = new Vector2(r.width / tex.width, r.height / tex.height);
            var offset = new Vector2(r.x / tex.width, r.y / tex.height);

            foreach (var prop in new[] { "_BaseMap", "_MainTex" })
            {
                if (!mat.HasProperty(prop)) continue;
                mat.SetTexture(prop, tex);
                mat.SetTextureScale(prop, scale);
                mat.SetTextureOffset(prop, offset);
            }
        }

        // 면 안에서 초상화가 차지할 크기. 여백을 남겨야 주사위처럼 보인다.
        private static Vector3 FaceScale(Sprite sprite)
        {
            const float fill = 0.78f;
            if (sprite == null || sprite.rect.height <= 0f) return new Vector3(fill, fill, 1f);

            float aspect = sprite.rect.width / sprite.rect.height;
            return aspect >= 1f
                ? new Vector3(fill, fill / aspect, 1f)
                : new Vector3(fill * aspect, fill, 1f);
        }

        /// <summary>
        /// 면 판 하나. 법선이 로컬 +Z이고, UV의 u는 로컬 −X 방향으로 증가한다 —
        /// 판이 바깥(법선 쪽)을 향할 때 화면에서 좌우가 뒤집히지 않게 하려면 그래야 한다.
        /// </summary>
        private static Mesh BuildFaceMesh()
        {
            var mesh = new Mesh { name = "ZJ Cube Face", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0f)
            };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            // 법선 쪽에서 봤을 때 시계 방향이어야 앞면으로 그려진다. 반대로 감으면
            // 뒷면 컬링에 걸려 판이 통째로 사라진다.
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── 잡일 ─────────────────────────────────────────────────────

        /// <summary>
        /// 인스펙터에 물린 머티리얼 에셋을 복제해 쓴다. 원본을 직접 쓰면 초상화 텍스처가
        /// 에셋에 눌러붙어 프로젝트 파일이 라운드마다 바뀐다.
        ///
        /// 비어 있으면 예전처럼 <see cref="Shader.Find"/>로 만들어 에디터에서는 돌아가지만,
        /// 빌드에서는 그 셰이더가 잘려 나가 큐브가 통째로 사라진다. 조용히 넘어가면
        /// "에디터에서는 되는데 빌드에서만 안 보인다"로 되돌아오므로 크게 알린다.
        /// </summary>
        private static Material CloneSource(Material source, string what)
        {
            if (source != null)
                return new Material(source) { hideFlags = HideFlags.DontSave };

            Debug.LogError($"[FinalJudgmentCube] {what} 머티리얼이 비어 있습니다. " +
                           "인스펙터에 Assets/Materials의 큐브 머티리얼을 물려 주세요 — " +
                           "빌드에서는 셰이더가 잘려 나가 큐브가 보이지 않습니다.");

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { hideFlags = HideFlags.DontSave };
        }

        private void KillSettle()
        {
            settle?.Kill();
            settle = null;
            if (cube != null) { cube.DOKill(); cube.localScale = Vector3.one; }
        }

        private void KillTweens() => KillSettle();

        // 패널이 통째로 꺼져도 트윈과 카메라가 남지 않게 한다.
        private void OnDisable()
        {
            KillTweens();
            active = false;
            currentSpin = 0f;
            if (rigCamera != null) rigCamera.enabled = false;
        }

        private void OnDestroy()
        {
            KillTweens();
            if (rig != null) Destroy(rig.gameObject);
            if (target != null) { target.Release(); Destroy(target); }
        }
    }
}
