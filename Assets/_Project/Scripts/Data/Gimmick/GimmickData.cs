using UnityEngine;

namespace Wassup.Data
{
    // season-gimmick-overwork unit 2 — 시즌 기믹 base SO.
    // BattleConfig.gimmickPool 슬롯용 base (concrete 는 BurnoutGimmickData / RedBullGimmickData, 상속 2단계 상한 준수).
    // 기믹 = 한 판의 특수 룰 묶음. 룰 수치는 concrete SO 가 전부 보유 (하드코딩 금지).
    public abstract class GimmickData : ScriptableObject
    {
        public string gimmickId = "G0";
        // 정서 카피 — 게임의 톤을 담당한다. 룰 이름이 아니라 애칭이므로 리빌에선 부제로 내려간다.
        public string displayName = "";
        // gimmick-match-integration unit 0 — 배치 페이즈 안내 카드 본문 (플레이어용 룰 설명).
        [TextArea(2, 4)]
        public string description = "";

        // gimmick-recognition-upgrade unit 0 — 문구 4단 분리.
        // displayName(정서 카피) 하나가 룰 이름 자리까지 겸직해서 "무슨 기믹인지 모르겠다"가 났다.
        // 아래 3개가 각자 화면 하나씩만 책임진다. 대상(아군/전체) 은 별도 필드로 두지 않는다 —
        // summary 문구가 이미 말한다("내 유닛은 오래 둘수록…").
        [Tooltip("룰 라벨 2~4자. 리빌 주제목 — 이름이 곧 룰이어야 읽힌다.")]
        public string ruleLabel = "";
        [Tooltip("한 줄 요약 15~25자. 리빌에서 description 3줄을 대신한다.")]
        public string summary = "";
        [Tooltip("리빌 대형 아이콘. 미할당이면 라벨만 표시(크래시 없음).")]
        public Sprite icon;
    }
}
