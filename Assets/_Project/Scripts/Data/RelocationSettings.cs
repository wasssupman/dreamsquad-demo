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

        [Header("재정비 보상 (unit 8 소비)")]
        // 코스트는 여기 없다 — 유닛 SO 의 cost 를 그대로 쓴다(값이 두 곳에 살면 갈린다).
        [Range(0f, 1f)]
        [Tooltip("재배치 활성화 시 최대 체력의 몇 배를 회복하는가 (0.5 = 절반). " +
                 "1 로 올리면 완전 회복 — 싼 탱커가 사실상 불사가 되므로 밸런스 확인 후.")]
        public float refitHealRatio = 0.5f;

        [Header("비행 (unit 3)")]
        [Tooltip("비행 기본 시간(초, Battle 시계)")]
        public float flightBaseSeconds = 0.35f;

        [Tooltip("sim 거리 1당 추가 비행 시간(초)")]
        public float flightSecondsPerUnit = 0.04f;

        [Tooltip("비행 시간 상한(초)")]
        public float flightMaxSeconds = 0.9f;

        // 던지기 아치 형태(높이 배수·좌우 변주·상승/하강 접선)는 탭 배치와 단일 소스(DragSwaySettings 의
        // tapArc*/tapThrow*) — DefenderDragPlacementController.ComputeThrowArc 가 공급. 여기 별도 아치 knob 없음.

        [Header("비행 키링(고리+줄)")]
        // 고리/줄의 룩(반경·폭·색·머티리얼·스타일)은 배치 키링과 단일 소스(DragSwaySettings)를 공유한다
        // — DefenderDragPlacementController.CreateKeyringHardware 가 소유. 여기엔 재배치 전용 sway 만 둔다.
        [Tooltip("고리 추종 부드러움(작을수록 sway 큼) — exp-lerp time(초)")]
        public float flightRingFollow = 0.08f;
    }
}
