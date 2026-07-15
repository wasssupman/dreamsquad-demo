using UnityEngine;

namespace Wassup.Data
{
    // season-gimmick-overwork unit 2 — 시즌 기믹 base SO.
    // SeasonData.gimmick 필드 슬롯용 base (concrete 는 OverworkGimmickData 등, 상속 2단계 상한 준수).
    // 기믹 = 한 판의 특수 룰 묶음. 룰 수치는 concrete SO 가 전부 보유 (하드코딩 금지).
    public abstract class GimmickData : ScriptableObject
    {
        public string gimmickId = "G0";
        public string displayName = "";
        // gimmick-match-integration unit 0 — 배치 페이즈 안내 카드 본문 (플레이어용 룰 설명).
        [TextArea(2, 4)]
        public string description = "";
    }
}
