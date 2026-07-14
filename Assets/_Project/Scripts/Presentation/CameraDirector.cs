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
            if (config == null) return;
            _kickStrength = Mathf.Clamp01(strength);
            _kickDuration = config.kickDuration;
            _kickRemaining = _kickDuration;
        }

        private void LateUpdate()
        {
            if (config == null) return;

            // 채널 활성 판정. punctuation/ambient 채널은 후속 유닛에서 이 지점에 OR 된다.
            bool flying = _flightDuration > 0f && _flightElapsed < _flightDuration;
            bool anyActive = _kickRemaining > 0f || flying;

            // 아이들: 정착 포즈(홈⊕현재 페이즈 델타)를 1회만 쓰고 이후 프레임은 no-op —
            // 매 프레임 transform/FOV 재기입(하이어라키 dirty + 네이티브 세터)을 모바일에서
            // 아낀다. Director 가 유일한 쓰기 주체라 써둔 포즈가 그대로 유지된다.
            if (!anyActive)
            {
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

        private void ComposeAndWrite(in CameraPoseDelta delta)
        {
            CameraComposeMath.Compose(_homePos, _homeRot, _homeFov, delta,
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
            _kickRemaining = 0f;
            _settled = false;
        }
    }
}
