using UnityEngine;

namespace Wassup.Presentation
{
    // camera-direction unit 0 — 카메라 base 포즈의 유일한 런타임 쓰기 주체.
    //
    // 매 LateUpdate 에 절대 합성: 최종 포즈 = **현재 상태 포즈** ⊕ 드래그 포커스 ⊕ 인스펙트
    // ⊕ 헤드룸 ⊕ 오버뷰 ⊕ 구두점 ⊕ 앰비언트 ⊕ 킥.
    //
    // unit 11 — base 는 더 이상 «씬에서 캡처한 홈 포즈» 가 아니다. 카메라 상태(배치/전투)마다
    // 완결된 레시피(대상·각도·거리·화면 세로 위치·화각)가 있고, 포즈는 그 레시피와 실제 판에서
    // **매 프레임 절대값으로 계산**된다. 상태끼리 공유하는 기준점이 없으므로 한 상태를 튜닝해도
    // 다른 상태가 딸려 오지 않는다(구 «홈 + 페이즈 델타» 구조의 결함).
    // 씬의 Main Camera 포즈는 런타임에 읽지 않는다 — 에디터 미리보기 전용이다.
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
        [Header("이 카메라의 씬 포즈(Transform·FOV)는 런타임에 쓰이지 않는다 — 에디터 미리보기 전용.\n포즈는 config 의 상태 레시피에서 매 프레임 계산된다.")]
        [SerializeField] private Wassup.Data.CameraDirectionConfig config;
        // unit 18 — 포스트 볼륨은 **스테이지가 소유한다**(map-diorama-stage 가 씬의 전역 Post 를
        // 스테이지 프리팹 안으로 옮겼다). 런타임 인스턴스라 씬에서 배선할 수 없어서 SerializeField
        // 가 아니라 브리지가 밀어준다 — `SetBoardBounds` 와 같은 단방향 계약이다.
        private UnityEngine.Rendering.Volume postVolume;

        private Camera _cam;

        // unit 11 — 이번 프레임의 base 포즈(상태 레시피 해). 모든 채널 델타의 기준이다.
        private Vector3 _statePos;
        private Quaternion _stateRot = Quaternion.identity;
        private float _stateFov = 60f;
        private bool _hasStatePose;
        // 직전에 확정한 base 포즈. 이번 해와 다르면 아이들 최적화를 풀어야 한다 —
        // 그래야 «화면비 변경 · 판 교체 · Play 중 인스펙터 튜닝» 이 즉시 화면에 반영된다.
        private Vector3 _lastSolvedPos;
        private Quaternion _lastSolvedRot = Quaternion.identity;
        private float _lastSolvedFov;

        // unit 11 — 보드 bounds 는 맵 빌드 때 BattleBridge 가 밀어준다. Director 가 맵이나
        // 브리지에서 당겨오지 않는다(경계 우회의 입구). 없으면 카메라를 건드리지 않는다.
        private Bounds _boardBounds;
        private bool _hasBoardBounds;

        private float _kickRemaining;
        private float _kickDuration;
        private float _kickStrength;
        private bool _settled; // 전 채널 비활성 상태에서 정착 포즈를 이미 써뒀는가 (아이들 프레임 no-op)

        // unit 2 — 구두점 채널 (additive 전용, 카메라 탈취 없음).
        // 줌 펄스: max-hold 재트리거(진폭 max, 누적 없음). 비행 중 가중치 0 페이드.
        // unit 16 — 셰이크는 입력이 둘이다: 한 방(임펄스, 여기서 감쇠)과 지속 레벨(heat —
        // ScoreHudView 가 매 프레임 밀어주는 킬 스트릭, 지연 ≤1프레임 허용 계약).
        // 둘의 합성은 max (ShakeWeight — 더하면 상한을 넘는다).
        private float _pulseRemaining;
        private float _pulseDuration;
        private float _pulseStrength;
        private float _shakeHeat;
        private float _shakeImpulseRemaining;
        private float _shakeImpulseDuration;
        private float _shakeImpulseStrength;
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
        // 손가락이 아니라 **고정 월드 좌표**라, NDC 를 그 프레임의 상태 포즈 기준으로 산출한다
        // (SetInspectFocus). 상태 포즈는 채널 델타가 얹히기 전 값이라 되먹임이 없다.
        // 스프링 없음 — 타겟이 안 움직여 스텝 변화가 없다. 부드러움은 가중치 페이드가 담당.
        private Vector2 _inspectNdc;       // 합성에 쓰는 추종된 현재값
        private Vector2 _inspectNdcTarget; // 피드가 지정한 목표(선택 유닛)
        private bool _inspectNdcInit;      // 이번 세션에서 목표에 한 번이라도 스냅했나
        private bool _inspectHasNdc;
        private int _inspectFedFrame = -10;
        private float _inspectWeight;
        private float _inspectReleaseFrom;
        private float _inspectReleaseElapsed;
        private bool _inspectReleasing;
        // hand-drag-tooltip unit 6 — 손패 헤드룸. 피드 주도 채널(포커스/인스펙트와 같은
        // staleness 규약)이라 손패 뷰가 죽거나 비활성돼도 자동으로 상태 pitch 로 복귀한다.
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

        // unit 11 — 상태 전환. 포즈를 얼려서 섞지 않는다: 매 프레임 양쪽 상태의 레시피를 각각
        // 풀고 그 «결과» 를 섞는다. 그래야 전환 도중에 판 크기나 화면비가 바뀌어도 따라온다.
        private Wassup.Data.CameraState _state;
        private Wassup.Data.CameraState _fromState;
        private bool _stateInit;
        private float _transitionElapsed;
        private float _transitionDuration; // 0 = 전환 없음
        private AnimationCurve _transitionEase;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            // unit 11 — 씬 포즈를 캡처하지 않는다. base 는 상태 레시피에서 계산된다.
            if (config == null)
                Debug.LogWarning("[CameraDirector] config 미배선 — 카메라를 건드리지 않는다.", this);
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
        }

        private void OnDestroy()
        {
            if (_gm != null) _gm.PhaseChanged -= OnPhaseChanged;
        }

        // unit 11 — 페이즈는 «기록» 만 한다. 어느 카메라 상태인지는 LateUpdate 가 매 프레임
        // 해석한다 — 이벤트 시점에 결정하면 «직전에 뭐였는지» 에 결과가 의존해 재현이 어렵다.
        private void OnPhaseChanged(Wassup.Core.GamePhase phase)
        {
            _currentPhase = phase;
        }

        // unit 11 — 페이즈 7종을 카메라 상태 2종으로 접는다.
        // 기믹 리빌은 배치 직전 준비 구간이라 배치와 같은 그림으로 본다.
        // 집계·결과·드래프트·None 은 전투로 흡수된다 — 집계는 판을 계속 보여주는 구간이고
        // 결과는 전면 UI 라 별도 상태가 필요 없다. 구 «미등록 페이즈 = hold» 는 은퇴했다.
        private static Wassup.Data.CameraState ResolveState(Wassup.Core.GamePhase phase)
            => (phase == Wassup.Core.GamePhase.Placement || phase == Wassup.Core.GamePhase.Gimmick)
                ? Wassup.Data.CameraState.Placement
                : Wassup.Data.CameraState.Battle;

        // unit 11 — 이번 프레임의 base 포즈를 확정한다. 이 함수가 카메라 «어디를 어떻게 보나» 의
        // 유일한 결정자다. false = 결정할 수 없음 → 호출부가 카메라를 건드리지 않는다.
        //
        // 전환 중에는 **양쪽 상태의 레시피를 각각 풀고 그 결과를 섞는다**. 포즈를 얼려 두면
        // 전환 도중에 판이 바뀌거나 화면비가 바뀌어도 따라오지 못한다.
        private bool UpdateStatePose()
        {
            // 판이 아직 안 왔다(맵 빌드 전 프레임 · GameManager 없는 씬 · 테스트 리그).
            // 예전엔 씬에서 캡처한 홈이 이 구간의 폴백이었다 — 이제 그냥 손대지 않는다.
            if (!_hasBoardBounds) return false;

            var resolved = ResolveState(_currentPhase);
            if (!_stateInit)
            {
                // 판 시작 시 첫 상태는 스냅(구 _phaseAppliedOnce 계약 계승).
                _stateInit = true;
                _state = resolved;
                _fromState = resolved;
                _transitionDuration = 0f;
                _transitionElapsed = 0f;
                _settled = false;
            }
            else if (resolved != _state)
            {
                var arrive = FindFraming(resolved);
                bool wasMidFlight = _transitionDuration > 0f;
                float linear01 = wasMidFlight
                    ? Mathf.Clamp01(_transitionElapsed / _transitionDuration)
                    : 1f;
                _fromState = _state;
                _state = resolved;
                _transitionDuration = (arrive != null && arrive.flightSec > 0f) ? arrive.flightSec : 0f;
                _transitionEase = arrive != null ? arrive.ease : null;
                // 전환 중 재전환 — 스냅 금지. 되돌아가는 것이므로 진행도를 거울로 뒤집어 잇는다.
                // 대칭 이징에서는 정확히 연속이고, 비대칭 커브에서는 미세한 꺾임이 남는다.
                _transitionElapsed = wasMidFlight ? _transitionDuration * (1f - linear01) : 0f;
                _settled = false;
            }

            bool transitioning = _transitionDuration > 0f;
            if (transitioning)
            {
                _transitionElapsed += Time.unscaledDeltaTime;
                if (_transitionElapsed >= _transitionDuration)
                {
                    _transitionDuration = 0f;
                    _transitionElapsed = 0f;
                    transitioning = false;
                }
            }

            // 원값을 그대로 넘긴다. 여기서 클램프하면 FrustumTangents 의 «aspect<0.01 → 16:9»
            // 폴백이 절대 안 걸리고, 게임뷰 0 폭 같은 상황에서 fit 거리가 100 배로 튄다.
            float aspect = _cam.aspect;

            var toFraming = FindFraming(_state);
            if (!CameraFramingMath.SolveStatePose(_boardBounds.center, toFraming, _boardBounds, aspect,
                    out var toPos, out var toRot, out float toFov, _poseCornerBuf))
            {
                WarnMissingFramingOnce(_state);
                return false; // 레시피가 없는 상태로는 전환하지 않는다 — 현재 포즈 유지
            }

            if (transitioning)
            {
                var fromFraming = FindFraming(_fromState);
                if (CameraFramingMath.SolveStatePose(_boardBounds.center, fromFraming, _boardBounds, aspect,
                        out var fromPos, out var fromRot, out float fromFov, _poseCornerBuf))
                {
                    float t01 = Mathf.Clamp01(_transitionElapsed / Mathf.Max(1e-4f, _transitionDuration));
                    float eased = (_transitionEase != null && _transitionEase.length >= 2)
                        ? _transitionEase.Evaluate(t01)
                        : Mathf.SmoothStep(0f, 1f, t01);
                    CommitStatePose(Vector3.LerpUnclamped(fromPos, toPos, eased),
                                    Quaternion.SlerpUnclamped(fromRot, toRot, eased),
                                    Mathf.LerpUnclamped(fromFov, toFov, eased));
                    return true;
                }
                // 출발 상태 레시피가 없다 — 섞을 것이 없으니 전환을 접고 도착 포즈로 간다.
                _transitionDuration = 0f;
                _transitionElapsed = 0f;
            }

            CommitStatePose(toPos, toRot, toFov);
            return true;
        }

        // base 포즈 확정. 직전 해와 다르면 아이들 최적화를 푼다 — 이 한 줄이 화면비 변경 ·
        // 판 교체 · Play 중 인스펙터 튜닝을 전부 커버한다(각각을 따로 감지할 필요가 없다).
        private void CommitStatePose(Vector3 pos, Quaternion rot, float fov)
        {
            if (!_hasStatePose
                || (pos - _lastSolvedPos).sqrMagnitude > 1e-10f
                || Quaternion.Angle(rot, _lastSolvedRot) > 1e-4f
                || !Mathf.Approximately(fov, _lastSolvedFov))
                _settled = false;

            _statePos = _lastSolvedPos = pos;
            _stateRot = _lastSolvedRot = rot;
            _stateFov = _lastSolvedFov = fov;
            _hasStatePose = true;
        }

        // 레시피가 없으면 카메라가 «조용히» 아무것도 안 한다 — 원인 추적이 불가능하다.
        // 상태마다 한 번만 경고한다(매 프레임 도는 경로라 스팸이 되면 안 된다).
        private bool _warnedPlacementFraming;
        private bool _warnedBattleFraming;

        private void WarnMissingFramingOnce(Wassup.Data.CameraState state)
        {
            bool warned = state == Wassup.Data.CameraState.Placement
                ? _warnedPlacementFraming : _warnedBattleFraming;
            if (warned) return;
            if (state == Wassup.Data.CameraState.Placement) _warnedPlacementFraming = true;
            else _warnedBattleFraming = true;
            Debug.LogWarning($"[CameraDirector] {state} 상태 레시피가 config 에 없다 — 그 상태로는 "
                + "카메라가 움직이지 않는다(현재 포즈 유지). CameraDirectionConfig.stateFramings 확인.", this);
        }

        private Wassup.Data.CameraStateFraming FindFraming(Wassup.Data.CameraState state)
        {
            var list = config.stateFramings;
            if (list == null) return null;
            for (int i = 0; i < list.Length; i++)
                if (list[i] != null && list[i].state == state) return list[i];
            return null;
        }

        // unit 11 — 보드 bounds 입력. 맵 빌드 직후 BattleBridge 가 한 번 밀어준다.
        //
        // 구 FrameBoard 는 fit 계산 + 홈 쓰기 + DoF 구동 + aspect 기억 네 가지를 겸했다.
        // 이제 포즈는 매 프레임 상태 레시피에서 계산되므로 여기는 **입력 저장 하나**만 한다.
        // Director 가 맵이나 브리지에서 bounds 를 당겨오지 않는 것이 계약이다 — 그 유혹이
        // 경계 우회의 입구다. 입력은 브리지가 미는 한 방향뿐이다.
        public void SetBoardBounds(Bounds boardWorld)
        {
            _boardBounds = boardWorld;
            _hasBoardBounds = true;
            // 전 채널이 비활성이면 LateUpdate 가 포즈를 다시 쓰지 않는다(_settled 아이들 no-op).
            // 판이 바뀌었으니 한 번은 반드시 다시 풀어야 한다.
            _settled = false;
        }

        // unit 18 — 스테이지의 포스트 볼륨 입력. `SetBoardBounds` 와 같은 단방향 push 다
        // (Director 가 맵·브리지에서 당겨오지 않는다). 스테이지가 파괴될 때 브리지가 null 을 민다.
        //
        // ⚠ 캐시를 리셋하는 것이 계약이다. `Volume.profile` 은 sharedProfile 의 **런타임 인스턴스**라
        // 스테이지가 바뀌면 다른 객체가 온다 — 안 비우면 새 프로파일의 첫 write 가 "직전과 같은 값"
        // 으로 판정돼 건너뛰어지고, 맵 교체 후 비네트가 그 판 내내 죽는다.
        public void SetPostVolume(UnityEngine.Rendering.Volume volume)
        {
            if (ReferenceEquals(postVolume, volume)) return;
            postVolume = volume;
            _vignetteWritten = -1f;
        }

        // ── heart-stress-axis unit 8 rev 2 — 마음 스트레스 비네트 ──────────────────
        //
        // **UI 오버레이가 아니라 포스트 비네트를 쓴다.** UI 로 하려면 풀스크린 비네트
        // 스프라이트의 밝은 띠 반경이 스프라이트에 박혀 있어 「화면 테두리 한참 안쪽에서
        // 연출이 나온다」가 됐고(rev 1 실측), 4변 프레임으로 우회해도 «화면이 물든다» 가
        // 아니라 «UI 액자가 생긴다» 로 읽힌다. 포스트 비네트는 **테두리 정렬이 구조적으로
        // 보장**되고, 스테이지 프로파일이 이미 포스트 스택을 돌리고 있어(Bloom·Tonemapping·
        // ColorAdjustments) 추가 패스도 없다.
        //
        // 타이머 마지막 10초 연출과 **기제 자체가 갈린다**: 그쪽은 UI 오버레이 원샷 플래시,
        // 이쪽은 포스트 비네트 지속. 같은 붉은 계열이어도 서로를 안 먹는다.
        //
        // ⚠⚠ **`active` 를 켜야 한다.** 씬 프로파일의 Vignette 오버라이드는 `active: 0` 으로
        // 저작돼 있었고, 그 상태에서는 파라미터를 아무리 `Override()` 해도 **컴포넌트 자체가
        // 통째로 건너뛰어져** 화면에 아무 일도 안 일어난다(실측 2026-08-24 — 「연출이 하나도
        // 안 나온다」의 진짜 원인이 이것이었다). `intensity` 만 쓰고 끝내면 영원히 안 보인다.
        //
        // 에셋을 고치지 않고 **코드가 켠다**: `Volume.profile` 은 sharedProfile 의 **런타임
        // 인스턴스**를 돌려주므로 프로파일 에셋은 건드려지지 않고(에디터 Play 에서도 디스크에
        // 남지 않는다), git 에서 dirty 해지지도 않는다. 저작 의도(평시 꺼짐)도 그대로 보존된다.
        //
        // 끄는 것은 `active=false` 다 — `IsActive()` 가 `intensity > 0` 도 보지만, 컴포넌트를
        // 통째로 빼는 쪽이 확실하고 평온 구간 비용이 정확히 0 이 된다.
        private float _vignetteWritten = -1f;

        /// <summary>마음 스트레스 비네트. intensity 0 = 완전히 끔(비용 0).</summary>
        public void SetStressVignette(float intensity, Color color, float smoothness)
        {
            if (postVolume == null) return;
            var profile = postVolume.profile;
            if (profile == null) return;
            if (!profile.TryGet(out UnityEngine.Rendering.Universal.Vignette v)) return;

            intensity = Mathf.Clamp01(intensity);
            bool on = intensity > 0.001f;
            if (v.active != on) v.active = on;
            if (!on)
            {
                if (_vignetteWritten > 0f) { v.intensity.Override(0f); _vignetteWritten = 0f; }
                return;
            }

            _vignetteWritten = intensity;
            v.intensity.Override(intensity);
            v.color.Override(color);
            v.smoothness.Override(Mathf.Clamp(smoothness, 0.01f, 1f));
        }


        // 코너 버퍼 — 미리 잡아두면 LocalCorners 가 제자리에서 채워 매 프레임 할당이 없다.
        private readonly Vector3[] _poseCornerBuf = new Vector3[8];

        // 임팩트 킥 (구 CameraImpactKick.Kick 승계 — 카드 흡수 임팩트 · 부착 거절).
        // config 배선 전 호출은 안전 no-op (spec unit 0 계약).
        //
        // unit 16 — 구 `FeedbackKick(strength, duration)` 을 여기로 합쳤다. 둘의 차이는
        // (a) `enableNonDragEffects` 게이팅과 (b) duration 출처였는데, (a) 가 은퇴하면서 남은
        // 것은 (b) 뿐이다. "얼마나 짧은가" 는 그 피드백의 성격이지 카메라의 성질이 아니라
        // 호출처가 줄 수 있어야 하고, 진폭은 config 소유라 킥의 물리적 느낌은 한 곳에서 튜닝된다.
        //
        // ⚠ duration 을 **명시하면 그 값이 전부다** — 0 이하는 「이 피드백은 킥 없음」이고
        // config 기본으로 대체되지 않는다. 인자를 아예 안 주는 것(sentinel `-1`)만 config 기본을
        // 쓴다. 둘을 겸직시키면 `rejectKickDuration` 을 0 으로 두어 거절 킥만 끄려던 저작이
        // 대신 더 긴 카드흡수용 킥을 부른다(selection-hand-attach unit 14 의 명문 계약).
        public void Kick(float strength = 1f, float duration = -1f)
        {
            if (config == null) return;
            float dur = duration < 0f ? config.kickDuration : duration;
            if (dur <= 0f) return;
            _kickStrength = Mathf.Clamp01(strength);
            _kickDuration = dur;
            _kickRemaining = dur;
        }

        // unit 2 — 헤비 임팩트 줌 펄스. 연타 시 envelope 누적 없이 max 유지(과누적 방지):
        // 진폭은 현재 유효값과 새 값의 max, 타이머는 재시작. pulseSec 0 = 펄스 끔.
        public void ZoomPulse(float strength = 1f)
        {
            if (config == null) return;
            float current = _pulseRemaining > 0f ? _pulseStrength : 0f;
            _pulseStrength = Mathf.Max(current, Mathf.Clamp01(strength));
            _pulseDuration = config.pulseSec;
            _pulseRemaining = _pulseDuration;
        }

        // unit 16 — 셰이크 한 방. **어느 호출처든 부를 수 있는 독립 채널**이다: 카메라는 무슨
        // 사건인지 알 필요가 없고 세기와 길이만 받는다. 재발동 규칙은
        // `CameraComposeMath.ShouldReplaceShakeImpulse` 소유 — 줌 펄스의 max-hold 를 복사하면
        // 안 되는 이유가 거기 적혀 있다.
        //
        // duration 에 기본값을 두지 않는 것이 계약이다 — 호출처가 SerializeField 로 저작하게
        // 강제해 코드에 시간 리터럴이 박히는 것을 막는다(제약 6). 진폭·주파수는 config 소유.
        public void Shake(float strength, float duration)
        {
            if (config == null || duration <= 0f) return;
            // 진폭 0 = 이 채널이 꺼진 것이다(unit 16 계약). 타이머도 걸지 않는다 — 걸어두면
            // 구두점 활성 판정이 이 채널을 세지 않아 타이머가 굶고(아이들 프레임은 감쇠 코드에
            // 도달하지 않는다), 나중에 진폭을 되살리는 순간 감쇠되지 않은 stale 임펄스가
            // envelope 1 로 그대로 터진다.
            if (config.shakeMaxPosAmp == 0f && config.shakeMaxRotAmp == 0f) return;
            if (!CameraComposeMath.ShouldReplaceShakeImpulse(
                    _shakeImpulseStrength, _shakeImpulseRemaining, _shakeImpulseDuration, strength))
                return;
            _shakeImpulseStrength = Mathf.Clamp01(strength);
            _shakeImpulseDuration = duration;
            _shakeImpulseRemaining = duration;
        }

        // unit 2 — 킬 스트릭 heat(0~1) = "계속 흔들리는 상태". 소유는 ScoreHudView(산정·감쇠) —
        // 여기는 미러만. 한 방(Shake)과 겹치면 max 로 합성된다(ShakeWeight).
        public void SetShakeHeat(float heat01)
        {
            if (config == null) return;
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
        // SetDragFocus 와 달리 월드 좌표를 받는 게 계약이다. 되먹임은 여기서 NDC 를 **그 프레임의
        // 상태 포즈 기준**으로 뽑아 차단한다 — 라이브 카메라 포즈로 뽑으면 카메라가 다가갈수록
        // NDC 가 0 으로 줄어 오프셋이 사라지고 다시 벌어지는 진동이 된다. 상태 포즈는 채널 델타가
        // 얹히기 전 값이라(이 채널의 기여를 포함하지 않아) 그 루프가 성립하지 않는다.
        // (FocusDelta 의 dirLocal = (ndc.x·tanH, ndc.y·tanV, 1) 복원식의 정확한 역변환.)
        public void SetInspectFocus(Vector3 worldPos)
        {
            if (config == null || _cam == null || !_hasStatePose) return;
            // unit 11 — 기준이 «홈 포즈» 에서 «그 프레임의 상태 포즈» 로 바뀌었다. 상태 포즈는
            // 채널 델타가 얹히기 **전** 값이라 여전히 되먹임이 없다(현재 카메라 포즈가 아니다).
            var local = Quaternion.Inverse(_stateRot) * (worldPos - _statePos);
            if (local.z <= 0.001f) return; // 카메라 뒤/평면 — 이번 프레임 피드 스킵(= 자연 해제)
            float tanV = Mathf.Tan(_stateFov * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Mathf.Max(0.01f, _cam.aspect);
            // selection-hand-attach unit 13 — **목표만** 갱신한다. 예전엔 여기서 _inspectNdc 를
            // 직접 대입해, 선택이 A→B 로 바뀌면 그 프레임에 프레이밍이 통째로 점프했다.
            // (가중치는 페이드가 걸려 있어 "재줌"은 원래 없었다 — 튀는 것은 NDC 스냅이었다.)
            // 실제 추종/스냅 판정은 Compose 에서 한다.
            // unit 13 rev2 — y 에서 bias 를 빼면 카메라가 유닛보다 **살짝 아래**를 겨냥하고,
            // 그 결과 유닛은 프레임 **위쪽**에 놓인다. 선택 중에는 손패가 항상 열려 하단
            // 대역을 덮으므로(계약 1), 하단에 배치된 유닛을 그 밑에서 꺼내려면 이게 필요하다.
            // pitch 로 같은 일을 하려 하면 보드 전체가 내려가 문제가 되레 악화된다(rev1 실패).
            _inspectNdcTarget = new Vector2(
                local.x / (local.z * Mathf.Max(1e-4f, tanH)),
                local.y / (local.z * Mathf.Max(1e-4f, tanV)) - config.inspectFrameBiasY);
            _inspectHasNdc = true;
            _inspectFedFrame = Time.frameCount;
        }

        // hand-drag-tooltip unit 6 — 손패 헤드룸 피드: 손패가 열려 있는 동안 매 프레임 호출.
        // 피드가 끊기면(2프레임 초과) 자동 해제되어 상태 pitch 로 복귀한다 — 손패 닫힘/페이즈
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
        // 루프 없음). 피드가 끊기면(2프레임 초과) 자동으로 상태 포즈로 복귀한다.
        public void SetMoveOverview()
        {
            if (config == null) return;
            _overviewFedFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            if (config == null || _cam == null) return;

            // unit 11 — base 포즈를 먼저 확정한다. 실패하면(레시피 없음 / 판 미도달) 카메라를
            // **건드리지 않는다** — 직전 포즈가 그대로 남는다.
            if (!UpdateStatePose()) return;
            bool flying = _transitionDuration > 0f; // 완료 프레임에 UpdateStatePose 가 0 으로 내린다

            // 채널 활성 판정.
            // unit 16 — 채널을 한꺼번에 잠그던 `enableNonDragEffects` 는 은퇴했다. 그 이름은
            // «무엇을 끄는가» 가 아니라 «무엇이 살아남았는가»(2026-07-14 드래그 포커스만 남긴
            // 판단) 기준이라, 이후 채널 다섯이 전부 예외로 빠져나가며 축이 썩었다. 이제 스위치는
            // 채널마다 자기 데이터가 갖는다 — 셰이크는 진폭, 킥은 duration, 펄스는 pulseSec.
            bool shakeConfigured = config.shakeMaxPosAmp != 0f || config.shakeMaxRotAmp != 0f;
            bool punctInput = _pulseRemaining > 0f
                || (shakeConfigured && (_shakeImpulseRemaining > 0f || _shakeHeat > 0.0001f));
            // 비행 중 구두점 가중치 0 페이드 (비행이 최우선). 페이드 진행 자체도 활성으로 취급.
            float punctTarget = flying ? 0f : 1f;
            bool punctFading = !Mathf.Approximately(_punctWeight, punctTarget);
            bool punctActive = punctInput && (_punctWeight > 0f || punctFading);
            // 브리딩: 켜진 페이즈 + 비행 아님 + 유효 파동 존재 → 목표 1. 가중치가 0 에 닿을
            // 때까지는 활성(크로스페이드). 유효 파동 검사로 퇴화 config(전 파동 주기 0 등)가
            // 모션 0 인 채 settle 최적화를 영구 무효화하는 것 방지(리뷰 반영).
            bool breathOn = IsBreathPhase(_currentPhase)
                && (config.breathPosAmp != 0f || config.breathRotAmp != 0f) // 음수 진폭 = 위상 반전(유효)
                && HasUsableBreathWave();
            float breathTarget = (breathOn && !flying) ? 1f : 0f;
            bool breathActive = breathTarget > 0f || _breathWeight > 0f;
            // 드래그 포커스: 최근 2프레임 내 피드 + 비행 아님 → 목표 1. 페이드 잔여도 활성.
            bool focusConfigured = config.focusDolly != 0f || config.focusLookWeight > 0f
                || config.focusFovDelta != 0f || config.placementFocusLead != 0f;
            bool focusFed = Time.frameCount - _focusFedFrame <= 2 && focusConfigured;
            float focusTarget = (focusFed && !flying) ? 1f : 0f;
            bool focusActive = focusTarget > 0f || _focusWeight > 0f;
            // 인스펙트 포커스 — 드래그 포커스와 같은 staleness 규약.
            bool inspectConfigured = config.inspectDolly != 0f || config.inspectFovDelta != 0f
                || config.inspectLookWeight > 0f;
            bool inspectFed = _inspectHasNdc && Time.frameCount - _inspectFedFrame <= 2 && inspectConfigured;
            float inspectTarget = (inspectFed && !flying) ? 1f : 0f;
            bool inspectActive = inspectTarget > 0f || _inspectWeight > 0f;
            // 손패 헤드룸 — 인스펙트와 같은 staleness 규약. **상태 전환 중에도 유지한다** —
            // 손패가 열려 있으면 헤드룸은 계속 필요하다(포커스/인스펙트와 달리 전환에 양보하지 않는다).
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
            // headroomActive 도 같다 — 빠뜨리면 손패를 열어도 pitch 가 즉시 상태 포즈로 덮인다.
            bool anyActive = _kickRemaining > 0f || flying || punctActive || breathActive
                || focusActive || inspectActive || headroomActive || overviewActive;

            // 아이들: 정착 포즈(현재 상태 포즈)를 1회만 쓰고 이후 프레임은 no-op —
            // 매 프레임 transform/FOV 재기입(하이어라키 dirty + 네이티브 세터)을 모바일에서
            // 아낀다. Director 가 유일한 쓰기 주체라 써둔 포즈가 그대로 유지된다.
            if (!anyActive)
            {
                // 아이들 진입 시 구두점 가중치는 목표값으로 스냅 — 비행 직후 0에 얼어붙어
                // 다음 펄스가 램프인과 겹쳐 약해지는 문제 방지(리뷰 반영). 입력 없으니 비가시.
                _punctWeight = punctTarget;
                if (_settled) return;
                ComposeAndWrite(CameraPoseDelta.Identity);
                _settled = true;
                return;
            }
            _settled = false;

            // 전환은 base 포즈(UpdateStatePose)가 이미 섞어 놨다 — 여기부터는 그 위에 얹는 델타뿐.
            var delta = CameraPoseDelta.Identity;

            // 드래그 포커스 채널 — 유닛 방향 dolly + 부분 lookat + 스와이프 리드.
            // base 위치 = 현재 상태 포즈 (회전도 상태 기준 — FocusDelta 주석 참조).
            if (focusTarget > 0f)
            {
                _focusReleasing = false;
                _focusWeight = Mathf.MoveTowards(_focusWeight, 1f,
                    Time.unscaledDeltaTime / Mathf.Max(0.01f, config.focusFadeInSec));
            }
            else if (_focusWeight > 0f)
            {
                // 복귀는 선형 MoveTowards 대신 초반이 빠른 cubic ease-out. 드래그 해제 직후
                // 즉시 반응하되, 상태 포즈에는 부드럽게 착지한다.
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

                // unit 12 — 배치 상태는 같은 채널을 «화면 밀기» 로 해석한다. 새 채널을 만들지
                // 않는 이유: 스프링·staleness·페이드가 이미 여기 있고 튜닝도 끝나 있다.
                // 전투는 기존 해석(전진 dolly + 부분 lookat + 스와이프 리드) 그대로 — 전투 중에도
                // 손패에서 카드를 끌어 배치하고, 그때 상태 대상은 보드 중앙 고정이다.
                if (_state == Wassup.Data.CameraState.Placement)
                {
                    float boardDepth = Vector3.Dot(_boardBounds.center - _statePos,
                                                   _stateRot * Vector3.forward);
                    delta = CameraComposeMath.Add(delta, CameraComposeMath.PanDelta(
                        _focusNdc, _stateFov, _cam.aspect, _focusWeight,
                        config.placementFocusLead, boardDepth));
                }
                else
                {
                    delta = CameraComposeMath.Add(delta, CameraComposeMath.FocusDelta(
                        _focusNdc, _focusNdcVel, _stateFov, _cam.aspect, _focusWeight,
                        config.focusDolly, config.focusFovDelta, config.focusLookWeight,
                        config.focusLeanPerSpeed, config.focusLeanMaxDeg));
                }
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
                // unit 13 — 목표 추종. **가중치가 0 에서 올라오는 첫 프레임(=새 선택 시작)은
                // 스냅**한다. 안 그러면 직전 유닛 위치에서 화면을 가로질러 날아온다
                // (선택 리티클이 "날아오지 않고 pop" 인 것과 같은 이유).
                // 추종이 도는 것은 가중치가 이미 살아 있는 **전환**뿐이다 — 그때 카메라가
                // 유닛 사이를 미끄러진다. 오버슈트가 없어야 해서 스프링이 아니라 지수 감쇠다.
                if (!_inspectNdcInit)
                {
                    _inspectNdc = _inspectNdcTarget;
                    _inspectNdcInit = true;
                }
                else if (config.inspectFollowRate > 0f)
                {
                    float k = 1f - Mathf.Exp(-config.inspectFollowRate * Time.unscaledDeltaTime);
                    _inspectNdc = Vector2.Lerp(_inspectNdc, _inspectNdcTarget, k);
                }
                else _inspectNdc = _inspectNdcTarget; // 0 이하 = 구 동작(즉시 스냅)

                delta = CameraComposeMath.Add(delta, CameraComposeMath.FocusDelta(
                    _inspectNdc, Vector2.zero, _stateFov, _cam.aspect, _inspectWeight,
                    config.inspectDolly, config.inspectFovDelta, config.inspectLookWeight,
                    0f, 0f));

                // unit 13 rev — 연출 pitch. FocusDelta 가 내는 pitch 는 lookat 파생(유닛을
                // 바라보느라 생기는 각도)이라 "낮춰서 올려다보는" 부각이 안 나온다. 손패
                // 헤드룸(handHeadroomPitchDeg)과 같은 형태로 가중치에 비례해 얹는다.
                if (config.inspectPitchDeg != 0f)
                    delta = CameraComposeMath.Add(delta, new CameraPoseDelta
                    {
                        pitchDeg = config.inspectPitchDeg * _inspectWeight,
                    });
            }
            else
            {
                _inspectHasNdc = false; // 다음 선택은 새 타겟에서 시작(스테일 NDC 방지)
                _inspectNdcInit = false; // 다음 획득은 스냅으로 — 가로질러 날아오지 않게
            }

            // 손패 헤드룸 채널 — 가중치를 0↔1 로 스프링 추종시켜 pitch + dolly 에 곱한다.
            // pitch 는 보드를 아래로 옮기고 dolly 는 줄인다(상단 여백 합산).
            // 진입/복귀가 같은 스프링이라 여는 맛과 닫는 맛이 대칭이다.
            if (headroomActive)
            {
                Wassup.UI.KeyringSim.SpringStep(ref _headroomWeight, ref _headroomVel,
                    headroomTarget, config.handHeadroomSpring, config.handHeadroomDamping, 0f,
                    Mathf.Max(Time.unscaledDeltaTime, 1e-4f));
                // localPos 는 상태 회전 기준(+Z = 카메라 전방)이라 음수 z = 후퇴 = 줌아웃.
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

            // unit 16 — 셰이크 임펄스 타이머도 가중치와 무관하게 실시간 감쇠(펄스와 같은 이유:
            // 비행 중 얼렸다가 비행 후 뒤늦게 재생되는 "지연 셰이크" 방지). 억제는 가중치가 담당.
            if (_shakeImpulseRemaining > 0f)
                _shakeImpulseRemaining = Mathf.Max(0f, _shakeImpulseRemaining - Time.unscaledDeltaTime);
            float shakeWeight = CameraComposeMath.ShakeWeight(
                _shakeImpulseStrength, _shakeImpulseRemaining, _shakeImpulseDuration, _shakeHeat);

            if (_punctWeight > 0f)
            {
                if (pulseEnv > 0f)
                {
                    // camera-fov-to-dolly — 줌 펄스는 전진(z)으로 낸다. FOV 를 흔들면 원근이
                    // 함께 변해 기울어진 보드에서 왜곡이 도드라진다. pulseFovDelta 는 은퇴
                    // (기본 0)지만 경로는 남겨둔다 — 둘을 섞어 쓰고 싶을 때를 위해.
                    float amp = _pulseStrength * pulseEnv * _punctWeight;
                    delta = CameraComposeMath.Add(delta, new CameraPoseDelta
                    {
                        localPos = new Vector3(0f, 0f, config.pulseDolly * amp),
                        fovDelta = config.pulseFovDelta * amp,
                    });
                }
                if (shakeWeight > 0.0001f)
                {
                    float dt = Time.unscaledDeltaTime;
                    _shakePhaseX = Mathf.Repeat(_shakePhaseX + config.shakeFreqX * dt, 1f);
                    _shakePhaseY = Mathf.Repeat(_shakePhaseY + config.shakeFreqY * dt, 1f);
                    delta = CameraComposeMath.Add(delta, CameraComposeMath.ShakeDelta(
                        _shakePhaseX, _shakePhaseY, shakeWeight * _punctWeight,
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
            CameraComposeMath.Compose(_statePos, _stateRot, _stateFov, delta,
                config.fovMin, config.fovMax,
                out var pos, out var rot, out var fov);
            transform.SetPositionAndRotation(pos, rot);
            _cam.fieldOfView = fov;
        }

        // 비활성 동안 다른 무언가가 카메라를 옮겼을 수 있다. 재활성 프레임에 한 번은 반드시 쓴다.
        private void OnEnable()
        {
            _settled = false;
        }

        private void OnDisable()
        {
            // unit 11 — **아무것도 복원하지 않는다.** 복원할 «홈» 이 없고, 재활성 프레임에
            // OnEnable 이 _settled 를 풀어 상태 포즈를 다시 쓴다. 예전엔 씬 캡처 포즈로 되돌렸다.
            // 헤드룸은 함께 턴다 — 재활성 시 스프링이 옛 속도로 튀는 것 방지.
            _headroomWeight = 0f;
            _headroomVel = 0f;
            _headroomFedFrame = -10;
            _overviewWeight = 0f;
            _overviewVel = 0f;
            _overviewFedFrame = -10;
            _kickRemaining = 0f;
            _pulseRemaining = 0f;
            _shakeHeat = 0f;
            _shakeImpulseRemaining = 0f;
            _shakeImpulseStrength = 0f;
            _breathWeight = 0f;
            _focusWeight = 0f;
            _focusReleasing = false;
            _focusReleaseElapsed = 0f;
            _focusFedFrame = -10;
            _settled = false;
        }
    }
}
