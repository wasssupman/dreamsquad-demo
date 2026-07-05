using UnityEngine;

namespace Wassup.Data
{
    // keyring-cord-preview — 드래그 프리뷰 키링 스윙(제어형 스프링 진자) + 줄/고리 비주얼 튜닝값.
    // 움직임은 자유 물리 시뮬이 아니라 이동범위·각도·스프링·댐핑으로 제어한다(고리 아래 실루엣이 진자처럼 스윙).
    // 컨트롤러가 런타임 AddComponent 라 인스펙터 튜닝이 안 되므로 SO 로 분리. DefenderSelector 에 할당 →
    // Configure 로 주입. 미할당이면 컨트롤러가 클래스 기본값 인스턴스로 폴백. 에셋 편집은 런타임 즉시 반영.
    [CreateAssetMenu(menuName = "Wassup/Drag Sway Settings", fileName = "DragSwaySettings")]
    public class DragSwaySettings : ScriptableObject
    {
        [Header("추종 (무게추 스프링 + 속도 상한)")]
        [Tooltip("고리→유닛 줄 길이(로컬, 월드=×visualScale). 고리 아래 매달리는 길이.")]
        public float ropeLength = 2.0f;
        [Tooltip("유닛 최대 기울임각(deg). 흔들리는 정도. ↓=덜 흔들림.")]
        public float maxAngle = 8f;
        [Tooltip("추종 강성/탄성(↑=팽팽/빠릿, ↓=늘어지고 부드럽게). 탄성 세기.")]
        public float spring = 100f;
        [Tooltip("감쇠(↓=바운스·출렁 큼/floaty, ↑=빨리 멎음). 탄성 잔진동.")]
        public float damping = 2.5f;
        [Tooltip("추종 최대 속도(월드/s). 빠른 스와이프 시 이 속도로만 제한 → 튀어나감 방지(탄성은 유지). 0=무제한.")]
        public float maxSpeed = 12f;

        [Header("줄/고리 비주얼")]
        [Tooltip("줄 폭(로컬, 월드=×visualScale). 너무 얇으면 sub-pixel 로 렌더 컬링됨.")]
        public float cordWidth = 0.14f;
        [Tooltip("줄 색.")]
        public Color cordColor = new Color(0.45f, 0.38f, 0.28f, 1f);
        [Tooltip("고리(링) 반경(로컬, 월드=×visualScale).")]
        public float ringRadius = 0.18f;
        [Tooltip("실루엣 추가 드롭(로컬). 머리는 줄 끝 자동정렬, 이 값은 미세조정. 0=머리가 줄 끝.")]
        public float charmDrop = 0.0f;
    }
}
