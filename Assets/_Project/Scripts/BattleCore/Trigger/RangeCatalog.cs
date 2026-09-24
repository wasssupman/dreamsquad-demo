using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — 「이 규칙은 host 에서 어떤 형·어떤 반경으로 작용하나」
    // (← 옛 `Core/Dreamcatcher/DcRangeCatalog.cs` 137줄 — 카드 단위 `ResolveCard` 는 카드 화면(7c)의 것).
    //
    // 형은 이미 있는 어휘다(제약 13 「효과의 형」): `RangeMetric` 의 `SelfArea`(몸에서 나오는 것) ·
    // `CellArea`(자리에 떨어지는 것) · `Euclidean`(원점 항 0) · `None`(fail-closed). 원점 항 매핑은
    // `SkillMath.TryOriginRadius` **하나**다 — 카탈로그는 반경 N 과 형만 담고, 원점 항은 host 를 아는
    // 자리가 **같은 함수**로 더한다(`RadiusWithOrigin`). 자를 복사하면 표기와 판정이 갈린다.
    //
    // 판정 키는 **concrete + 트리거**다. `DeathSiteBlast` 는 스킬이 「실려 온 자리에서 터진다」만 알고
    // 자리의 주인은 트리거(=감지자)가 정해서 두 형을 겸한다 — `OnDeath` = 시체가 터진다(몸형) ·
    // `OnRetire` = 비워진 칸에 운석이 내린다(자리형). 한 케이스로 묶으면 둘 중 하나가 반드시 틀린다.
    public enum RangeShape : byte { None = 0, Circle = 1 }

    public readonly struct RangeSpec
    {
        public readonly RangeShape Shape;
        /// <summary>**도형 반경 N 뿐이다** — 원점 항은 안 들어 있다(unit 23a).</summary>
        public readonly float RadiusTiles;
        public readonly RangeMetric Metric;

        // ⚠ 기본 인자 없음 — 두면 metric 을 빠뜨린 줄이 조용히 자리형으로 간다(리뷰 M-6).
        public RangeSpec(RangeShape shape, float radiusTiles, RangeMetric metric)
        {
            Shape = shape; RadiusTiles = radiusTiles; Metric = metric;
        }

        public static readonly RangeSpec None = new RangeSpec(RangeShape.None, 0f, RangeMetric.None);

        /// <summary>
        /// 화면에 그릴 반경 = N + 원점 항(`TryOriginRadius`). 매핑이 거절하면 **0 = 안 그림** —
        /// 판정은 후보 0 인데 표기가 원을 그리면 화면이 판정보다 관대해진다(리뷰 L-2/M-1).
        /// 대상 몸은 더하지 않는다 — 「대상 그림자가 링에 닿으면 걸린다」가 판정식과 동치다.
        /// </summary>
        public float RadiusWithOrigin(float hostBodyRadiusTiles)
            => SkillMath.TryOriginRadius(Metric, hostBodyRadiusTiles, out float originR)
                ? RadiusTiles + originR : 0f;
    }

    public static class RangeCatalog
    {
        public static RangeSpec Resolve(TriggerKind trigger, TriggerPayload payload, int tileRange)
            => Resolve(SkillRouting.SkillIdFor(trigger, payload), tileRange, trigger);

        public static RangeSpec Resolve(int skillId, int tileRange, TriggerKind trigger)
        {
            if (skillId == SelfAreaBlastSkill.Id
                || skillId == AreaSleepSkill.Id
                || skillId == AreaCcSkill.Id
                || skillId == AreaDotSkill.Id
                || skillId == AreaStackSkill.Id
                || skillId == AreaTauntSkill.Id
                || skillId == AllySpeedAuraSkill.Id
                || skillId == AllyStatAuraSkill.Id
                || skillId == OpponentStatAuraSkill.Id
                || skillId == GrantShieldSkill.Id)   // 반경 0 = 자기만 → 아래 가드가 None
            {
                // 몸에서 나오는 것 — 같은 카드도 배스티온(1.5)에 붙으면 버스터즈(0.5)보다 1칸 넓다.
                return Circle(tileRange, RangeMetric.SelfArea);
            }
            if (skillId == EmitPatternSkill.Id)
                return Circle(tileRange, RangeMetric.Euclidean);   // 탄 비행 거리 — 원점 항 0
            if (skillId == DeathSiteBlastSkill.Id && trigger == TriggerKind.OnDeath)
                return Circle(tileRange, RangeMetric.SelfArea);    // 시체가 터진다
            if (skillId == DeathSiteBlastSkill.Id && trigger == TriggerKind.OnRetire)
                return Circle(tileRange, RangeMetric.CellArea);    // 비워진 칸에 운석이 내린다
            // `DeathSiteBlast × OnKill`(죽인 적의 자리 — 부착 시점엔 알 수 없다) · `DeathSiteHazard` ·
            // `ConeBreath`(부채꼴은 원이 아니다 — 제외 확정) · 대상형 · 즉발형 · 스탯류 ·
            // 궤도(**범위가 아니다**) · 액티브 칸 조준 · 미배선 전부 — 없는 범위를 지어내지 않는다.
            return RangeSpec.None;
        }

        private static RangeSpec Circle(int tileRange, RangeMetric metric)
            => tileRange > 0 ? new RangeSpec(RangeShape.Circle, tileRange, metric) : RangeSpec.None;
    }
}
