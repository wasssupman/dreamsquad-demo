using System;
using UnityEngine;

namespace Wassup.Data
{
    // ingame-dreamcatcher Unit 1 — which allied units a card targets.
    // dreamstone-loadout Unit 3 — All appended at the end (existing DreamcatcherCard
    // assets serialize axis as int 0~2; inserting earlier would relabel them).
    public enum CardTargetAxis { ClassRanger, ClassGuardian, Cost1, All }

    // ingame-dreamcatcher Unit 1 — what a card buffs. Maps to StatModifier in
    // Unit 2: AttackDamage→DamageMul, AttackSpeed→AttackSpeedMul,
    // EffectiveHealth→DmgTakenMul (damage-taken reduction proxy), MoveSpeed→MoveSpeedMul.
    // dreamstone-loadout Unit 6 — CostRate appended at the end (existing card/stone
    // assets serialize kind as int 0~3; inserting earlier would relabel them).
    // CostRate has no StatModifier/entity mapping — BattleBridge.MapDcEffect's
    // switch has no case for it and safely no-ops via its default branch; the value
    // is consumed entirely by GameManager -> CostRuntime.SetRegenRateMultiplier.
    // dreamcatcher-new-abilities unit 0 — DamageVsCc: 활성 CcEffect(기절/수면/DoT/넉백)가
    // 걸린 적에게 추가 피해 %. ⚠ 이동감속(Slow)은 이 엔진에서 CcEffect 가 아니라 MoveSpeedMul
    // StatModifier 라 여기 해당 없음(카드 문안에 "둔화" 표기 금지). StatKind.DamageVsCcMul
    // 로 매핑(unit 2). append-only.
    public enum CardBuffKind { AttackDamage, AttackSpeed, EffectiveHealth, MoveSpeed, CostRate, DamageVsCc }

    // dreamcatcher-deck-builder Unit 0 — deck-rule category (deck cap RETIRED, now
    // on CardType.Squad). Reused as a concept label: dreamcatcher-squad-warmup adds
    // Subconscious(무의식). deck-builder no longer colors/labels by this (dormant for
    // frame color); a card may still declare its concept here. Append at end.
    public enum CardCategory { Normal, Unique, Subconscious }

    // dreamcatcher-card-taxonomy — Squad(축 스탯 버프) / Unit(개별 부착 메커니즘).
    // The deck cap now keys on this (Squad ≤2), not on CardCategory. Default 0
    // = Squad preserves existing stat cards without touching their assets.
    // dreamcatcher-taxonomy-cleanup — the sole authoritative taxonomy field.
    // Runtime scope derives from it (Unit = host-attached mechanics / else =
    // axis-set stat buff); the old redundant CardBinding was removed.
    // dreamcatcher-awakening-hand unit 0 — Active appended at the end (common
    // per-match dreamcatchers wrapping a SkillData; the skill field arrives in
    // unit 2). Appending keeps existing assets' serialized ints stable.
    public enum CardType { Squad, Unit, Active }

    [Serializable]
    public struct CardEffect
    {
        public CardBuffKind kind;
        public float percent; // +10 = +10%, -50 = -50%
    }

    [CreateAssetMenu(fileName = "DreamcatcherCard", menuName = "Wassup/DreamcatcherCard", order = 20)]
    public class DreamcatcherCard : ScriptableObject
    {
        public string id;
        public string displayName;
        public CardTargetAxis axis;
        // deck-builder no longer keys deck rules on this (that moved to CardType), but it
        // is load-bearing: DeckBuilderView reads category==Subconscious for the 무의식
        // frame/art-fallback color, and gift-phase uses it for the Rim(림의 선물) 풀 필터 +
        // 덱빌더 제외.
        public CardCategory category = CardCategory.Normal;
        public CardEffect[] effects; // usually 1; fortress has 2
        // dreamcatcher-card-art Unit 0 — tarot-style card art shown on the deck
        // page (image + effect text column). Nullable: view falls back to a
        // category color when unassigned. Appended last to keep serialization
        // order stable for existing card assets.
        public Sprite art;
        // dreamcatcher-unit-trigger Unit 0 — appended last to keep serialization
        // order stable for existing card assets (mechanics deserialize as empty).
        // effects[] and mechanics[] may coexist, but the current interpretation
        // path consumes mechanics only for type=Unit cards (Squad/axis apply stays
        // effects-only). Bake-time read only — never iterate mechanics per-frame
        // (managed array). dreamcatcher-taxonomy-cleanup — scope keys on CardType
        // now (the redundant CardBinding was removed).
        public DcMechanic[] mechanics;
        // dreamcatcher-attack-mod-bounce Unit 0 — card class (c): always-on
        // attack-output modifications (usually 0~1). Appended last; bake-time
        // read only, same rules as mechanics above.
        public DcAttackModSpec[] attackMods;
        // dreamcatcher-card-taxonomy — Squad/Unit type. Deck cap keys on this.
        // Appended last; zero-init = Squad for existing stat cards.
        public CardType type;
        // dreamcatcher-awakening-hand unit 2 — the SkillData an Active-type card
        // wraps (common per-match dreamcatcher; cast via the existing skill
        // pipeline, cost paid in awakening). Only meaningful when type==Active;
        // other types ignore it. SkillData is a pure-data SO, so the definition
        // layer stays ECS-free. Appended last (existing assets deserialize null).
        public SkillData skill;
        // dreamcatcher-card-description Unit 0 — authored 효과/메커니즘 설명. 덱빌더
        // 상세 팝업에서 자동 수치라인(effects[]) 아래에 렌더된다(빈 값이면 블록 생략).
        // effects[] 자동생성이 못 덮는 Unit(mechanics/attackMods)·Active(skill) 카드의
        // 유일한 읽을 수 있는 설명 소스. 순수 데이터(문자열) — 정의 계층 ECS-free 유지.
        // 끝에 추가 → 기존 카드 에셋은 빈 문자열로 역직렬화(inert).
        [TextArea] public string description;
    }
}
