using UnityEngine;
using UnityEngine.UI;
using Wassup.Presentation;

namespace Wassup.BattleCoreUnity.Cards
{
    // battle-core-rebuild unit 8a — 손패가 열릴 때 카드 뒤로 피어오르는 꿈 유체 배경.
    // 옛 `UI/Dreamcatcher/DreamcatcherFluidBackdrop.cs`(fluid-paint-mixing unit 4)의 복사·적응본이다.
    // 바뀐 것은 **상태원 하나**다 — 옛 `DreamcatcherHandView.State` 대신 `CoreHandView.State` 를 폴링한다.
    // 시뮬레이터(`FluidPaintSim`)·렌더 경로·알파·페이드 속도는 옛 씬 저작 그대로다
    // (`BattleScene.unity` FluidBackdrop: mode HandGated · maxAlpha 0.7 · fadeSpeed 6 · referenceSize 1920×1080).
    //
    // 손패 오픈 중(배틀 슬로모 = GPU 여유)만 sim 을 구동·표시 → 상시 배틀 부하 없음.
    // 손패 파일(공유 대형 파일)을 수정하지 않고 공개 상태만 읽는다 — 옛 것과 같은 규율.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CoreHandFluidBackdrop : MonoBehaviour
    {
        public enum GateMode { HandGated, AlwaysOn }

        [SerializeField] private CoreHandView _handView;
        [SerializeField] private FluidPaintSim _sim;
        [SerializeField] private RawImage _image;
        [Tooltip("HandGated=손패 오픈 중만 표시(perf 한정) / AlwaysOn=상시(검증·특수용)")]
        [SerializeField] private GateMode _mode = GateMode.HandGated;
        [Tooltip("표시 시 최대 알파 — backdrop 은 은은하게")]
        [SerializeField, Range(0f, 1f)] private float _maxAlpha = 0.7f;
        [Tooltip("페이드 추종 속도(클수록 빠름)")]
        [SerializeField] private float _fadeSpeed = 6f;

        private CanvasGroup _group;

        /// <summary>지금 표시 대상인가(페이드 목표가 켜짐). 테스트의 증언 창.</summary>
        public bool Open { get; private set; }

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false; // backdrop 은 입력 비간섭
            _group.interactable = false;
        }

        private void Update()
        {
            // HandGated 는 손패가 열린 동안만. 손패 미배선이면 표시 안 함(무회귀).
            Open = _mode == GateMode.AlwaysOn
                   || (_handView != null && _handView.State == CoreHandView.HandState.Hand);

            // 닫히면 sim 을 꺼 step 을 멈춘다(GPU 절약). 다시 열면 OnEnable 이 재할당·씨앗 → 매번 새 bloom.
            if (_sim != null && _sim.enabled != Open) _sim.enabled = Open;

            if (_image != null && _sim != null && _sim.IsReady && _image.texture != _sim.DyeTexture)
                _image.texture = _sim.DyeTexture;

            // 손패는 슬로모지만 UI 는 realtime 계약 — unscaled 로 페이드.
            float target = Open ? _maxAlpha : 0f;
            float a = 1f - Mathf.Exp(-Mathf.Max(0.01f, _fadeSpeed) * Time.unscaledDeltaTime);
            _group.alpha = Mathf.Lerp(_group.alpha, target, a);
        }
    }
}
