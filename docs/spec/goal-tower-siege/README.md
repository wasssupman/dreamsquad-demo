# goal-tower-siege — 골에 도달한 적이 타워를 때린다

> 상태: **초안 2026-08-07 · 미착수**
> 선행 필수: `three-minute-survival` 완료 (안정도 값·체력바·패배 조건·tie-break 가 거기 있다)

## 목표

`three-minute-survival` 은 적이 골에서 사라지며 안정도를 **1회 즉발**로 깎는다. 이 spec 은
같은 안정도를 **지속 피해**로 바꾼다: 적이 골에 도달해도 죽지 않고 멈춰서 자기 공격으로 골
타워를 때리며, 방어유닛이 그 적을 잡으면 안정도를 지켜낸다.

검증 질문: *"골이 뚫린 뒤에도 플레이어가 만회할 수 있고, 그 만회 여부가 안정도 잔량으로
드러나는가?"*

**바꾸는 것은 피해가 도착하는 방식 하나다.** 안정도 값·체력바·패배 조건·점수 tie-break 는
선행 spec 이 이미 소유하므로 재설계하지 않는다.

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
