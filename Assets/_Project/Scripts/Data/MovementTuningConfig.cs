using UnityEngine;

namespace Wassup.Data
{
    // battle-core-rebuild 5a 후속 — 적이 어떻게 서고 어떻게 퍼지나의 **저작**.
    //
    // 옛 전투에서 이 넷은 `BattleBridge` 직렬화 필드였다(`agentRadiusTiles`·`spawnSubLaneCount`·
    // `spawnSpreadFraction`·`spawnSpreadTopScale`). 이식 중 코어 리터럴로 굳었던 것을 다시
    // 판 밖으로 꺼낸다 — 값의 정본은 판 밖이고(계약 6) 이 넷은 `configHash` 에 든다.
    //
    // ⚠ **뷰 설정이 아니다.** 화면이 아니라 판이 읽는 값이라 `Data/BattleView/` 가 아니라
    // 여기 있고, 소비처는 `MatchDefinitionBuilder` 다.
    [CreateAssetMenu(menuName = "Wassup/Movement Tuning Config", fileName = "MovementTuningConfig")]
    public sealed class MovementTuningConfig : ScriptableObject
    {
        [Tooltip("적의 몸 반지름(칸). **군집 통과로 검산한 값** — 단독 통과는 검산이 아니다. " +
                 "0.35 에서 6맵 100초 교착이 났고 0.25 에서 소멸했다.")]
        [SerializeField, Range(0f, 0.49f)] private float agentRadiusTiles = 0.25f;

        [Tooltip("측면 분산의 레인 수. 스폰 순번을 대칭 N 레인에 round-robin 배정한다. 1 = 전부 칸 중앙.")]
        [SerializeField, Range(1, 7)] private int spawnSubLaneCount = 3;

        [Tooltip("분산 반폭(칸 폭 비). **0 이 곧 「분산 끔」**이다. 0.49 를 넘길 수 없다 — " +
                 "반 칸을 넘으면 옆 칸을 침범해 칸 환산·골 판정이 유닛을 다른 칸으로 본다.")]
        [SerializeField, Range(0f, 0.49f)] private float spawnSpreadFraction = 0.2f;

        [Tooltip("위쪽(+) 범위만 좁히는 배율. 키 큰 캐릭터 보정. 1 = 대칭.")]
        [SerializeField, Range(0f, 1f)] private float spawnSpreadTopScale = 0.5f;

        [Tooltip("보스 일반 도약의 비행 창(초). 이 동안 공격·이동을 못 하고(맞기는 한다) 창 끝에 착지 슬램이 터진다. " +
                 "옛 브리지 값 0.83. 뷰(`CoreLeapPresenter`)는 이 값을 도약 사건으로 받는다.")]
        [SerializeField, Min(0.05f)] private float bossLeapFlightSeconds = 0.83f;

        public float AgentRadiusTiles => agentRadiusTiles;
        public float BossLeapFlightSeconds => bossLeapFlightSeconds;
        public int SpawnSubLaneCount => spawnSubLaneCount;
        public float SpawnSpreadFraction => spawnSpreadFraction;
        public float SpawnSpreadTopScale => spawnSpreadTopScale;
    }
}
