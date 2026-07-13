using UnityEngine;

namespace Wassup.Presentation
{
    // card-fly-to-target-absorb unit 1 — 카메라 미세 임팩트 킥.
    //
    // 타일맵 배틀 카메라는 BattleBridge.ApplyTilemapCameraPreset 이 리빌드/페이즈 전환
    // 시에만 transform 을 **절대값**으로 세팅한다(매프레임 아님, pitch 페이즈별 재계산).
    // 그 rig 와 싸우지 않기 위해, 이 컴포넌트는 LateUpdate 에서 **직전 프레임에 얹은
    // 오프셋을 먼저 되돌린 뒤**(self-cancel) rig 가 이번 프레임 정한 base 위에 새 오프셋을
    // additive 로 얹는다. rig 가 언제 base 를 갱신하든 정확히 base+kick 이 되고, 킥이 끝나면
    // 완전히 원위치로 복귀한다. 짧은 소량 킥(전역 셰이크 서비스 아님 — 그건 후속 후보).
    public class CameraImpactKick : MonoBehaviour
    {
        [SerializeField] private float positionAmp = 0.08f; // 월드 유닛(카메라 로컬 축)
        [SerializeField] private float rotationAmp = 0.35f; // 도(pitch/roll 소량)
        [SerializeField] private float duration = 0.16f;

        private float _t;          // 남은 킥 시간
        private float _dur;        // 이번 킥 총 시간
        private float _strength;   // 0~1
        private Vector3 _dir;      // 킥 방향(카메라 로컬)
        private float _roll;       // roll 부호

        private Vector3 _appliedPos;       // 직전 프레임에 얹은 위치 오프셋(월드)
        private Quaternion _appliedRot = Quaternion.identity; // 직전 프레임 회전 오프셋

        public void Kick(float strength = 1f)
        {
            _strength = Mathf.Clamp01(strength);
            _dur = Mathf.Max(0.01f, duration);
            _t = _dur;
            // 아래로 살짝 내리꽂는 킥(흡수 임팩트) + 미세 roll. index 없는 단발이라
            // 방향은 고정 소량(결정론) — 랜덤 셰이크 아님.
            _dir = new Vector3(0f, -1f, 0f);
            _roll = 1f;
        }

        private void LateUpdate()
        {
            // 1) 직전 프레임 오프셋 되돌리기 — rig 가 정한 base 로 복원.
            transform.rotation = Quaternion.Inverse(_appliedRot) * transform.rotation;
            transform.position -= _appliedPos;
            _appliedPos = Vector3.zero;
            _appliedRot = Quaternion.identity;

            if (_t <= 0f) return;

            _t -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / _dur);         // 1→0
            float env = k * k;                           // 빠른 decay
            float mag = _strength * env;

            // 2) base 위에 additive 로 얹기(카메라 로컬 축 기준).
            Vector3 posOff = transform.TransformVector(_dir) * (positionAmp * mag);
            Quaternion rotOff = Quaternion.AngleAxis(_roll * rotationAmp * mag, transform.forward)
                                * Quaternion.AngleAxis(rotationAmp * mag, transform.right);

            transform.position += posOff;
            transform.rotation = rotOff * transform.rotation;
            _appliedPos = posOff;
            _appliedRot = rotOff;
        }

        private void OnDisable()
        {
            // 되돌려 원위치 보장(도메인 리로드/비활성 시 잔여 오프셋 방지).
            transform.rotation = Quaternion.Inverse(_appliedRot) * transform.rotation;
            transform.position -= _appliedPos;
            _appliedPos = Vector3.zero;
            _appliedRot = Quaternion.identity;
            _t = 0f;
        }
    }
}
