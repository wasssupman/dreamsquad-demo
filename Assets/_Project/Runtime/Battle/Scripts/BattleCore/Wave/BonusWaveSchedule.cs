// salvaged from Assets/_Project/Scripts/Data/BonusWaveSchedule.cs (battle-core-rebuild unit 4)
// 이식 시 바뀐 것: 네임스페이스와 필드 표기(파스칼)뿐. 산식은 동일하다.
namespace Somnia.Battle.BattleCore.Wave
{
    // 보너스 웨이브의 배분·타임라인. **순수 함수**다.
    //
    // 기존 웨이브 생성기와 코드 경로를 공유하지 않는다 — 그쪽은 덱·시드·컨셉·레인을 다루고
    // 이쪽은 「N기를 P개 포탈에 순서대로」가 전부다. 결정론은 seeded RNG 가 아니라 **구조**다:
    // 같은 입력이면 언제나 같은 출력이고 호출 순서·횟수에 의존하지 않는다.
    public static class BonusWaveSchedule
    {
        public struct Entry
        {
            public int PortalIndex;
            public float SpawnAtSec;
            public int RingIndex;
            public int RingCount;
        }

        /// <summary>
        /// `portalCount` 는 **맵 저작 개수**다(분모를 저작 에셋에 두면 밸런서가 3으로 바꿔도
        /// 런타임은 2로 돈다). 잘못된 입력(0 이하)은 빈 배열 — 매 판 부르는 경로라 던지지 않는다.
        /// </summary>
        public static Entry[] Build(int portalCount, int enemyCount,
                                    float firstSpawnAtSec, float spawnIntervalSec)
        {
            if (portalCount <= 0 || enemyCount <= 0) return System.Array.Empty<Entry>();

            var result = new Entry[enemyCount];
            for (int i = 0; i < enemyCount; i++)
            {
                int portal = i % portalCount;
                result[i] = new Entry
                {
                    PortalIndex = portal,
                    // 시각은 **전체 순번** 기준이다(포탈별 순번이 아니라) — 두 포탈이 번갈아
                    // 뱉어야 「순차로 나온다」가 화면에서 성립한다.
                    SpawnAtSec = firstSpawnAtSec + i * spawnIntervalSec,
                    RingIndex = i / portalCount,
                    RingCount = CountForPortal(portalCount, enemyCount, portal),
                };
            }
            return result;
        }

        /// <summary>포탈 p 가 뱉는 마리수. 안 나눠떨어지면 앞쪽 포탈이 하나 더.</summary>
        public static int CountForPortal(int portalCount, int enemyCount, int portalIndex)
        {
            if (portalCount <= 0 || enemyCount <= 0) return 0;
            int baseCount = enemyCount / portalCount;
            return portalIndex < (enemyCount % portalCount) ? baseCount + 1 : baseCount;
        }
    }
}
