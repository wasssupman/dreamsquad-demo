using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wassup.Data;

namespace Wassup.UI
{
    // lobby-keyring-drag — 로비 캐릭터 키링 드래그.
    // 스와이프하면 키링 모드: 고리(손가락) → 줄 → 캐릭터가 매달려 스프링 스윙.
    // 동작 모델은 인게임 keyring-cord-preview 계약의 캔버스 px 이식(코드 공유 없음).
    // 전 좌표는 캐릭터 부모 RectTransform 로컬. 튜닝값은 LobbyKeyringSettings SO.
    // 놓으면 중력 낙하 → 바닥에서 작은 바운스 후 정지 → 캐릭터 행동 재개. 낙하 중 재잡기 허용.
    public class LobbyKeyringDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private LobbyKeyringSettings settings;

        private enum Phase { Idle, Dragging, Falling }

        private static Sprite _ringSprite; // 절차적 annulus, 1회 생성 공유

        private RectTransform _rt;
        private RectTransform _parentRt;
        private Canvas _canvas;
        private ILobbyKeyringTarget _target;
        private float _floorY; // 바닥 = 초기 anchoredPosition.y

        private Phase _phase;
        private int _pointerId;
        private Vector2 _fingerLocal; // 손가락(=고리) 위치
        private Vector2 _headPos;     // 캐릭터 머리 실제 위치(스프링 지연)
        private Vector2 _headVel;
        private float _fallVelY;      // 낙하 세로 속도(px/s)
        private RectTransform _cord;
        private RectTransform _ring;
        private Image _cordImage;
        private Image _ringImage;

        // 드래그/낙하 중 — 캐릭터 클릭 리액션 차단 가드용(unit 2 에서 Falling 포함).
        public bool IsBusy => _phase != Phase.Idle;

        // outgame-tutorial unit 6 — 챕터 C(로비 키링 안내)의 완료 신호. 잡은 사건만
        // 알린다: OnBeginDrag 의 조기 return(settings null · 두 번째 포인터 · 좌표 변환
        // 실패)에서는 발화하지 않으므로, 구독자는 "정말 드래그가 시작됐다"로 믿어도 된다.
        public event Action DragStarted;

        // lobby-background-parallax — 배경 패럴랙스가 "키링 스와이프 중" 만 구동되도록 구독하는 신호.
        // 세션 1개(두 번째 포인터 무시)라 static 참조 하나로 충분. 별도 정리 로직이 필요 없다:
        // phase 가 Dragging 을 벗어나면 자동 false, 오브젝트가 파괴되면 Unity 의 null 비교가 잡는다.
        // Falling(놓은 뒤 낙하)은 제외 — 스와이프 중이 아니다.
        private static LobbyKeyringDrag _active;
        public static bool AnyDragging => _active != null && _active._phase == Phase.Dragging;

        // 피벗→머리(상단 중앙) 세로 오프셋. 회전 0 기준.
        private float HeadOffsetY => _rt.rect.height * (1f - _rt.pivot.y) * Mathf.Abs(_rt.localScale.y);

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _parentRt = (RectTransform)_rt.parent;
            _canvas = GetComponentInParent<Canvas>();
            _target = GetComponent<ILobbyKeyringTarget>();
            _floorY = _rt.anchoredPosition.y;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (settings == null || _phase == Phase.Dragging) return; // 세션 1개 — 두 번째 포인터 무시
            if (!TryScreenToLocal(eventData, out var local)) return;

            bool regrab = _phase == Phase.Falling; // 낙하 중 재잡기 — 이미 suspended
            _pointerId = eventData.pointerId;
            _fingerLocal = local;
            // 현재 위치에서 시작해 스프링이 자연히 끌어올린다(목표 스냅 금지 — 잡아 올리는 느낌).
            _headPos = _rt.anchoredPosition + (Vector2)(_rt.localRotation * new Vector3(0f, HeadOffsetY, 0f));
            _headVel = regrab ? new Vector2(0f, _fallVelY) : Vector2.zero; // 낙하 속도 승계
            if (!regrab) _target?.SuspendForKeyring();
            BuildRig();
            _phase = Phase.Dragging;
            _active = this; // 배경 패럴랙스 게이트(AnyDragging)
            DragStarted?.Invoke(); // 성공 경로 말미 — 상태를 모두 세운 뒤 알린다
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_phase != Phase.Dragging || eventData.pointerId != _pointerId) return;
            if (TryScreenToLocal(eventData, out var local)) _fingerLocal = local;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_phase != Phase.Dragging || eventData.pointerId != _pointerId) return;
            BeginFall();
        }

        // 놓기: 리그 제거, x 는 놓은 위치(착지 범위 클램프) 고정, 스윙 속도의 y 성분을
        // 초기 낙하 속도로 승계(툭 놓는 연속감). 착지 판정은 Tick 의 FallStep.
        private void BeginFall()
        {
            DestroyRig();
            var p = _rt.anchoredPosition;
            _rt.anchoredPosition = new Vector2(
                Mathf.Clamp(p.x, settings.landingMinX, settings.landingMaxX), p.y);
            _fallVelY = _headVel.y;
            _phase = Phase.Falling;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // Update 에서 분리 — 기존 로비 스크립트처럼 에디터 검증 툴이 dt 를 직접 주입할 수 있게.
        public void Tick(float dt)
        {
            if (_phase == Phase.Idle || settings == null) return;
            dt = Mathf.Max(dt, 1e-4f);
            if (_phase == Phase.Dragging) TickDrag(settings, dt);
            else TickFall(settings, dt);
        }

        private void TickDrag(LobbyKeyringSettings s, float dt)
        {
            // 무게추 스프링 + 속도 상한. 워밍업(가속 램프) 금지 — 인게임 계약 승계.
            // keyring-unify 0 — 수학은 KeyringSim 공유(Vector2 오버로드, 인게임 Vector3 와 bit-exact).
            Vector2 headTarget = _fingerLocal + Vector2.down * s.ropeLength;
            KeyringSim.SpringStep(ref _headPos, ref _headVel, headTarget, s.spring, s.damping, s.maxSpeed, dt);

            // 기울임 = 줄(머리→고리) 방향, maxAngle 클램프. 회전 중심은 머리 —
            // reparent 없이 피벗 위치를 역산(pos = 머리 - Rotate(머리 오프셋, θ)).
            Vector2 toRing = (_fingerLocal - _headPos).normalized;
            float lean = KeyringSim.LeanAngle(toRing.x, toRing.y, s.maxAngle);
            var rot = Quaternion.Euler(0f, 0f, lean);
            _rt.localRotation = rot;
            _rt.anchoredPosition = _headPos - (Vector2)(rot * new Vector3(0f, HeadOffsetY, 0f));

            UpdateRig(s);
        }

        private void TickFall(LobbyKeyringSettings s, float dt)
        {
            var p = _rt.anchoredPosition;
            float y = p.y;
            bool landed = KeyringSim.FallStep(ref y, ref _fallVelY, _floorY, dt, s.gravity, s.bounceDamping, s.bounceMinSpeed);
            _rt.anchoredPosition = new Vector2(p.x, y);
            if (!landed)
            {
                // 낙하 동안 기울임은 직립으로 서서히 복귀
                _rt.localRotation = Quaternion.RotateTowards(
                    _rt.localRotation, Quaternion.identity, s.fallUprightSpeed * dt);
                return;
            }
            _rt.localRotation = Quaternion.identity;
            _phase = Phase.Idle;
            _target?.ResumeFromKeyring();
        }

        private bool TryScreenToLocal(PointerEventData eventData, out Vector2 local)
        {
            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRt, eventData.position, cam, out local);
        }

        // 줄 = 고리→머리 2점 직선(회전+세로 스케일한 흰 사각 Image), 고리 = annulus 스프라이트.
        private void UpdateRig(LobbyKeyringSettings s)
        {
            if (_ring != null)
            {
                _ring.anchoredPosition = _fingerLocal;
                _ring.sizeDelta = Vector2.one * (s.ringRadius * 2f);
                _ringImage.color = s.cordColor;
            }
            if (_cord != null)
            {
                // 줄 끝은 rect 상단이 아니라 머리 안쪽(cordAttachDrop 만큼 아래) — 줄이 캐릭터
                // 뒤에 그려지므로 머리 뒤로 연결돼 보인다(상단 투명 여백 위에 뜨는 것 방지).
                Vector2 attach = _headPos - (Vector2)(_rt.localRotation * new Vector3(0f, s.cordAttachDrop, 0f));
                Vector2 d = attach - _fingerLocal;
                _cord.anchoredPosition = (_fingerLocal + attach) * 0.5f;
                _cord.sizeDelta = new Vector2(s.cordWidth, d.magnitude);
                _cord.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
                _cordImage.color = s.cordColor;
            }
        }

        private void BuildRig()
        {
            var s = settings;
            var st = s.style; // keyring-unify 1 — 스타일 단일 소스. null = 전체 절차적 폴백.
            var cordGo = new GameObject("KeyringCord", typeof(RectTransform));
            _cord = (RectTransform)cordGo.transform;
            _cord.SetParent(_parentRt, false);
            _cord.SetSiblingIndex(_rt.GetSiblingIndex()); // 캐릭터 뒤에 깔림
            _cordImage = cordGo.AddComponent<Image>();    // 스프라이트 미할당 = 흰 사각형, 틴트로 색
            _cordImage.raycastTarget = false;
            if (st != null && st.cordSprite != null) _cordImage.sprite = st.cordSprite;
            if (st != null && st.uiCordMaterial != null) _cordImage.material = st.uiCordMaterial; // 샤인/홀로 셰이더

            var ringGo = new GameObject("KeyringRing", typeof(RectTransform));
            _ring = (RectTransform)ringGo.transform;
            _ring.SetParent(_parentRt, false);
            _ring.SetSiblingIndex(_rt.GetSiblingIndex()); // 줄 앞·캐릭터 뒤
            _ringImage = ringGo.AddComponent<Image>();
            _ringImage.sprite = st != null && st.ringSprite != null ? st.ringSprite : RingSprite();
            if (st != null && st.uiRingMaterial != null) _ringImage.material = st.uiRingMaterial; // 발광(홀로) 셰이더
            _ringImage.raycastTarget = false;
        }

        private void DestroyRig()
        {
            if (_cord != null) Destroy(_cord.gameObject);
            if (_ring != null) Destroy(_ring.gameObject);
            _cord = null; _ring = null;
            _cordImage = null; _ringImage = null;
        }

        // OnDisable/씬 전환 즉시 정리 — 연출 없이 바닥 스냅(멱등, CleanupSession 패턴).
        private void CancelSession()
        {
            DestroyRig();
            float x = Mathf.Clamp(_rt.anchoredPosition.x, settings.landingMinX, settings.landingMaxX);
            _rt.anchoredPosition = new Vector2(x, _floorY);
            _rt.localRotation = Quaternion.identity;
            _phase = Phase.Idle;
            _target?.ResumeFromKeyring();
        }

        private void OnDisable()
        {
            if (_phase != Phase.Idle) CancelSession();
        }

        // 흰색 annulus(도넛) 스프라이트 1회 생성. 두께는 고정 비율 — 절차적 플레이스홀더.
        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 64;
            float outer = size * 0.5f - 1f;
            float inner = outer * 0.62f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(outer - r) * Mathf.Clamp01(r - inner); // 1px 소프트 엣지
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _ringSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            _ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return _ringSprite;
        }
    }
}
