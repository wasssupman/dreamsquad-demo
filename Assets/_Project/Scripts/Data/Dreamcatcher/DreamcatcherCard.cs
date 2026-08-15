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

    // dreamcatcher-attach-requirement unit 0 — 부착 **시점**의 정적 술어(누구에게 붙을
    // 수 있나). 발동 시점의 동적 술어인 DcGateKind 와 레이어가 다르다.
    // unit 7 rev — 값 칸을 하나로 합쳐(attachType + attachValue) 종류별 companion 필드를
    // 없앴다. 이 enum 은 attachValue 를 **어떻게 읽을지**만 정한다. append-only.
    public enum DcAttachType { None, Class, UnitId }

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
        // dreamcatcher-card-visibility unit 0 — 인벤토리 노출 스위치. 0 = 숨김(덱 페이지
        // 컬렉션에서 제외 + 로그인 시 저장 덱에서 장착 해제), 그 외 = 노출. 시트 DcCards
        // 탭의 같은 이름 컬럼과 1:1. 기존 에셋은 YAML 에 이 키가 없어 초기값 1 을 유지하므로
        // 백필하지 않는다(id 처럼 비면 매칭이 깨지는 키가 아니라 기본값이 곧 정답).
        public int visible = 1;
        public CardTargetAxis axis;
        // deck-builder no longer keys deck rules on this (that moved to CardType).
        // gift-phase-removal unit 0 — 림의 선물이 폐지되면서 마지막 **규칙** 소비처(Rim 풀
        // 필터 + 덱빌더 제외)도 사라졌다. 이제 이 필드는 순수한 **시각 라벨**이다:
        // CardCategoryStyle 이 Subconscious 를 보라 프레임 + "무의식" 칩으로 그린다.
        // 덱에 넣을 수 있는지는 오직 DeckRules(10장 · Squad ≤2)와 visible 이 정한다.
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
        // Plain summary mirror for SO inspection/export. Structured data remains the
        // formatter source of truth; unsupported effect/mechanic/skill combinations
        // may use this text as a fallback. Definition layer stays ECS-free.
        // 끝에 추가 → 기존 카드 에셋은 빈 문자열로 역직렬화(inert).
        [TextArea] public string description;
        // subconscious-curse-expansion unit 1 (몽마의 계약) — 부착 커밋 시 선불로
        // 지불하는 유출 허용치(0 = 없음). SO 는 불변 — 지불은 BattleBridge 런타임
        // 오프셋(_leakAllowancePenalty)으로만 반영되고 환불되지 않는다(§6 리스크
        // 선불·세탁 차단). 끝에 추가 → 기존 카드 에셋은 0 으로 역직렬화(inert).
        public int leakAllowanceCost;
        // dreamcatcher-attach-requirement unit 0(+unit 7 rev) — 부착 대상 제한.
        // defender에 부착되는 type=Unit/Squad 경로가 소비한다. Squad에서는 실제 버프
        // 수혜 axis와 별개인 수명 앵커 제한이다. BountyMark는 적 타겟이라 이 게이트를 안 탄다.
        // 판정은 DreamcatcherAttachEval.MeetsAttachRequirement 한 곳.
        // 끝에 추가 → 기존 카드 에셋은 None/빈문자열로 역직렬화(제한 없음).
        //
        // 시트에서 제한을 되돌릴 때는 attachType 에 None 을 **명시**해야 한다 —
        // 빈 셀은 blank=keep 이라 해제가 아니다.
        public DcAttachType attachType;
        // attachType 이 해석 방식을 정한다:
        //   Class  → DefenderClass 이름("Guardian", 대소문자 무시). 파싱 실패/None = fail-closed.
        //   UnitId → DefenderUnitData.id(저장용 안정 키)와 **ordinal** 비교. 표시명/에셋명 아님.
        // 빈 문자열이면 두 경우 다 fail-closed(어디에도 안 붙음).
        // 값 칸이 하나뿐이라 "종류를 바꿨는데 옛 값이 되살아나는" 함정이 구조적으로 없다
        // (unit 7 rev 의 동기 — 구 3필드 설계의 잔존 companion 문제).
        public string attachValue;

        // 적 지정 판별 — 전용 필드 없이 mechanics 파생(BountyMark payload 보유 = 적 타겟).
        // 조준 라우팅(DreamcatcherCardDragSlot.Classify)과 손패 태그 칩
        // (CardCategoryStyle.TargetTag)이 공유하는 단일 소스. 관리 배열 순회 —
        // bake/UI 시점 전용, per-frame 호출 금지(mechanics 주석과 동일 규칙).
        // ⚠ 예외 하나(review): 에디터 인스펙터(DreamcatcherCardEditor → validator)는
        // repaint 마다 이 메서드를 부른다. 배열 몇 칸이라 에디터에선 무관하지만, 여기에
        // 실제 비용이 있는 작업을 넣으면 인스펙터가 그 비용을 매 프레임 문다.
        public bool HasBountyMark()
        {
            if (mechanics == null) return false;
            for (int i = 0; i < mechanics.Length; i++)
                if (mechanics[i].payload.kind == DcPayloadKind.BountyMark) return true;
            return false;
        }
    }
}
