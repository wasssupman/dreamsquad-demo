# Dreamcatcher Content 2 — 악몽의 여운 / 끝을 보는 눈

> 상태: **완료 2026-07-14** (units 0~4, 사용자 Play 육안 확인). 코드(0~3) EditMode 검증(rig 743 passed 741/failed 0). 에셋(4): 두 카드 SO+catalog 등록(total 25), 사용자 육안 Play e2e 통과. **남은 후속: 실아트 배정(placeholder guid 유지 교체)** + 시트 완전 미러링(`dreamcatcher-sheet-sync` 확장, 선택). 인계=`5_handoff_summary.md`. 사용자 결정 2건: 시트=catalog-only, 사망=엄격 lapse.
>
> 설계 의도: 물리 이동·넉백·충돌 효과가 아니라 디펜스 게임의 핵심인 **킬 보상**과 **목표 지점 도달 위협 우선 제거**를 강화하는 Unit 드림캐쳐 2종을 추가한다.

## 목표

- **악몽의 여운**: 부착 유닛이 킬 크레딧을 받으면 공격력 +15%를 5초 동안 얻는다. 같은 슬롯의 재처치는 수치를 중첩하지 않고 지속시간을 갱신한다. **production C# 0** — 기존 `OnKill × SelfStatBuff` arm(포식의 갈망) + `SelfStatBuff(AttackDamage)` bake(최후의 발악) 조합 재사용.
- **끝을 보는 눈**: 부착 유닛은 사거리 안의 적 가운데 목표 지점까지 남은 FlowField 경로가 가장 짧은 적을 기본 공격의 주 대상으로 고르고, 그 주 대상에게 주는 직접 피해가 20% 증가한다.
- 두 카드 모두 기존 덱빌더 COLLECTION, 전투 손패, 부착 아이콘 경로를 그대로 사용한다.
- 신규 이동/CC/물리 효과, 신규 플레이 오브젝트, 신규 VFX 시스템은 만들지 않는다.

## 검증 질문

> `악몽의 여운`은 킬 크레딧 시 정확히 공격력 +15%를 5초 동안 유지·갱신하고, `끝을 보는 눈`은 곡선 경로에서도 월드 거리가 아니라 **실제 남은 FlowField 경로**를 기준으로 매 공격의 주 대상을 선택·고정하며 그 대상의 직접 피해만 1.2배로 만드는가? 두 카드가 없는 유닛의 기존 최근접/가디언/투사체 공격은 무회귀인가?

## 사용자 결정 (2026-07-13 확정)

1. **시트 연동 = catalog-only, roundtrip 없음.** 자매 스펙 `dreamcatcher-kill-and-threshold`(2026-07-13 완료)가 동일 능력카드 계열에서 확립한 "능력 카드는 Unity-authored, 시트 row 불요, catalog 등록만" 선례 승계. DcCards/DcMechanics/DcAttackMods 시트 행 추가·export/import roundtrip은 이 spec 범위 밖(→ 후속 후보). eye의 `damageMul`은 SO에만 존재. 이 결정으로 시트 baseline drift(AwakeningConfig 20/20/20/4 vs 저장 스냅샷 30/15/5) 밀어올림 위험이 이 spec에서 사라진다.
2. **사망/despawn 처리 = 엄격 lapse.** 잠근 대상이 wind-up 중 죽거나 despawn하면 그 공격은 재선택 없이 lapse(데미지 0, 공격 트리거 미증가). 다른 적으로 갈아타지 않는다. 다음 공격에서 START 시점 최전방을 새로 선택. (연출 정직성 우선 — facing과 실제 타격 대상 불일치 없음.)

## 카드 스펙

| 필드 | 악몽의 여운 | 끝을 보는 눈 |
|---|---|---|
| asset | `Card_NightmareAfterglow.asset` | `Card_EyeOnTheEnd.asset` |
| id | `nightmare_afterglow` | `eye_on_the_end` |
| displayName | `악몽의 여운` | `끝을 보는 눈` |
| type / category | `Unit` / `Unique` | `Unit` / `Unique` |
| effects | 빈 배열 | 빈 배열 |
| mechanics | `OnKill × SelfStatBuff` 1슬롯 | 빈 배열 |
| attackMods | 빈 배열 | `FrontmostTarget`, `damageMul=1.20` 1슬롯 |
| 전용 아트 | `dreamcatcher_card_21.png` | `dreamcatcher_card_22.png` |
| 시트 행 | 없음(catalog-only) | 없음(catalog-only) |
| 기본 덱 | 변경하지 않음 | 변경하지 않음 |

### 악몽의 여운 authoring

```text
mechanics[0]
  trigger.kind = OnKill
  payload.kind = SelfStatBuff
  payload.buffStat = CardBuffKind.AttackDamage   // 데이터 계층 enum, Battle StatKind 아님
  payload.magnitude = 15                          // percent, bake 시 배율 번역
  payload.duration = 5                            // 유한 TTL (devouring=4s 선례)
```

카드 문안 (KillAttribution 정확판):

> 이 유닛에게 처치가 귀속되면 5초 동안 공격력 +15%. 다시 처치하면 지속시간이 갱신된다.

`포식의 갈망`(devouring, `OnKill × SelfStatBuff`)의 트리거 arm + `최후의 발악`(last_stand, `SelfStatBuff(AttackDamage)`)의 buffStat bake를 조합해 재사용한다. 두 arm 모두 오늘 완료됨. 따라서 이 카드의 production C# 변경은 **0**이다.

> MEDIUM 반영: 카피의 "직접 피해로 처치"는 실제 귀속(프레임 내 `source != Null` 최대 amount entry, `KillAttribution.Consider`)과 다르므로 "처치가 귀속되면"으로 표현한다.

### 끝을 보는 눈 authoring

```text
attackMods[0]
  kind = FrontmostTarget
  count = 0          // 이 kind에서는 미사용
  tileRange = 0      // 기본 공격 사거리를 사용하므로 미사용
  damageMul = 1.20
```

카드 문안:

> 기본 공격은 사거리 안에서 목표 지점에 가장 가까운 악몽을 우선 노린다. 그 주 대상에게 주는 직접 피해 +20%.

`DcAttackModKind.FrontmostTarget`은 기존 enum(`None, ProjectileBounce`) 끝에 append한다. 정의 계층의 `DcAttackModSpec`은 순수 데이터/ECS-free를 유지하고, `count`·`tileRange`는 이 kind에서 해석하지 않는다.

## Feature-wide 계약 (load-bearing)

### 1. "최전방"은 월드 거리가 아니라 FlowField 남은 거리다

- 적용 대상은 `FrontmostTarget` mod가 부착된 **Defender**의 기본 공격뿐이다.
- 우선 후보는 기존 공격 가능 조건을 모두 통과한 살아 있는 `AttackUnitTag` 적이다.
  - faction/`AttackState.targetMask` 일치
  - `DeadTag`, `PendingDeployment`, **`PastGoalTag`** 제외 (계약 3 참조)
  - 기본 공격의 Chebyshev 타일 사거리 안
  - 유효한 FlowField 셀이고 `dist[cell] != int.MaxValue`
- 우선순위는 다음 고정 순서다.
  1. `FlowFieldSingleton.dist[cell]` 오름차순 — 목표까지 BFS 잔여 비용이 작을수록 우선
  2. 공격자와 후보의 XZ 제곱거리 오름차순
  3. `Entity.Index`, 이어서 `Entity.Version` 오름차순 — 완전 동률 결정성
- `FlowFieldSingleton`은 Effects 소유이며 Combat은 RO로만 읽는다.
- 비교/선택은 ECS 타입을 모르는 Burst-compatible 순수 helper(`FrontmostTargeting`)로 분리하고 EditMode 테스트로 고정한다.
- 기존 `AttackSystem`의 target snapshot을 재사용한다. 카드별/공격자별 `NativeArray`나 managed 할당, 두 번째 전역 target query를 만들지 않으며 기존 `O(attackers × targets)` 점근 복잡도를 늘리지 않는다.

### 2. 실시간 추적이 아니라 공격 단위 선택·고정이다 (엄격 lapse)

- **START**: 쿨다운이 끝나 실제 공격이 시작되는 순간 현재 최전방 후보를 한 번 선택하고 잠근다.
- **wind-up / hitDelay**: 잠긴 대상이 이동하거나 다른 적이 더 앞서도 이 공격의 대상 identity는 바뀌지 않는다.
- **RESOLVE**:
  - 잠긴 대상이 살아 있고 여전히 사거리 안이면 그 대상을 판정한다.
  - 잠긴 대상이 살아 있지만 사거리 밖이면 다른 적으로 갈아타지 않고 해당 공격은 lapse한다. (근접 유닛의 원격 타격 방지)
  - **잠긴 대상이 죽거나 despawn되었으면 재선택하지 않고 lapse한다** (사용자 결정 2 — 엄격 lapse). 공격 횟수 트리거도 증가하지 않는다.
  - 잠긴 대상이 `PastGoalTag`를 얻었으면(목표 도달, 누수 판정 대상) 해당 공격은 lapse한다.
- 성공 RESOLVE와 모든 lapse 경로는 lock의 active/target/snapshot을 반드시 초기화한다.
- **다음 공격**은 다시 START 시점의 현재 최전방을 선택한다.
- 발사 후 투사체 비행 중의 사망/추적/튕김은 기존 Projectile 파이프라인 계약을 그대로 따른다. 이 카드는 비행 중 실시간 타겟 스캐너를 추가하지 않는다.
- `hitDelaySec=0` 공격도 같은 프레임에 선택 → 사용 → 잠금 해제 순서를 거친다.

카드 전용 상태는 새 Combat 컴포넌트 `FrontmostAttackLock`으로 둔다. 필드는 `active` / `target` / `damageMulSnapshot` / `targetIsPriority`를 분리한다(계약 4·5). Bridge가 mod 최초 부착 때 한 번 추가하고, `AttackSystem`만 RW한다. 매 공격마다 컴포넌트를 add/remove하는 structural change는 금지하고 값만 갱신한다. 전 유닛 공용 `AttackState`나 적 전용 `FocusTarget`에는 섞지 않는다. 신규 시스템·NativeQueue·드레인 없음. 수명은 defender entity와 같아 별도 dispose/teardown 없음.

### 3. PastGoalTag·FlowField·후보 부재 시 안전 폴백

> **goal-tower-siege unit 1 로 이 계약은 뒤집혔다(2026-08-08).** `PastGoalTag` 는 더 이상
> "유출 대기(다음 프레임 소멸)" 가 아니라 **"골에 붙어 타워를 때리는 중"** 이다. 골에 도달한
> 적은 살아서 그 자리에 남으므로 배제 대상이 아니며, 오히려 경로상 가장 앞선 적이라
> frontmost 의 정의에 정확히 부합한다. 아래 서술의 `PastGoalTag 제외`·`lapse` 는 전부 폐기됐다.


- **PastGoalTag 제외**: `MovementSystem`은 골 도달 시 `PastGoalTag`를 붙이고 그 적은 `dist[goalCell]=0`이라 flow-dist 랭킹에서 무조건 1순위가 된다. 하지만 이 적은 이미 누수 판정 대상(다음 프레임 소멸)이므로 eye의 priority·eye 전용 fallback 모두에서 제외한다. lock 대상이 wind-up 중 PastGoal이 되면 계약 2대로 lapse.
- FlowField가 없거나 모든 이동 적이 unreachable이면 기존 최근접 타게팅으로 폴백한다.
- 폴백으로 고른 대상은 `끝을 보는 눈`의 +20% 대상이 아니다(`targetIsPriority=false`, `priorityDamageMul` inert).
- 카드가 없는 공격자는 기존 최근접/적 class filter/FocusUntilDead/Aggroed 경로를 byte-for-byte에 가깝게 유지한다.

### 4. +20%는 "주 대상 직접 피해"에만, Threat와 동기 유지

- `damageMul=1.20`은 `FrontmostTarget`으로 고른 주 대상이 **실제 Damage-kind 피해자가 된 경우 그 피해에만** 곱한다. (기존 "직접 피해" 표현 대신 "priority entity가 실제 Damage-kind victim일 때"로 정확화 — `ProjectileHitSystem` 기준.)
- START에서 현재 유효한 모든 `FrontmostTarget` 슬롯의 `damageMul` 곱을 lock에 snapshot한다(`damageMulSnapshot`). 그 공격 중 카드 상태가 바뀌어도 이미 시작한 공격의 배율은 불변. wind-up 도중 카드를 부착해도 진행 중 공격에는 소급 적용하지 않는다.
- 일반 `ModifierStats.damageMul`, `DamageVsCc`와는 곱으로 합성. Heal/ApplyStat/ApplyStack에는 미적용.
- **Threat 동기(HIGH 5)**: `ProjectileHitSystem`은 모든 피해 사이트에서 `IncomingDamage`와 `ThreatTable.TryCredit`에 동일 값을 넣는다. priority 배율은 **victim별 finalDamage를 한 번 계산**해 `IncomingDamage`와 `TryCredit` 양쪽에 동일하게 사용한다(desync 금지).
- 근접·다중타겟: 잠긴 primary만 1.2배, secondary는 기본 피해.
- Homing SingleSplash: direct target victim만 1.2배, splash secondary는 기본.
- ProjectileBounce: 현재 direct victim이 priority entity와 같을 때만 1.2배. A→B→A로 되돌아오면 A에 다시 적용.
- Ballistic/TileAoe: 잠근 priority entity가 착탄 범위 안에 실제로 남아 있고 그 entity가 Damage victim일 때만 1.2배. 나머지 범위 피해는 기본.
- 전달 경로: `ProjectileSpawnRequest` → Bridge drain → `ProjectileState`에 `priorityTarget`/`priorityDamageMul` inert 필드. zero-init 기본값 `Null/0` = 보너스 비활성이며 실제 계산은 `priorityDamageMul > 0 ? priorityDamageMul : 1`. 기존 request 생산자 전수 수정 없이 모든 기존 투사체가 inert.
- priority 배율은 저장된 output/state 원본에 미리 bake하거나 변이하지 않고, `ProjectileHitSystem`이 실제 victim의 Damage를 enqueue할 때만 곱한다(splash/bounce base damage 오염 방지). `AttackOutputLog`에 보너스를 기록할지는 unit 3에서 확정.

### 5. 부착 자격·가디언·다중타겟

- **부착 자격(HIGH 4)**: 현재 부착 guard(`BattleBridge.Dreamcatcher.cs:180-199`)는 `DefenderUnitTag`만 요구한다. `AttackState`는 힐러/ally-targeting caster를 포함한 모든 defender가 가지므로 판별자가 아니다. eye는 **양수 Damage output 하나 이상**(`AttackOutputElement`에 `AttackOutputKind.Damage` entry) + `damageMul > 0`을 추가로 요구한다. 힐러/output 없는 caster는 거절해 무효 부착을 막는다. `count/tileRange`는 검사하지 않는다(현재 전역 `count > 0` guard를 kind별로 분기).
- `FrontmostTarget` 카드가 붙은 가디언은 카드 정책이 기존 `AggroTargeting`의 primary 선택보다 우선한다. primary는 잠긴 최전방 적으로 강제. 가디언 override는 `priorityTarget != Null`이 아니라 **`lock.active` 기준**으로 판단한다.
- 남은 `attackTargetCount - 1` 슬롯은 primary를 제외한 기존 in-range 후보를 거리순으로 채운다.
- 실제로 맞힌 모든 대상의 기존 `AggroHitEvent`는 유지 → 가디언 어그로 부여/상한은 계속 Effects 경로.
- 카드가 없는 가디언은 기존 `AggroTargeting.SelectTargets` 경로 그대로.

### 6. 중복 카드 정책

`Unique`는 중복 금지가 아니라 콘텐츠 분류/표현 값이며(덱빌더·`DeckRules`에 동일 카드 중복 금지 없음, equipped 카드도 재-ADD 가능), 동일 SO도 덱에서 독립 entry로 존재할 수 있다. 유닛당 총 부착 상한 3만 적용.

- `악몽의 여운` 복사본마다 별도 `DcTriggerSlot`과 `statBuffStackId`. 한 복사본 내부 재처치는 +15%를 더 쌓지 않고 자신의 TTL만 5초 갱신. 복사본 2장은 독립이므로 총 +30%.
- `끝을 보는 눈`의 타겟 모드는 `any FrontmostTarget`으로 idempotent하게 켜고, 복사본 `damageMul`은 곱. 2장 = `1.2 × 1.2 = 1.44` (이 곱은 Bridge가 아니라 AttackSystem START에서 유효 슬롯을 집계 — MEDIUM 반영, unit 2).
- `Unique=1장 제한` 신규 도입은 별도 덱/손패 정책이므로 범위 밖.

### 7. 악몽의 여운 킬 귀속·갱신

- 기존 OnKill 귀속 승계: 같은 프레임 `IncomingDamage` 중 `source != Entity.Null` 최대 피해 entry의 source가 killer(`KillAttribution.Consider`).
- 유닛의 직접 공격과 owner가 보존된 기본/카드 투사체 처치는 발동.
- DoT/설치물/환경 피해처럼 `source=Null` 처치는 미발동. DoT source 전파 확장은 비목표.
- 한 슬롯은 고정 `statBuffStackId` 재사용 → 재처치 시 modifier 개수/수치가 늘지 않고 남은 시간이 5초로 갱신.

### 8. 콘텐츠·UI 계약 (catalog-only, 시트 N/A)

- 두 카드는 전용 2:3 카드 아트 2장(`1024×1536`, Single Sprite, mipmap off)을 사용. 기존 아트 `dreamcatcher_card_01~20`은 모두 사용 중이므로 신규 21/22 제작.
- 두 비-Active SO를 `DreamcatcherCardCatalog.asset`에 등록. 기본 10장 덱과 씬은 변경하지 않는다. 새 런타임 리프레셔(`DcSheetRuntimeRefresher`)가 catalog를 자동 열거하므로 OutgameScene 추가 수정 불필요.
- 기존 덱빌더/손패/팝업/부착 아이콘이 `art`, `displayName`, `description`, catalog를 자동 소비 → UI 코드 변경 없음.
- **시트 export/import roundtrip은 하지 않는다**(사용자 결정 1). 능력 카드 값은 Unity-authored(mechanics/attackMods에 baked). `DcSheetApplier`는 id-match 부분갱신이라 시트에 없는 카드는 무터치이며, 시트에 신규 행을 넣지 않아도 기존 값 손실이 없다. 완전 시트 미러링(Afterglow buffStat 컬럼, eye DcAttackMods 행)은 `dreamcatcher-sheet-sync` 확장으로 이관(후속 후보).
- Unit 카드는 자동 mechanic 수치 렌더가 없으므로 `15%`·`5초`·`20%`는 카드 문안과 실제 데이터에서 함께 육안 검증한다(수치 drift 주의).

## 작업 단위 (6 units)

README 승인 후 아래 numbered spec을 **한 개씩 작성·승인·구현·검증·커밋**한다. Codex 재검토가 제안한 8-unit 분할은 시트 roundtrip 시퀀싱(결정 1로 스코프 아웃)이 만든 것이라 채택하지 않는다.

| # | 예정 문서 | 작업 | 핵심 완료 기준 |
|---|---|---|---|
| 0 | `0_frontmost_definition.md` | `DcAttackModKind.FrontmostTarget` append, `FrontmostAttackLock`(active/target/damageMulSnapshot/targetIsPriority), projectile `priorityTarget/priorityDamageMul` inert 필드 | 미사용 기본값 inert, compile green |
| 1 | `1_frontmost_rank_and_bake.md` | 순수 `FrontmostTargeting`(flow-dist 랭크 + PastGoal/unreachable 제외 + tie) + EditMode, Bridge per-kind validation/bake(Damage output+damageMul>0 요구) | 경로거리·tie·unreachable·PastGoal제외·count=0 attach 고정, 힐러/output없는 caster 거절 |
| 2 | `2_attack_commit.md` | AttackSystem START lock/RESOLVE/엄격 lapse(사망·despawn·범위밖·PastGoal), fallback, guardian primary(lock.active), 1.44 집계 | 공격 중 순위 변화 고정, 사망 시 재선택 없이 lapse, 무카드 무회귀 |
| 3 | `3_priority_damage.md` | melee/projectile direct-primary +20% + Threat 동기(victim finalDamage 1회→IncomingDamage+TryCredit) | primary만 ×1.2, secondary/splash 제외, bounce/ballistic 계약, threat 미desync |
| 4 | `4_card_assets.md` | 아트 21/22 Sprite import → 두 SO(Afterglow mechanics + Eye attackMod) → catalog 등록 → Afterglow refresh/expiry PlayMode + Eye e2e Play | COLLECTION/손패 노출, 두 카드 e2e, art!=null, 2:3 원본 |
| 5 | `5_handoff_summary.md` | 인계 | 범위·테스트·잔여 리스크(시트 미러링 후속) 기록 |

> art는 SO보다 먼저 import하되 `art != null` 검증은 SO 생성 시점(unit 4 내부)에 한다(MEDIUM 반영). Afterglow는 0-code라 별도 unit 없이 unit 4 에셋에 흡수.

## 예상 구현 범위

### production 코드 — 끝을 보는 눈

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcAttackModKind.FrontmostTarget` append + kind별 필드 의미 주석.
- `Assets/_Project/Scripts/Battle/Combat/FrontmostAttackLock.cs` 신규 — Combat-owned 공격 단위 잠금(active/target/damageMulSnapshot/targetIsPriority).
- `Assets/_Project/Scripts/Battle/Combat/FrontmostTargeting.cs` 신규 — ECS 비의존 후보 비교/선택 순수 helper.
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — 전역 `count > 0` guard를 kind별 validation으로 분기. Frontmost는 `damageMul > 0` + host의 양수 Damage output 요구, `count/tileRange` 무시. Frontmost slot bake + lock 최초 추가.
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — Frontmost 후보 선택(PastGoal 제외), START lock, RESOLVE 사용/엄격 lapse, guardian primary, damageMul snapshot, 1.44 집계.
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs`, `ProjectileState.cs` — `priorityTarget`/`priorityDamageMul` inert 필드.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 위 필드 drain 전달.
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — victim finalDamage 1회 계산 → priority entity의 Damage victim에만 배율, IncomingDamage+ThreatTable.TryCredit 동일 적용.

`DcAttackModSpec`의 기존 `kind/count/tileRange/damageMul`로 충분(production 필드 추가 없음, 주석 보완만).

### production 코드 — 악몽의 여운

없음. 기존 `BattleBridge.Dreamcatcher`의 `SelfStatBuff` bake, `DamageApplicationSystem`의 OnKill arm, `ModifierApplySystem`의 동일 stackId merge, `StatModifierTickSystem`의 TTL 만료 재사용.

### 신규/수정 에셋

- `Card_NightmareAfterglow.asset(.meta)`, `Card_EyeOnTheEnd.asset(.meta)`
- `dreamcatcher_card_21.png(.meta)`, `dreamcatcher_card_22.png(.meta)`
- `DreamcatcherCardCatalog.asset` (2종 등록)

### 테스트

- `FrontmostTargetingTests` EditMode: BFS 잔여거리 우선(월드 근접보다), distance/world/entity tie-break, unreachable 제외, **PastGoal 제외**, 후보 없음.
- Bridge/EditMode: `FrontmostTarget(count=0, damageMul=1.2)` bake, non-defender/invalid kind/비양수 multiplier/**Damage output 없는 유닛** 거절, 중복 mod 1.44.
- AttackSystem 통합: 미부착 최근접 무회귀, START 후 순위 변화에 locked 유지, **locked 사망 시 재선택 없이 lapse**, 살아있으나 범위 밖 lapse, **PastGoal 전이 시 lapse**, hazard fallback, guardian primary/secondary + AggroHitEvent 유지.
- 피해 통합: melee primary 1.2/secondary 1.0, homing direct 1.2/splash 1.0, bounce 타 대상 1.0/priority 복귀 1.2, ballistic AOE priority만 1.2, **Threat 크레딧이 IncomingDamage와 동일 배율**.
- `NightmareAfterglow` 실제 SO PlayMode: 첫 처치 후 DamageMul +0.15, 같은 슬롯 재처치 후 +0.30 안 됨(TTL만 갱신), 5초 유지·만료 후 baseline, Afterglow 투사체 처치 발동.
- asset/catalog: 두 SO shape/id/art, non-Active catalog 등록, ID 중복 없음.

## 파이프라인 커버리지

신규 플레이 오브젝트 없음 → 오브젝트 스폰→View/Pool 대조 **N/A**. 기존 기본 공격/투사체 파이프라인의 선택·피해 파라미터만 확장.

| 경계 | 이번 spec | 신규 시스템/큐 |
|---|---|---|
| 정의 SO | 기존 `mechanics[]`, `attackMods[]` + enum kind 1개 | 없음 |
| Mono→ECS | 기존 `BattleBridge` bake/투사체 drain 확장 | 없음 |
| Combat 상태 | `FrontmostAttackLock` 1개, 기존 mod buffer | 시스템 없음 |
| 타게팅 | 기존 AttackSystem 후보 스냅샷 + 순수 rank helper | 없음 |
| 투사체 | Request/State inert 필드 2개 + Hit arm multiplier | 없음 |
| Presentation/UI | 기존 art/catalog/description 자동 소비 | 없음 |
| 씬 | 변경 없음 | 없음 |

## 범위 밖 / 후속 후보

- **시트 미러링**: eye `DcAttackMods` 행, Afterglow `buffStat` 컬럼, export/import roundtrip → `dreamcatcher-sheet-sync` 확장. (시트 baseline drift `AwakeningConfig` 20/20/20/4 vs 스냅샷 30/15/5도 그 스펙에서 정리.)
- 전 유닛 공용 타겟 우선순위 프레임워크 또는 AttackSystem 전체 리팩터
- 비행 중 매 틱 타겟 재검색/타겟 라인 UI
- 물리 이동, 넉백, 충돌, CC, 신규 투사체/VFX/SFX
- DoT/환경 킬의 source 전파 및 OnKill 귀속 확장
- `Unique` 카드의 덱/동일 유닛 중복 금지 정책
- 기본 10장 덱 자동 편입, 보유/해금 경제, 밸런스 수치 재조정
- 무관한 씬·폰트·ProBuilder dirty 변경

## 착수 리스크

- 최대 회귀면은 `AttackSystem`의 기존 최근접/Focus/Aggro/Guardian 선택 순서(`:40-44` query, `:171-177` 랭킹). 카드 mod 보유 분기 바깥은 건드리지 않고, 무카드 통합 테스트를 먼저 고정한다.
- `priorityTarget`을 발사 데미지에 미리 곱하면 splash/bounce 과증폭 + Threat desync. 배율은 `ProjectileHit`의 실제 victim 판정 시 IncomingDamage+TryCredit에 동시 적용한다.
- `PastGoalTag` 적은 `dist=0`이라 flow 랭킹 1순위가 되지만 누수 판정 대상 → 반드시 제외.
- 현재 dirty(폰트 5·ProBuilder·Screenshots.meta·crash blob·CardAbsorb.mp3)는 무관 → exact-path staging으로 격리.
