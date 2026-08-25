using PrimeTween;
using UnityEngine;

namespace Wassup.Presentation
{
    // 천장 줄에 매달린 것(조명·간판·화분)의 느린 진자 흔들림.
    //
    // 이 컴포넌트는 무엇이 매달려 있는지 모른다 — 자기 Transform(= 천장 앵커 피벗)의
    // localRotation 만 흔들고, 피벗 밑에 붙은 자식(Light / 스프라이트 / LineRenderer 줄)이
    // 호를 따라간다. 그래서 조명 전용이 아니다: 흔들고 싶은 것을 피벗 자식으로 넣으면 된다.
    // 피벗은 천장(줄이 걸린 점)에 두고 대상을 줄 길이만큼 -y 로 내리는 것이 진자의 전제.
    //
    // 모션 = 주축 Yoyo 진자 + 직교축 미세 Yoyo. 두 주기를 정수배가 아니게 두면 합성 궤적이
    // 같은 그림으로 반복되지 않아 기계적 반복감이 사라진다.
    // Yoyo × Ease.InOutSine 은 닫힌 형태로 순수 사인파다: lerp(-A, +A, 0.5-0.5cos(πu)) = -A·cos(πu),
    // 되돌아오는 반주기도 같은 식에 이어 붙어 전체가 -A·cos(π·t/d). 즉 실제 진자와 같은
    // 양끝 감속 / 중앙 최고속을 공짜로 얻는다.
    [DisallowMultipleComponent]
    public class HangingSway : MonoBehaviour
    {
        public enum SwayAxis { X, Y, Z }

        [Header("주축 (큰 흔들림)")]
        [Tooltip("진자가 회전하는 축(로컬). Z 회전 = 화면 좌우 흔들림, X 회전 = 앞뒤.")]
        [SerializeField] private SwayAxis mainAxis = SwayAxis.Z;
        [Tooltip("최대 기울임각(deg). 한쪽 끝까지의 각도 — 실제 진폭은 이 값의 2배를 왕복한다.")]
        [SerializeField] private float mainAmplitudeDeg = 6f;
        [Tooltip("한 왕복(좌→우→좌) 시간(초). ↑=느리고 무거움.")]
        [SerializeField] private float mainPeriodSec = 3.8f;

        [Header("보조축 (미세 변주)")]
        [Tooltip("주축과 직교한 축을 골라야 궤적이 8자로 퍼진다.")]
        [SerializeField] private SwayAxis crossAxis = SwayAxis.X;
        [Tooltip("0 이면 보조축을 아예 돌리지 않는다(1축 순수 진자).")]
        [SerializeField] private float crossAmplitudeDeg = 1.5f;
        [Tooltip("주축 주기의 정수배를 피할 것 — 정수배면 같은 궤적이 반복된다.")]
        [SerializeField] private float crossPeriodSec = 5.3f;

        [Header("위상")]
        [Tooltip("0~1 = 한 왕복 안에서의 시작 지점. 같은 씬의 다른 개체와 다르게 주면 동조하지 않는다.")]
        [SerializeField, Range(0f, 1f)] private float phase01;
        [Tooltip("켜면 Time.timeScale 을 무시한다(일시정지 중에도 흔들림 유지).")]
        [SerializeField] private bool ignoreTimeScale;

        private Quaternion _baseRot = Quaternion.identity;
        private Tween _mainTween;
        private Tween _crossTween;
        private float _mainAngle;
        private float _crossAngle;

        private void Awake()
        {
            _baseRot = transform.localRotation;
        }

        private void OnEnable()
        {
            StartSway();
        }

        private void OnDisable()
        {
            StopSway();
        }

        private void StartSway()
        {
            // 에디터 편집 중(비 Play)에는 PrimeTween 이 업데이트 루프를 돌지 않는다 —
            // [ExecuteAlways] 가 없으니 여기 도달하지 않지만, 재컴파일 타이밍 방어로 남긴다.
            if (!Application.isPlaying) return;

            _mainTween = StartAxisTween(mainAmplitudeDeg, mainPeriodSec, true);
            _crossTween = StartAxisTween(crossAmplitudeDeg, crossPeriodSec, false);
            Apply();
        }

        private Tween StartAxisTween(float amplitudeDeg, float periodSec, bool isMain)
        {
            // 진폭 0 / 비정상 주기는 트윈을 만들지 않는다(PrimeTween 은 duration <= 0 을 거부).
            if (Mathf.Approximately(amplitudeDeg, 0f) || periodSec <= 0f) return default;

            // 한 왕복 = Yoyo 2사이클. 그래서 트윈 duration 은 주기의 절반.
            float halfPeriod = periodSec * 0.5f;
            Tween tween = isMain
                ? Tween.Custom(this, -amplitudeDeg, amplitudeDeg, halfPeriod,
                    static (host, angle) => { host._mainAngle = angle; host.Apply(); },
                    Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: ignoreTimeScale)
                : Tween.Custom(this, -amplitudeDeg, amplitudeDeg, halfPeriod,
                    static (host, angle) => { host._crossAngle = angle; host.Apply(); },
                    Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: ignoreTimeScale);

            // 위상은 startDelay 로 줄 수 없다 — PrimeTween 의 startDelay 는 매 사이클마다
            // 다시 적용돼서 왕복마다 멈칫한다. 무한 트윈도 받는 elapsedTimeTotal 로 밀어넣는다.
            if (phase01 > 0f) tween.elapsedTimeTotal = phase01 * periodSec;
            return tween;
        }

        private void StopSway()
        {
            _mainTween.Stop();
            _crossTween.Stop();
            _mainAngle = 0f;
            _crossAngle = 0f;
            transform.localRotation = _baseRot;
        }

        private void Apply()
        {
            Vector3 euler = AxisVector(mainAxis) * _mainAngle + AxisVector(crossAxis) * _crossAngle;
            transform.localRotation = _baseRot * Quaternion.Euler(euler);
        }

        private static Vector3 AxisVector(SwayAxis axis) => axis switch
        {
            SwayAxis.X => Vector3.right,
            SwayAxis.Y => Vector3.up,
            _ => Vector3.forward,
        };

#if UNITY_EDITOR
        // Play 중 인스펙터로 값을 만지면 즉시 반영한다(트윈은 생성 시점 값을 굽기 때문에 재시작 필요).
        private void OnValidate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            StopSway();
            StartSway();
        }
#endif
    }
}
