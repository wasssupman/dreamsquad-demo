using UnityEngine;

namespace Wassup.Data
{
    // gimmick-match-integration unit 0 — 매치 전역 기믹 기능 config.
    // 추후 시트에서 데이터로 관리될 예정이라 순수 데이터 컨테이너로 유지한다.
    // 기믹 소스는 더 이상 SeasonData 가 아니라 이 config 의 gimmickPool 이다
    // (매치 시작 시 GameManager 가 시드 기반으로 1개 배정).
    [CreateAssetMenu(menuName = "Wassup/Config/BattleConfig", fileName = "BattleConfig")]
    public sealed class BattleConfig : ScriptableObject
    {
        [Tooltip("기믹 기능 전체 on/off. false = 기존 클린 플레이(기믹 없음).")]
        public bool gimmickEnabled = true;

        [Tooltip("전체 기믹 목록. 매치 시작 시 여기서 시드 기반 랜덤 1개 배정.")]
        public GimmickData[] gimmickPool = System.Array.Empty<GimmickData>();

        // match-intro-phase-toggles unit 0 — 배치 페이즈 토글. 기믹 토글과 같은 성격이라 같은 자리에 둔다.
        // false 여도 배치 페이즈 진입 자체(트레이 구성·코스트 리셋·ECS 배치 상태)는 그대로 돌고,
        // 창의 길이와 입력 가능 여부만 바뀐다 — 진입을 건너뛰면 트레이가 빈 채로 전투에 들어간다.
        [Tooltip("배치 페이즈 on/off. false = 3초 카운트다운(입력 불가) 후 자동 전투 시작.")]
        public bool placementPhaseEnabled = true;

        [Tooltip("placementPhaseEnabled=false 일 때 자동 시작까지의 카운트다운(초).")]
        public float autoStartCountdownSeconds = 3f;
    }
}
