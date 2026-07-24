using UnityEngine;

namespace Wassup.Presentation
{
    // camera-direction unit 0 — 카메라 base 포즈의 유일한 런타임 쓰기 주체.
    //
    // 매 LateUpdate 에 절대 합성: 최종 포즈 = 홈 포즈 ⊕ 페이즈 비행 ⊕ 구두점 ⊕ 앰비언트 ⊕ 킥.
    // 홈 포즈 = 씬 authored 포즈를 Awake 에서 캡처 — 씬에서 카메라를 직접 튜닝하는 기존
    // 워크플로우가 그대로 유지된다. 이번 유닛은 킥 채널만 실동작(나머지는 후속 유닛).
    //
    // 실행 순서 계약: LateUpdate 에서 카메라를 읽는 소비자(빌보드/데미지넘버/드래그 프리뷰,
    // 전부 order 0)보다 항상 먼저 최종 포즈를 확정해야 한다 → 음수 order. 단 GameManager(-100)
    // 보다는 뒤(-90) — Start 순서가 결정적이 되어 씬 시작 페이즈를 항상 스냅으로 잡는다.
    // 구 CameraImpactKick 의 self-cancel 패턴은 매 프레임 절대 쓰기 소유자와 양립 불가
    // (revert 가 이중 차감이 됨)라서 킥을 채널로 흡수하고 해당 컴포넌트는 은퇴.
    [DefaultExecutionOrder(-90)]
    [RequireComponent(typeof(Camera))]
    public class CameraDirector : MonoBehaviour
    {
        [SerializeField] private Wassup.Data.CameraDirectionConfig config;

        private Camera _cam;
        private Vector3 _homePos;
        private Quaternion _homeRot;
        private float _homeFov;

        private float _kickRemaining;
        private float _kickDuration;
        private float _kickStrength;
        private bool _settled; // 전 채널 비활성 상태에서 정착 포즈를 이미 써뒀는가 (아이들 프레임 no-op)

        // unit 2 — 구두점 채널 (additive 전용, 카메라 탈취 없음).
        // 줌 펄스: max-hold 재트리거(진폭 max, 누적 없음). 셰이크: ScoreHudView 가 매 프레임
        // 밀어주는 킬 스트릭 heat 비례(지연 ≤1프레임 허용 계약). 비행 중 가중치 0 페이드.
        private float _pulseRemaining;
        private float _pulseDuration;
        private float _pulseStrength;
        private float _shakeHeat;
        private float _shakePhaseX;
        private float _shakePhaseY;
        private float _punctWeight = 1f;

        // unit 5 — 드래그 포커스 채널. 드래그 컨트롤러가 매 프레임 터치 스크린 좌표를 피드,
        // 프레임 staleness 로 자동 해제(명시 Clear 불필요 — 컨트롤러 파괴/정리 누락에도 붙박이 방지).
        // rev 3: 입력 = 스크린 NDC(월드 비의존), 추종 = 스프링-댐핑(KeyringSim) — 스프링 속도가
        // 곧 스와이프 리드 속도(정지 시 0 수렴, 확확 안 바뀜).
        private Vector2 _focusNdcTarget;
        private Vector2 _focusNdc;
        private Vector2 _focusNdcVel;
        private bool _focusSpringInit;
        private int _focusFedFrame = -10;
        private float _focusWeight;
        private float _focusReleaseFrom;
        private float _focusReleaseElapsed;
        private bool _focusReleasing;

        // unit-dreamcatcher-inspect unit 4 — 인스펙트 포커스 채널. 선택 유닛 쪽으로 당겨온다.
        // 드래그 포커스와 같은 형태(NDC → FocusDelta, staleness 자동 해제)지만 입력이 다르다:
        // 손가락이 아니라 **고정 월드 좌표**라, NDC 를 홈 포즈 기준으로 산출한다(SetInspectFocus).
        // 스프링 없음 — 타겟이 안 움직여 스텝 변화가 없다. 부드러움은 가중치 페이드가 담당.
        private Vector2 _inspectNdc;
        private bool _inspectHasNdc;
        private int _inspectFedFrame = -10;
        private float _inspectWeight;
        private float _inspectReleaseFrom;
        private float _inspectReleaseElapsed;
        private bool _inspectReleasing;
        // hand-drag-tooltip unit 6 — 손패 헤드룸. 피드 주도 채널(포커스/인스펙트와 같은
        // staleness 규약)이라 손패 뷰가 죽거나 비활성돼도 자동으로 홈 pitch 로 복귀한다.
        private int _headroomFedFrame = -10;
        private float _headroomWeight;
        private float _headroomVel;
        // defender-relocation unit 6 — 이동모드 줌아웃 오버뷰(좌표 없는 config 구동 채널, 헤드룸 미러).
        private int _overviewFedFrame = -10;
        private float _overviewWeight;
        private float _overviewVel;

        // unit 3 — 앰비언트 브리딩 채널. 파동 위상 누적(절대 시각 비사용 — 장세션 float 정밀도),
        // 켜진 페이즈에서만, 비행 중 가중치 0 크로스페이드 후 서서히 복귀.
        private float[] _breathPhases = System.Array.Empty<float>();
        private float _breathWeight;
        private Wassup.Core.GamePhase _currentPhase = Wassup.Core.GamePhase.None;

        // unit 1 — 페이즈 비행 채널. _flightDelta 가 현재(정착 또는 보간 중) 페이즈 델타.
        // 미등록 페이즈 = hold(현재 델타 유지), 최초 적용만 스냅 (spec 계약).
        private CameraPoseDelta _flightDelta;
        private CameraPoseDelta _flightFrom;
        private CameraPoseDelta _flightTo;
        private float _flightElapsed;
        private float _flightDuration; // 0 = 비행 없음
        private AnimationCurve _flightEase;
        private bool _phaseAppliedOnce;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _homePos = transform.position;
            _homeRot = transform.rotation;
            _homeFov = _cam.fieldOfView;
            if (config == null)
                Debug.LogWarning("[CameraDirector] config 미배선 — 모든 연출 채널 비활성(홈 포즈 고정).", this);
            else if (config.breathWaves != null && config.breathWaves.Length > 0)
            {
                // 파동별 위상 누적기 — SO 의 시작 위상으로 시드.
                _breathPhases = new float[config.breathWaves.Length];
                for (int i = 0; i < _breathPhases.Length; i++)
                    _breathPhases[i] = config.breathWaves[i] != null ? config.breathWaves[i].phase01 : 0f;
            }
        }

        private Wassup.Core.GameManager _gm; // 구독 대상 캐시 — 언구독이 teardown 순서 무관하도록

        private void Start()
        {
            // 구독은 Start — GameManager(-100)와 Awake 순서가 동률이라 Instance 보장 지점 사용.
            // 이미 지나간 페이즈(GameManager.Start 가 먼저 돈 경우)는 CurrentPhase 스냅으로 커버.
            _gm = Wassup.Core.GameManager.Instance;
            if (_gm == null) return; // BattleScene 외 씬 — 비행 채널 비활성
            _gm.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(_gm.CurrentPhase);
            // "활성화 시점 최초 적용만 스냅" — 시작 페이즈가 미등록(hold)이어도 스냅 기회는
            // 여기서 소진된다. 이후 등록 페이즈 첫 진입(예: squad 플로우 None→Gift→Placement)이
            // 한참 뒤여도 스냅 팝 없이 비행한다.
            _phaseAppliedOnce = true;
        }

        private void OnDestroy()
        {
            if (_gm != null) _gm.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(Wassup.Core.GamePhase phase)
        {
            if (config == null) return;
            _currentPhase = phase; // 브리딩 on/off 판정용 — 포즈 등록 여부와 무관하게 기록
            if (!config.enableNonDragEffects) return;
            var pose = FindPhasePose(phase);
            if (pose == null) return; // 미등록 페이즈 = hold

            var target = new CameraPoseDelta
            {
                localPos = pose.localPosOffset,
                pitchDeg = pose.pitchOffset,
                fovDelta = pose.fovOffset,
            };

            // 최초 적용(씬 시작 시점의 현재 페이즈) 또는 flightSec 0 이하 = 즉시 스냅.
            if (!_phaseAppliedOnce || pose.flightSec <= 0f)
            {
                _phaseAppliedOnce = true;
                _flightDelta = target;
                _flightDuration = 0f;
                _settled = false;
                return;
            }

            // 비행 중 재전환 포함 — 현재 보간값에서 새 목표로 재시작(스냅 금지).
            _flightFrom = _flightDelta;
            _flightTo = target;
            _flightElapsed = 0f;
            _flightDuration = pose.flightSec;
            _flightEase = pose.ease;
        }

        private Wassup.Data.CameraPhasePose FindPhasePose(Wassup.Core.GamePhase phase)
        {
            var poses = config.phasePoses;
            if (poses == null) return null;
            for (int i = 0; i < poses.Length; i++)
                if (poses[i] != null && poses[i].phase == phase) return poses[i];
            return null;
        }

        // 임팩트 킥 (구 CameraImpactKick.Kick 승계 — 호출처: DreamcatcherHandView 카드 흡수 임팩트).
        // config 배선 전 호출은 안전 no-op (spec unit 0 계약). kickDuration 0 = 킥 비활성
        // (envelope 의 duration<=0 가드가 단일 소유 — 별도 최소치 클램프를 두지 않는다).
        public void Kick(float strength = 1f)
        {
            if (config == null || !config.enableNonDragEffects) return;
            _kickStrength = Mathf.Clamp01(strength);
            _kickDuration = config.kickDuration;
            _kickRemaining = _kickDuration;
        }

        // unit 2 — 헤비 임팩트 줌 펄스. 연타 시 envelope 누적 없이 max 유지(과누적 방지):
        // 진폭은 현재 유효값과 새 값의 max, 타이머는 재시작. pulseSec 0 = 펄스 끔.
        public void ZoomPulse(float strength = 1f)
        {
            if (config == null || !config.enableNonDragEffects) return;
            float current = _pulseRemaining > 0f ? _pulseStrength : 0f;
            _pulseStrength = Mathf.Max(current, Mathf.Clamp01(strength));
            _pulseDuration = config.pulseSec;
            _pulseRemaining = _pulseDuration;
        }

        // unit 2 — 킬 스트릭 heat(0~1). 소유는 ScoreHudView(산정·감쇠) — 여기는 미러만.
        public void SetShakeHeat(float heat01)
        {
            if (config == null || !config.enableNonDragEffects) return;
            _shakeHeat = Mathf.Clamp01(heat01);
        }

        // unit 5 rev 3 — 드래그 포커스 피드: 터치/포인터 **스크린 좌표(px)**. 드래그 중(온보드)
        // 매 프레임 호출 — 피드가 끊기면(2프레임 초과) 자동 해제. 월드 좌표를 받지 않는 것이
        // 계약(카메라 포즈 되먹임 원천 차단).
        public void SetDragFocus(Vector2 screenPos)
        {
            if (config == null) return;
            _focusNdcTarget = new Vector2(
                screenPos.x / Mathf.Max(1f, Screen.width) * 2f - 1f,
                screenPos.y / Mathf.Max(1f, Screen.height) * 2f - 1f);
            _focusFedFrame = Time.frameCount;
        }

        // unit-dreamcatcher-inspect unit 4 — 인스펙트 포커스 피드: 선택 유닛의 **월드 좌표**.
        // 선택 중 매 프레임 호출 — 피드가 끊기면(2프레임 초과) 자동 해제된다(붙박이 줌 방지).
        //
        // SetDragFocus 와 달리 월드 좌표를 받는 게 계약이다. 되먹임은 여기서 NDC 를 **홈 포즈
        // 기준**으로 뽑아 차단한다 — 현재 포즈로 뽑으면 카메라가 다가갈수록 NDC 가 0 으로 줄어
        // 오프셋이 사라지고 다시 벌어지는 진동이 된다. 홈 포즈는 고정이라 그 루프가 없다.
        // (FocusDelta 의 dirLocal = (ndc.x·tanH, ndc.y·tanV, 1) 복원식의 정확한 역변환.)
        public void SetInspectFocus(Vector3 worldPos)
        {
            if (config == null || _cam == null) return;
            var local = Quaternion.Inverse(_homeRot) * (worldPos - _homePos);
            if (local.z <= 0.001f) return; // 홈 카메라 뒤/평면 — 이번 프레임 피드 스킵(= 자연 해제)
            float tanV = Mathf.Tan(_homeFov * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Mathf.Max(0.01f, _cam.aspect);
            _inspectNdc = new Vector2(
                local.x / (local.z * Mathf.Max(1e-4f, tanH)),
                local.y / (local.z * Mathf.Max(1e-4f, tanV)));
            _inspectHasNdc = true;
            _inspectFedFrame = Time.frameCount;
        }

        // hand-drag-tooltip unit 6 — 손패 헤드룸 피드: 손패가 열려 있는 동안 매 프레임 호출.
        // 피드가 끊기면(2프레임 초과) 자동 해제되어 홈 pitch 로 복귀한다 — 손패 닫힘/페이즈
        // 이탈/씬 파괴 어느 경로든 별도 teardown 호출 없이 복귀가 보장된다(이 파일의
        // 포커스/인스펙트 채널과 같은 계약).
        //
        // 좌표를 받지 않는 것이 계약이다. 이 채널은 "얼마나 눕힐지"만 config 에서 읽고
        // 카메라 상태를 되먹임하지 않으므로 진동 루프가 원천적으로 없다.
        public void SetHandHeadroom()
        {
            if (config == null) return;
            _headroomFedFrame = Time.frameCount;
        }

        // defender-relocation unit 6 — 이동모드(목적지 선택) 중 매 프레임 호출. 어떤 경로로 진입했건
        // 동일한 줌아웃 고정 오버뷰를 준다. 좌표를 받지 않는 게 계약(SetHandHeadroom 과 동일 — 되먹임
        // 루프 없음). 피드가 끊기면(2프레임 초과) 자동으로 홈 포즈로 복귀한다.
        public void SetMoveOverview()
        {
            if (config == null) return;
            _overviewFedFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            if (config == null) return;

            // 현재 제품 설정: 스와이프 드래그 포커스만 사용한다. 토글을 런타임에 끄는 경우에도
            // 이미 진행 중이던 다른 채널이 한 프레임도 남지 않도록 즉시 비운다.
            if (!config.enableNonDragEffects)
            {
                _kickRemaining = 0f;
                _pulseRemaining = 0f;
                _shakeHeat = 0f;
                _breathWeight = 0f;
                _flightDelta = default;
                _flightDuration = 0f;
            }

            // 채널 활성 판정.
            bool flying = config.enableNonDragEffects && _flightDuration > 0f && _flightElapsed < _flightDuration;
            bool punctInput = config.enableNonDragEffects && (_pulseRemaining > 0f || _shakeHeat > 0.0001f);
            // 비행 중 구두점 가중치 0 페이드 (비행이 최우선). 페이드 진행 자체도 활성으로 취급.
            float punctTarget = flying ? 0f : 1f;
            bool punctFading = !Mathf.Approximately(_punctWeight, punctTarget);
            bool punctActive = punctInput && (_punctWeight > 0f || punctFading);
            // 브리딩: 켜진 페이즈 + 비행 아님 + 유효 파동 존재 → 목표 1. 가중치가 0 에 닿을
            // 때까지는 활성(크로스페이드). 유효 파동 검사로 퇴화 config(전 파동 주기 0 등)가
            // 모션 0 인 채 settle 최적화를 영구 무효화하는 것 방지(리뷰 반영).
            bool breathOn = config.enableNonDragEffects && IsBreathPhase(_currentPhase)
                && (config.breathPosAmp != 0f || config.breathRotAmp != 0f) // 음수 진폭 = 위상 반전(유효)
                && HasUsableBreathWave();
            float breathTarget = (breathOn && !flying) ? 1f : 0f;
            bool breathActive = breathTarget > 0f || _breathWeight > 0f;
            // 드래그 포커스: 최근 2프레임 내 피드 + 비행 아님 → 목표 1. 페이드 잔여도 활성.
            bool focusConfigured = config.focusDolly != 0f || config.focusLookWeight > 0f
                || config.focusFovDelta != 0f;
            bool focusFed = Time.frameCount - _focusFedFrame <= 2 && focusConfigured;
            float focusTarget = (focusFed && !flying) ? 1f : 0f;
            bool focusActive = focusTarget > 0f || _focusWeight > 0f;
            // 인스펙트 포커스 — 드래그 포커스와 같은 staleness 규약. enableNonDragEffects 로
            // 게이팅하지 않는다: 그 토글은 앰비언트 연출(킥/펄스/브리딩/비행) 억제용이고 현재
            // 에셋에서 꺼져 있다. 인스펙트 줌은 명시적 제품 기능이라 묶으면 조용히 죽는다.
            bool inspectConfigured = config.inspectDolly != 0f || config.inspectFovDelta != 0f
                || config.inspectLookWeight > 0f;
            bool inspectFed = _inspectHasNdc && Time.frameCount - _inspectFedFrame <= 2 && inspectConfigured;
            float inspectTarget = (inspectFed && !flying) ? 1f : 0f;
            bool inspectActive = inspectTarget > 0f || _inspectWeight > 0f;
            // 손패 헤드룸 — 인스펙트와 같은 staleness 규약이고 같은 이유로
            // enableNonDragEffects 에 묶지 않는다. 비행 중에도 유지한다(페이즈 비행은
            // 현재 꺼져 있고, 켜지더라도 손패가 열려 있으면 헤드룸은 계속 필요하다).
            bool headroomConfigured = config.handHeadroomPitchDeg != 0f || config.handHeadroomDolly != 0f;
            bool headroomFed = Time.frameCount - _headroomFedFrame <= 2 && headroomConfigured;
            float headroomTarget = headroomFed ? 1f : 0f;
            // 스프링이라 가중치가 0 을 스쳐 지나거나(언더댐핑) 미세 잔류할 수 있다.
            // 절대값 + 속도까지 봐야 복귀 도중 idle 최적화에 얼어붙지 않는다.
            bool headroomSettled = Mathf.Abs(_headroomWeight) < 0.0005f
                && Mathf.Abs(_headroomVel) < 0.0005f;
            bool headroomActive = headroomTarget > 0f || !headroomSettled;
            // 이동모드 오버뷰 — 헤드룸과 동일 staleness/스프링 규약.
            bool overviewConfigured = config.moveOverviewDolly != 0f || config.moveOverviewPitchDeg != 0f;
            bool overviewFed = Time.frameCount - _overviewFedFrame <= 2 && overviewConfigured;
            float overviewTarget = overviewFed ? 1f : 0f;
            bool overviewSettled = Mathf.Abs(_overviewWeight) < 0.0005f && Mathf.Abs(_overviewVel) < 0.0005f;
            bool overviewActive = overviewTarget > 0f || !overviewSettled;
            // inspectActive 를 빠뜨리면 아래 idle 최적화(_settled)가 줌을 한 프레임 만에 덮어쓴다.
            // headroomActive 도 같다 — 빠뜨리면 손패를 열어도 pitch 가 즉시 홈으로 덮인다.
            bool anyActive = _kickRemaining > 0f || flying || punctActive || breathActive
                || focusActive || inspectActive || headroomActive || overviewActive;

            // 아이들: 정착 포즈(홈⊕현재 페이즈 델타)를 1회만 쓰고 이후 프레임은 no-op —
            // 매 프레임 transform/FOV 재기입(하이어라키 dirty + 네이티브 세터)을 모바일에서
            // 아낀다. Director 가 유일한 쓰기 주체라 써둔 포즈가 그대로 유지된다.
            if (!anyActive)
            {
                // 아이들 진입 시 구두점 가중치는 목표값으로 스냅 — 비행 직후 0에 얼어붙어
                // 다음 펄스가 램프인과 겹쳐 약해지는 문제 방지(리뷰 반영). 입력 없으니 비가시.
                _punctWeight = punctTarget;
                if (_settled) return;
                ComposeAndWrite(_flightDelta);
                _settled = true;
                return;
            }
            _settled = false;

            // 비행 채널 — 이징 커브(비어 있으면 smoothstep 폴백)로 from→to 보간.
            if (flying)
            {
                _flightElapsed += Time.unscaledDeltaTime;
                float t01 = Mathf.Clamp01(_flightElapsed / _flightDuration);
                float eased = (_flightEase != null && _flightEase.length >= 2)
                    ? _flightEase.Evaluate(t01)
                    : Mathf.SmoothStep(0f, 1f, t01);
                _flightDelta = CameraComposeMath.Lerp(_flightFrom, _flightTo, eased);
                if (t01 >= 1f) { _flightDelta = _flightTo; _flightDuration = 0f; }
            }

            var delta = _flightDelta;

            // 드래그 포커스 채널 — 유닛 방향 dolly + 부분 lookat + 스와이프 리드.
            // base 위치 = 홈⊕비행 localPos (회전은 홈 기준 — FocusDelta 주석 참조).
            if (focusTarget > 0f)
            {
                _focusReleasing = false;
                _focusWeight = Mathf.MoveTowards(_focusWeight, 1f,
                    Time.unscaledDeltaTime / Mathf.Max(0.01f, config.focusFadeInSec));
            }
            else if (_focusWeight > 0f)
            {
                // 복귀는 선형 MoveTowards 대신 초반이 빠른 cubic ease-out. 드래그 해제 직후
                // 즉시 반응하되, 홈 포즈에는 부드럽게 착지한다.
                if (!_focusReleasing)
                {
                    _focusReleasing = true;
                    _focusReleaseFrom = _focusWeight;
                    _focusReleaseElapsed = 0f;
                }
                _focusReleaseElapsed += Time.unscaledDeltaTime;
                float t01 = _focusReleaseElapsed / Mathf.Max(0.01f, config.focusFadeOutSec);
                _focusWeight = _focusReleaseFrom * (1f - CameraComposeMath.EaseOutCubic01(t01));
            }
            if (_focusWeight > 0f)
            {
                // 스프링-댐핑 추종 — 타겟(NDC)으로 스무스하게 수렴, 스프링 속도 = 리드 속도.
                if (!_focusSpringInit)
                {
                    _focusNdc = _focusNdcTarget; // 첫 활성화는 현 포인터에 스냅(스테일 스윙 방지)
                    _focusNdcVel = Vector2.zero;
                    _focusSpringInit = true;
                }
                Wassup.UI.KeyringSim.SpringStep(ref _focusNdc, ref _focusNdcVel, _focusNdcTarget,
                    config.focusSpring, config.focusDamping, 0f,
                    Mathf.Max(Time.unscaledDeltaTime, 1e-4f));

                delta = CameraComposeMath.Add(delta, CameraComposeMath.FocusDelta(
                    _focusNdc, _focusNdcVel, _homeFov, _cam.aspect, _focusWeight,
                    config.focusDolly, config.focusFovDelta, config.focusLookWeight,
                    config.focusLeanPerSpeed, config.focusLeanMaxDeg));
            }
            else
            {
                _focusSpringInit = false; // 다음 드래그는 새 포인터 위치에서 시작
            }

            // 인스펙트 포커스 채널 — 선택 유닛 방향 dolly + 부분 lookat + 줌.
            // 드래그 포커스와 같은 페이드 규약(진입 MoveTowards / 해제 cubic ease-out).
            // 리드/린은 0 — 스와이프가 아니라 고정 타겟이다.
            if (inspectTarget > 0f)
            {
                _inspectReleasing = false;
                _inspectWeight = Mathf.MoveTowards(_inspectWeight, 1f,
                    Time.unscaledDeltaTime / Mathf.Max(0.01f, config.inspectFadeInSec));
            }
            else if (_inspectWeight > 0f)
            {
                if (!_inspectReleasing)
                {
                    _inspectReleasing = true;
                    _inspectReleaseFrom = _inspectWeight;
                    _inspectReleaseElapsed = 0f;
                }
                _inspectReleaseElapsed += Time.unscaledDeltaTime;
                float t01 = _inspectReleaseElapsed / Mathf.Max(0.01f, config.inspectFadeOutSec);
                _inspectWeight = _inspectReleaseFrom * (1f - CameraComposeMath.EaseOutCubic01(t01));
            }
            if (_inspectWeight > 0f)
            {
                delta = CameraComposeMath.Add(delta, CameraComposeMath.FocusDelta(
                    _inspectNdc, Vector2.zero, _homeFov, _cam.aspect, _inspectWeight,
                    config.inspectDolly, config.inspectFovDelta, config.inspectLookWeight,
                    0f, 0f));
            }
            else
            {
                _inspectHasNdc = false; // 다음 선택은 새 타겟에서 시작(스테일 NDC 방지)
            }

            // 손패 헤드룸 채널 — 가중치를 0↔1 로 스프링 추종시켜 pitch + dolly 에 곱한다.
            // pitch 는 보드를 아래로 옮기고 dolly 는 줄인다(상단 여백 합산).
            // 진입/복귀가 같은 스프링이라 여는 맛과 닫는 맛이 대칭이다.
            if (headroomActive)
            {
                Wassup.UI.KeyringSim.SpringStep(ref _headroomWeight, ref _headroomVel,
                    headroomTarget, config.handHeadroomSpring, config.handHeadroomDamping, 0f,
                    Mathf.Max(Time.unscaledDeltaTime, 1e-4f));
                // localPos 는 홈 회전 기준(+Z = 카메라 전방)이라 음수 z = 후퇴 = 줌아웃.
                delta = CameraComposeMath.Add(delta, new CameraPoseDelta
                {
                    pitchDeg = config.handHeadroomPitchDeg * _headroomWeight,
                    localPos = new Vector3(0f, 0f, config.handHeadroomDolly * _headroomWeight),
                });
            }
            else
            {
                // 안착 — 잔류 속도를 털어 다음 개방이 깨끗한 정지에서 출발하게 한다.
                _headroomWeight = 0f;
                _headroomVel = 0f;
            }

            // 이동모드 오버뷰 채널 — 헤드룸 미러. dolly(음수=후퇴=줌아웃) + 선택적 pitch 를 가중치로 곱한다.
            if (overviewActive)
            {
                Wassup.UI.KeyringSim.SpringStep(ref _overviewWeight, ref _overviewVel,
                    overviewTarget, config.moveOverviewSpring, config.moveOverviewDamping, 0f,
                    Mathf.Max(Time.unscaledDeltaTime, 1e-4f));
                delta = CameraComposeMath.Add(delta, new CameraPoseDelta
                {
                    pitchDeg = config.moveOverviewPitchDeg * _overviewWeight,
                    localPos = new Vector3(0f, 0f, config.moveOverviewDolly * _overviewWeight),
                });
            }
            else
            {
                _overviewWeight = 0f;
                _overviewVel = 0f;
            }

            // 구두점 채널 — 가중치 페이드 갱신 후 펄스/셰이크 합산.
            _punctWeight = config.punctuationFadeSec <= 0f
                ? punctTarget
                : Mathf.MoveTowards(_punctWeight, punctTarget,
                    Time.unscaledDeltaTime / config.punctuationFadeSec);

            // 펄스 타이머는 가중치와 무관하게 실시간 감쇠 — 비행 중 얼렸다가 비행 후
            // 뒤늦게 재생되는 "지연 펄스" 방지(리뷰 반영). 억제는 가중치(시각)가 담당.
            float pulseEnv = 0f;
            if (_pulseRemaining > 0f)
            {
                _pulseRemaining -= Time.unscaledDeltaTime;
                pulseEnv = CameraComposeMath.KickEnvelope(_pulseRemaining, _pulseDuration);
            }

            if (_punctWeight > 0f)
            {
                if (pulseEnv > 0f)
                {
                    delta = CameraComposeMath.Add(delta, new CameraPoseDelta
                    {
                        fovDelta = config.pulseFovDelta * _pulseStrength * pulseEnv * _punctWeight,
                    });
                }
                if (_shakeHeat > 0.0001f)
                {
                    float dt = Time.unscaledDeltaTime;
                    _shakePhaseX = Mathf.Repeat(_shakePhaseX + config.shakeFreqX * dt, 1f);
                    _shakePhaseY = Mathf.Repeat(_shakePhaseY + config.shakeFreqY * dt, 1f);
                    delta = CameraComposeMath.Add(delta, CameraComposeMath.ShakeDelta(
                        _shakePhaseX, _shakePhaseY, _shakeHeat * _punctWeight,
                        config.shakeMaxPosAmp, config.shakeMaxRotAmp));
                }
            }

            // 브리딩 채널 — 가중치 크로스페이드 + 파동 위상 누적·합산.
            _breathWeight = config.breathFadeSec <= 0f
                ? breathTarget
                : Mathf.MoveTowards(_breathWeight, breathTarget,
                    Time.unscaledDeltaTime / config.breathFadeSec);
            if (_breathWeight > 0f)
            {
                float dt = Time.unscaledDeltaTime;
                int n = Mathf.Min(_breathPhases.Length, config.breathWaves.Length);
                for (int i = 0; i < n; i++)
                {
                    var wave = config.breathWaves[i];
                    if (wave == null || wave.periodSec <= 0f) continue;
                    _breathPhases[i] = Mathf.Repeat(_breathPhases[i] + dt / wave.periodSec, 1f);
                    delta = CameraComposeMath.Add(delta, CameraComposeMath.BreathWaveDelta(
                        _breathPhases[i], wave.posWeight, wave.pitchWeight, _breathWeight,
                        config.breathPosAmp, config.breathRotAmp));
                }
            }

            // 킥 채널.
            if (_kickRemaining > 0f)
            {
                _kickRemaining -= Time.unscaledDeltaTime;
                float env = CameraComposeMath.KickEnvelope(_kickRemaining, _kickDuration);
                delta = CameraComposeMath.Add(delta,
                    CameraComposeMath.KickDelta(_kickStrength * env, config.kickPosAmp, config.kickRotAmp));
            }

            ComposeAndWrite(delta);
        }

        private bool IsBreathPhase(Wassup.Core.GamePhase phase)
        {
            var phases = config.breathPhases;
            if (phases == null) return false;
            for (int i = 0; i < phases.Length; i++)
                if (phases[i] == phase) return true;
            return false;
        }

        private bool HasUsableBreathWave()
        {
            int n = Mathf.Min(_breathPhases.Length, config.breathWaves != null ? config.breathWaves.Length : 0);
            for (int i = 0; i < n; i++)
            {
                var w = config.breathWaves[i];
                if (w != null && w.periodSec > 0f
                    && (w.posWeight.x != 0f || w.posWeight.y != 0f || w.pitchWeight != 0f))
                    return true;
            }
            return false;
        }

        private void ComposeAndWrite(in CameraPoseDelta delta)
        {
            CameraComposeMath.Compose(_homePos, _homeRot, _homeFov, delta,
                config.fovMin, config.fovMax,
                out var pos, out var rot, out var fov);
            transform.SetPositionAndRotation(pos, rot);
            _cam.fieldOfView = fov;
        }

        private void OnDisable()
        {
            // 홈 포즈 복귀 보장 (도메인 리로드/비활성 시 잔여 오프셋 방지).
            if (_cam == null) return; // Awake 전 비활성화 — 캡처된 홈 없음
            transform.SetPositionAndRotation(_homePos, _homeRot);
            _cam.fieldOfView = _homeFov;
            // 헤드룸도 함께 턴다 — 재활성 시 스프링이 옛 속도로 튀는 것 방지.
            _headroomWeight = 0f;
            _headroomVel = 0f;
            _headroomFedFrame = -10;
            _overviewWeight = 0f;
            _overviewVel = 0f;
            _overviewFedFrame = -10;
            _kickRemaining = 0f;
            _pulseRemaining = 0f;
            _shakeHeat = 0f;
            _breathWeight = 0f;
            _focusWeight = 0f;
            _focusReleasing = false;
            _focusReleaseElapsed = 0f;
            _focusFedFrame = -10;
            _settled = false;
        }
    }
}
