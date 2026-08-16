# dreamcatcher-content-4 — 신규 드림캐쳐 3장 (궤도 화염구 · 수면 특효 · 퇴근 운석)

상태: **완료 2026-08-16** — units 0~8 구현·커밋 + **사용자 Play 확인 완료**.
인계는 [`6_handoff_summary.md`](6_handoff_summary.md).

투트랙 리뷰: Track B(ECS) **APPROVE** · Track A(품질) REQUEST CHANGES → 지적 반영 완료(`2ef68650`).
Play 확인에서 나온 것: 차폐 소팅·부착 즉시 발동·크기/지속 = unit 7(`a36e784e`) ·
화염구 2개 = unit 8(`d09a2a99`) · 주기 5초 = `b4ebad82`.

검증: EditMode **2446 전량**(잔여 실패 5건은 이 작업과 무관한 사전 실패 — 맵 문서 4 · Whirlpot 1) ·
PlayMode 이 feature 분 **12/12**(퇴근 교차 무발동 3 + 수면 배율 2 포함, 포커스 on/off 2회 연속) ·
궤도 × 재타격 **end-to-end 3건**.

**남은 것**: 시트 push 1회(카드 3장, 계약 13) · 원격 푸시(사용자 승인 대기).

## 상위 목표

드림캐쳐 3장을 추가한다. 각 장이 서로 다른 축을 하나씩 연다 —
**궤적**(도는 투사체) · **상시 공격 변조**(수면 특효) · **사건**(퇴근).

| # | id | 표시명(가칭) | 게임에서 일어나는 일 | 조합 |
|---|---|---|---|---|
| 1 | `flame_spinner` | 불꽃 팽이 | T초마다 부착 유닛 주위를 화염구가 N초간 돈다. 스친 적에게 M피해(같은 적은 쿨타임 후 재타격) | `PeriodicTimer` × `SelfOrbitProjectile` |
| 2 | `nightmare_hunt` | 악몽 사냥 | 잠든 적을 때리면 그 타격의 피해 ×2 | attackMod `DamageVsSleeping` (트리거 없음) |
| 3 | `severance_meteor` | 퇴직 위로금 | 이 유닛이 퇴근하면 비워진 그 자리에 운석이 떨어진다 | `OnRetire` × `SelfTileAoe` |

## 검증 질문

> ① 화염구가 **눈에 보이게 돌면서** 스친 적을 반복해서 깎는가 — 그리고 N초 뒤 사라지는가?
> ② 잠든 적을 때린 그 한 방만 2배인가(옆의 깨어 있는 적은 그대로인가)?
> ③ **퇴근에서만** 운석이 떨어지고 **사망에서는 안 떨어지는가** — 그 역도 참인가?

## 작업 단위

| 파일 | 담당 | 작업 구분 | 목적 |
|---|---|---|---|
| `0_shared_vocabulary.md` | **오케스트레이터 단독(선행)** | 토대 | 정의 계층 enum 3종 append + 적용성 + **bake seam 3벌** + 요청 필드. 병렬 3레인의 **공통 충돌면을 미리 없앤다** |
| `1_orbit_trajectory.md` | 레인 A | 엔진 | `MovementKind.OrbitAroundPoint` — 순수함수 + 이동 arm + 뷰 |
| `2_rehit_cooldown.md` | 레인 A | 엔진 | 관통 페이로드 **재타격 쿨타임** 축 (0 = 기존 방향탄 무변화) |
| `3_flame_spinner_card.md` | 레인 A | 카드 | 주기 트리거 arm(화염구 발사) + 카드 에셋 |
| `4_sleep_damage_mod.md` | 레인 B | 카드 | 수면 대상 피해 배율 2지점 적용 + 카드 에셋 |
| `5_retire_meteor_card.md` | 레인 C | 카드 | 퇴근 경로 슬롯 직독 → 운석 cast + 카드 에셋 |
| `6_handoff_summary.md` | 오케스트레이터 | 인계 | 커밋·검증·되돌리면 안 되는 것 |

## 병렬 실행 구조 (2026-08-16 사용자 결정)

3종을 **전담 에이전트 3레인**이 동시에 구현한다. 단 병렬은 **구현까지**다.

```
                            ┌─> 레인 A1: 1 궤도 궤적   ─┐
[0] 공통 어휘 (단독 커밋) ──┼─> 레인 A2: 2 재타격 쿨타임 ┴─> 3 불꽃 팽이 카드 (A1·A2 수렴 후)
                            ├─> 레인 B : 4 악몽 사냥
                            └─> 레인 C : 5 퇴직 위로금
                                      │
                            [수렴] 컴파일 · 테스트 · Play · 커밋 (직렬)
```

> **레인 A 를 둘로 쪼갠 근거(스펙 리뷰 반영).** 초판은 A 가 unit 1→2→3 을 순차로 들어 전체 작업의
> 약 70% 를 혼자 지고 B·C 는 일찍 놀았다 — 병렬 이득이 크게 깎인다. 실제로 unit 1 과 2 는
> **파일이 겹치지 않는다**: 1 = `MovementKind.cs` · `Orbit.cs` · `ProjectileMoveSystem.cs` ·
> `ProjectileViewPool.cs`, 2 = `PathHitRecord.cs` · `ProjectileHitSystem.cs`.
> 궤적과 페이로드는 직교 축이라(`projectile-trajectory-payload` 의 원래 분해) 서로를 기다릴 이유가
> 없다. unit 3 만 둘의 수렴을 기다린다.

**계약 P1 — 공통 충돌면은 unit 0 이 먼저 없앤다.** 세 레인이 전부 건드릴 파일
(`DcMechanic.cs` · `DcApplicability.cs` · `BattleBridge.Dreamcatcher.cs` · `DreamcatcherCardText.cs`
· `ProjectileSpawnRequest`)의 변경을 unit 0 에 모아 **먼저 한 커밋으로** 끝낸다. 이후 세 레인의
파일 소유는 서로 겹치지 않는다.

**계약 P2 — 파일 소유는 배타적이다.** 레인은 아래 표 밖의 파일을 수정하지 않는다. 필요해지면
멈추고 보고한다(임의 확장 금지 — 그것이 병렬의 유일한 실패 모드다).

| 레인 | 배타 소유 |
|---|---|
| A1 | `Projectile/Orbit.cs`(신규) · `Projectile/ProjectileMoveSystem.cs` · `Presentation/ProjectileViewPool.cs` |
| A2 | `Projectile/PathHitRecord.cs` · `Projectile/ProjectileHitSystem.cs`(**PathHit arm + lookup 선언줄만**) |
| B | `Battle/Combat/AttackSystem.cs` · `Data/Dreamcatcher/Card_NightmareHunt.asset` |
| C | `Bridge/BattleBridge.cs` 의 **`RetireDefender` 함수 내부만** · `Data/Dreamcatcher/Card_SeveranceMeteor.asset` |
| unit 3 (A1·A2 수렴 후) | `Battle/Combat/BossPeriodicTriggerSystem.cs` · `Data/Projectiles/Projectile_FlameOrb.asset` · `Data/Dreamcatcher/Card_FlameSpinner.asset` |
| 공용(오케스트레이터) | unit 0 의 파일들(`DcMechanic` · `MovementKind` · `MovementBinding` · `DcApplicability` · `BattleBridge.Dreamcatcher` · `BattleBridge.SpawnProjectile` · `ProjectileData` · `ProjectileState` 선언 · `DreamcatcherCardText`) · `DreamcatcherCardCatalog.asset` · 덱/시트 등록 |

검증: unit 0 커밋 **후** 시작하므로 `BattleBridge.cs`·`ProjectileState.cs` 는 동시 편집 대상이
아니다(unit 0 이 이미 끝냈고, 이후 이 파일들을 만지는 레인은 C 하나뿐 — 그것도 함수 하나 내부).
`DcAttackModSlot.cs` 는 **변경 없음** — `DamageVsSleeping` 은 기존 `damageMul` 을 그대로 쓴다.

**계약 P3 — 레인은 커밋하지 않고 Unity 를 만지지 않는다.** 에디터·MCP 브리지·git index 는
**단일 자원**이다. 세 레인이 동시에 refresh/Play/commit 하면 서로의 컴파일 실패를 보고,
`.git/index.lock` 이 경합한다. 레인의 산출물은 **소스 + EditMode 테스트 코드**까지이고,
컴파일·테스트 실행·Play 검증·커밋은 오케스트레이터가 **직렬로** 수행한다.

**계약 P4 — 각 레인은 자기 문서 하나만 읽고 완결한다.** 레인 A 는 1·2·3 을 순서대로(내부 의존
있음), B·C 는 각자 한 파일. 교차 참조가 필요하면 그건 계약이 unit 0 에 없다는 신호다.

## Feature-wide 계약

1. **신규 라이프사이클 0.** 화염구는 기존 투사체 정거장(발사 요청 → 브리지 드레인 →
   `ProjectileState` → Move/Hit → 파괴)에 **이동 수학 하나**만 더한다. 신규 NativeQueue 채널 0,
   신규 투사체 태그 0 (`projectile-emission-pattern` 계약 5 상속).
2. **궤도 중심은 발사 시점 고정점이다.** 방어유닛은 타일 고정이라 host 를 추적할 필요가 없다
   → `BallisticArcToPoint` 계열(대상 엔티티 무참조). host 가 죽거나 퇴근해도 이미 나간 화염구는
   자기 수명을 산다(투사체의 기존 계약과 동일).
3. **재타격 쿨타임이 켜지면 관통 예산을 소모하지 않는다.** 안 그러면 화염구가 몇 명 스치고 수명
   전에 사라진다. `rehitCooldownSec > 0` 인 투사체의 유일한 종료 조건은 **수명**이다.
   `rehitCooldownSec == 0` 은 기존 방향탄 동작 그대로(무회귀).
   **이 값은 탄 SO 가 소유한다** — `pierceCount` 와 같은 자리(`ProjectileData` → 드레인이 직접
   읽어 `ProjectileState` 로). payload/슬롯/요청 struct 를 관통시키지 않는다(ECS 리뷰 M1).
   ⚠ 기록 갱신은 **ECB 로 불가능**하다(원소 수정 오퍼레이션이 없다) — RW 버퍼 lookup 으로
   직접 쓴다. bounce 의 outputs 감쇠가 같은 형태의 선례다(unit 2 §3).
3-1. **화염구의 피격 판정은 캡슐 스윕이다.** 매 프레임 직전 위치→현재 위치 선분과 적 중심의
   최근접 거리가 **피격 반경** 이하면 히트(`SweepHitMath`) — 프레임 사이 터널링이 구조적으로 없다.
   **피격 반경(탄 SO 의 `hitThreshold`)과 궤도 반경(카드 `tileRange`)은 다른 축**이다.
   후보 풀은 `AttackUnitTag` 하드코딩이라 **아군을 때리는 경로가 존재하지 않는다**(진영 필드 불요).
   **통행 층은 host 사양을 따른다** — 발사 arm 이 `AttackState.targetTraversalLayers` 를 실어
   보낸다. 안 실으면 0 = 무제한이라(`PlacementLayers.CanTarget` 이 0 을 무조건 통과) 지상만
   때리는 유닛에 이 카드를 붙였을 때 **그 유닛이 못 때리는 비행 적을 화염구가 때리는** 뒷문이
   생긴다. 초판 계약은 "필터는 그대로 탄다"고만 적어 이 구멍을 못 봤다(ECS 리뷰 M2).
   ⚠ 원운동 + 직선 스윕 = **현(chord)**. 프레임당 회전각이 크면 궤도 선상의 적을 스쳐 지나간다 —
   저작 상한으로 막고 코드로 클램프하지 않는다(unit 1 §4-1).
4. **수면 특효는 트리거가 아니라 상시 공격 변조다.** 이미 `DamageVsCc` 가 판정되는 2지점
   (투사체 발사 시 `bestTarget` 스냅샷 · 근접 `hitTarget` 별)에 형제로 붙는다.
   > 기각한 대안: 게이트 축 × `HeavyStrike`. 강공은 그 공격의 **전 victim** 을 배율해서 잠든 적
   > 옆의 깨어 있는 적까지 2배가 된다 — 사양 초과.

   ⚠ **«피해자별» 은 근접에서만 참이다** (Track A 리뷰 H1, 2026-08-16). 원거리 host 는 배율이
   발사 시점 `bestTarget` 기준으로 **탄의 damage 에 구워지므로**, 그 탄의 splash·bounce·관통
   2차 피해가 배율을 **승계**한다 — 잠든 적을 쏘면 옆의 깨어 있는 적도 2배를 받고, 반대로 깨어
   있는 적을 쏘고 잠든 적을 splash 로 스치면 배율이 **안** 붙는다. 이는 `shatter_hymn`
   (`DamageVsCcMul`)의 기존 관례를 그대로 따른 결과이며 **이 엔진의 원거리 배율 규약**이다.
   초판 계약이 "피해자별"을 전 host 로 과일반화했다.
   > 실제 노출 범위는 좁다 — splash 반경/바운스/관통이 있는 host 에서만 갈린다. 평범한 단발
   > 호밍(궁수)은 직격 대상뿐이라 계약대로 동작한다. 근본 해결(히트 시점 victim별 판정)은
   > `shatter_hymn` 까지 같이 옮겨야 하므로 후속 후보다.
4-1. **"2배"는 곱 체인의 공격자 쪽에 들어간다.**
   `output × damageMul × CC특효 × [수면 ×2] × 조준보너스 × 강공` → `IncomingDamage`,
   그 뒤 Units 가 `× dmgTakenMul` → 실드 흡수 → HP 차감. 전부 곱이라 **같은 적 기준 최종 HP
   감소도 정확히 2배**이고 화면 숫자도 2배다. **예외는 실드** — 흡수량까지 합쳐야 2배다.
   `shatter_hymn`(CC 피해 증가)과는 곱으로 중첩된다(수면도 CC다 — 의도된 중첩, 테스트로 고정).
5. **잠을 깨우는 그 타격이 2배를 받는다.** 피해 계산(Combat)과 수면 해제(`CcClearRequests`,
   Units→Effects)가 다른 시스템이라 구조적으로 그렇게 된다. 버그가 아니라 의도다.
6. **퇴근은 사망이 아니다 — 교차 무발동이 load-bearing 이다.** `OnRetire` 카드는 죽을 때 안 터지고,
   `OnDeath` 카드는 퇴근할 때 안 터진다. `defender-clock-out` 계약 1 이 `DeadTag` 를 안 다는 것으로
   후자를 이미 보장하고, 이 spec 은 전자를 새로 보장한다. **자동 테스트로 양방향 고정.**
7. **`OnRetire` 는 적에게 열리지 않는다.** `DcTrigger.EnemyTriggerArmed` 무변경 —
   적은 퇴근하지 않으므로 fail-closed 가 곧 정답이다.
7-1. **퇴근 운석은 다른 운석과 값을 공유하지 않는다.** 액티브 카드 운석(`SkillData`)·시즌 기믹
   폭격(`ClockOutGimmickData`)·퇴근 운석(카드 payload)은 **출처가 셋이고 파라미터가 독립**이다.
   공유하는 것은 겉모습(`Projectile_Meteor.asset`)뿐이며 그것도 탄 SO 복제로 갈라진다.
   ⚠ 다른 두 운석의 값을 참조하거나 상수로 복제하지 않는다(제약 6).
8. **운석은 신규 payload 를 만들지 않는다.** 기존 `SelfTileAoe`(= SkyFall × TileAoe, 작별선물 ·
   진동갑주 · 실드폭발과 같은 경로)를 쓰되 `payload.duration` 을 **낙하 예고 초**로 해석한다
   (`AreaBarrage` 의 duration=텔레그래프 선례). 기존 카드는 duration 0 이라 무변화.
9. **주기 트리거의 방어유닛 개방은 bake 한 줄이다.** `BossPeriodicTriggerSystem` 은 이미 진영
   중립이다(게이트가 `DcTriggerSlot` 버퍼 존재뿐). 카드 bake 가 `periodSeconds` 를 안 실어
   보내서 슬롯이 **조용히 무발동**이었다 — unit 0 이 배선하고 `<=0` 을 loud 거절한다.
10. **카드발 데미지에 attacker damageMul 미적용** (기존 계약 유지). 화염구·운석 모두 flat.
11. **하드코딩 0.** 지속/주기/피해/반경/재타격 쿨타임/각속도/운석 예고는 전부 카드 에셋 저작.
12. **art = null** 로 출시(category 색 폴백), 실아트는 후속(guid 유지 교체 관례).
13. **시트 push 는 feature 종료 시 1회** (비파괴 업서트). 카드별로 하지 않는다.

## 파이프라인 커버리지

투사체 아키타입(`docs/reference/object-pipeline-map.md` §투사체) 대조 — 정거장 신설 0:

| 정거장 | 앵커 | 이번 spec |
|---|---|---|
| 데이터 SO | `Data/ProjectileData.cs` | **+1 필드**(`rehitCooldownSec`) — `pierceCount` 와 같은 자리·같은 역할(드레인이 직접 읽는 탄의 성질). 화염구 look 은 `Projectile_FlameOrb.asset` 신규 인스턴스 1개, 운석은 기존 `Projectile_Meteor.asset` 재사용 |
| 스폰 진입점 | RESOLVE / 폭탄 / 캐스트 드레인 / emitter | **+0.** 화염구 = `BossPeriodicTriggerSystem` 의 기존 캐리어 경로, 운석 = 브리지 직접 cast(기존 `SelfTileAoe` 와 동일) |
| ECS 컴포넌트 (Combat) | `Projectile/ProjectileState`·`PathHitRecord` | `ProjectileState` +1 필드(`rehitCooldownSec`) · `PathHitRecord` +1 필드(재타격 시각) |
| 시뮬 시스템 | `ProjectileMoveSystem`(궤적) · `ProjectileHitSystem`(페이로드) | Move 에 `OrbitAroundPoint` arm 1개 · Hit 의 PathHit arm 에 쿨타임 분기 |
| 이벤트 큐 | `ProjectileHitEventsSingleton` | **무변경 — 신규 채널 0** |
| View/Pool | `Presentation/ProjectileViewPool.cs` | sim XZ 궤도라 **view-Y arm 불요**(높이는 `visualHeightOffset`). facing 은 기존 `AlongVelocity` 가 접선을 준다 — 구현 시 육안 확인 |
| 씬 wiring | BattleBridge `_projectileViewPool` | **무변경 — 신규 씬 배선 0** |

## 후속 후보 (현 스코프 밖)

- ~~**화염구 다중화**~~ → **완료 (unit 8, `d09a2a99`)**. 예고대로 궤적·히트 무변경이었고
  발사 arm 이 `Orbit.PhaseOf` 로 위상을 나눠 쏘는 것이 전부였다. 현재 저작 = 2개.
  3개 이상도 카드 `orbitCount` 값만 바꾸면 된다(bake 가 1~16 클램프).
  ⚠ 개수는 DPS 에 선형이다 — 늘릴 때 `magnitude` 를 같이 본다.
- **`BossPeriodicTriggerSystem` 개명** — 이름이 거짓이 된다(방어유닛도 탄다). `PeriodicTriggerSystem`
  으로 기계적 rename. 이번 spec 에 넣지 않는 이유는 **병렬 레인의 배타 소유 파일을 rename 하면
  세 레인이 전부 리베이스**되기 때문. 수렴 후 별도 커밋.
- **재타격 쿨타임의 다른 소비자** — 관통 방향탄(샷건너 등)에 켜면 "긴 사거리를 훑으며 반복 타격"이
  데이터로 열린다. 지금은 화염구만 쓴다.
- **`OnRetire` × 다른 payload** — 퇴근 자리에 핫식스 드랍(`clock-out` 후속 후보의 원안,
  `PickupKind.Redbull` 라이브)·아군 버프 등. 이번엔 `SelfTileAoe` 한 쌍만 배선한다.
- **"아무 아군이나 퇴근하면" 스코프** — DC 트리거는 전부 host-scoped. squad-wide 사건 축은
  아직 없다(`clock-out` 후속 후보의 지적 그대로).
- **원거리 배율을 히트 시점 victim별 판정으로 이관** [M] — 계약 4의 ⚠ 절 참조. 지금은 배율이
  발사 시점에 탄 damage 로 구워져 splash/bounce/관통 2차 victim 이 승계한다. 고치려면
  `ProjectileState` 에 배율을 실어 히트 arm 3곳(직격·splash·PathHit)에서 victim 별로 판정해야
  하는데, **`shatter_hymn`(`DamageVsCcMul`)이 정확히 같은 형태**라 둘을 같이 옮기지 않으면 두
  카드가 서로 다른 규칙으로 갈린다. 그래서 이 spec 범위 밖. 착수 조건 = splash/바운스 host 에
  이 카드를 얹는 조합이 실제 밸런스 문제로 관측될 때.
- **수면 외 CC 특효** — `DamageVsSleeping` 은 kind 하나다. 기절/넉백 특효가 필요하면 CC 선택자를
  붙이는 rev(지금 만들면 소비자 0인 축).
- **궤도 × 히트 루프 통합 회귀 픽스처 보강** [S] — unit 1(궤적 수학)·unit 2(재타격 판정)는 각각
  고정됐고 M5 로 end-to-end 1건을 넣었지만, 첫 프레임 스폰 위치(리뷰 M3)·현(chord) 샘플링
  같은 궤도 고유 성질은 아직 Play 육안이 유일한 검증이다.
- 3장 실아트 (guid 유지 교체).
