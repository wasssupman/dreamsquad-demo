using UnityEngine;
using UnityEngine.UI;
using Wassup.Presentation;

namespace Wassup.UI
{
    // fluid-paint-mixing unit 4 — Dreamcatcher 핸드가 열릴 때 카드 뒤로 피어오르는 꿈 유체 backdrop.
    // 핸드 오픈 중(배틀 슬로모 = GPU 여유)만 sim 을 구동·표시 → 상시 배틀 부하 없음.
    // DreamcatcherHandView(공유 대형 파일)를 수정하지 않고 공개 State 만 폴링한다. 렌더 경로는 unit 3 검증분과 동일.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class DreamcatcherFluidBackdrop : MonoBehaviour
    {
        public enum GateMode { HandGated, AlwaysOn }

        [SerializeField] private DreamcatcherHandView handView;
        [SerializeField] private FluidPaintSim sim;
        [SerializeField] private RawImage image;
        [Tooltip("HandGated=핸드 오픈 중만 표시(perf 한정) / AlwaysOn=상시(검증·특수용)")]
        [SerializeField] private GateMode mode = GateMode.HandGated;
        [Tooltip("표시 시 최대 알파 — backdrop 은 은은하게")]
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.7f;
        [Tooltip("페이드 추종 속도(클수록 빠름)")]
        [SerializeField] private float fadeSpeed = 6f;

        private CanvasGroup _group;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false; // backdrop 은 입력 비간섭
            _group.interactable = false;
        }

        private void Update()
        {
            // HandGated 는 핸드가 열린 동안만. handView 미배선이면 표시 안 함(무회귀).
            bool open = mode == GateMode.AlwaysOn
                        || (handView != null && handView.State == DreamcatcherHandView.HandState.Hand);

            // 닫히면 sim 을 꺼 step 을 멈춘다(GPU 절약). 다시 열면 OnEnable 이 재할당·씨앗 → 매번 새 bloom.
            if (sim != null && sim.enabled != open) sim.enabled = open;

            if (image != null && sim != null && sim.IsReady && image.texture != sim.DyeTexture)
                image.texture = sim.DyeTexture;

            // 핸드는 슬로모지만 UI 는 realtime 계약 — unscaled 로 페이드.
            float target = open ? maxAlpha : 0f;
            float a = 1f - Mathf.Exp(-Mathf.Max(0.01f, fadeSpeed) * Time.unscaledDeltaTime);
            _group.alpha = Mathf.Lerp(_group.alpha, target, a);
        }
    }
}
