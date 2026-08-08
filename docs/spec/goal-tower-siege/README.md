# goal-tower-siege — 골에 도달한 적이 타워를 때린다

> 상태: **rev 3 — 붕괴 후 스트레스(unit 4) 추가 · 자동 검증 완료 2026-08-09**
> (EditMode 전량 + PlayMode GoalStability·Endless·TallyFlow 그린. Play 육안·밸런스 실측만 대기)
> 인계: `3_handoff_summary.md` · **검증 체크리스트는 통합본**
> `docs/spec/three-minute-survival/5_verification_checklist.md`
> 선행: `three-minute-survival` units 0~3 커밋됨(`a7d1b015`, 컴파일만 검증).
> 안정도 값·체력바·패배 조건·tie-break 는 거기 있고, 이 spec 은 **피해가 도착하는 방식만** 바꾼다.

## 목표

`three-minute-survival` 은 적이 골에서 사라지며 안정도를 **1회 즉발**로 깎는다. 이 spec 은
같은 안정도를 **지속 피해**로 바꾼다: 적이 골에 도달해도 죽지 않고 멈춰서 자기 공격으로 골
타워를 때리며, 방어유닛이 그 적을 잡으면 안정도를 지켜낸다.

검증 질문: *"골이 뚫린 뒤에도 플레이어가 만회할 수 있고, 그 만회 여부가 안정도 잔량으로
드러나는가?"*

**바꾸는 것은 피해가 도착하는 방식 하나다.** 안정도 값·체력바·패배 조건·점수 tie-break 는
선행 spec 이 이미 소유하므로 재설계하지 않는다.

## 작업 단위

| # | 문서 | 작업 구분 | 목적 |
|---|---|---|---|
| 0 | `0_tower_entity_and_pool.md` | 브리지 + 태그 1개 | 골 타워 = `Faction.Defender` 건물 엔티티. 신규 시스템 0 |
| 1 | `1_enemy_siege.md` | ECS (Units) + 브리지 | 적이 골에서 죽지 않는다 — 파괴 중단·타겟팅 배제 해제·돌격형 자폭 |
| 2 | `2_coverage_and_cadence.md` | ECS (Combat) + 문서 | `TileAoe` 피해자 풀 · 케이던스 실측 · 계약 문서 갱신 |
| 3 | `3_handoff_summary.md` | 인계 | 구현 종료 시 작성 |
| 4 | `4_stress_after_breach.md` | 규칙 rev 3 | 골 파괴 = 유출 개통, 스트레스 상한이 패배를 소유 |

## Feature-wide 계약 (rev 2)

- **골 타워는 `Faction.Defender` 진영의 건물 엔티티다.** 적의 base `targetMask` 가 이미 그
  진영을 포함하므로 **타겟팅 코드가 0줄**이다 — 전용 Faction 비트·마스크 부여 훅·도발 패치
  전부 없다.
- **`DefenderUnitTag` 는 붙이지 않는다.** 그건 "플레이어가 놓은 유닛" 축이라, 붙이면 배치·
  코스트·카드 부착·시너지·피로도/열기·픽업·실드가 딸려온다. 진영과 유닛 태그의 분리는
  Blocking 해저드의 선례와 같다.
- **피해·사망은 표준 경로다.** `IncomingDamage` → `DamageApplicationSystem` → `DeadTag` →
  `UnitLifecycleSystem` 파괴. 전용 시스템·공유 풀·미러 없음. **신규 ISystem 0개.**
- **체력은 타워마다 자기 것.** ~~하나라도 부서지면 패배~~ → unit 4 에서 **유출 개통**으로 개정(상한 0 인 덱만 즉시 패배). 표시는 가장 위험한 골(최소 체력).
  구 "공유 1풀" 결정을 대체한다(2026-08-08).
- **적의 뷰를 despawn 하지 않는다.** 공성 적은 살아 있으므로 뷰·현상금 표식·데이터 등록부를
  건드리지 않는다.
- **공격 수단이 없는 적(Runner·Swift)은 자폭한다.** 골에 눌러앉으면 아무 피해도 못 주면서
  웨이브 전멸 판정만 막으므로, 기존대로 사라지며 `stabilityDamage` 를 부딪힌 골에 넣는다.
- **원거리 적은 골 셀에 못 들어올 수 있다** — 사거리에서 멈춰 타워를 쏜다. 그 적은
  `PastGoalTag` 를 못 받아 스트레스 카운터에 안 잡힌다(수용된 대가).
- 보스는 `hunting` 대상에 타워가 포함되므로(`DefenderFieldSystem` 이 `Faction.Defender` 로
  필터) 방어유닛이 남아 있어도 골로 향한다 — rev 1 이 "의도된 구멍" 으로 남겼던 항목이
  진영 변경으로 자연 해소됐다.
- **힐러가 골을 수리할 수 있다** — 아군 타겟 후보가 `Faction.Defender` 라 타워가 포함된다.
  Play 로 보고 판단할 것(막으려면 후보에서 `GoalTowerTag` 배제).

## 파이프라인 커버리지

골 타워는 신규 플레이 오브젝트다. 가장 가까운 아키타입은 **해저드 — Blocking**(정적 ·
`Health` 보유 · 적의 공격 대상)이라 그 표를 대조했다.

| 정거장 | 골 타워의 대응 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/AttackDeck.cs` → `goalStabilityMax`(기존) | 전용 SO 신설 없음 — 최대치는 이미 덱이 소유 |
| 스폰 진입점 | `BattleBridge` 맵 빌드 직후 골 셀 순회 | 판 시작 1회 결정론 생성이라 요청 큐 불요(해저드의 staged-request 와 다름) |
| ECS 컴포넌트 (Units) | `GoalTowerTag`·`GoalTowerHealth` + `Health`/`IncomingDamage`/`FactionTag`/`LocalTransform` | `EffectSpawner.SpawnBlockingHazard` 와 동형 |
| 시뮬 시스템 | `GoalTowerDamageSystem`(Units, `[UpdateBefore(DamageApplicationSystem)]`) | 버퍼 직접 소비 → 풀 → 전 타워 미러 |
| 이벤트 큐 | **N/A** — 체력은 원샷 사건이 아니라 **상태**다. 브리지가 싱글턴을 폴링한다 |
| View | **N/A(재사용)** — 바는 `UnitOverheadView` 타워 스킨(three-minute-survival unit 1), 구조물 메쉬는 `theme.goalStructureProp`. 신규 프리팹 0 |
| 씬 wiring | **N/A** — 새 SerializeField 없음 |

## 왜 분리했나

초안에서 이 설계는 `three-minute-survival` 의 unit 0 이었고, 리뷰에서 결함 대부분이 이 하나에서
나오는 것이 확인돼 분리했다. 요구사항(점수·웨이브·안정도 노출)은 ECS 변경 0으로 달성되고,
공성만이 ECS 를 요구한다.

## 착수 전에 반드시 읽을 함정 (2026-08-07 리뷰 실측)

1. **공유 체력 미러 산술** — 골이 2개면 타워도 2기다. `taken = Σ(Health.max − Health.value)` 는
   틀린다: write-back 이 `value = pool.value` 라서 다음 프레임의 delta 가 **누적 결손**이 된다
   (`pool' = 2·pool − max`, 골 2개면 `3·pool − 2·max` → 첫 피격 후 5~7프레임에 허위 패배).
   올바른 형태는 **직전 프레임 미러값 스냅샷** 기준 delta 이거나,
   `[UpdateBefore(DamageApplicationSystem)]` 로 타워 `IncomingDamage` 를 직접 소비하는 것이다.
   후자가 개별 타워 `DeadTag`(`DamageApplicationSystem.cs:308`) 문제까지 없앤다.
   `DamageApplicationSystem.cs:172` 는 `newHp` 에 0 하한이 없어 오버킬 프레임에 음수가 된다.
2. **유출 이벤트가 뷰를 죽인다** — `DrainGoalEvents`(`BattleBridge.cs:4648-4649,4653`)가 스프라이트/
   Spine 을 despawn 하고 현상금 표식을 회수한다. `GoalReachedEvent` 를 그대로 재사용하면
   **안 보이는 적이 타워를 때리고 허공에 데미지 폰트만 뜬다.** 채널을 분리하거나 despawn 3줄을
   옮겨야 한다.
3. **`PastGoalTag` 배제 지점은 3곳이 아니라 5곳**이고, `AttackSystem` 의 주 최근접 타겟 루프
   (`:424-441`)에는 **애초에 필터가 없다** — 방어유닛은 이미 공성 적을 때릴 수 있다. 실제 배제는
   `AttackSystem.cs:475`(frontmost 추적) · `:583`(frontmost 락) · `:1595`(니들 폴백) ·
   `ProjectileEmitterSystem.cs:101` · `ProjectileMoveSystem.cs:72`. `NearestTargeting.cs` 에는
   필터가 없다(순수 랭킹 유틸).
4. **원거리 적이 골 앞에서 멈춘다** — `targetMask` 에 `GoalTower` 를 OR 하면 사거리 3타일 적이
   골 3칸 앞에서 `Engaging`(`EnemyAiStateSystem.cs:100`) → `engageMovement == Halt` 면 정지
   (`MovementSystem.cs:162-184`). 골 셀에 도달하지 않으므로 `PastGoalTag` 도, 스트레스 카운트도
   발생하지 않는다. 유닛별 `EnemyBehavior.engageMovement` 실측이 선행이다.
5. **투사체 피해자 풀이 진영 하드코딩** — `ProjectileTargetFaction` 은 2값이고
   (`ProjectileState.cs:12`) `ProjectileHitSystem.cs:491-493` 의 두 풀은 `DefenderUnitTag` /
   `AttackUnitTag` 쿼리다. `PayloadKind.TileAoe` 로 공격하는 적(보스 계열)은 **타워에 0 피해**다.
   어느 공격 아키타입이 타워를 때릴 수 있는지 표로 명시하고 못 때리는 것은 N/A + 이유로 남긴다.
6. **보스는 골에 도달하지 않는다** — `MovementSystem.cs:128-137` 의 `hunting` 이 방어유닛이 하나
   라도 살아 있으면 `IsGoalCell` 판정을 건너뛴다(leak-proof). 보스 공성은 별도 결정이 필요하다.
7. **도발된 적은 타워를 못 때린다** — `TauntAttackGrantSystem.cs:48` 이 `targetMask` 를
   `Defender` 단독으로 덮어쓴다. 골에 도달한 상태로 도발이 걸리면 아무것도 못 하면서 필드를
   점유해 전멸 트리거를 막는다.
8. **드림캐쳐 계약 5곳** — `dreamcatcher-content-2/README.md:80,99,107-109,236` ·
   `dreamcatcher-attack-decoupling/README.md:67` · `2_payload_target_fallback.md:26` 이
   "PastGoalTag 제외"를 명시적 계약으로 기재한다. 의미를 바꾸면 이 문서들을 같은 커밋에서
   갱신해야 한다(안 하면 미래 세션이 버그로 오인해 되돌린다).
9. **거동이 깨지는 테스트** — `UnitLifecycleSystemTests:50-52`(`PastGoalTag → DestroyEntity` 를
   assert) · `FrontmostAttackLockTests:171,251`("골 도달 시 락 해제" 를 assert).
10. **웨이브 케이던스와의 상호작용** — 공성 적은 `AttackUnitTag` 를 유지하므로
    `NoQueuedAttackersRemain()` 이 거짓이 되고 **전멸 즉시 진행이 꺼진다**(20초 상한만 작동).
    골 인접에 배치칸이 없는 맵이면 영구적이다. 6개 맵의 골 인접 `Place` 타일 실측이 선행이다.
11. **타워 파괴 시각** — `goalStructureProp` 은 맵 빌드 시 1회 배치되는 정적 prop 으로 엔티티를
    추적하지 않는다(Blocking 해저드는 `BlockingHazardPresenter` 가 담당). 파괴 연출을 원하면
    View 정거장을 따로 정해야 한다.
12. **사막 테마는 골 구조물이 없다** — `desert.asset:71` `goalStructureProp: {fileID: 0}`.

## 확정된 것 (초안에서 살릴 판단)

- `Faction.GoalTower = 1 << 3` 은 비어 있고 `targetMask` 는 스폰 시 코드 베이크라 직렬화 호환
  문제가 없다(`Faction.cs:6-12`, `BattleBridge.cs:6690`).
- 타워를 **Units 맥락**에 두고 Combat 이 읽기만 하는 구성, 안정도를 **싱글턴 상태**로 두고
  브리지가 폴링하는 구성은 맥락 통신 규칙에 맞다(원샷 사건이 아니라 상태 → 큐 불요).
- 타워 아키타입의 선례는 `EffectSpawner.SpawnBlockingHazard`(`Health` + `IncomingDamage` +
  `FactionTag` + `LocalTransform`)다.
- 힐러·아군 버프가 타워로 새지 않는다: 힐 게이트가 `mask == (int)Faction.Defender` **정확 일치**
  (`AttackSystem.cs:366`), `DefenderFieldSystem.cs:55` 는 Defender 만, 투사체 힐은 버퍼 보유
  가드(`ProjectileHitSystem.cs:174`). 단 타워가 `ModifierStats` 를 얻으면 `MaxHealthScaleSystem`
  이 `Health.max` 를 재계산하므로 **"타워는 ModifierStats/StatModifierSlot/ShieldSlot/
  IncomingHeal 을 갖지 않는다"** 를 계약으로 박고 테스트로 고정할 것.
