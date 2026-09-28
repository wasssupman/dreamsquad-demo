using System.Collections.Generic;
using Wassup.Skills;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7a — 규칙 하나의 **부착된 인스턴스**. 정의(`BindingDef`)는 판 밖 값이고,
    // 이것은 그 규칙이 그 소유자에게 붙어서 생긴 **상태**(카운터·경계 래치·주기 누적·발동 수·수명)다.
    //
    // 개체가 아니다 — 소유자의 등록부 항목이다(`Unit.Bindings` / Match 목록). 그래서 소유자가 사라지면
    // 같이 떨어지고(소멸 사건 1건씩), 판 사이에 살아남지 않는다.
    public sealed class Binding
    {
        public BindingDef Def;

        /// <summary>
        /// skill-data-table unit 1a — 이 규칙의 **효과 값**(붙는 순간 `MatchDefinition.EffectOf` 로 해석한 사본). `Def` 와 같은
        /// 성질의 값이다 — 인스턴스는 정의의 사본을 들고, 효과 값은 이 칸에서만 읽는다(`Def` 의 인라인 효과 칸이 아니다).
        /// </summary>
        public EffectDef Effect;

        /// <summary>`MatchDefinition.Bindings` 의 줄. 런타임에 조립한 규칙(카드 — 7b)은 -1.</summary>
        public int DefIndex = -1;

        /// <summary>소유자. `SimEntityId.Match`(0) = 판 호스트.</summary>
        public SimEntityId Owner = SimEntityId.None;

        /// <summary>
        /// skill-data-table unit 2 — **이 규칙을 건 쪽의 진영**. 붙인 쪽이 채운다(유닛 저작 = 그 유닛 · 카드·액티브·드림스톤 =
        /// 손패의 주인 = 플레이어). 주체 없는 시전(판 시전 · 판 주기 · 표식 — 사건 주체가 `SimEntityId.Match`)의 시전 진영은
        /// 이 값에서만 나온다 — 감지자·드레인이 진영을 손으로 박지 않는다.
        /// </summary>
        public Faction CastFaction = Faction.None;

        /// <summary>
        /// **판 안에서 단조 증가 · 재사용 없음**(F1). 억제 키(E2)이자 카드 슬롯 판별자(6a `SlotTag.OfBinding`)다.
        /// 재사용하면 낡은 핸들이 새 대상을 가리킨다.
        /// </summary>
        public int InstanceId;

        /// <summary>부착 순번(판 안). 같은 소유자 안에서 `InstanceId` 와 같은 순서다.</summary>
        public int Seq;

        public int FireCount;

        // 트리거 상태 — 종류마다 쓰는 칸이 다르다(겸직 없음).
        /// <summary>`AttackN`·`OnDamagedN` 의 N 카운터.</summary>
        public int Counter;
        /// <summary>`PeriodicTimer` 누적(잔여 이월 — 드리프트 없음).</summary>
        public float Elapsed;
        /// <summary>`HealthThreshold` 래치 k(1 부터, 단조 전진 — 회복으로 되감기지 않는다).</summary>
        public int NextBoundary = 1;
        /// <summary>`HealthThreshold` 기준 최대 체력 — **부착(스폰) 시점 스냅샷**.</summary>
        public float MaxHpRef;
        /// <summary>`Timed` 수명의 남은 초.</summary>
        public float Remaining;

        /// <summary>떨어졌나. 떨어진 뒤에도 이미 줄 선 발동은 이 플래그를 보고 버린다.</summary>
        public bool Detached;

        /// <summary>
        /// 발사 명세 버스트(`EmitProjectilePattern`). 한 바인딩의 버스트가 겹칠 수 있어(주기 &lt; 버스트)
        /// 목록이고, 끝난 인스턴스는 돌려쓴다. `FireCountBase` 는 **durable** — 버스트마다 0 에서 시작하면
        /// 순회 선정이 영원히 같은 순위를 고른다.
        /// </summary>
        public readonly List<PatternSlotState> Emitters = new List<PatternSlotState>(1);
        public int PatternFireCountBase;

        public SimEntityId Host => Owner;
    }
}
