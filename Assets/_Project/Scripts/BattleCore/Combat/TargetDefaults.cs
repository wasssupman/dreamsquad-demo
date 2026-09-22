// salvaged from Assets/_Project/Scripts/Battle/Combat/EnemyTargetFilter.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: 컴포넌트는 안 옮겼다(정의표 필드가 됐다). 기본값 규칙만 옮겼다 —
// 이동이 「어느 거점으로 갈까」를 물을 때 같은 마스크를 써야 하기 때문이다.
using Wassup.Battle.Units;

namespace Wassup.BattleCore.Combat
{
    // 적의 기본 타겟 = **상대 진영 전부**.
    //
    // ⚠ **기본값을 열거로 적지 말 것.** 적측만 비트를 하나씩 열거하고 있었고, 그래서 방어
    // 본능이 라이브에 서자 **아무 적도 후보로 보지 못하는 무적 포탑**이 됐다. 그 사고의
    // 교훈은 «본능 비트를 빠뜨렸다» 가 아니라 **«기본값을 열거로 적었다»** 다 — 파생 그룹으로
    // 적으면 방어측 종류가 늘어날 때 이 값이 자동으로 따라간다.
    //
    // `BlockingHazard` 는 `AnyDefender` 밖에 따로 있다 — 방벽은 진영×종류 축의 거점이 아니라
    // «부술 수 있는 벽» 이고, 그 사실을 여기서 감추지 않는다.
    public static class TargetDefaults
    {
        public const int EnemyMask = Factions.AnyDefender | (int)Faction.BlockingHazard;

        public const int DefenderMask = Factions.AnyEnemy;

        /// <summary>0(미저작) = 기본값. 그 외는 저작값을 그대로 존중한다 — 저작은 «이 적은 특수하다»는 선언이다.</summary>
        public static int ResolveEnemy(int authoredMask)
            => authoredMask == 0 ? EnemyMask : authoredMask;

        public static int ResolveDefender(int authoredMask)
            => authoredMask == 0 ? DefenderMask : authoredMask;
    }
}
