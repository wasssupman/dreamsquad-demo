# 6a — 모디파이어 · 군중 제어 · 지속 피해 · 실드 · 스택 (조각 C · 1/4)

> 조각 C 는 원래 unit 6 하나였다. 복사·적응 대상이 실측 **6,331줄**(해저드·필드 1,517 · 픽업/사직서/기믹 1,179 · 모디파이어 854 · 뷰 풀 4 924 · 브리지 효과 851 · 실드/체력 419 · 지속 피해 314 · 군중 제어 273)이라 한 커밋이 될 수 없어 **6a·6b·6b2·6c** 로 나눴다(5a 의 선례). 번호 체계는 그대로다(unit 7~10 참조 무변).

## 목적

지금 판에서 **버리고 있는 것을 받게 만든다.** unit 3 은 공격 출력(`AttackOutputDef`)과 군중 제어 요청(`CcRequest`)을 정해 줄에 넣는 데까지 왔고, 소비자가 없어 그대로 사라진다(`CombatPhase.FlushCc` 의 「unit 6 자리」 주석이 그 구멍이다). 이 unit 이 그 소비자다 — **슬롯·병합·만료·집계**가 서고, 적이 느려지고 타고 잠들고 실드가 막는다.

해저드·장판·효과 타일은 6b, 픽업·사직서·열기/피로는 6b2, 그 그림(상태 FX·오라·빔)은 6c 다.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 모디파이어 | `BattleCore/Effects/ModifierSet.cs`(슬롯·병합·회수·dirty) · `Effects/EffectiveStats.cs`(접힌 값) · `Effects/ModifierMath.cs`(salvage — `CombineMul` 그대로) · **`Effects/ModifierAuthoring.cs`(salvage — `FromMultiplier` · `StackCap`)** · `Effects/ModifierKinds.cs`(`StatKind` 7 · `CombineOp` 3 · `ModifierOrigin` 14 · **`SlotTag`**) |
| 스택 | `Effects/StackSet.cs`(2축 키) · `Effects/StackRules.cs`(임계 판정 순수 함수) · 정의표 `Match/StackRuleDef.cs` |
| 군중 제어 | `Effects/CcState.cs`(슬롯 3 · 병합 · 감쇠 · 행동 잠금) · `Effects/CcMerge.cs`(salvage `CcEffectMerge`) |
| 지속 피해 | `Effects/DotSet.cs`((출처, 원소) 2축) · `Effects/DotTick.cs`(salvage) |
| 실드·체력 | `World/CombatParts.cs` 의 `ShieldSlots` 확장 · `Effects/ShieldMath.cs`(salvage `Merge/Absorb/Sum/ValueFromSource`) · `Effects/MaxHealthScale.cs` |
| 부착 지점 | `World/Unit.cs` — `Modifiers`·`Cc`·`Dot`·`Stacks` **항상 있음**(UML §2 의 `*--`) · `Unit.ActionLocked` 에 CC 잠금 OR 합류 · **`Unit.BaseMaxHealth`(0 = 미캡처)** 추가 · `Unit.Reset` 에서 **0 으로 되돌린다**(풀 재사용 시 앞 점유자의 기준값이 물리면 최대체력 배율이 통째로 어긋난다, E11) · `UnitPartPool.Reclaim` 갱신 |
| 틱 단계 | **새 phase 를 만들지 않는다**(UML §4 자리 그대로): `FieldPrepPhase` 끝(지속 피해 부여·틱 — 옛 `DotApplySystem` 캡처 위치 16 = **이동 앞**) · `TickProjectilePhase` 끝(스탯 만료 → 집계 → 최대체력 → 스택 만료/임계 — `3_combat.md` 변경 대상 표가 「스탯 만료/집계 자리는 unit 6」으로 예약해 둔 자리) · `CombatPhase.FlushCc` 안(군중 제어 슬롯 적용 · 기상 · 감쇠) |
| 정의표 | `Match/MatchDefinition.cs` 에 `StackRuleDef[] StackRules` + canonicalize. `CombatDefs` 의 `AttackOutputDef.Stat/Op/StackKind` 소비 개통 |
| 사건 | `Match/CoreEvent.cs` 34~40: `ModifierApplied`·`ModifierRevoked`·`StackChanged`·`StackThreshold`·`CcApplied`·`CcCleared`·`DotApplied`·`ShieldGranted` (append-only — `_Count` **앞**) |
| 테스트 | `Tests/EditModeCore/`: `ModifierSetTests` · `StackRuleTests` · `CcStateTests` · `DotSetTests` · `ShieldMathTests` · `MaxHealthScaleTests` · **`CoreSkillEnumPinTests`**(`StatKind`·`CombineOp`·`CcRequestKind` ↔ `Wassup.Skills` 의 `Skill*` 미러가 **값·개수 모두 일치** — 어셈블리가 갈려 컴파일러가 못 잡는다. 옛 `SkillModifierKindPinTests` 는 unit 9 에서 죽으므로 그 그물을 여기서 다시 친다) + `DeterminismTests` 확장 |

## 구현

1. **부여는 큐가 아니라 관문 함수다.** 옛 3채널(`StatModifierApplyEvents`·`StackModifierApplyEvents`·`DotApplyEvents`)은 Entities 산물이라 안 옮긴다(계약 1). 생산자가 `unit.Modifiers.ApplyStat(...)` 를 **그 자리에서** 부른다. 진입 가드는 한 곳 — `EffectEligibility.AcceptsModifier/AcceptsCc`(거점 전면 면역 F3 · 보스 잠금 면역). 두 번째 사건이 첫 슬롯을 덮지 않는다(F22)는 테스트로 남긴다.
2. **지연은 큐가 아니라 «단계 위치»로 계승한다**(F29). 함수가 즉시 쓴다고 해서 「즉시 반영」이 되는 것이 아니다 — **생산자 단계가 소비자 단계보다 뒤면 그 효과는 여전히 다음 틱에 든다.** 옛 전투의 1프레임 비대칭(생산자 11 중 8)은 채널이 아니라 **시스템 순서**가 만든 것이고, 새 코어가 같은 순서를 쓰므로 **규칙은 안 바뀐다**. rev 3 §4 가 이 축을 **「변경 없음」으로 닫았다**(`04_trigger_layer_rev3.md`) — 그러니 단계 위치를 옮길 때는 그것이 곧 밸런스 변경임을 알고 옮긴다.
3. **병합 키는 4축이되 전역 번호판은 폐기한다**(F26). 키 = `(출처 SimEntityId, StatKind, CombineOp, SlotTag)`.
   ⚠ **`SlotTag` 는 enum 하나가 아니라 (종류, 판별자) 짝이다.** 옛 번호판 여섯 자리 중 **둘이 매개변수였다**: 스택 파생은 `100 + (int)StackKind`(`StackModifierTickSystem.cs:157`)라 불 스택과 얼음 스택이 서로 다른 슬롯을 쓰고, 드림캐쳐는 `_dcStackCounter++`(`BattleBridge.Dreamcatcher.cs:169`·`441` 외 4곳)라 카드 효과마다 새 칸을 분양받는다. 판별자를 접어 종류 하나로 만들면 **「4키가 전부 겹쳐 강한 배치 감속이 약한 스택 감속으로 깎이던」 버그가 재현된다** — 번호판이 바로 그 수정본이다.
   그래서 `SlotTag { Kind(OnPlace·Tile·AllyField·Card·StackDerived·Gimmick), Discriminator(int) }` 이고 **`StackDerived` 는 `StackKind`, `Card` 는 바인딩 `instanceId`** 를 싣는다(나머지는 0). `ModifierOrigin` 14 는 **꼬리표로 존치**(오라 판정·로그가 읽는다) — 병합 키가 아니다.
4. **회수는 슬롯 삭제다**(F27·F28·F33 — 00_synthesis §C-4 권고). 옛 회수는 「항등값 재발행」이라 ⑴ 강제 고정에 항등이 없고 ⑵ 상한을 실으면 조용히 실패하며 ⑶ 효과 타일은 회수가 없어 개체당 1회로 봉인됐다. `ModifierSet.Revoke(key)` 한 함수가 셋을 한꺼번에 푼다. `ModifierRevoked` 사건을 낸다(오라가 꺼진 것을 뷰가 알아야 한다).
5. **접힌 값은 읽는 자리에서 늦게 접는다.** `EffectiveStats` 는 `dirty` 일 때만 재계산하고 재계산 지점은 **읽는 자리**다. **만료도 dirty 를 켠다**(F21 — 안 켜서 만료가 영원히 안 돌고 모디파이어가 무한 지속된 이력). 결합식은 `ModifierMath.CombineMul` 그대로: `clamp((1+Σadd)×Πmul, 바닥, 천장)`, 일반 `[0.2, 5]` · 이동 `[0.15, 3]` · 최대체력 바닥 `0.05`. **비율 합성 규약은 현행 float 유지**(00_synthesis §C-3 — 고정소수점 전환은 모든 저작 수치의 재조정을 부른다).
6. **저작 분류와 상한은 `ModifierAuthoring` 이 한 곳에서 한다**(2026-07-03 사용자 결정, `docs/spec/modifier-additive-authoring/`). 올리는 버프(배율 ≥ 1)는 **가산**, 깎는 디버프는 **곱셈**이다. 호출처 4곳(`AllyBuffFieldSystem`·`EcsSkillContext`·`DreamCocoonSystem`·`DamageApplicationSystem`)이 전부 이 함수를 지난다.
   ⚠ **상한은 「배율 − 1」 × 최대 중첩 기준이다**(`StackCap`). 가산 버킷에 실리는 값이 «배율 − 1» 이라 상한만 배율 기준으로 계산하면 **조용히 한 스택만큼 어긋난다.** 상한은 단일 관문에서만 걸고(F4 — 옛 클램프는 기존 슬롯 갱신 경로에만 있어 신규 슬롯 2경로로 샜다) **크기만 막고 남은 시간은 안 막는다**(광란이 가장 뜨거운 지점에서 꺼지던 함정). `RegenPerSec` 는 배율 클램프 밖이고 `max(0, ·)` 만 건다 — 결합식이 `(0+Σadd)×Πmul` 이라 **곱셈 슬롯만 있으면 0** 인 성질을 그대로 옮긴다(F5).
7. **스택은 2축 키**(`출처, StackKind`)이고 **출처 태그를 안 싣는다**(F2 — 그래서 스택은 오라 판정 대상이 아니다. 스탯 슬롯과의 의도된 비대칭). 저작은 `StackRuleDef`: 최대 중첩(미등록 폴백 **5**, F14) · 1회 지속 · 임계 규칙 배열. **임계는 오름차순 저작 가정이고 이번엔 검증한다** — `MatchDefinitionBuilder` 가 어긋난 저작을 loud 거절(F13 의 「저작자 책임」을 fail-closed 로). `Edge` 는 올라가는 길에만 발화하고, `Consume` 은 발화 뒤 기준을 **차감된 최종 중첩**에 맞춘다.
8. **임계 규칙의 주인은 스택 종류가 아니라 저작 자산이다**(F31 — 00_synthesis §C-6 권고). 옛 전투는 `StackKind` 당 전역 한 벌이라 드래곤과 킨들러가 불 스택 규칙을 물리적으로 공유했고, 드래곤을 4→10 올렸더니 킨들러가 같이 올라갔다. `StackRuleDef[]` 는 **저작 자산당 한 줄**이고 부여자가 줄 번호를 싣는다. 미지정(-1)이면 그 `StackKind` 의 첫 줄이 폴백이다. **값은 오늘과 같게 저작한다** — 이 unit 이 여는 것은 축이지 밸런스가 아니다.
9. **런타임 군중 제어 슬롯은 셋이다**: `Impulse`(넉백) · `Stun` · `Sleep`. 저작 `CcKind` 5종 중 **`Slow` 와 `DoT` 는 저작 토큰**이고 런타임은 다른 파이프라인으로 간다 — 감속은 **이동속도 모디파이어**(6b 구현 3), 지속 피해는 `DotSet`. 슬롯은 종류당 하나, 시간은 긴 쪽이다. 크기·벡터·주기는 들어온 값으로 갱신하되 **진행률은 새 주기로 비례 환산**한다(F6 — 큰 주기에서 쌓인 타이머가 작은 주기로 넘어가 조기 발동하는 것을 막는다). **Root 는 없다** — 전면 정지가 필요해지면 이동 배율 0 이 아니라 전용 플래그로 만든다(바닥 클램프 0.15 에 걸린다).
10. **행동 잠금은 START 만 막는다.** `Unit.ActionLocked` 에 `Cc.IsLocked`(잠·기절) 를 OR 로 합류시킨다 — 이미 시작한 스윙은 완료되고, **쿨다운은 잠긴 동안에도 돈다**(풀리는 즉시 때린다). 넉백은 잠금이 아니다(밀리는 중에도 때린다).
11. **피격 기상은 피해가 든 직후 같은 틱**이다(F25). 자리는 `CombatPhase.FlushCc` 이고 가드 두 겹은 unit 3 이 이미 세웠다(같은 틱에 걸린 수면은 기상 대상에서 뺀다 — C9). **기절은 안 깬다.** 감쇠는 그 뒤, 즉 **이동 뒤·피해 뒤**다(옛 `CcDecaySystem` 위치). 무한은 `+∞` 로 자연 통과.
12. **지속 피해 병합 키는 (출처, 원소) 2축이다**(설계 불변식 13). `DotOrigin`(Stack·Zone·OnPlace) = 어느 파이프라인이 만들었나 · `DotElement`(Bleed·Fire·Ice·Poison) = 화면에 보이는 그림. **둘을 한 필드로 겸직시키지 말 것** — 합치면 출혈 중인 적이 화염 장판을 밟았을 때 장판을 나가도 장판 요율로 계속 타는 과피해(실측 총 ~194, 의도 50)가 재현된다. 신규 슬롯은 **진입 즉시 1회** 준다(F7), 지급은 **앞에서부터** 제거는 **뒤에서부터**(F8 — 역순 지급이면 피해 숫자 표시 순서가 조용히 뒤집힌다). 옛 두 벌 분리는 병렬 쓰기 안전성 회피라 **한 단계로 접는다**(F9).
    ⚠ **다중 공격자 도트는 합산하지 않는다** — 난도질꾼 2기가 물어도 출혈은 한 슬롯이고 남은 시간만 긴 쪽이다(2026-07-30 사용자 결정 「그대로 두기」). 2축 키의 **귀결이지 버그가 아니다**. 뒤집으려면 `enemy-fire-stack-shooter` README 계약 2·6-1 을 인용하고 재승인을 받는다.
13. **실드.** 같은 출처는 `max`(중첩 불가) · 다른 출처는 합산 · 소모는 **오래된 것부터**(삽입 순 FIFO, E10). 출처 키는 수명 링크가 아니다 — 건 사람이 죽어도 남는다(F24 — `SimEntityId` 미재사용이 그 근거). **이미 더 센 실드가 있으면 다시 걸지도 않고 사건도 안 낸다**(F20 — `ValueFromSource` 가 헛발동을 막는다). 피해 순서는 **받는 피해 배율 → 실드 흡수 → 체력**이고 **완전 흡수는 피격이 아니다**(기상·가시갑옷·피해 숫자·킬 귀속이 전부 그 분기로 갈린다).
    ⚠ **실드는 시간으로 사라지지 않는다.** 만료 경로가 **구조적으로 없는 것**이 파열 판정(합 > 0 → 0)의 전제다(`dreamcatcher-shield-break` 계약) — 수명을 열면 「아무도 안 때렸는데 파열이 터진다」.
14. **최대 체력 배율은 Effects 가 정하고 체력은 한 곳만 쓴다.** `MaxHealthMul` 이 1 에서 벗어난 첫 틱에 `Unit.BaseMaxHealth` 를 캡처(lazy-attach)하고, 축소 시 현재값을 클램프하되 **복원에 무료 회복은 없다**(E11). 기준은 **항상 스폰 시점 원본**이다 — 현재 최대치에 곱하면 누적 오염이 난다. 바닥 1 HP.
15. **투사체가 건 디버프의 출처는 발사자다**(F30 — 라이브 결함). 옛 `ProjectileHitSystem` 은 `source` 로 **투사체 개체**를 보내 발사마다 새 슬롯이 생겨 곱누적됐다. ⚠ **고칠지는 사용자 결정 대기**다(README 「사용자 결정 필요」) — 고치면 곱누적 → 상시 배율이라 킨들러류가 약해지고 **수치 재조정과 한 묶음**이 된다. 답이 오기 전에는 **현행(투사체 출처)을 박제**하고 이 행을 `rules.md` 에 「결정 대기」로 남긴다.
16. **제약 13.** 이 unit 이 새로 만드는 도달 판정은 없다 — 부여는 전부 unit 3 의 판정 결과를 받는다. 다만 사건의 `Site` 짝은 형을 지킨다: 부여 사건의 `SiteFired` 는 **건 쪽의 몸**(몸에서 나오는 것), `SiteTarget` 은 **맞은 쪽의 몸**이다. 값이 없으면 0 을 남긴다.

## 파이프라인 커버리지

이 unit 은 플레이 오브젝트를 신설하지 않는다(효과는 개체가 아니라 개체의 **부분**이다). 아키타입 표가 필요한 것은 6b·6b2(해저드·픽업·사직서)와 6c(상태 FX)다.

| 정거장 | 이 unit |
|---|---|
| 저작 | `StackModifierSO`(무변) · `AttackUnitData`/`DefenderUnitData` 출력(무변) |
| 정의표 | `StackRuleDef[]` 신설 · `AttackOutputDef` 소비 개통 |
| 생성 | N/A — 개체가 없다. 부여는 `Unit` 의 부분에 슬롯이 는다 |
| 매 프레임 | `FieldPrepPhase` 끝(지속 피해) · `TickProjectilePhase` 끝(만료·집계·스택) · `CombatPhase.FlushCc`(군중 제어) |
| 소멸 | 슬롯 만료 = `ModifierRevoked`/`CcCleared`. 개체 소멸은 `Unit.Reset` 이 이미 비운다 |
| 뷰 | N/A — 사건만 연다. 그림은 6c |

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 부여 3채널 + `ModifierApplySystem` | 큐가 통째로 사라진다(직접 호출). ⚠ **반영 시점은 큐가 아니라 단계 위치가 정한다** — 구현 2 가 그 계승이다 | 제거(계약 1) |
| 전역 `stackId` 번호판 · 회수 = 항등 덮어쓰기 | 앞은 `SlotTag` **(종류, 판별자) 짝**으로(판별자를 접으면 안 된다 — 구현 3), 뒤는 슬롯 삭제로(F27 강제 고정 항등 없음 · F28 상한과 충돌 · F33 효과 타일 봉인이 한꺼번에 풀린다) | 제거 · F26~F28 |
| `ModifierStatsDirty`(IEnableable) · ECB 회피 · `RemoveAtSwapBack` | 순수 C# 에서 전부 공짜다. 살아남을 규칙 둘: 「슬롯이 바뀐 틱에만 재계산 + **만료도 켠다**」(F21) · 「승자를 순회 순서에 맡기지 않는다」(F23 — 6b 의 겹친 장판) | 완료 · 제거 |
| `StackPolicy.PerStackInline`·`DecayTick` | 소비처 0 인 죽은 확장 포인트. `RefreshAll` 하나만 옮긴다 | 제거(E-소비0) |
| `CcKind.DoT`·`CcKind.Slow` 런타임 슬롯 | 둘 다 **저작 토큰으로만** 남긴다 — 런타임은 `DotSet` 과 이동속도 모디파이어다(구현 9) | 완료 |
| `CcSource`(직접 출처 예외 축) | `boss-jjangssen` unit 8 이 이미 은퇴시켰다(근거였던 「스택 DoT 가 CC 큐를 공유」가 `dot-effect-extraction` 으로 소멸) | 제거(선행) |
| **새 틱 phase 신설**(`EffectApplyPhase`·`EffectTickPhase`) | 초안이 둘을 신설하려 했으나 **UML §4 가 이미 자리를 예약해 뒀다**(`FieldPrepPhase` = 존·CC·주기 · `TickProjectilePhase` = 효과 틱·만료·집계·스택). 단계를 늘리면 그 예약이 죽고 `class-diagram.md` §4 와 `3_combat.md` 변경 대상 표가 동시에 어긋난다 | 제거 |
| `regenPerSec` 음수 Override | 생산자 0. 「곱셈 슬롯만 있으면 0」 성질은 그대로 옮기고 **툴팁이 귀띔하던 것을 정의표 주석으로** | 보류 · 후속 후보 |
| **실드 부여의 한 틱 지연**(`Inbox.ShieldPending`) | unit 3 이 **현행 비대칭 그대로**(C17)로 이미 결정했다. 뒤집으려면 별도 근거와 골든 재굽기가 따로 필요하다 | 보류 · 재결정 |
| 스택 임계 배열의 **무검증 오름차순** | fail-closed 로 승격(구현 7). 옛 fail-silent 는 옮기지 않는다 | 제거 · F13 |
| 오라 판정(`ModifierAuraClassifier`) | 순수 함수라 salvage 는 싸지만 **소비처가 6c**(오라 풀)다. 여기서 만들면 부르는 곳이 없다 | 보류 · 6c |
| 선택 패널의 **실효 스탯 델타 칩** | 값은 이 unit 에서 생기지만 그리는 자는 5b 의 패널이다 — `ReadoutOf` 한 함수만 바뀐다 | 보류 · 6c |

## 고친 것 (기존 코어·Unity 층 변경)

*(구현 중 채운다.)*

## 완료 기준

- [ ] **EditMode 코어 lane 초록** + 새 테스트 7묶음: `ModifierSetTests`(병합 4축 · **`SlotTag` 판별자가 스택 종류·카드별로 슬롯을 가른다** · 상한 `(배율−1)×최대중첩` · 회수 삭제 · 만료 dirty) · `StackRuleTests`(Edge 는 올라가는 길에만 · Consume 기준 재조정 · 폴백 5 · 오름차순 거절 · **자산별 규칙**) · `CcStateTests`(슬롯 3종 · 시간은 긴 쪽 · 주기 비례 환산 · 잠금은 START 만 · 보스/거점 면역) · `DotSetTests`(2축 키 · 첫 틱 즉발 · 앞→주고 뒤→지움 · **장판 나가면 장판 요율이 멈춘다** · 다중 공격자 미합산) · `ShieldMathTests`(같은 출처 max · 다른 출처 합 · FIFO 소모 · 완전 흡수 ≠ 피격 · 헛발동 없음 · **시간 만료 경로 부재**) · `MaxHealthScaleTests`(바닥 1 · 축소 클램프 · 복원 무료 회복 없음 · **풀 재사용 후 `BaseMaxHealth` 가 0 에서 시작**) · `CoreSkillEnumPinTests`.
- [ ] **증상 단언 3건**(규칙이 화면에서 보이는 형태로): ⑴ 감속을 건 적이 **같은 판에서 느리게 이동한다**(칸 수로) ⑵ 출혈 중인 적이 화염 장판을 밟았다 나오면 **불 피해가 멈춘다** ⑶ 실드가 다 막은 피격은 **수면을 안 깨운다**.
- [ ] `DeterminismTests` 확장 — 「쿨다운·스택 여러 개가 걸린 두 판이 같다」. 키 스냅샷 순회 3곳의 **주석 주장을 테스트로** 바꾼다.
- [ ] **골든 체크박스를 여기서 들지 않는다** — 정의표가 6b·6b2 에서 더 바뀌므로 재굽기는 **조각 C 의 마지막 코어 변경(6b2)에서 한 번**이다. 이 unit 의 의무는 **재굽기 전에 「값이 실제로 바뀐 시나리오」와 「해시만 바뀐 시나리오」를 구분해 기록**하는 것이다 — 안 하면 구현 2·15 의 체감 변동이 해시 변동에 묻힌다.
- [ ] `ledgers/rules.md` **F2~F10 · F13·F14 · F20~F31** 이 코드 포인터로 매핑(F1 카드 핸들은 unit 7, F11·F12·F15~F19 는 6b, F32~F36 은 6b·6b2). 보류였던 F26·F27·F28 은 **결정 + 근거 한 줄**로 등급이 바뀌고, F29 는 **「변경 없음」(rev 3 §4)** 으로, F30 은 **「결정 대기」**로 닫힌다.
- [ ] `ledgers/bridge-methods.md` 미정 **59 → 51**: `TryGetUnitStatReadout/2` · `ShieldRatioOf/2` · `GatherOverheadStacks/1` · `DotAuraKind/1` · `KnockbackOn/1` · `BuildStackThresholdRegistry/0` · `GetStackThresholds/1` · `TryQueueDeployedDefenderMaxHealthDamage/2` 가 「새 주인」 또는 「삭제」로 닫힌다.
- [ ] `core-reviewer` APPROVE — 특히 **매니저 0**(`EffectManager` 같은 이름이 없고, 효과 상태는 `Unit` 의 부분이다) · **하드코딩 0**(클램프 경계 4개는 상수로 남되 근거 주석 동반, 나머지 수치는 정의표) · `Unity.Entities` 0 · **틱 phase 수 무변**.
- [ ] 6a 단독으로는 화면이 안 바뀌는 것이 정상이다(그림은 6c). **사용자 플레이는 6c 뒤 한 번**.
