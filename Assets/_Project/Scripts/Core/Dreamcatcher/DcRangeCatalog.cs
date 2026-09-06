using UnityEngine;
using Wassup.Data;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Core
{
    // dreamcatcher-attach-range-preview unit 1 — 「이 카드는 host 에서 어떤 도형·어떤 반경으로 작용하나」.
    public enum DcRangeShape : byte { None = 0, Circle = 1 }

    public readonly struct DcRangeSpec
    {
        public readonly DcRangeShape shape;
        // ★ **unit 23a — 여기 든 것은 «도형 반경 N» 뿐이다.** 원점 항은 **안 들어 있다.**
        // 종전엔 `N + 칸 반폭` 이 통째로 들어 있었는데, 그러면 host 몸이 반경을 바꾸는 형
        // (자기중심 광역)을 표현할 수 없다 — 카탈로그는 host 를 모르기 때문이다.
        // 화면에 그릴 값은 `RadiusWithOrigin(host 몸)` 이다.
        public readonly float radiusTiles;
        // 원점 항을 무엇으로 더할지 — **판정과 «같은 매핑»을 쓴다**(`SkillMath.TryOriginRadius`).
        // 여기서 상수를 다시 쓰면 표기와 판정이 갈리고, 그게 unit 5 의 「화면이 규칙을 틀리게
        // 가르친다」다. 그래서 자를 복사하지 않고 **같은 함수를 부른다.**
        public readonly RangeMetric metric;
        // ⚠ **기본 인자를 두지 않는다**(리뷰 M-6). 두면 `ISkillContext` 가 세운 `None = 0`
        // fail-closed 규율의 정반대가 된다 — arm 을 추가하며 metric 을 빠뜨리면 **조용히 자리형**으로 간다.
        public DcRangeSpec(DcRangeShape shape, float radiusTiles, RangeMetric metric)
        { this.shape = shape; this.radiusTiles = radiusTiles; this.metric = metric; }
        public static readonly DcRangeSpec None = new DcRangeSpec(DcRangeShape.None, 0f, RangeMetric.None);
        public bool Equals(in DcRangeSpec o) => shape == o.shape && radiusTiles == o.radiusTiles && metric == o.metric;

        // 화면에 그릴 반경 — host 몸을 아는 자리(브리지)가 부른다.
        // ⚠ 매핑이 거절하면(`None`/은퇴한 자) **0 을 돌려 「안 그림」으로 접는다**(리뷰 L-2/M-1).
        // 종전엔 bare radius 를 돌려줘, 판정은 후보 0(fail-closed)인데 표기는 원을 그리는
        // **방향이 반대인** 상태였다. 화면이 판정보다 관대하면 그게 곧 「규칙을 틀리게 가르친다」다.
        public float RadiusWithOrigin(float hostBodyRadiusTiles)
            => SkillMath.TryOriginRadius(metric, hostBodyRadiusTiles, out float originR)
                ? radiusTiles + originR : 0f;
    }

    // 공간성 카탈로그 — **concrete(skillId) 로 판정**하고 값은 보지 않는다. 예외 하나:
    // DeathSiteBlast 는 concrete 가 「실려 온 자리에서 터진다」만 알고 **자리의 주인은 트리거가
    // 정하므로**(죽인 자리 ↔ 자기 자리) 트리거까지 본다 — 아래 Resolve 3-인자 판 참조.
    //
    // `tileRange` 는 kind 별로 7가지 다른 뜻을 갖는다(피해감소% · 누적 상한 · maxStack · 폴백 반경 · 궤도 반경 ·
    // 실드 0 = 자기만 · 팅김 탐색 반경). 시트(`DcSheetApplier`)가 값을 매 로그인마다 덮으므로 「30 이면
    // 퍼센트겠지」 식 값 추정은 다음 시트 편집에 깨진다. 모르는 concrete 는 None — **없는 범위를 지어내지
    // 않는다**(fail-closed).
    //
    // 반경은 **판정 입력의 복사본**이다 — unit 23a 부터는 «복사» 가 아니라 **같은 함수**를 부른다
    // (`DcRangeSpec.RadiusWithOrigin` → `SkillMath.TryOriginRadius`). 자를 두 벌 두면 갈린다.
    // 카탈로그가 담는 것은 **도형 반경 N + 형**이고, 원점 항은 host 를 아는 자리(브리지)가 더한다.
    // 대상 몸(targetR)은 그리지 않는다 — 「대상 그림자가 링에 닿으면 걸린다」가 판정식과 동치라서다.
    //
    // ECS 무참조 · bake/UI 시점 전용(managed SO 읽기) — per-frame 호출 금지. `DcApplicability` 와 같은 자리.
    public static class DcRangeCatalog
    {
        // 트리거 문맥이 없는 호출부용 — DeathSite 계열은 자리의 주인을 몰라 fail-closed(None)로 접힌다.
        public static DcRangeSpec Resolve(int skillId, int tileRange)
            => Resolve(skillId, tileRange, DcTriggerKind.None);

        public static DcRangeSpec Resolve(int skillId, int tileRange, DcTriggerKind trigger)
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
                || skillId == GrantShieldSkill.Id)          // 0 = 자기만 → 아래 가드가 None 으로 접는다
            {
                // **몸에서 나오는 것** — 원점 항 = host 몸. 그래서 같은 카드도 배스티온(1.5)에
                // 붙으면 버스터즈(0.5)보다 1칸 넓다(제약 13).
                return tileRange > 0
                    ? new DcRangeSpec(DcRangeShape.Circle, tileRange, RangeMetric.SelfArea)
                    : DcRangeSpec.None;
            }
            if (skillId == EmitPatternSkill.Id)
            {
                // 탄 비행 거리 — 원점 항 없음.
                return tileRange > 0
                    ? new DcRangeSpec(DcRangeShape.Circle, tileRange, RangeMetric.Euclidean)
                    : DcRangeSpec.None;
            }
            // 사망폭발·퇴근 운석(사용자 결정 2026-09-03) — concrete 는 같은 DeathSiteBlast 지만
            // **자기 사망/퇴근 트리거는 자리의 주인이 부착 유닛 자신**이고, 배치 유닛은 움직이지
            // 않으므로 「지금 서 있는 자리 중심 원」이 거짓말이 아니다. 반경은 착탄식 복사본
            // (N + 칸 반폭) — SelfAreaBlast 와 같은 자다. 처치(OnKill) 트리거는 죽인 **적**의
            // 자리라 부착 시점에 알 수 없어 그대로 None(아래 fail-closed).
            // ⚠ **unit 23 에서 이 둘의 «형» 이 갈렸다**(제약 13, 사용자 결정 2026-09-06):
            //   `OnDeath`  = **몸에서 나오는 것** — 죽은 그 유닛이 터진다 → 원점 항 = host 몸
            //   `OnRetire` = **자리에 떨어지는 것** — 운석이 «비워진 칸» 에 내린다. 퇴근한 유닛이
            //                그것을 «불렀을» 뿐이라 그 유닛의 몸은 안 붙는다(지정은 귀속이지 기하가 아니다).
            // 한 케이스로 묶어 두면 둘 중 하나가 반드시 틀린다.
            if (skillId == DeathSiteBlastSkill.Id && trigger == DcTriggerKind.OnDeath)
            {
                return tileRange > 0
                    ? new DcRangeSpec(DcRangeShape.Circle, tileRange, RangeMetric.SelfArea)
                    : DcRangeSpec.None;
            }
            if (skillId == DeathSiteBlastSkill.Id && trigger == DcTriggerKind.OnRetire)
            {
                return tileRange > 0
                    ? new DcRangeSpec(DcRangeShape.Circle, tileRange, RangeMetric.CellArea)
                    : DcRangeSpec.None;
            }
            // DeathSiteBlast(처치 트리거 — 죽인 적의 자리) · DeathSiteHazard(동일 — 카드도 잿불뿐) ·
            // ConeBreath(부채꼴 제외 확정) · 대상형·즉발형·스탯류·궤도(범위가 아니다) ·
            // TileStatBurst(액티브 칸 조준 — 부착 페이로드 아님) · 미배선 전부.
            return DcRangeSpec.None;
        }

        // 카드 단위 — `mechanics` 를 돌며 첫 공간 spec 을 고른다. 공간 spec 이 2개 이상 서로 다르면 첫 것을
        // 쓰고 1회 경고한다(라이브 안전망). 정식 방어는 에셋 lane 의 단일 도형 불변식 테스트(README 계약 6).
        // `attackMods[].tileRange`(팅김 탐색 반경)는 착탄점 기준이라 host 중심 범위가 아니다 — 보지 않는다.
        // 경고는 **카드당 1회**(리뷰 L-9) — 드래그마다 호출되므로 호출당 1회면 로그가 스팸이 된다.
        private static readonly System.Collections.Generic.HashSet<string> WarnedCards = new();

        public static DcRangeSpec ResolveCard(DreamcatcherCard card)
        {
            if (card == null || card.mechanics == null) return DcRangeSpec.None;
            var first = DcRangeSpec.None;
            for (int i = 0; i < card.mechanics.Length; i++)
            {
                var m = card.mechanics[i];
                var spec = Resolve(DcSkillRouting.SkillIdFor(m.trigger.kind, m.payload.kind), m.payload.tileRange, m.trigger.kind);
                if (spec.shape == DcRangeShape.None) continue;
                if (first.shape == DcRangeShape.None) { first = spec; continue; }
                if (!first.Equals(spec) && WarnedCards.Add(card.id ?? ""))
                    Debug.LogWarning($"[DcRangeCatalog] '{card.id}': 공간 페이로드가 둘 이상이고 도형이 다르다 — "
                                     + "첫 것만 그린다. 범위 채널은 하나다(단일 도형 불변식).");
            }
            return first;
        }
    }
}
