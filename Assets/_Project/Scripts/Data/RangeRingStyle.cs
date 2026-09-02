using UnityEngine;

namespace Wassup.Data
{
    // dreamcatcher-attach-range-preview 0b — 바닥 범위 링의 look 한 벌.
    //
    // 배치 사거리 링(라임 · 선 0.95 · 채움 0.12)은 「선이 말하고 채움은 보험」이지만, 작은 반경 + 손가락
    // 중심 조건(액티브 셀 조준 · 카드 부착 프리뷰)에서는 **채움이 주신호**다(사용자 결정 2026-09-02 D1).
    // 그래서 링 하나에 스타일이 둘 이상 필요하고, 값은 전부 SO 에 산다(제약 6).
    //
    // 색 규칙(README 계약 9): hue = 「무엇에 대한 말인가」. 라임 = 내 유닛 공격 도달 · 시안 계열 = 드림캐쳐 행위.
    // 같은 hue 두 표면은 형태로 갈린다 — UI 오버레이는 선, 바닥 캐리어는 채움.
    [System.Serializable]
    public struct RangeRingStyle
    {
        [Tooltip("선과 채움의 색(알파는 아래 두 값이 따로 정한다).")]
        public Color color;
        [Range(0f, 1f)] [Tooltip("SDF 내부 채움 알파. 작은 반경에선 이게 주신호다.")]
        public float fillAlpha;
        [Range(0f, 1f)] [Tooltip("링(선) 알파.")]
        public float lineAlpha;
        [Tooltip("채움 알파를 rangePulseSpeed 로 흔든다. 사거리 채움 펄스는 사용자 요청으로 제거된 이력이 있어 기본 off.")]
        public bool pulse;

        public RangeRingStyle(Color color, float fillAlpha, float lineAlpha, bool pulse = false)
        {
            this.color = color;
            this.fillAlpha = fillAlpha;
            this.lineAlpha = lineAlpha;
            this.pulse = pulse;
        }
    }
}
