using UnityEngine;

namespace Wassup.Data
{
    // season-gimmick-onsen unit 0 — "뜨끈하니 좋네요오오.. 뜨겁네?"(온천) 기믹.
    // 룰: 맵 위 모든 유닛(아군+적)이 heatInterval 마다 열기 +1. 스택 획득 시 최대체력의
    //     healPercent 만큼 회복하되, 스택이 flipThreshold 를 초과하면 같은 크기의 손실로 반전
    //     (lossPercent, HP 1 바닥 — 열기는 아무도 못 죽인다). "열기"는 스택 명칭일 뿐, 별도
    //     스탯 디버프 없음.
    // ECS 소비는 OnsenGimmickConfig 로 복사돼 들어간다 (BattleBridge 주입 seam).
    [CreateAssetMenu(fileName = "Gimmick_Onsen", menuName = "Wassup/Gimmick/Onsen", order = 42)]
    public sealed class OnsenGimmickData : GimmickData
    {
        [Header("룰 — 열기(Heat) 누적 → 회복↔손실 반전")]
        [Tooltip("모든 유닛의 열기 누적 주기 (초)")]
        [Min(0f)]
        public float heatInterval = 5f;
        [Tooltip("이 스택 이하 = 회복, 초과 = 손실로 반전")]
        public byte flipThreshold = 5;
        // [Min] 가드: 음수 percent 는 HeatMath.Delta 에서 회복↔손실 부호를 뒤집는다
        // (음수 heal = 데미지로 라우팅). authoring 레이어에서 차단 — 순수 함수는 안 건드림.
        [Tooltip("스택 획득 시 회복량 = 최대체력 × 이 비율 (0.1 = 10%)")]
        [Min(0f)]
        public float healPercent = 0.1f;
        [Tooltip("과열(반전) 시 손실량 = 최대체력 × 이 비율. HP 1 미만으로는 안 내림.")]
        [Min(0f)]
        public float lossPercent = 0.1f;
        [Tooltip("열기 스택 카운터 상한 (flipThreshold+1 이면 충분 — 이후 효과 동일)")]
        public byte heatMaxStack = 6;
    }
}
