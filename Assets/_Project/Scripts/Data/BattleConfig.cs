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
    }
}
