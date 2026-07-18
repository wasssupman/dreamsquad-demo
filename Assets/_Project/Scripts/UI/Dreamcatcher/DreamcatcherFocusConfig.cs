using UnityEngine;

namespace Wassup.UI
{
    // dreamcatcher-attach-lockon unit 0 — 부착 조준 포커스 연출의 모든 시각·타이밍
    // 노브. 경제/규칙 노브(AwakeningConfig)와 분리한 feel 튜닝 블록(런타임 튜닝 SO
    // DragSwaySettings 선례). DreamcatcherHandView 가 [SerializeField] 로 보유하고
    // DreamcatcherFocusPresenter 가 소비한다. 하드코딩 0 (제약 #6).
    [CreateAssetMenu(menuName = "Wassup/Dreamcatcher/Focus Config", fileName = "DreamcatcherFocusConfig")]
    public class DreamcatcherFocusConfig : ScriptableObject
    {
        [Header("A · 전장 dim")]
        public Color dimColor = new Color(0.02f, 0.02f, 0.06f, 1f);
        [Range(0f, 1f)] public float dimAlpha = 0.42f;
        public float dimFadeInSec = 0.12f;
        public float dimFadeOutSec = 0.10f;

        [Header("화살표 시인성 + 끝점 당김")]
        public Color arrowOutlineColor = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        public float arrowOutlinePad = 3f;      // 아웃라인 쿼드가 본체보다 큰 px
        [Range(0f, 1f)] public float arrowMinAlpha = 0.7f; // tail 최소 알파(기존 0.45 상향)
        [Range(0f, 1f)] public float arrowLockBlend = 0.7f; // 락온 시 끝점 대상 당김 계수
        public float arrowHeadSize = 54f;       // 삼각 화살촉 크기(비주얼 개선·확대)
        // 3-상태: idle(대상없음)=흰색 / 부착가능=하늘색 / 부착불가=붉은색.
        public Color arrowNeutralColor = new Color(1f, 1f, 1f, 0.7f);         // idle(대상 없음) — 흰색
        public Color arrowValidColor = new Color(0.42f, 0.95f, 1f, 1f);       // 부착 가능 — 하늘색(시안)
        public Color arrowInvalidColor = new Color(1f, 0.42f, 0.46f, 1f);     // 부착 불가 — 붉은색(파스텔)

        [Header("C · 락온 리티클")]
        public Color reticleColor = new Color(0.42f, 0.95f, 1f, 1f);   // 시안 락온
        public Color reticleInvalidColor = new Color(0.62f, 0.64f, 0.7f, 0.9f); // full=회색
        public float reticleCornerLen = 22f;    // 코너 브래킷 팔 길이
        public float reticleThickness = 4f;
        public float reticlePadding = 14f;       // 유닛 렉트 바깥 여백
        public float reticleMinScreenSize = 132f; // 코너가 손끝 반경 초과 보장
        public float reticleSnapSpring = 22f;    // 위치/크기 스프링(클수록 빠름)
        public float reticlePopScale = 1.14f;    // 첫 락온 팝
        public float reticleInvalidGap = 10f;    // invalid 폼: 코너 바깥으로 벌어짐
        [Header("정체 히스테리시스")]
        public float lockSwitchHysteresisPx = 40f; // 새 후보가 이만큼 더 가까워야 전환

        [Header("D · 오프셋 아이덴티티 콜아웃")]
        public Vector2 calloutScreenOffset = new Vector2(0f, 96f); // 락온 렉트 상단 + 손끝 밖
        public float calloutIconSize = 56f;
        public Color calloutBgColor = new Color(0.06f, 0.05f, 0.13f, 0.92f);
        public Color calloutBorderColor = new Color(0.42f, 0.95f, 1f, 0.9f);
        public Color calloutValidTextColor = Color.white;
        public Color calloutFullTextColor = new Color(1f, 0.55f, 0.4f, 1f); // X/3 full 강조
        public float calloutEdgeClampPad = 24f;
        public float calloutFadeSec = 0.12f;

        [Header("B · 유효 base-ring")]
        public Color baseRingColor = new Color(0.42f, 0.95f, 1f, 0.5f);
        public float baseRingRadius = 46f;
        public float baseRingThickness = 4f;
        public float baseRingPulseSec = 1.1f;
        [Range(0f, 1f)] public float baseRingLockedFade = 0.15f; // 락온 유닛 링 감광
        public float baseRingRevealRadius = 420f;  // 포인터 근처만 강조(0=전량 상시)
        [Range(0f, 1f)] public float baseRingDistanceFade = 0.35f; // 먼 링 최소 알파 배율

        [Header("E · 확정 비트")]
        // 확정 = 손끝 밖 링 펄스 + 콜아웃 펀치 + 햅틱. (리티클 수렴/플래시는 커밋 시
        // 리티클이 즉시 teardown 돼 무의미 → 노브 없음.)
        public Color confirmPulseColor = new Color(0.7f, 1f, 1f, 0.9f);
        public float confirmPulseSec = 0.28f;
        public float confirmPulseMinRadius = 120f; // 손끝 반경 초과 보장
        public bool enableHaptic = true;

        [Header("F · 포커스 유닛 틴트")]
        // 락온된 defender 몸체(Spine)에 상태색 틴트 — 리티클/화살표와 같은 언어를
        // 유닛 자체에도 얹는다. Spine 은 곱셈 틴트(0~1)라 '밝힘' 불가·색조/어둠만
        // 가능 → dim 된 전장 위 색조 대비로 pop(리티클이 1차, 틴트는 2차 보강).
        // 적(EnemyMark)은 health-tint 합성 문제로 비적용.
        public bool enableFocusTint = true;
        public Color focusTintValid = new Color(0.60f, 1f, 1f);   // 부착 가능 — 시안 캐스트
        public Color focusTintInvalid = new Color(1f, 0.52f, 0.56f); // 부착 불가 — 붉은 캐스트
    }
}
