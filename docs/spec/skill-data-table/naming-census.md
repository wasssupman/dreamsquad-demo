# skill-data-table — 명칭 전수조사 (ledger)

> 실측 2026-09-28 · 브랜치 `unified-effect-layer` · 범위 = `Scripts/{BattleCore,Skills,BattleCoreUnity,Data,Core}` · `Editor/{UnitStatImport,BattleCore}` · `Tests/**` 중 스킬·트리거·효과·소유 파이프라인. 코드 변경 0 — 이 문서는 **개명 후보 원장**이고 결정은 각 unit 이 한다.
> 원칙(사용자): **시트(기획자 층)는 소유자 종류를 구분해도 된다 · 코드(구현 층)에서 소유자 간 공유되는 것은 소유자 이름을 달지 않는다.**
> 표기: `occ/파일(테스트)` = `grep -w` 출현 수 / 파일 수(그중 테스트 파일). 파일:줄은 이 커밋 기준.

## 0. 용어집 (정본 어휘 — U6 정렬)

| 개념 | 한국어(문서·시트) | 코드 정본 | 폐기·금지 어휘 |
|---|---|---|---|
| 효과 줄(스킬의 정체 · U6) | 효과 | `EffectDef`(코어 · unit 1a) · `EffectData`(저작 SO · unit 4) · `effect_id` | `DcPayloadSpec` · `SkillData`(액티브 흡수) · 「payload」 |
| 효과 종류 | 효과 종류 | `EffectKind` | `TriggerPayload` · `DcPayloadKind` · `SkillEffectType` |
| 효과 실행자(concrete) | 실행자 | `ISkill` · `*Skill`(`Wassup.Skills`) · 코어 로컬 = `ICoreEffect` | `BindingDef.Effect`(실행자인데 「효과」) |
| 소유 줄(언제 → 효과 id) | 소유 줄 · (시트) 스킬 | 코어 `BindingDef`(정의) · `Binding`(판 위 인스턴스) · 저작 `BindingSpec` · 소유자 필드 `bindings` | `DcMechanic` · `mechanics` · `nightmareMechanics` · 「Mechanic」 · 가칭 `SkillRule`(Rule 은 검증 어휘) |
| 트리거 | 트리거 | `TriggerKind` · 저작 `TriggerSpec` | `DcTriggerKind` · `DcTriggerSpec` |
| 주체 · 게이트 | 주체 · 게이트 | `BindingSubject` · `GateKind` · `GateSubject` | `DcTriggerSubject` · `DcGateKind` · `DcGateSubject` |
| 소유자 · 소유자 종류 | 소유자(카드 · 방어유닛 · 적 · 기믹 · 판) | `OwnerKind`(현 `BindingOrigin`) · `Binding.Owner` | `UnitAuthored` · 「Nightmare」 · 「UnitSkill」 |
| 숙주 | 숙주(카드가 붙는 유닛) | `Host*`(`HostProfile` · `HostKinds`) — 카드 관점에서만 | 소유자와 혼용 금지(`Binding.Host => Owner` 별칭은 카드 관점) |
| 출처 꼬리표(누가 걸었나) | 출처 | `ModifierOrigin` · `SkillModifierOrigin` · `DotOrigin`(유지 · 후속 `*Source` 후보) | 실행자가 소유자 출처를 **박는 것**(B11) |
| 원점(기하) | 원점 · 발사 자리 · 효과 좌표 | `SkillOrigin` · `OriginBodyRadius` · `TryOriginRadius`(제약 13) | 출처 뜻으로 「Origin」 신설 금지 |
| 탄두(투사체 착탄 형) | 탄두 | `ProjectilePayload` · `PayloadKind` | 효과 종류에 「payload」 |
| 규칙(검증·순수 규칙) | 검증 규칙 | `EffectComboRule` · `StackRules` · `DeckRules` | 소유 줄에 「Rule」 |
| 공격 수식자 | 공격 수식자 | `AttackModDef`(코어) · 카드 저작 `CardAttackModSpec` | `DcAttackModSpec` |
| 카드(소유자) | 카드 · 드림캐쳐(플레이어 언어) | 코드 접두어 = `Card`(코어 `CardDef` · `HandDeck` · 시트 `Cards` 와 같게) | 같은 소유자에 `Dc` · `Dreamcatcher` · `Card` 세 접두어 혼용 |

## A. 소유자 전용 — 유지

| # | 개념 | 식별자(대표) | occ/파일 | 유지 이유 |
|---|---|---|---|---|
| A1 | 카드 자원 · 손패 | `HandDeck` · `CardDef` · `CardKind` · `CardAttachment` · `Command.AttachCard/CastActive` · `CoreEventKind.CardAttached/CardDetached/CardCast`(60~62) · `CoreHand*` · `CardSlot` · `CardInput` | — | 플레이어 카드 자원(계약 3 — 판 자원 담당자가 진영을 본다) |
| A2 | 카드 덱·각성 설정 | `AwakeningConfig` · `DeckRuleConfig` · `DeckRules` · `DreamcatcherDeck` · `DreamcatcherCycleDeck` · `DreamcatcherCardCatalog` | — | 아웃게임·손패 규칙 |
| A3 | 부착 규칙 | `CardBindings`(`Plan` · `FireOnAttach` · `IsMarked` — `CardBindings.cs:143` 의 `Origin == Card`) · `Applicability`(`HostProfile`) · `DcAttachType` · `attachType/attachValue` · `DreamcatcherAttachEval.{MeetsAttachRequirement, HasInvalidAttachRequirement, TryParseAttachClass}` · `DcAttachRequirementValidator` · `BindingSubjectFilter.PlacedDefender` | 17/9 · 69/13 | 부착 관문 = 게임 규칙(계약 4 부여 게이트). 배치는 방어유닛만 |
| A4 | 카드 고유 저작 | `DreamcatcherCard`(SO) · `CardType` · `CardCategory` · `CardTargetAxis` · `leakAllowanceCost` · `attackMods`(`DcAttackModSpec` 14/7 · `DcAttackModKind` 46/14) | — | `tables.md` §7 — 카드 고유 값. 공격 수식자는 재사용 소유자 0(§11) · 접두어만 C8 |
| A5 | 스쿼드 | `CardType.Squad` · `BakeSquad` · `SquadDeck/SquadDraw/SquadPreset` | — | 스쿼드 카드 전개 · 로스터 |
| A6 | 드림스톤 | `DreamstoneData` · `DreamstoneStatSkill` · `BakeDreamstones` · `ModifierOrigin.Dreamstone` | — | 범위 밖(`tables.md` §11) |
| A7 | 기믹 | `GimmickBindings` · `ICoreEffect` 구현 · `GimmickKind` · `ResignationBarrage` · `BindingOrigin.Gimmick` · `ModifierOrigin.Gimmick/Burnout` | 14/4 | 닫힌 축(제약 8 — 시즌 기믹 4종) |
| A8 | 적 웨이브 · 분열 | `WaveGenerator` 계열 · `SplitChain` · `SplitOnDeath`(→ `Enemies.split_*`) | — | 적 고유 값 |
| A9 | 방어유닛 평타 능력 | `DefenderAbilityData` · `BombThrow/DirectionalVolley/SummonPatrol/HazardCastAbility` | 10/8 | 평타 경로(`CombatDefinitionBuilder`) · 이 spec 무관 |
| A10 | 출처 꼬리표를 읽는 표시 | `CoreVfxSpawner.OnTriggerFired`(`:441` `Origin != Card` → 카드 발동 임팩트) · `CoreUnitOverheadUiLayer:108`(카드 줄 펄스) · `DcVisualConfig`(4/2 — 카드 발동 임팩트 간격) | — | ⑪ 「출처 = 꼬리표」 — 표시가 소유자를 구분하는 것은 허용. 단 「사용자 확인」 4 |
| A11 | 카드 UI · 아웃게임 | `Scripts/UI/Dreamcatcher/*` · `DreamcatcherCardText` · `DcInspectPanelView` | — | 플레이어 표면 — 손대면 「사용자 확인」 1 |
| A12 | 카드 단위 자가진단 | `CardProbe`(31/17(13)) · `CardProbeResult/Options` · `CoreCardSelfCheckMenu` · `CoreCardSnapshotMenu` · `CardBakeSnapshotTests` · `CardEffectWitnessTests` | — | 진단 단위 = 카드. 유닛·적 규칙 프로브는 후속 후보 |
| A13 | 시나리오 테스트 이름 | `HardCaseDreamcatcherOnEnemyProbeTests` · `HardCaseBossSkillOnDefenderProbeTests` · `DreamCocoonTests` · `BountyMarkTests` | — | 시나리오를 이름으로 말한다 |

## B. 공유인데 소유자 이름 — 개명 후보

직렬화 위험: `[Serializable]` struct·enum **타입 이름은 YAML 에 안 남는다**(필드 키 + enum 정수만). `MonoBehaviour`/`ScriptableObject` 클래스 개명 = 파일명 동시 개명 + `.meta` 보존(GUID 유지)이면 씬·에셋 참조 무사. 해시(`BindingDef.Canonicalize`)는 **키 문자열**(`"payload"` · `"skill"` · `"origin"` …)과 enum **정수**만 싣는다 → C# 식별자 개명은 해시 무관, 키 문자열·정수를 바꾸면 재베이크.

| # | 현 이름 | 제안 | occ/파일(테스트) | 직렬화 · 해시 위험 | unit |
|---|---|---|---|---|---|
| B1 | `DcMechanic`(`Data/Dreamcatcher/DcMechanic.cs:417`) | `BindingSpec`(트리거 · 주체 · 게이트 + 효과 SO 참조) | 51/21(11) | 타입명 YAML 무관 · 내용물이 바뀌어 **이전 스크립트**가 맡는다(4 문서) | 4 |
| B2 | `DcTriggerSpec` | `TriggerSpec` | 25/9(6) | 없음(필드 키 `trigger` 유지 시) | 4 |
| B3 | `DcPayloadSpec` | 은퇴 → `EffectData` SO | 32/14(10) | YAML `payload:` 블록 → 효과 SO 로 이전(dedupe · dry-run) | 4 |
| B4 | 거울 enum `DcTriggerKind` · `DcPayloadKind` · `DcGateKind` · `DcGateSubject` · `DcTriggerSubject` | 코어 `TriggerKind` · `EffectKind` · `GateKind` · `GateSubject` · `BindingSubject` 직접 사용 | 194/27(16) · 225/33(18) · 41/12(4) · 29/10(4) · 10/4(2) | **정수 동일**(Trigger 0~9 · Payload 0~32 · Gate 0~1 · Subject 0~1) → 저장 정수 변환 불필요. 번역 함수 `ToCore*` · `CoreTriggerEnumPinTests` 제거 | 4 |
| B5 | `DcCcKind {Stun,Impulse,Sleep}` | `Data.Authoring.CcKind`(= `SkillCcKind`) | 30/12(5) | **정수 변환 필요**: Stun 0→3 · Impulse 1→1 · Sleep 2→4 | 4 |
| B6 | `DcStackKind {Fire,Ice,Bleed,Poison}` | `Data.Authoring.StackKind` | 37/10(4) | **정수 +1**(코어는 `None = 0`) | 4 |
| B7 | `CardBuffKind`(payload `buffStat` — 유닛 오라 `AllyStatAura`·적도 쓴다) | `BuffStat`(타입만) | 82/17(8) | 정수 불변 · YAML 무관. `StatKind` 로 합치지 말 것(`EffectiveHealth` · `CostRate` 는 `StatKind` 에 없다) | 4 |
| B8 | 소유자 필드 `DreamcatcherCard.mechanics` · `UnitSkillAbility.mechanics` · `AttackUnitData.nightmareMechanics`(`:241`) | 세 소유자 SO 공통 `bindings` | 176/34(18) · 27/13(7) | **YAML 키** — `mechanics:` 71 에셋 · `nightmareMechanics:` 24 에셋. 원소 타입이 바뀌므로 `[FormerlySerializedAs]` 로 해결 안 됨 → 이전 스크립트 + 옛 필드 제거 | 4 |
| B9 | 규칙 레일 능력 `UnitSkillAbility` · `ShieldCastAbility`(코드가 굽는 넷째 저장처 `BindingDefinitionBuilder.cs:67`) | 은퇴 → 방어유닛 `bindings` | 12/9(4) · 5/4(1) | 능력 에셋(규칙 레일 18) 삭제 + `DefenderUnitData.abilities` 참조 정리 · CreateAssetMenu `Wassup/Ability/Unit Skill` 제거 | 4 |
| B10 | `BindingOrigin { UnitAuthored, Card, Gimmick, Match }`(`TriggerKinds.cs`) | `OwnerKind { Unit, Card, Gimmick, Match }`(시트 `owner_kind` 와 같은 어휘) | 25/16(6) · `UnitAuthored` 6/4 | **정수 해시됨**(`"origin"` 키 · 스냅샷 `origin=`) → 값·키 문자열 유지 | 4(빌더와 함께) |
| B11 | 실행자가 출처를 박는다: `SelfStatBuffSkill` · `BountyMarkSkill` · `SelfBuffLethalSkill` → `SkillModifierOrigin.Dreamcatcher` · `AllySpeedAuraSkill` → `.Boss` · `CombatPhase.cs:1424`(고치) → `ModifierOrigin.Dreamcatcher` | ~~출처 = 소유 줄의 `OwnerKind` 에서 파생~~ → **U15 로 뒤집힘**: 꼬리표는 연출이고 **효과(실행자)가 박는다** — 소유자에서 파생하지 않는다. 겸하던 병합 칸 규칙은 `SimIntent.PerBindingSlot` 으로 분리(unit 2) | 7 자리 / 5 파일 | 라이브 무변. 값 이름(`Dreamcatcher` · `Boss`)의 개명은 B13 과 함께(unit 4) | 2(분리) · 4(이름) |
| B12 | `SlotKind.Card = 4` · `SlotTag.OfCard` · `IntentApplier.TagFor`(`:154` `Origin == Dreamcatcher` 로 칸 가르기) | `SlotKind.BindingInstance` · `SlotTag.OfBinding` · 판별 = 「규칙 인스턴스 소유 버프」 | ≈6/4 | 코어 런타임 · 정수 4 유지 → 해시·골든 무관 | 1a(이름) · 2(판별) |
| B13 | `CoreDcAuraVisualPool` · `ModifierAuraClassifier.HasActiveDreamcatcherModifier` — 후반부는 **저작 선언 부착 오라**(보스 바람 오라 포함) | `CoreAuraVisualPool` · `HasActiveEmpowerModifier`(B11 결정 뒤) | 3/2 · 2/2 | MonoBehaviour — 파일+클래스 개명, `.meta` 보존(`BattleCoreScene.unity` 는 GUID 참조) | 4 |
| B14 | 파일 `Trigger/CardSkills.cs`(`PlacementSleepSkill` · `DreamstoneStatSkill` — 코어 로컬 실행자) | `CoreSkills.cs` | 파일명 0 참조 | 없음 | 1a |
| B15 | `DcPayloadKinds.IsHandOp` | `EffectKinds.IsHandOp`(또는 `SkillRouting` 로) | 3/2 | 없음 | 4 |
| B16 | 폴더 `Scripts/Data/Dreamcatcher/` 가 공유 저작(`DcMechanic.cs`)을 든다 | `Scripts/Data/Effects/`(unit 4 문서) | — | `.meta` 이동 | 4 |
| B17 | 빌더 어휘: 라벨 `"{owner} mechanic {i}"`(`BindingDefinitionBuilder.cs:95`) · `"카드 '…' effect {i}"` · `CardDefinitionBuilder.BakeMechanic` · `Bake(hostIsEnemy:)` · `ToCore*`·`ToSkill*` 번역 7 · `BakeShieldCast` | 빌더 하나 · 라벨 `"{owner} binding {i}"` · `hostIsEnemy` → `OwnerKind` 파생 | 49/14 · 35/18 | 라벨은 해시 밖이나 **굽기 스냅샷 텍스트**(`Fixtures/*_bake_snapshot.txt` `[rule n] … mechanic 0`)에 찍힌다 → 스냅샷 diff 0 계약과 충돌. 라벨 개명은 스냅샷 재생성과 **격리 커밋** 또는 이전 끝까지 보류 | 4 |
| B18 | 시트 층 `DcSheetApplier` · `DcSheetImportDto`(`DcCardDto` · `DcMechanicDto` · `DcCardEffectDto` · `DcAttackModDto` · `DcSkillDto` · `DcConfigDto` · `DcSheetPayload`) · `DcSheetExporter`(`CardRow` · `MechanicRow` · `SkillRow`) · `DcSheetRuntimeRefresher` | 은퇴 → 표 이름 DTO(`EffectRowDto` · `BindingRowDto` · `CardRowDto` …) · 임포터 하나 | 10/9 · 29/5 · 8/5 · 9/6 | unit 4 첫 커밋이 mechanics 경로 차단 · 시트 쓰기는 승인 후 | 4(차단) · 5 |
| B19 | UI 가 공유 enum 을 읽는다: `DreamcatcherCardText`(`DcPayloadKind`/`DcTriggerKind` 29회) · `Data/UnitKitSummary.cs` · `Data/SplitChain.cs` | 코어 enum 으로 교체(정수 동일이라 기계적) | 3 파일 | 문안 출력 동치 확인 필요 → 「사용자 확인」 1 | 4 |
| B20 | `Data/Authoring/DcTrigger`(`GateComboSupported` · `HasDetector` · `Tick`) — 코어 `SkillRouting` · `TriggerCounters` 와 중복 | 은퇴(UI 는 `SkillRouting` 호출) | 82/8(2) | 없음 | 4 |
| B21 | **죽은 옛 사본**(코어 이식 후 라이브 호출 0): `Core/Dreamcatcher/DcSkillRouting`(17/5) · `DcApplicability`(+`DcHostArchetype` · `DcHostProfile` · `DcProjectileRoute` · `DcRejectReason` — 46/12) · `DcRangeCatalog`(+`DcRangeShape` · `DcRangeSpec` — 29/7) · `Data/Dreamcatcher/SkillPayloadPolicy`(4/3) · `DreamcatcherAttachEval.WouldApply`(테스트만) | 개명 아님 → **삭제**(A3 의 부착 제한 3함수는 남긴다) | 합 ≈ 130 | 테스트 2건이 옛 사본을 **동치 오라클**로 쓴다(`CardViewAssetTests.cs:81` · `DcCardRangeInvariantTests`) → 기대값 고정 후 삭제. `Tests/EditMode/Dc*Tests` 4 · `DcApplicabilityMatrixTests` 동반 정리 | 4 |

## C. 어휘 불일치 — 같은 개념 · 여러 이름

| # | 개념 | 지금 쓰이는 이름(occ/파일) | 정본(용어집) | 조치 · 위험 | unit |
|---|---|---|---|---|---|
| C1 | 효과 종류 | `TriggerPayload`(코어 319/38) · `DcPayloadKind`(225/33) · `SkillEffectType`(액티브 32/7) · 필드 `BindingDef.Payload` · `BindPayload`(7/3) | `EffectKind` | `TriggerPayload` 는 오명(트리거가 아니라 효과의 종류). 타입만 개명 · 멤버·정수·해시 키 `"payload"` 유지 → 골든 무관. 액티브 6 은 append(`tables.md` §3) | 1a(코어 타입) · 4(저작) |
| C2 | 효과 실행자 | `ISkill`/`*Skill` · `BindingDef.Effect`(ISkill — `BindingDef.cs:55`) · `BindingDef.CoreEffect`/`ICoreEffect`(8/3) · `SkillId` · 해시 키 `"skill"` | 실행자 = `Skill` 어휘 | `BindingDef.Effect` → `Skill`(unit 1a 의 `EffectDef`·`EffectIndex` 와 충돌 해소) · `CoreEffect` → `CoreSkill`(선택). `ISkill` 계열 29 concrete 는 유지 | 1a |
| C3 | 소유 줄 | `DcMechanic`/`mechanics`/`nightmareMechanics` · `BindingDef`/`Binding` · 문서 「규칙 줄」 · 4 문서 가칭 `SkillRule` · 스냅샷 `[rule n]` · 시트 `Skills` 탭 | 코드 `Binding*` · 저작 `BindingSpec` · 필드 `bindings` | 가칭 `SkillRule` 는 쓰지 않는다(C4). 문서의 「규칙 줄」은 「소유 줄」로 점진 교체(코드 무관) | 4 |
| C4 | Rule | `EffectComboRule` · `StackRules` · `DeckRules` · `ThresholdRule` · `StructureAuthoringRules` · `BonusSpawnAuthoringRules` | 검증·순수 규칙 전용 | 소유 줄에 `Rule` 금지 | — |
| C5 | payload 세 뜻 | ① 효과 종류(`TriggerPayload` · `DcPayloadSpec` · YAML `payload:`) ② 탄두(`PayloadKind` 112/32 · `ProjectilePayload` 18/12) ③ 전송 봉투(`DcSheetPayload` · `SheetPushPayload` · `UnitStatImportPayload`) | ② 만 코어 어휘 | ① 은 C1 로 해소 · ③ 은 에디터 도구 문맥이라 유지 | 1a · 4 |
| C6 | Origin 두 뜻 | 기하: `SkillOrigin`(24/8) · `OriginBodyRadius`(31/14) · `TryOriginRadius` · `RadiusWithOrigin` · `LeapOrigin` / 출처: `BindingOrigin`(25/16) · `ModifierOrigin`(66/22) · `SkillModifierOrigin`(33/13) · `DotOrigin`(43/13) · `ToCoreOrigin` | Origin = 기하(제약 13) · 소유자 종류 = `OwnerKind` | `BindingOrigin` → `OwnerKind`(B10). 나머지 출처 3 enum 은 유지(개명 시 142 출현 · 이득 작음) → 후속 `*Source` 후보 | 4 · 후속 |
| C7 | Effect 과적 | `CardEffect`(스쿼드 스탯 · 29/9) · `HazardEffect`(장판 하위 3/3) · `EffectTile*`(보드 효과 타일) · `EffectApply`(32/12) · `EffectWitness` · `OnHitEffectType` · `DerivedEffectKind` · `SkillEffectType` | Effect = 효과 줄 | `CardEffect` → `CardStatEffect`(시트 `CardStatEffects` 와 같게 · 필드 `effects:` 키 유지 → YAML 무관) · `SkillEffectType` → C1 흡수. 나머지는 합성 명사라 유지 | 4 |
| C8 | 카드 접두어 셋 | `Dc*`(카드 전용: `DcAttachType` 69/13 · `DcAttackModKind/Spec` · `DcVisualConfig` · `DcAttachRequirementValidator` 3/2 · `DcInspectPanelView`) · `Dreamcatcher*`(`DreamcatcherCard` 261/65) · `Card*` | 코드 `Card*` | 카드 전용 `Dc*` → `Card*`(정수·YAML 무관 — `DcVisualConfig` 는 SO 라 파일+`.meta` 보존 · `CoreVfxSpawner._dcVisual` 필드는 `[FormerlySerializedAs]`). `DreamcatcherCard` SO 타입은 유지(65 파일 churn · 이득 작음 → 후속) | 4(선택) |
| C9 | 방어유닛 이름 | 코어 `Unit`(방어유닛+적 몸 전부) · `Defender*` · `UnitAuthored` · 시트 스탯 탭 `Defenders` · `tables.md` `Units` · `owner_kind = unit` | 코드 `Defender` = 방어유닛 · `Unit` = 몸 일반 | `owner_kind` 값·탭 이름 → D2 | 0 · 5 |
| C10 | 적 이름 | `AttackUnitData`(적 SO · 143/58) · `nightmareMechanics` · `SkillModifierOrigin.Boss` · 문서 「악몽」 | 코드 `Enemy` | 필드는 B8 · 출처는 B11 · `AttackUnitData` 타입은 유지(후속) | 4 · 후속 |
| C11 | 액티브 | `SkillData`(48/20) · `CardType.Active` · `CastActive` · 시트 `DcSkills` · `DcSkillDto` · `tables.md` 트리거 `Cast` | 카드 종류 `Active` · 효과 `Active*` · 트리거 `Cast` | `SkillData` 은퇴(효과 SO 흡수) — 「스킬」이 액티브 SO 이름으로 남으면 U6 와 충돌 | 4 |
| C12 | 트리거 카운터 | `Data/Authoring/DcTrigger.Tick` · 코어 `TriggerCounters`(7/3) | `TriggerCounters` | B20 | 4 |
| C13 | 스킬 두 뜻 | U6 「스킬 = 효과」 · 코드 `ISkill` = 실행자 · 시트 `Skills` 탭 = 소유 줄 · `UnitSkillAbility` | 문서: 스킬(사용자 언어) = 효과 · 코드: Skill = 실행자 | 시트 탭 이름이 U6 와 어긋남 → D1 | 0 · 5 |

## D. 시트(기획자) 라벨 — 소유자 구분이 바람직한 곳

| # | 자리 | 현재 | 제안(`tables.md` 기준) | 비고 |
|---|---|---|---|---|
| D1 | 효과 표 · 소유 줄 표 탭 | `DcMechanics`(카드) · `DcSkills`(액티브) · 유닛/적 스킬 탭 없음(SO 만) | `Effects` + `Skills`(소유 줄) | U6 「스킬 = 효과」와 탭 뜻이 엇갈린다. 대안: ⓐ `Effects` + `Skills` ⓑ `Skills`(효과) + `SkillOwners` ⓒ `Effects` + `Bindings` — **사용자 확인** 3 |
| D2 | 소유자 표 · `owner_kind` 값 | `DcCards` · 스탯 `Defenders` · `Enemies` | `Cards` · `Units` · `Enemies` · `owner_kind = card · unit · enemy` | 스탯 탭이 `Defenders` 인데 `Units` 신설 시 둘이 된다 · `unit` 은 코어에서 적도 포함 → `Defenders` · `defender` 로 맞추기 권장 — **사용자 확인** 3 |
| D3 | 소유 줄의 소유자 구분 | 소유자마다 저장처가 다르다 | 한 표 + `owner_kind` 열(코드는 한 형식) · 소유자별 보기는 시트 필터 뷰 | 탭을 소유자별로 쪼개면 임포터가 소유자를 알게 된다(계약 2 위반 경향) |
| D4 | 카드 자식 표 | `DcCardEffects` · `DcAttackMods` | `CardStatEffects` · `CardAttackMods` | 카드 전용이라 `Card` 접두어가 맞다 |
| D5 | 카드 설정 | `DcConfig`(각성 · 덱 규칙 합집합) | `CardConfig`(또는 유지) | 범위 밖 표 — 이름만 |
| D6 | 효과 `kind` 열 값 | `payloadKind` = `DcPayloadKind` 이름 | 코어 `EffectKind` 멤버 이름(정수 동일) | 기획자가 영문 enum 이름을 읽는다 — 한국어 표시 열 여부 **사용자 확인** 3 |
| D7 | 트리거 `trigger` 열 값 | `triggerKind` = `DcTriggerKind` 이름 | `TriggerKind` 이름(동일 철자) + `Cast` | 철자 무변 |
| D8 | 스탯 탭 | `Defenders` · `Enemies` · `CostConfig` | 변경 없음(`tables.md` 머리말) | — |
| D9 | 에디터 표면(개발자용) | 창 `Window/Wassup/Unit Stat Import` 의 「Import Dreamcatcher」·「Export Dreamcatcher …」 버튼 · 메뉴 `Wassup/Tools/Validate Dreamcatcher Attach Requirements` · CreateAssetMenu `Wassup/Ability/Unit Skill` · `Wassup/BattleView/DC Visual Config` | 표 이름 단위 버튼(「Import Effects/Skills/…」) · Unit Skill 메뉴 제거 | 플레이어 비노출 — unit 5 에서 정리 |

## 권장 개명 배치 (unit 별)

| 배치 | 내용 | 규모 | 위험 · 검증 |
|---|---|---|---|
| **1a-이름**(1a 첫 커밋 · 동작 0) | C1 `TriggerPayload` → `EffectKind`(타입만) · C2 `BindingDef.Effect` → `Skill`(+ 선택 `CoreEffect` → `CoreSkill`) · B12 이름(`SlotKind.BindingInstance` · `OfBinding`) · B14 `CoreSkills.cs` · `TriggerKinds.cs` 머리 주석(「저작 미러」 서술 뒤집기) | ≈ 45 파일 | 순수 C# · 직렬화 0 · 해시 키 문자열·정수 불변 → 골든 무관. 헤드리스 + EditMode Core. 「이름만」 격리 커밋이라 리뷰가 diff 를 기계적으로 본다 |
| **2-출처** | B11 출처를 `OwnerKind` 에서 파생 · B12 판별(`Origin == Dreamcatcher` → 규칙 인스턴스 소유) | 5~7 파일 | 라이브 무변을 굽기 스냅샷 + PlayMode Core 로 증명. 「사용자 확인」 2 선결 |
| **4-차단**(4 첫 커밋) | B18 옛 임포터 mechanics 경로 끄기(4 문서 그대로) | 2 파일 | — |
| **4-enum** | B4 거울 enum 5종 은퇴(정수 동일) · B5/B6 CcKind·StackKind(정수 변환) · B7 `BuffStat` · B15 · B19 UI 교체 · B20 · C12 | ≈ 40 파일 | B5/B6 만 저장 정수가 바뀐다 → 이전 스크립트가 변환 + 굽기 스냅샷 diff 0. B19 는 문안 동치(「사용자 확인」 1) |
| **4-형식** | B1 `BindingSpec` · B2 `TriggerSpec` · B3 `EffectData` · B8 `bindings` · B9 능력 2종 은퇴 · B10 `OwnerKind` · B16 폴더 · B17 빌더 하나 · C7 `CardStatEffect` · C11 `SkillData` 흡수 | 95 에셋(YAML 키) + ≈ 35 파일 | **최고 위험**: YAML 키 교체는 이전 스크립트 dry-run 표 → 사용자 확인 → 적용. `[FormerlySerializedAs]` 로 못 푼다(원소 타입 변경). B17 라벨은 스냅샷 텍스트에 찍혀 **라벨 개명은 스냅샷 재생성 격리 커밋** |
| **4-정리** | B21 죽은 사본 삭제(오라클 테스트 2건 기대값 고정 먼저) · B13 `CoreAuraVisualPool`(GUID 보존) · C8 카드 전용 `Dc*` → `Card*`(선택) | ≈ 20 파일 · 테스트 6 | `DcVisualConfig` 개명 시 `_dcVisual` `[FormerlySerializedAs]` · 씬 GUID 확인 |
| **5-시트** | B18 DTO·임포터·익스포터·리프레셔 교체 · D1~D7 탭·열 · D9 에디터 표면 | ≈ 12 파일 | 시트 쓰기 승인 후 · 확인은 curl 읽기 전용 |
| 후속(spec 밖) | 출처 enum 3종 `*Source` · `DreamcatcherCard` / `AttackUnitData` SO 타입 · 유닛·적 규칙 프로브 · 문서 「규칙 줄」 표기 | — | `docs/spec/README.md` 백로그로 |

## 사용자 확인 (플레이어·기획자에게 보이는 것)

1. **카드·유닛 문안** — `DreamcatcherCardText`(29 자리) · `UnitKitSummary` 가 enum 교체(B19)를 탄다. 정수가 같아 문안은 그대로여야 하지만 **문안 변경은 에이전트가 정하지 않는다** — 달라지면 멈추고 보고.
2. ~~강화 오라·상태 FX 의 출처~~ — **결정 U15(효과별 표시)**. 같은 효과면 누가 들었든 같은 연출(unit 2 에서 반영).
3. **시트 탭·열 이름**(D1 · D2 · D6) — `Skills` 가 효과 표인가 소유 줄 표인가 · `Units` vs 기존 `Defenders` · `owner_kind` 값 철자 · 효과 종류의 한국어 표시 열.
4. **카드 발동 연출은 카드 줄만**(A10) — 같은 효과를 유닛이 들면 발동 임팩트·카드 펄스가 없다. 유지 확인.

## 집계

A 13 · B 21(그중 삭제 1묶음 · 이전형 4) · C 13 · D 9 · 사용자 확인 4.
