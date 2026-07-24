using UnityEngine;

namespace Wassup.Data
{
    // defender-relocation unit 1 — 재배치 UX 노브 응집 (spec README 계약 1: 하드코딩 금지).
    // redeploySeconds 는 unit 3(착지 후 재전개)이 소비한다.
    [CreateAssetMenu(menuName = "Wassup/Relocation Settings", fileName = "RelocationSettings")]
    public class RelocationSettings : ScriptableObject
    {
        [Header("진입")]
        [Tooltip("이동모드 진입 쿨다운(초, 실시간) — 확정/취소 무관 적용(슬로모 남용 방지)")]
        public float entryCooldownSeconds = 3f;

        [Header("이동모드")]
        [Tooltip("이동모드 자동 취소 타임아웃(초, 실시간) — 무한 슬로모 차단")]
        public float moveModeTimeoutSeconds = 8f;

        [Tooltip("이동모드 유닛 하이라이트 틴트")]
        public Color highlightColor = new Color(0.45f, 0.9f, 1f, 1f);

        [Header("재전개 (unit 3 소비)")]
        [Tooltip("착지 후 전투 복귀까지 대기(초, Battle 시계 — 슬로모에 정직)")]
        public float redeploySeconds = 1.5f;

        [Header("비행 (unit 3)")]
        [Tooltip("비행 기본 시간(초, Battle 시계)")]
        public float flightBaseSeconds = 0.35f;

        [Tooltip("sim 거리 1당 추가 비행 시간(초)")]
        public float flightSecondsPerUnit = 0.04f;

        [Tooltip("비행 시간 상한(초)")]
        public float flightMaxSeconds = 0.9f;

        [Tooltip("비행 아치 높이(카메라 up 방향, sim 단위) — 던지는 아치 가시성")]
        public float flightArcHeight = 1.8f;

        [Header("비행 키링(고리+줄)")]
        [Tooltip("유닛 위 고리까지 줄 길이(카메라 up, sim 단위)")]
        public float flightRopeLength = 0.9f;

        [Tooltip("고리 반지름(sim 단위)")]
        public float flightRingRadius = 0.16f;

        [Tooltip("줄/고리 두께")]
        public float flightCordWidth = 0.05f;

        [Tooltip("고리 추종 부드러움(작을수록 sway 큼) — SmoothDamp time")]
        public float flightRingFollow = 0.08f;

        [Tooltip("줄/고리 색")]
        public Color flightKeyringColor = new Color(0.95f, 0.85f, 0.5f, 1f);
    }
}
