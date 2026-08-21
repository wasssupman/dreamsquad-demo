using UnityEngine;
using Wassup.Core.TimeControl;

namespace Wassup.Presentation
{
    // instinct-wreck — 부서진 본능(3×3 공격 거점)의 잔해.
    //
    // 이 뷰는 게임 규칙을 하나도 소유하지 않는다. 「부서졌다」는 sim 이 이미 정했고
    // (Health 0 → 사망 경로), 브리지가 그 사건을 셀로 풀어 `Collapse()` 한 번을 부른다.
    // 여기 있는 것은 «부서진 것이 어떻게 생겼나» 뿐이다.
    //
    // **메쉬 정점은 건드리지 않는다**(spec README 결정). KayKit `cannon_base` 리그가 이미
    // base → turret → barrel 3개의 별개 GameObject 라, 「포신이 떨어진다」는 정점 조작이 아니라
    // 트랜스폼 하나 떼어내는 일이다. 실루엣 변형은 부품 분해 + 주저앉음 + 그을림으로 만든다.
    //
    // 수치는 전부 프리팹 저작이다(계약 4) — 잔해의 모양은 «이 프랍이 어떻게 생겼나» 이지
    // «이 거점이 얼마나 센가» 가 아니라서 `StructureData`(SO)에 넣지 않는다.
    public sealed class StructureWreckView : MonoBehaviour
    {
        // ── unit 0 — 잔해 자세 ──────────────────────────────────────────────
        [Header("잔해 자세")]
        [Tooltip("붕괴 시 끌 포신 조준 프리젠터. 안 끄면 매 Update 가 barrel.rotation 을 되써서 " +
                 "떨어지는 포신이 «누운 채 조준하는» 상태가 된다.")]
        [SerializeField] private StructureTurretView turret;
        // 값은 «탄 것으로 읽히되 진영색이 남는» 선이다(사용자 결정 2026-08-21). 0.30 까지
        // 내리면 노란 아군 본능이 거의 검게 변해 어느 편이었는지 안 읽히고, 0.58 까지 올리면
        // 그을림이 아니라 먼지로 보인다. 0.45 에서 아군(호박빛)과 적(붉음)이 여전히 갈린다.
        [Tooltip("그을림 색. MPB 로만 쓴다 — 공용 머티리얼은 건드리지 않는다.")]
        [SerializeField] private Color scorchTint = new Color(0.45f, 0.40f, 0.37f, 1f);
        [Tooltip("주저앉음 비율. 실루엣이 낮아져야 «무너졌다» 가 멀리서도 읽힌다.")]
        [Range(0.2f, 1f)] [SerializeField] private float settleScale = 0.72f;
        [Tooltip("주저앉는 데 걸리는 시간. 즉시 스냅이 아니라 짧은 동작이어야 사건으로 보인다.")]
        [Min(0f)] [SerializeField] private float settleSeconds = 0.25f;

        // ── unit 1 — 떨어지는 부품 ──────────────────────────────────────────
        [Header("떨어지는 부품")]
        [Tooltip("떼어낼 부품(cannon_barrel_*). 프리팹에서 지정한다 — 이름 문자열 탐색 금지.")]
        [SerializeField] private Transform[] debris;
        [Min(0f)] [SerializeField] private float debrisPopSpeed = 1.6f;
        [Min(0f)] [SerializeField] private float debrisOutwardSpeed = 0.9f;
        [Min(0f)] [SerializeField] private float debrisGravity = 9f;
        [SerializeField] private float debrisSpinDegPerSec = 320f;
        [Tooltip("착지 후 구르다 멎기까지. 이 동안 회전이 감쇠하며 옆으로 눕는다.")]
        [Min(0.01f)] [SerializeField] private float debrisRestSeconds = 0.35f;
        [Tooltip("착지면을 바닥 타일에서 살짝 띄운다 — 코플레이너 z-acne 회피(PropGroundLift 선례).")]
        [Min(0f)] [SerializeField] private float debrisGroundLift = 0.02f;

        // ── unit 1·2 — 잔해 VFX ─────────────────────────────────────────────
        [Header("잔해 VFX")]
        [Tooltip("붕괴 시 켤 비활성 자식들. [0] 파괴 직후 버스트(1회) · [1] 파괴 이후 잔불 연기(루프). " +
                 "위치·스케일·수명은 전부 프리팹 저작이고 코드는 스위치 한 줄만 갖는다.")]
        [SerializeField] private GameObject[] wreckVfx;

        private bool _collapsed;
        private MeshRenderer[] _meshRenderers;
        private MaterialPropertyBlock _mpb;

        private Vector3 _settleFrom;
        private Vector3 _settleTo;
        private float _settleElapsed = -1f;   // 음수 = 진행 중 아님

        private DebrisPiece[] _pieces;
        private int _pieceCount;

        private struct DebrisPiece
        {
            public Transform t;
            public Vector3 velocity;
            public Vector3 spinAxis;
            public float spin;
            public float restY;
            public Quaternion restRotation;
            public float restElapsed;   // 음수 = 아직 공중
        }

        private void Awake()
        {
            // 파티클 렌더러는 제외한다 — 연기를 검댕색으로 틴트하면 잔불이 안 보인다.
            _meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            _mpb = new MaterialPropertyBlock();
        }

        /// 붕괴 1회. 반환값 = 떼어낸 부품을 담은 컨테이너(없으면 null).
        /// **호출자가 그것을 자기 뷰 스윕에 등록한다** — 새 정리 경로를 만들지 않기 위해서다
        /// (spec 계약 10: `OnDestroy` 정리는 씬 언로드 fake-null 레이스를 탄다).
        public GameObject Collapse()
        {
            if (_collapsed) return null;   // 멱등 — 두 번 곱해지면 스케일이 무너진다
            _collapsed = true;

            // 계약 5 — 포신을 돌리던 손을 먼저 놓게 한다.
            if (turret != null) turret.enabled = false;

            ApplyScorch();
            BeginSettle();
            var debrisRoot = DetachDebris();
            EnableWreckVfx();
            return debrisRoot;
        }

        private void ApplyScorch()
        {
            for (int i = 0; i < _meshRenderers.Length; i++)
            {
                var r = _meshRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", scorchTint);
                _mpb.SetColor("_Color", scorchTint);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void BeginSettle()
        {
            _settleFrom = transform.localScale;
            _settleTo = _settleFrom * settleScale;
            if (settleSeconds <= 0f)
            {
                transform.localScale = _settleTo;
                return;
            }
            _settleElapsed = 0f;
        }

        // 떼어낸 부품은 프랍 루트 **밖**으로 나가야 한다 — 안에 두면 주저앉는 몸통을 따라
        // 같이 줄어들어 「떨어진 포신」이 아니라 「작아진 포신」이 된다.
        private GameObject DetachDebris()
        {
            if (debris == null || debris.Length == 0) return null;

            GameObject root = null;
            _pieces = new DebrisPiece[debris.Length];
            _pieceCount = 0;

            for (int i = 0; i < debris.Length; i++)
            {
                var piece = debris[i];
                if (piece == null)
                {
                    // 저작 실수는 조용히 넘기지 않는다 — 「부서졌는데 아무것도 안 떨어지는」
                    // 상태가 버그처럼 읽힌다(unity-vfx-integration red flag와 같은 판단).
                    Debug.LogWarning($"[StructureWreckView] debris[{i}] 미할당 — {name}", this);
                    continue;
                }

                if (root == null)
                {
                    root = new GameObject($"{name}_Debris");
                    root.transform.SetParent(transform.parent, worldPositionStays: false);
                    root.transform.localPosition = Vector3.zero;
                    root.transform.localRotation = Quaternion.identity;
                    root.transform.localScale = Vector3.one;
                }

                // worldPositionStays — 프랍 루트에 곱해진 viewScale 이 lossyScale 로 보존된다
                // (KayKit 리그는 3노드 전부 균등 스케일이라 전단이 생기지 않는다).
                piece.SetParent(root.transform, worldPositionStays: true);

                // 바깥 방향 = **포신이 마지막으로 겨눈 쪽**. 이미 그 각으로 서 있으니 그쪽으로
                // 넘어지는 게 자연스럽고, 프랍마다 다른 그림이 난수 없이 나온다.
                Vector3 outward = piece.forward;
                outward.y = 0f;
                if (outward.sqrMagnitude < 1e-6f) outward = Vector3.forward;
                outward.Normalize();

                _pieces[_pieceCount++] = new DebrisPiece
                {
                    t = piece,
                    velocity = Vector3.up * debrisPopSpeed + outward * debrisOutwardSpeed,
                    spinAxis = Vector3.Cross(Vector3.up, outward).normalized,
                    spin = debrisSpinDegPerSec,
                    restY = transform.position.y + debrisGroundLift,
                    restRotation = Quaternion.LookRotation(outward, Vector3.up),
                    restElapsed = -1f,
                };
            }
            return root;
        }

        private void EnableWreckVfx()
        {
            if (wreckVfx == null) return;
            for (int i = 0; i < wreckVfx.Length; i++)
            {
                if (wreckVfx[i] == null)
                {
                    Debug.LogWarning($"[StructureWreckView] wreckVfx[{i}] 미할당 — {name}", this);
                    continue;
                }
                wreckVfx[i].SetActive(true);
            }
        }

        private void Update()
        {
            if (!_collapsed) return;
            // 배틀 도메인 시계 — 슬로모가 걸리면 잔해도 같이 느려진다(계약 6).
            // ⚠ 파티클(Shuriken)은 자기 시뮬레이션이라 이 계약 밖이다.
            float dt = TimeManager.Instance.DeltaTime(TimeDomain.Battle);
            if (dt <= 0f) return;

            TickSettle(dt);
            TickDebris(dt);
        }

        private void TickSettle(float dt)
        {
            if (_settleElapsed < 0f) return;
            _settleElapsed += dt;
            float k = Mathf.Clamp01(_settleElapsed / settleSeconds);
            // ease-out — 무너지는 것은 처음이 빠르고 끝이 느리다.
            float eased = 1f - (1f - k) * (1f - k);
            transform.localScale = Vector3.Lerp(_settleFrom, _settleTo, eased);
            if (k >= 1f) _settleElapsed = -1f;
        }

        // 물리(Rigidbody/Collider)를 쓰지 않는다 — 이 프로젝트의 뷰는 물리 씬에 의존하지 않고,
        // 물리를 켜면 잔해가 보드 위를 굴러다니며 어디로 갈지 저작할 수 없게 된다.
        private void TickDebris(float dt)
        {
            for (int i = 0; i < _pieceCount; i++)
            {
                ref var p = ref _pieces[i];
                if (p.t == null) continue;

                if (p.restElapsed < 0f)
                {
                    p.velocity.y -= debrisGravity * dt;
                    var pos = p.t.position + p.velocity * dt;
                    if (pos.y <= p.restY)
                    {
                        pos.y = p.restY;
                        p.restElapsed = 0f;
                    }
                    p.t.position = pos;
                    p.t.rotation = Quaternion.AngleAxis(p.spin * dt, p.spinAxis) * p.t.rotation;
                    continue;
                }

                // 착지 후 — 구르던 회전이 감쇠하며 바깥 방향으로 눕는다.
                p.restElapsed += dt;
                float k = Mathf.Clamp01(p.restElapsed / debrisRestSeconds);
                p.t.rotation = Quaternion.Slerp(p.t.rotation, p.restRotation, k);
            }
        }
    }
}
