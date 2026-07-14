using UnityEngine;

namespace Wassup.Presentation
{
    // camera-direction unit 0 — 카메라 base 포즈의 유일한 런타임 쓰기 주체.
    //
    // 매 LateUpdate 에 절대 합성: 최종 포즈 = 홈 포즈 ⊕ 페이즈 비행 ⊕ 구두점 ⊕ 앰비언트 ⊕ 킥.
    // 홈 포즈 = 씬 authored 포즈를 Awake 에서 캡처 — 씬에서 카메라를 직접 튜닝하는 기존
    // 워크플로우가 그대로 유지된다. 이번 유닛은 킥 채널만 실동작(나머지는 후속 유닛).
    //
    // 실행 순서 계약: LateUpdate 에서 카메라를 읽는 소비자(빌보드/데미지넘버/드래그 프리뷰)보다
    // 항상 먼저 최종 포즈를 확정해야 한다 → DefaultExecutionOrder(-100).
    // 구 CameraImpactKick 의 self-cancel 패턴은 매 프레임 절대 쓰기 소유자와 양립 불가
    // (revert 가 이중 차감이 됨)라서 킥을 채널로 흡수하고 해당 컴포넌트는 은퇴.
    [DefaultExecutionOrder(-100)]
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
        private bool _settled; // 전 채널 비활성 상태에서 홈 포즈를 이미 써뒀는가 (아이들 프레임 no-op)

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _homePos = transform.position;
            _homeRot = transform.rotation;
            _homeFov = _cam.fieldOfView;
            if (config == null)
                Debug.LogWarning("[CameraDirector] config 미배선 — 모든 연출 채널 비활성(홈 포즈 고정).", this);
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

            // 킥 채널. flight/punctuation/ambient 채널은 후속 유닛에서 이 지점에 누적되고,
            // 활성 여부도 anyActive 에 OR 된다.
            bool anyActive = _kickRemaining > 0f;

            // 아이들: 홈 포즈를 1회만 쓰고 이후 프레임은 no-op — 매 프레임 transform/FOV
            // 재기입(하이어라키 dirty + 네이티브 세터)을 모바일에서 아낀다. Director 가 유일한
            // 쓰기 주체라 마지막으로 써둔 홈 포즈가 그대로 유지된다(소유 계약과 충돌 없음).
            if (!anyActive)
            {
                if (_settled) return;
                transform.SetPositionAndRotation(_homePos, _homeRot);
                _cam.fieldOfView = _homeFov;
                _settled = true;
                return;
            }
            _settled = false;

            _kickRemaining -= Time.unscaledDeltaTime;
            float env = CameraComposeMath.KickEnvelope(_kickRemaining, _kickDuration);
            var delta = CameraComposeMath.KickDelta(_kickStrength * env, config.kickPosAmp, config.kickRotAmp);

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
