using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6b2 — 기믹 셈판 테스트의 공용 고정구.
    //
    // ⚠ 여기 수치는 **게임 값이 아니라 픽스처**다(`CoreCombatFixtures` 와 같은 규율). 라이브 값은
    // 기믹 SO → `MatchDefinitionBuilder` → `GimmickDef` 이고, 그 경로는 에셋 lane
    // (`GimmickAuthoringTests`)이 감시한다.
    public static class CoreGimmickFixtures
    {
        /// <summary>그 기믹 **하나만** 후보로 두고 켠다 — 후보가 하나면 시드와 무관하게 그것이 뽑힌다.</summary>
        public static MatchDefinition With(MatchDefinition def, GimmickDef gimmick)
        {
            def.Gimmicks = new[] { gimmick };
            def.Mode.GimmickEnabled = true;
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        public static GimmickDef RedBull(float lifetime = 2f, int maxActive = 6, float mul = 1.5f,
                                         float duration = 1f, float fraction = 0.5f)
            => new GimmickDef
            {
                Id = "fixture_redbull",
                Kind = GimmickKind.RedBull,
                RedBull = new RedBullSpec
                {
                    SpawnInterval = 3f,
                    Lifetime = lifetime,
                    MaxActive = maxActive,
                    LastRunAttackSpeedMul = mul,
                    LastRunDuration = duration,
                    LastRunDamageFraction = fraction,
                },
            };

        public static GimmickDef ClockOut(int threshold = 3, int meteorCount = 4, int meteorProjectile = -1)
            => new GimmickDef
            {
                Id = "fixture_clockout",
                Kind = GimmickKind.ClockOut,
                ClockOut = new ClockOutSpec
                {
                    ResignationThreshold = threshold,
                    MeteorCount = meteorCount,
                    MeteorDamage = 10f,
                    MeteorTileRange = 1,
                    MeteorWarningSec = 1f,
                    MeteorStaggerSec = 0.2f,
                    MeteorProjectileDefIndex = meteorProjectile,   // -1 = 없음(S4 — 0 은 유효 줄이다)
                },
            };

        public static GimmickDef Onsen(int flip = 2, float heal = 0.1f, float loss = 0.1f, int max = 6)
            => new GimmickDef
            {
                Id = "fixture_onsen",
                Kind = GimmickKind.Onsen,
                Onsen = new OnsenSpec
                {
                    HeatInterval = 1f,
                    FlipThreshold = flip,
                    HealPercent = heal,
                    LossPercent = loss,
                    HeatMaxStack = max,
                },
            };

        public static GimmickDef Burnout(int amount = 1, int rule = -1)
            => new GimmickDef
            {
                Id = "fixture_burnout",
                Kind = GimmickKind.Burnout,
                Burnout = new BurnoutSpec
                {
                    FatigueInterval = 1f,
                    FatigueAmount = amount,
                    FatigueStackRule = rule,
                },
            };
    }
}
