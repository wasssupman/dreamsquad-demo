namespace Wassup.Data
{
    // bonus-wave-pull unit 4 — 보너스 웨이브의 배분·타임라인. **순수 함수**다(제약 10 의 (c)
    // sim-critical). 기존 웨이브 생성기(WavePatternGenerator)와 코드 경로를 공유하지 않는다 —
    // 그쪽은 덱·시드·컨셉·레인을 다루고 이쪽은 「N기를 P개 포탈에 순서대로」가 전부다.
    //
    // 결정론은 seeded RNG 가 아니라 **구조**다(README 계약 3). 같은 입력이면 언제나 같은 출력이고
    // 호출 순서·횟수에 의존하지 않는다.
    public static class BonusWaveSchedule
    {
        public struct Entry
        {
            public int portalIndex;    // 어느 포탈에서
            public float spawnAtSec;   // 버튼 누른 시점 기준 절대 시각
            public int ringIndex;      // 그 포탈에서 몇 번째인가 (겹침 오프셋 각도용)
            public int ringCount;      // 그 포탈이 뱉는 총 마리수 (같은 용도)
        }

        // portalCount 는 맵 저작 개수, enemyCount·시각은 BonusWaveData 소유.
        // 잘못된 입력(0 이하)은 빈 배열 — 호출부가 매 판 부르는 경로라 예외를 던지지 않는다.
        public static Entry[] Build(
            int portalCount, int enemyCount, float firstSpawnAtSec, float spawnIntervalSec)
        {
            if (portalCount <= 0 || enemyCount <= 0) return System.Array.Empty<Entry>();

            var result = new Entry[enemyCount];
            for (int i = 0; i < enemyCount; i++)
            {
                int portal = i % portalCount;
                result[i] = new Entry
                {
                    portalIndex = portal,
                    // 시각은 **전체 순번** 기준이다(포탈별 순번이 아니라) — 두 포탈이 번갈아
                    // 뱉어야 「순차로 나온다」가 화면에서 성립한다.
                    spawnAtSec = firstSpawnAtSec + i * spawnIntervalSec,
                    ringIndex = i / portalCount,
                    ringCount = CountForPortal(portalCount, enemyCount, portal),
                };
            }
            return result;
        }

        // 포탈 p 가 뱉는 마리수. enemyCount 가 portalCount 로 안 나눠떨어질 때 앞쪽 포탈이 하나 더.
        public static int CountForPortal(int portalCount, int enemyCount, int portalIndex)
        {
            if (portalCount <= 0 || enemyCount <= 0) return 0;
            int baseCount = enemyCount / portalCount;
            return portalIndex < (enemyCount % portalCount) ? baseCount + 1 : baseCount;
        }
    }
}
