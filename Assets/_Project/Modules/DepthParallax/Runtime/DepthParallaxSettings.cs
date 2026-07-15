using UnityEngine;

namespace Wassup.DepthParallax
{
    // depth-parallax unit 0 — 뎁스맵 패럴랙스 모듈의 제네릭 시각 튜너블.
    // 이 SO 는 콘텐츠(디펜더/컷신/카드)를 전혀 모른다. 셰이더 파라미터 + 틸트 스프링 값만 담는다.
    // 인스턴스(.asset)는 소비처(예: Assets/_Project/Data/)가 소유하고, 유닛별 프레임/뎁스/틸트는
    // 런타임에 API 로 주입된다(모듈 SO 에 굽지 않음). 콘텐츠 플래그를 여기에 새지 않는다.
    [CreateAssetMenu(menuName = "Wassup/Depth Parallax Settings", fileName = "DepthParallaxSettings")]
    public class DepthParallaxSettings : ScriptableObject
    {
        [Header("Parallax")]
        [Tooltip("peak UV 오프셋(0..~0.04). 셰이더 _Amplitude. 너무 크면 외곽 늘어짐(rubber-sheet).")]
        public float amplitude = 0.022f;
        [Tooltip("힌지 평면(0..1). 셰이더 _DepthCenter. 이 값 기준 near/far 가 반대로 움직여 회전감.")]
        public float depthCenter = 0.5f;
        [Tooltip("뎁스 극성(±1). 셰이더 _DepthSign. near/far 가 뒤집혀 보이면 -1.")]
        public float depthSign = 1f;

        [Header("Perspective / Highlight")]
        [Tooltip("클립공간 사다리꼴 세기(0.03~0.08). 셰이더 _Persp. ortho 에서 회전감의 핵심.")]
        public float perspective = 0.03f;
        [Tooltip("하이라이트 스윕 세기. 셰이더 _HiStrength.")]
        public float highlightStrength = 0.12f;
        [Tooltip("하이라이트 밴드 폭. 셰이더 _HiWidth.")]
        public float highlightWidth = 0.25f;

        [Header("Tilt Spring")]
        [Tooltip("틸트 추종 강성(↑=빠릿).")]
        public float tiltSpring = 90f;
        [Tooltip("틸트 감쇠(↓=출렁, ↑=빨리 멎음).")]
        public float tiltDamping = 19f;   // ≈2·√spring → 임계감쇠(ζ≈1). 낮으면 틸트가 링잉→이미지 출렁.
        [Tooltip("틸트 추종 최대 속도(0=무제한).")]
        public float tiltMaxSpeed = 8f;
        [Tooltip("이 시간(초) 동안 피드가 없으면 틸트 target 을 0 으로(릴리즈). 드래그 종료 자동 복귀.")]
        public float tiltStalenessSeconds = 0.06f;
    }
}
