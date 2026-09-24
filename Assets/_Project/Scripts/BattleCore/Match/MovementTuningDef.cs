using System.Globalization;
using System.Text;
using Unity.Mathematics;

namespace Wassup.BattleCore
{
    // battle-core-rebuild 5a 후속 — **적이 어떻게 서고 어떻게 퍼지나**의 저작값.
    //
    // unit 2 가 이식할 때 이 넷을 `EnemySpawn` 안에 리터럴로 박았고(`LaneFraction(_, 5, 0.4f, 1f)` ·
    // `AgentRadiusTiles = 0.25f`), 그중 셋이 **옛 씬 값과 달랐다**(3 · 0.2 · 0.5). 저작값이 없는
    // 채로 상수를 고른 것이라, 적이 스폰 지점에서 퍼지는 폭이 기록 없이 넓어져 있었다 —
    // 계약 2(「땜빵·우연은 의도만 옮긴다」)가 막으려는 것이 정확히 이 종류의 조용한 편차다.
    //
    // 제약 6(모든 수치는 SO 에서) + 계약 6(값의 정본은 판 밖)대로 정의표로 올린다.
    // **`configHash` 에 든다** — 이 값이 움직이면 같은 seed 라도 다른 판이고, 골든이 그것을
    // 「조건이 바뀌었다」로 정직하게 말해야 한다.
    //
    // ⚠ 기본값은 **옛 씬 값**이다. 고정구가 `new MatchDefinition{…}` 로 정의표를 직접 만들 때도
    // 라이브와 같은 판이 되게 하려는 것이고, 0 으로 떨어지면 몸 반지름 0(충돌 소멸)과
    // 레인 1(분산 없음)이 조용히 성립한다.
    public struct MovementTuningDef
    {
        /// <summary>
        /// 적의 몸 반지름(칸). **군집 통과로 검산한 값**이다 — 단독 통과는 검산이 아니다
        /// (0.35 에서 6맵 100초 교착이 났고 0.25 에서 소멸했다).
        /// </summary>
        public float AgentRadiusTiles;

        /// <summary>
        /// 측면 분산의 레인 수. 스폰 순번을 대칭 N 레인에 round-robin 배정한다.
        /// 1 이면 전부 칸 중앙에서 나온다.
        /// </summary>
        public int SpawnSubLaneCount;

        /// <summary>
        /// 분산 반폭(칸 폭 비). **0 이 곧 「분산 끔」**이다 — 옛 전투의 `spawnSpreadEnabled`
        /// bool 축을 따로 옮기지 않은 이유가 이것이다(끄는 방법이 이미 값 안에 있다).
        /// ⚠ `SpawnSpread.MaxHalfFraction`(0.49)에서 clamp 된다 — 반 칸을 넘으면 옆 칸을 침범해
        /// 칸 환산·골 판정·칸 트림이 유닛을 다른 칸으로 본다(M14).
        /// </summary>
        public float SpawnSpreadFraction;

        /// <summary>위쪽(+) 범위만 좁히는 배율. 키 큰 캐릭터 보정. 1 = 대칭.</summary>
        public float SpawnSpreadTopScale;

        /// <summary>
        /// unit 7d — **보스 일반 도약의 비행 창(초).** 이 창 동안 보스는 공격도 자기주도 이동도 못 하고(맞기는 한다),
        /// 창이 끝나는 틱에 착지 슬램이 터진다. 옛 값의 집은 브리지 직렬화 필드(`bossLeapTotalSeconds`)였고 슬램도
        /// 브리지가 뷰 도착 시각에 쐈다 — 그 시각이 판의 규칙이라 판 밖 저작으로 올린다. 뷰는 이 값을 사건으로 받는다.
        /// </summary>
        public float BossLeapFlightSeconds;

        /// <summary>
        /// unit 7d — **분열 자식이 부모 칸 중심에서 퍼지는 반경(칸 폭 비).** 옛 브리지 리터럴 `tileSize * 0.25f` 의 저작 자리.
        /// ⚠ 0.49 미만이어야 자식이 **부모와 같은 칸**에 남는다(옆 칸이 골이면 「처치했는데 유출」 — E1). 0 = 한 점에 겹쳐 난다.
        /// </summary>
        public float SplitSpreadFraction;

        /// <summary>
        /// unit 7d 후속 — **분열 자식 상한**(한 죽음에 서는 자식 수의 천장). 밸런스 값이 아니라 저작 사고 방어선이다
        /// (옛 `BattleBridge.MaxSplitChildren`). 빌더가 저작을 이 값으로 자르고, 코어 `EnemySplit` 이 한 번 더 자른다 —
        /// 고정구·헤드리스는 빌더를 안 지나기 때문이다(M2).
        /// </summary>
        public int SplitMaxChildren;

        /// <summary>
        /// 「기본값이면 canonical 줄을 안 쓴다」의 float 허용 오차. SO 직렬화 왕복(0.83 → 0.83000001)이 1 ulp 를 흔들어도
        /// 해시가 뒤집히지 않게 한다 — 판정 수치가 아니라 **해시 안정성**의 값이다(M1).
        /// </summary>
        public const float CanonicalDefaultEpsilon = 1e-6f;

        /// <summary>옛 씬 값. 「기본값 = 라이브」가 이 표의 계약이다.</summary>
        public static MovementTuningDef Default() => new MovementTuningDef
        {
            AgentRadiusTiles = 0.25f,
            SpawnSubLaneCount = 3,
            SpawnSpreadFraction = 0.2f,
            SpawnSpreadTopScale = 0.5f,
            BossLeapFlightSeconds = 0.83f,
            SplitSpreadFraction = 0.25f,
            SplitMaxChildren = 8,
        };

        internal static bool SameAsDefault(float value, float def) => math.abs(value - def) <= CanonicalDefaultEpsilon;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "agentRadiusTiles", AgentRadiusTiles, inv);
            MatchDefinition.Put(sb, "spawnSubLaneCount", SpawnSubLaneCount, inv);
            MatchDefinition.Put(sb, "spawnSpreadFraction", SpawnSpreadFraction, inv);
            MatchDefinition.Put(sb, "spawnSpreadTopScale", SpawnSpreadTopScale, inv);
            // unit 7d — **옛 값과 같으면 안 쓴다**(고정구·라이브 기본값의 해시 무변 — 칸을 더했다는 사실만으로 골든이 빨개지면 오보다).
            var d = Default();
            // ⚠ float 은 **허용 오차로** 견준다(M1) — `!=` 정확 비교는 SO 왕복의 1 ulp 로 해시를 뒤집는다.
            if (!SameAsDefault(BossLeapFlightSeconds, d.BossLeapFlightSeconds))
                MatchDefinition.Put(sb, "bossLeapFlightSeconds", BossLeapFlightSeconds, inv);
            if (!SameAsDefault(SplitSpreadFraction, d.SplitSpreadFraction))
                MatchDefinition.Put(sb, "splitSpreadFraction", SplitSpreadFraction, inv);
            if (SplitMaxChildren != d.SplitMaxChildren)
                MatchDefinition.Put(sb, "splitMaxChildren", SplitMaxChildren, inv);
        }
    }
}
