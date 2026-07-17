using UnityEngine;

namespace Wassup.Data
{
    // defender-directional-volley unit 6 — 공격방향 페이즈 튜닝값. 컨트롤러가 런타임
    // AddComponent 라 인스펙터 튜닝이 안 되므로 SO 로 분리(DragSwaySettings 선례).
    // DefenderSelector 에 할당하면 Configure 로 주입되고, 미주입 시 이 기본값.
    [CreateAssetMenu(fileName = "DirectionAimSettings", menuName = "Wassup/DirectionAimSettings", order = 21)]
    public class DirectionAimSettings : ScriptableObject
    {
        [Header("Gesture")]
        [Tooltip("이 픽셀 이상 끌어야 방향으로 친다. 미만이면 방향 없음(가이드 유지, 재스와이프 대기).")]
        public float deadZonePx = 48f;

        [Header("Time / Camera")]
        [Tooltip("방향 지정 중 전투 시간 배율. 드래그 슬로우모를 이어받는 값 — 0 아님(전투가 멈추면 안 된다).")]
        [Range(0.01f, 1f)] public float slowmoScale = 0.2f;

        [Header("Guide")]
        [Tooltip("유닛 화면 위치에서 방향 글리프까지 거리(px).")]
        public float guideRadiusPx = 130f;
        [Tooltip("방향 글리프 폰트 크기(px).")]
        public float guideFontSize = 64f;
        [Tooltip("선택되지 않은 방향 색.")]
        public Color idleColor = new Color(1f, 1f, 1f, 0.45f);
        [Tooltip("스와이프로 선택된 방향 색.")]
        public Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("선택된 방향 글리프 확대 배율.")]
        public float highlightScale = 1.35f;
    }
}
