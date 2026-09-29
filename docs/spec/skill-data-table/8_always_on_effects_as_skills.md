# 8 — 상시 효과도 스킬 줄로 (저작 · 이전)

## 목적

카드 전용 저장처 둘을 없앤다. 스쿼드 스탯 효과(`DreamcatcherCard.effects` 15개 · 13장)와 공격 수식자(`DreamcatcherCard.attackMods` 3개 — 통통구슬 · 끝을 보는 눈 · 악몽 사냥)를
**효과 줄 + 소유 줄**로 옮겨, 카드 · 방어유닛 · 적이 같은 형식으로 든다(U6 · 계약 2). 게임 동작은 바뀌지 않는다.

## 효과 종류 (`TriggerKinds.cs` `EffectKind` append — 39~42)

| 종류 | 한국어 | 칸 | 굽는 모양 |
|---|---|---|---|
| `FactionStatBuff` | 아군 전체 스탯 | `buff_stat` · `percent` · `ally_filter` | 코어 줄 = 남의 배치 × `SelfStatBuff`(영구) + 직업 · 코스트 필터 + 회수 → 소유자의 진영 버프 줄(unit 7) |
| `ProjectileBounce` | 투사체 튕김 | `count` · `range_tiles` · `mul` | 공격 수식자(오늘 카드 경로 값 가드 그대로) |
| `FrontmostTarget` | 최전방 우선 | `mul` | 〃 |
| `DamageVsSleeping` | 수면 적 특효 | `mul`(> 1) | 〃 |

- 소유 줄의 트리거 = **`None`(보유 시작 순간 · 이후 계속)**. 카드는 오늘도 이 뜻이다(부착 즉시). 코어에 새 트리거를 만들지 않는다 — 빌더가 위 모양으로 편다(강타 `HeavyStrike` → 수식자 선례 · `BindingSpecBuilder`).
- `EffectComboRule`: 위 4종 ⇔ `None`(모든 소유자 종류) · 그 밖 트리거와의 조합은 거절. 강타는 그대로(`AttackN`).
- `ally_filter` = 새 효과 칸(오늘 카드 `axis` 값 `All` · `ClassRanger` · `ClassGuardian` · `Cost1`). 수혜 대상은 **효과의 뜻**이다(실드 `shield_filter` 선례) — 소유자를 바꿔도 따라간다. 배치 오라(`PlacementAura`)의 필터도 카드 `axis` 대신 이 칸에서 읽는다.
- 이름: 효과 층이 카드 이름을 들지 않는다 — `CardTargetAxis` → `AllyFilter` 로 개명(정수 직렬화 무변). 카드 `axis` 칸은 카드 표시(머리 칩 · 분류 문구)로 남고, Squad 카드의 효과 `ally_filter` 와 다르면 검증 경고.

## 변경 대상

- `Scripts/BattleCore/Trigger/TriggerKinds.cs` · `EffectComboRule.cs` · `Scripts/Data/Effects/{EffectValues,EffectSlots}.cs`(새 칸 · 종류별 사용 칸 표)
- `Scripts/BattleCoreUnity/{BindingSpecBuilder,CardDefinitionBuilder,BindingDefinitionBuilder}.cs` — `None` × 4종을 펴는 한 경로(카드 · 방어유닛 · 적 공용). 「Squad 카드의 소유 줄은 읽지 않는다」 가드 은퇴 · Squad 카드 = `FactionStatBuff` 줄만(카드 분류 검증 — 덱 상한이 이 분류를 본다). 방어유닛 · 적이 든 수식자도 카드와 같은 숙주 적합성(`Applicability.EvaluateAttackMod`)을 지난다.
- `Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `effects` · `attackMods` 칸 제거(이전 뒤). `CardEffect` 타입은 남는다(`DreamstoneData` 가 쓴다).
- `Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` — 스쿼드 · 수식자 문안을 소유 줄의 효과 값에서 읽는다. **액티브 문안도** `SkillData` 수치 대신 시전 줄의 효과 값 + 카드 `cooldownSec` 에서 읽는다(쿨다운 두 원천 해소 — 시트에서 고친 값이 실제와 문안에 같이 간다).
- 이전 도구(에디터 · unit 4 선례): 효과 에셋 18개(`effect_id` = 카드 id · 효과가 둘이면 `_{slot}` — `tables.md` §10) + 카드 소유 줄 + 배치 오라 효과의 `ally_filter`. **dry-run 표 → 사용자 확인 → 적용** → 옛 칸 제거 → 재직렬화.

## 완료 기준

- 굽기 스냅샷 2종: 바뀌는 줄은 라벨 · 효과 id 뿐이고 그 줄을 커밋 메시지에 나열(해석된 값 · 순서 동일). 재베이크 0.
- 전 카드 문안 전/후 대조 테스트 동일(액티브 6장 포함).
- 빌더 픽스처: 같은 `FactionStatBuff` 효과를 카드와 방어유닛이 참조 → 같은 코어 줄 모양 · 방어유닛이 튕김 수식자를 들면 그 유닛 공격에 실린다.
- 헤드리스 · EditMode 3어셈블리(알려진 빨강 외 0) · PlayMode Core 초록.
