# 8 — 상시 효과도 스킬 줄로 (저작 · 이전)

## 목적

카드 전용 저장처 둘을 없앤다. 스쿼드 스탯 효과(`DreamcatcherCard.effects` 15개 · 13장)와 공격 수식자(`DreamcatcherCard.attackMods` 3개 — 통통구슬 · 끝을 보는 눈 · 악몽 사냥)를
**효과 줄 + 소유 줄**로 옮겨, 카드 · 방어유닛 · 적이 같은 형식으로 든다(U6 · 계약 2 · 11). 게임 동작은 바뀌지 않는다.

## 효과 종류 (`TriggerKinds.cs` `EffectKind` append — 39~42)

| 종류 | 한국어 | 칸 | 굽는 모양 |
|---|---|---|---|
| `FactionStatBuff` | 아군 전체 스탯 | `buff_stat` · `percent` · `ally_filter` | 코어 줄 = 남의 배치 × `SelfStatBuff`(영구) + 직업 · 코스트 필터 + 회수 → **소유자의 진영 버프 줄**(카드 `SquadBindings` · unit 7 의 유닛 · 적 칸) |
| `ProjectileBounce` | 투사체 튕김 | `count` · `range_tiles` · `mul` | 공격 수식자(오늘 카드 경로 값 가드 그대로) |
| `FrontmostTarget` | 최전방 우선 | `mul` | 〃 |
| `DamageVsSleeping` | 수면 적 특효 | `mul`(> 1) | 〃 |

- 소유 줄 트리거 = **`None`(보유 시작 순간 · 이후 계속)** — 카드는 오늘도 이 뜻이다(부착 즉시). 코어에 새 트리거를 만들지 않는다. 빌더가 위 모양으로 편다(강타 `HeavyStrike` → 수식자 선례).
- `EffectComboRule`: 위 4종 ⇔ `None` · 다른 트리거와의 조합 거절. 강타는 그대로(`AttackN` — 오늘도 누구나 든다).
- **방어유닛 · 적 × 위 4종 = 「배선 전」 거절**(굽기가 말하고 뺀다 — 조용히 반쪽만 도는 상태를 만들지 않는다 · `SkillRouting.HasDetector` 의 「배선 전엔 닫아 둔다」 선례). 배선은 unit 7(후속). 카드는 숙주가 방어유닛일 때만.
- `ally_filter` = 새 효과 칸(값 = 오늘 카드 `axis` 의 `All` · `ClassRanger` · `ClassGuardian` · `Cost1`). 수혜 대상은 **효과의 뜻**(계약 12 · `shield_filter` 선례). 배치 오라(`PlacementAura`)의 필터도 이 칸에서 읽는다.
- 한 원천: Squad 카드의 머리 칩 · 분류 문구(`CardCategoryStyle.cs:77` · `DreamcatcherCardText.cs:78`)는 그 카드 효과 줄의 `ally_filter` 에서 파생하고, 카드 `axis` 는 Unit 카드 표시 전용으로 남긴다. 효과 층의 enum 이름(`CardTargetAxis`)은 이번에 바꾸지 않는다(개명 = 후속 후보).

## 굽기 스냅샷 동치의 조건 (어기면 라이브가 바뀐다)

1. `FactionStatBuff` 줄은 카드 종류가 아니라 **효과 종류로** `SquadBindings` 에 간다 — `Bindings` 로 가면 `FireOnAttach` 가 `None` 만 보고 건너뛰어(`CardBindings.cs:93-96`) 이미 있던 아군에게 안 걸린다.
2. 수식자 3종은 규칙 줄을 만들지 않는다(`AddRow` 금지 — 줄 번호가 밀린다).
3. 효과 에셋은 옛 줄과 1:1(`effect_id` = 카드 id · 효과 둘이면 `_{slot}` — `tables.md` §10) · 효과 표 순서 보존.
4. `None` × 4종은 `CombosAllow` · `SkillRouting.Resolve` **앞**에서 가로챈다(`AttachInstant` 갈래와 같은 자리 · `BindingSpecBuilder.cs:75-79` — `HasDetector(None)` = false · 라우팅 null 이라 뒤로 가면 버려진다).

## 변경 대상

- `Scripts/BattleCore/Trigger/{TriggerKinds,EffectComboRule}.cs` — 카드 수식자의 숙주 적합성은 오늘처럼 부착 때(`Applicability.EvaluateAttackMod`).
- `Scripts/Data/Effects/{EffectValues,EffectSlots}.cs` — 새 칸 · **종류별 사용 칸 표**(정본 = `tables.md` §3 · 전 `EffectKind` 커버 테스트 — unit 9 export 가 쓴다).
- `Scripts/BattleCoreUnity/{BindingSpecBuilder,CardDefinitionBuilder,BindingDefinitionBuilder}.cs` — `None` × 4종을 펴는 한 경로(카드 · 방어유닛 · 적 공용). 「Squad 카드의 소유 줄은 읽지 않는다」 가드 은퇴 · Squad 카드 = `FactionStatBuff` 줄만(카드 분류 검증 — 덱 상한이 본다).
- `Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `effects` · `attackMods` 칸 제거(이전 뒤). `CardEffect` 타입은 남는다(`DreamstoneData`).
- `Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` · `UI/Outgame/CardCategoryStyle.cs` — 스쿼드 · 수식자 문안과 칩을 효과 값에서. **액티브 문안도** `SkillData` 수치(`:535-563`) 대신 시전 줄 효과 값 + 카드 `cooldownSec` 에서(쿨다운 두 원천 해소).
- 이전 도구(에디터 · unit 4 선례): 효과 에셋 18 + 카드 소유 줄 + 배치 오라 효과 `ally_filter`. **dry-run 표 → 사용자 확인 → 적용** → 옛 칸 제거 → 재직렬화.

## 완료 기준

- 굽기 스냅샷 2종: 바뀌는 줄은 라벨 · 효과 id 뿐이고 커밋 메시지에 나열(해석된 값 · 순서 동일). 재베이크 0.
- 전 카드 문안 · 칩 전/후 대조 테스트 동일(액티브 6장 포함).
- 빌더 픽스처: 두 카드가 같은 `FactionStatBuff` 효과를 참조 → 같은 코어 줄 모양(복사 0) · 방어유닛 · 적이 새 4종을 들면 굽기가 「배선 전」을 말하고 뺀다 · Squad 카드에 다른 종류 줄 → 거절.
- 헤드리스 · EditMode 3어셈블리(알려진 빨강 외 0) · PlayMode Core 초록.
- 단계 A 구현 2026-09-29(이전 **전**까지 · 에셋 무변) — `bc68220da` 종류 4 append · `EffectComboRule` ⓪'(상시 ⇔ `None` · 방어유닛 · 적 · 적 숙주 카드 = 새 사유 `NotWired`) · kind_ko · `8f3a8b2d2` `allyFilter` 칸 · 종류별 사용 칸 표(`EffectSlots.UsedColumns` · 정본 `tables.md` §3) · `e32db6849` 굽기(조합 · 라우팅 앞 가로채기 · 진영 버프 → `SquadBindings` · 수식자 = 규칙 줄 0 · 라벨 = 옛 모양이라 스냅샷 글자 동일 목표 · Squad = 진영 버프 줄만 · 오라 필터 = 효과 칸) · `dddeb1eec` 문안 · 칩(액티브 = 시전 줄 + 카드 `cooldownSec`) · `33a8f3a58` 이전 도구(순수 계획 `AlwaysOnEffectMigration` · 메뉴 `Wassup/BattleCore/Skill Data Table/2부 이전 dry-run` · 적용(미실행)) · dry-run 표 `dry-run/dry_run_part2.md`(효과 줄 18 · 오라 1 · 깃발 0 · id 충돌 0 — 헤드리스 `tools/skill-data-table/dry_run_part2.sh`).
- 과도기 규칙: 상시 효과 줄이 없는 카드 = 옛 칸(`effects` · `attackMods` · 카드 `axis`)에서 오늘 결과 · 있으면 옛 칸 무시(`DreamcatcherCard.HasAlwaysOnRows`) · 배치 오라 `allyFilter` 가 기본값(첫 값)이면 카드 축.
- 남은 것(단계 B — 사용자 dry-run 승인 뒤 · 리드): 메뉴 적용 → 굽기 스냅샷 · 전 카드 문안 테스트(동치) → 옛 칸 · 폴백 제거(`BakeSquad` · attackMods 루프 · `AuraFilter` 폴백 · `RuleOwner.Axis` · `HasAlwaysOnRows` · 문안 옛 갈래 · `AlwaysOnEffectMigration*` · 메뉴 · `tools/skill-data-table/dry_run_part2.sh`) → 재직렬화. ⚠ 로그인 시트 import(`DcCardEffects` · `DcAttackMods`)는 옛 칸에 쓴다 — 이전된 카드는 그 칸을 안 읽는다(탭 은퇴 = unit 9).
