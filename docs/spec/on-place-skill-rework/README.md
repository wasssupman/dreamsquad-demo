# on-place-skill-rework — 배치 스킬을 공용 트리거×페이로드 위로 + 3종 재설계

> 상태: units 0~11 구현 완료 (9~11 = 2026-08-17, 커밋 `8995140e`, 사용자 육안 확인됨).
> units 9~11 배경 — unit 8 이 넣은 임자 게이트가
> 실전에서 회귀를 만들었다(예고 중 이동한 적에게 **피해 0**, 뭉친 적의 낙하가 한 발로 접힘).
> 원인은 상수가 아니라 구조다: **탄 하나에 조준이 둘**(궤적=칸/발사시점, 페이로드=적/착탄시점).
> 자세히는 `10_skyfall_on_target_axis.md`.
> 인계: `7_handoff_summary.md`
> rev3 = 투트랙 스펙 리뷰(설계 critic + ecs-reviewer, 양측 REQUEST CHANGES) 반영 +
> 사용자 결정 2건(캐논 = 1:1 융단폭격 / 이관 방향 = 규칙으로 수렴, 이번 범위는 신규 2종).
>
> 선행/모체: `docs/spec/defender-on-place-skills/`(현행 배치 스킬 파이프라인),
> `docs/spec/nightmare-catcher/`(보스 트리거×페이로드 프레임워크),
> `docs/spec/defender-ability-assets/`(능력 서브에셋 — 후속 후보 「라이더 3그룹 이관」),
> `docs/spec/projectile-emission-pattern/`(발사 명세 SO + emitter),
> `docs/spec/aggro-targeting/`(후속 후보 「도발(에픽 가디언)」),
> 파킹 설계: `docs/plans/2026-07-15-effect-trigger-unification-design.md`.

## 상위 목표

**둘을 한 번에 한다.**

① **메커니즘 통일** — 「실행 조건 만족 → 스킬 실행」은 적이든 방어유닛이든 같아야 한다
(사용자 지시 2026-08-16). 지금은 적/보스/드림캐쳐 카드만 `DcMechanic{트리거 × 페이로드}` 데이터로
선언하고, 방어유닛 **자기** 배치 스킬은 `OnPlaceEffectType` enum(값 10개 중 9개 사용,
`SlowPulse` 는 사장) + `BattleBridge` 하드 switch 라는 **별개 어휘**를 쓴다. 트리거 어휘에
`OnPlace` 를 추가하고 방어유닛이 자기 규칙을 선언할 자리를 만들어, 배치 스킬이 **같은 슬롯·같은
감지 규약** 위에 앉게 한다.
(⚠ `DcTriggerKind.OnRetire`(8)는 이미 방어유닛 사건이고 발화도 브리지 `RetireDefender` 다 —
다만 **드림캐쳐 카드 전용**이라 유닛이 자기 규칙으로 선언하는 길은 아직 없다.)

② **3종 재설계** — 캐논·배스티온·말파이트의 배치 스킬을 그 유닛이 **평소 못 하는 일**로 바꾼다.
지금 셋은 전부 "평소 하던 것을 배치 순간에 한 번 더"라 화면에서 읽히지 않는다
(`defender-on-place-skills` unit 4 가 전방 관통 4종에서 확인한 것과 같은 증상).

| 유닛 | 지금 (에셋 실측) | 바꿀 사건 |
|---|---|---|
| 캐논 (Ranger) | `MeleeBurst` r2 · 80 · **전용 연출 없음**(공용 `placementVfxPrefab` 만) | 반경 2 안 **모든 적에게 1:1** 미사일 낙하 (융단폭격) |
| 배스티온 (Guardian, `aggroCapacity 2`) | `MeleeBurst` r1 · 50 + **밀쳐냄** | 반경 2 안 적 **전원**을 N초 도발 (상한·선점 우회) |
| 말파이트 (Fighter) | `StunNearby` r1 · **0.8초** = 자기 평타 넉업과 동일 | 반경 2 · **3초** 정지 |

**캐논의 피해 총량은 바뀌지 않는다** — 적당 정확히 80 으로 기존과 같다. 그 근거는 `impactTileRange 0`
**하나가 아니다**: 셀을 겨누는 낙하탄은 반경 0 이어도 그 칸 전원을 때리므로, emitter 가 **칸당 1발**로
접어야 비로소 성립한다(리뷰가 잡은 결함 — 안 접으면 같은 칸 2기가 각자 160). 바뀌는 것은 예고 0.4초와 하늘에서 내려오는 그림이다. 사용자 의도(2026-08-16):
*"융단폭격을 비주얼로 살리는 것"*. 정체성 근거 = 평타는 단일 대상 1발, 배치는 **다대일 동시 1:1** —
평타로 구조적으로 불가능하다.

배관이 이미 진영 중립이라 ①의 실비용은 작다: `DcTriggerSlot` 은 방어유닛에도 붙고
(드림캐쳐 카드 경로, `BattleBridge.Dreamcatcher.cs:757`), `BossPeriodicTriggerSystem` 은 이름만
Boss 고 게이트는 슬롯 유무다. 없는 것은 **`OnPlace` 트리거**와 **방어유닛 자기 규칙의 집** 둘뿐이다.

## 검증 질문

> ① **캐논의 배치 스킬이 신규 페이로드 0개로 만들어지는가?** 기존 `EmitProjectilePattern` 을
> 그대로 쓰고 발사 축(범위·fan-out)만 데이터로 열면 된다 = 어휘 공용화가 실증된다.
> (rev2 의 "코드 0줄"은 폐기 — 1:1 fan-out 은 발수가 후보 수에 따라 동적이라 emitter 조각이
> 필요하다. 대신 그 조각은 **패턴 시스템 안**이고 배치 스킬 전용 코드가 아니다.)
> ② 배치 순간에 **무슨 일이 일어났는지 화면만 보고 말할 수 있는가?** 캐논 = "적마다 미사일이
> 하나씩 떨어졌다", 배스티온 = "적들이 한꺼번에 나한테 달려왔다", 말파이트 = "적들이 한동안 멈췄다".

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 토대 | `0_onplace_trigger.md` | `DcTriggerKind.OnPlace` + 방어유닛 규칙의 집(능력 SO) + bake 진영 중립화 + 감지자 + payload arm 공용 추출 |
| 1 | 발사 | `1_pattern_scope.md` | 패턴 후보를 host 반경으로 제한(`scopeTileRange`) — 순수함수 + emitter 필터 |
| 2 | 캐논 | `2_sky_strike_cannon.md` | 에셋 4개 + 배선. **코드 0줄이 목표** |
| 3 | 도발 토대 | `3_taunt_state.md` | 시한 어그로(`Aggroed.remainingTime`) + 획득 요청 채널 확장 |
| 4 | 배스티온 | `4_taunt_bastion.md` | `AreaTaunt` 페이로드 + 에셋 |
| 5 | 말파이트 | `5_malphite_stun.md` | 범위·지속 상향 + 띄움 길이 분리 (레거시 경로 잔존) |
| 6 | 문안·검증 | `6_text_and_validation.md` | 설명 문안 + 시트 `desc` + Play 육안 검증 |
| 7 | 인계 | `7_handoff_summary.md` | 커밋·계약·실측 요약 |
| 8 | 캐논 rev | `8_visual_density_fanout.md` | 미사일 1발 = 적 1기 — 착탄에 임자(`target`) 게이트를 넣어 unit 1 의 «칸당 1발» 접기 제거 |
| 9 | 재현 | `9_stale_aim_repro.md` | 낡은 조준을 빨갛게 — 예고 중 이동한 적에게 피해 0 · 같은 칸 착탄점 0.28타일. **가만히 있는 더미가 이 결함을 가렸다** |
| 10 | 축 개통 | `10_skyfall_on_target_axis.md` | 근본 수정 — 「하늘낙하 × **적** 조준」 짝(`SkyFallOnTarget`)을 열어 탄의 조준을 하나로 되돌린다 |
| 11 | 철거 | `11_remove_dual_aim.md` | unit 1 접기 · unit 8 게이트 · `SubCellOffset` 삭제(소비자 0). `TileAoe` 가 다시 순수 광역 |

> 순서 근거: 0 → 1 → 2 가 캐논 사슬(0·1 은 각자 단독으로 아무 동작도 안 한다 —
> `projectile-emission-pattern` unit 2 선례). 3 → 4 가 배스티온 사슬. 5 는 독립. 6 은 마지막.

## Feature-wide 계약 (load-bearing)

1. **실행 메커니즘은 진영을 가리지 않는다.** 배치 스킬은 `DcMechanic{trigger, payload}` 규칙이다.
   적/보스가 쓰는 슬롯(`DcTriggerSlot`)·감지 규약·페이로드 어휘를 그대로 쓴다.
   ⚠ 이 spec 은 **ISkill 통합 레이어를 만들지 않는다**(사용자: "추후에"). 하는 일은 배치 스킬을
   그 레이어가 흡수할 수 있는 **형태(rule)로 표현**하는 것뿐이다.
2. **`OnPlaceEffectType` enum 을 더 늘리지 않는다.** 신규는 전부 규칙으로 간다.
   `directionalAttack` flag → 능력이 선언하는 `RequiresFacing` 으로 갈아탄 선례
   (`defender-ability-assets` 계약 4)와 같은 형태다.
   - ⚠ **만료 조건(과도기가 영구가 되지 않게)**: 이번엔 캐논·배스티온만 옮기므로 레거시 arm 은
     **하나도 죽지 않는다** — `MeleeBurst`(4)는 **Bruiser** 가, `StunNearby`(9)는 말파이트가
     계속 쓴다. 그래서 **다음 on-place 작업이 레거시 전량 이관을 선행 조건으로 삼는다.**
     그때 필요한 신규 kind 는 `AreaCc{DcCcKind}` 하나뿐이고(Bruiser 는 기존 `SelfTileAoe` 재사용),
     그 시점에 enum·브리지 switch·flat 필드 7개가 실제로 사라진다.
   - **수렴 방향(사용자 결정 2026-08-16)**: 정본은 `트리거 × 페이로드` 규칙이고 장차 `ISkill`
     이 그 엔진에 이름을 준다. 반대 방향(능력마다 전용 ECS 상태 + 전용 시스템)으로 가지 않는다 —
     그게 지금 캐스트 4종(volley/hazard/shield/bomb)의 모습이고 새 스킬마다 시스템이 하나씩 는다.
     장기 이관 순서: **on-place → 캐스트 4종 → 기믹 bespoke**(파킹 설계의 매핑표 그대로).
3. **페이로드는 기존 어휘를 먼저 쓴다.** 신규 `DcPayloadKind` 는 **실제로 표현 불가일 때만**
   append. 이번에 신규는 `AreaTaunt` 하나 — 캐논은 `EmitProjectilePattern`(17) 그대로,
   말파이트는 레거시 경로 유지.
4. **방어유닛 자기 규칙의 집 = 능력 SO.** `DefenderUnitData` 에 flat 필드를 늘리지 않는다
   (`defender-ability-assets` 후속 후보가 "통합 착수 시 ability SO 가 그 rule 의 데이터 홈이
   될 수 있음"이라 예약해 둔 자리). 적의 `AttackUnitData.nightmareMechanics` 와 대칭.
5. **페이로드 arm 을 복제하지 않는다.** `EmitProjectilePattern` 실행은 이미 `AttackSystem` 과
   `BossPeriodicTriggerSystem` **두 곳에 사본**이 있다. 세 번째를 만들지 말고 공용 헬퍼로 뽑는다
   — 통합 레이어가 올 때 그 헬퍼가 이관 단위가 된다.
6. **탄의 성질은 barrel SO 소유, 패턴은 복제하지 않는다**(`projectile-emission-pattern` 계약 3).
   폭발 반경·낙하 높이·비주얼은 `ProjectileData`, 패턴 SO 는 selection·shots·scope·telegraph 만.
7. **도발은 어그로의 별도 레이어가 아니라 어그로 자체의 시한 획득이다.** `Aggroed` 에 시간
   한 필드. 상위 레이어를 새로 두면 `Aggroed` 를 읽는 소비처 6곳이 전부 "둘 중 어느 쪽이냐"를
   물어야 한다 — 보스 어그로 면역이 **부착 1곳 차단**으로 풀린 것과 같은 판단.
8. **도발도 어그로 획득 게이트를 전부 통과한다.** 우회하는 것은 **capacity 상한과 선점** 둘뿐.
   보스 면역 · `EnemyTargetFilter`(유닛을 안 노리는 거점 전담 적) · 공격 수단 부재 ·
   **도달 불가**는 그대로 막는다. 도달 불가를 풀면 `aggro-tile-chase` 가 없앤 좀비가 부활한다.
9. **새 on-place 경로는 「이번 프레임 합법 후보」만 본다** — `DeadTag`·`UltimateLeapState` 제외
   (`defender-on-place-skills` unit 4 계약). emitter 의 방어유닛 host 풀은 **이미 이 계약을
   지킨다**(`WithNone<DeadTag, UltimateLeapState>`, 실측 확인).
   ⚠ 그러나 **`AggroStateSystem` 드레인에는 `UltimateLeapState` 게이트가 없다**(`DeadTag` 만).
   오늘은 보스 면역이 우연히 가려 줄 뿐이므로, `AreaTaunt` 의 후보 쿼리가 **직접** 빼야 한다.
   (rev2 초안이 "드레인이 둘 다 검사한다"고 적었던 것은 거짓이다.)
10. **밀쳐냄과 도발은 같은 유닛에 공존하지 않는다.** 배스티온의 `onPlacePush` 는 0 으로 끈다.
    한 배치에서 반대 방향 두 힘이 걸리면 어느 쪽도 안 읽힌다.

## 파이프라인 커버리지 — 캐논 낙하탄 (투사체 아키타입)

`docs/reference/object-pipeline-map.md` §투사체 대조. **정거장 신설 0, 스폰 진입점 신설 0**:

| 정거장 | 앵커 | 이번 spec |
|---|---|---|
| 데이터 SO | `Data/ProjectileData.cs` · `ProjectilePatternData.cs` | **+`Projectile_CannonStrike`·`Pattern_Cannon_Strike`**(에셋) · 패턴 SO 에 필드 2개(`scopeTileRange`·`fanOutToAllCandidates`) |
| 스폰 진입점 | RESOLVE / 폭탄 / 캐스트 드레인 / **emitter** | **무변경** — 기존 emitter 진입점 재사용 |
| ECS 컴포넌트 | `ProjectileState`·`EmitterInstance`·`PatternSlot` | **무변경** (방어유닛 emitter 배선은 머신거너 선례 그대로) |
| 시뮬 시스템 | `ProjectileMoveSystem`·`HitSystem`·`ProjectileEmitterSystem` | emitter 에 scope 필터 + fan-out 발수. Move/Hit **무변경** |
| 이벤트 큐 | `ProjectileHitEventsSingleton` | **무변경 — 신규 채널 0** |
| View/Pool | `ProjectileViewPool` | **무변경** — SkyFall 낙하 view-Y arm 재사용 |
| 씬 wiring | 브리지 `_projectileViewPool` | **무변경** |

도발/스턴은 투사체가 아니라 상태라 이 표의 대상이 아니다(어그로 오버헤드 표시는
`aggro-targeting` unit 13 reconcile 이 `Aggroed` 유무로 자동 처리 — 신규 배선 0).

## 후속 후보 (스코프 밖)

- **레거시 배치 효과 전량 이관** [M] · **계약 2 의 만료 조건 — 다음 on-place 작업의 선행 조건이다.**
  `OnPlaceEffectType` 의 남은 어휘를 규칙으로 옮기고 enum·브리지 switch·flat 필드 7개를 걷어낸다.
  실측 사용처: `MeleeBurst`(**Bruiser**) · `StunNearby`(말파이트) · `BindNearby`(Archer) ·
  `BoostNearbyDefenders`(Guardian) · `GainCost`(Scout) · `ReduceSkillCooldown`(Ranger) ·
  `ApplyStackNearby`(Slasher) · `DotNearby`(Busters) · `ForwardProjectile`(전방 관통 4종).
  겹치는 페이로드가 이미 있다: `MeleeBurst`≈`SelfTileAoe`, `ApplyStackNearby`≈`ApplyStackToTarget`.
  신규로 필요한 건 `AreaCc{DcCcKind}` 하나 정도. `defender-ability-assets` 후속 후보
  「라이더 3그룹 이관」과 같은 작업이다.
  ⚠ **현재 배치 스킬 PlayMode 커버리지는 3종뿐**(`DotNearby`·`ApplyStackNearby`·`ForwardProjectile`).
  이관 전에 테스트를 먼저 깔지 않으면 회귀를 못 잡는다.
- **`scope × 타겟 잠금` 한계** [S] · `reselectPerShot=false` 로 잠근 대상이 스코프 밖으로 걸어나가도
  남은 발이 따라간다. fan-out(캐논)은 미해당이나 다음 소비자가 밟는다.
- **단일 선택 경로의 안정 키** [S] · `projectile-emission-pattern` 후속 후보 그대로 **살아 있다.**
  fan-out 은 모든 후보를 1회씩 때려 순서 무관이지만, RoundRobin/Shuffle 단일 선택으로 방어유닛
  패턴을 열면 같은 셀 적의 tie-break 가 스냅샷 순서에 걸린다.
- **ISkill 통합 레이어** [L] · `docs/plans/2026-07-15-effect-trigger-unification-design.md` 의
  파킹된 설계(트리거 엔진 중립화 + `EffectDomain` 태그). 이 spec 이 만드는 `OnPlace` 트리거와
  공용 payload 헬퍼가 그 이관 단위가 된다. **착수 시점은 사용자 결정.**
- **전방 관통 4종 재설계** [M] · `defender-on-place-skills` 의 미해결 항목. 같은 축이지만
  4종이 서로 구분되지 않는 문제(머신거너·마크스맨이 동일)가 얽혀 별건.
- **배치 페이즈 발동 정책** [M] · 전투 시작 전 배치는 적이 없어 셋 다 통째로 낭비된다
  (`_onPlaceTriggeredEntities` 는 발사 여부와 무관하게 시도를 소진한다).
  ⚠ **이 spec 이 그 비용을 키운다** — 전엔 최소한 즉발 피해가 있었지만, 이제 배스티온은 배치
  스킬이 **그 유닛의 전부**라 적 0마리 배치는 코스트 5 를 내고 화면에 아무 일도 안 일어난다.
  최소 대응 후보: 규칙 경로에 한해 「후보 0이면 시도를 소진하지 않는다」(레거시 5분기 회귀
  표면 0) 또는 전투 시작 시점 지연.
  ⚠ **캐논은 그보다 나쁘다(실측 2026-08-16, unit 2).** 투사체를 쓰는데 브리지의
  `DrainProjectileSpawnRequests` 가 `Update` 의 `if (!_running) return;` 아래라, 배치 페이즈엔
  트리거·스코프·fan-out 이 다 돌아 **캐리어까지 만들어지고도**(실측 `maxCarrier=3`) 투사체가
  0이다. 낭비가 아니라 **요청이 큐에 남는다** — 전투가 시작되면 뒤늦게 터질 수 있다. 이
  후속 후보를 착수할 때 «소진 정책» 뿐 아니라 **잔류 캐리어 처리**도 같이 봐야 한다.
- **도발 연출** [S] · 현 어그로 표시는 머리 위 "!" 플레이스홀더다(`aggro-targeting` 후속
  「어그로 아이콘 → unit-status-fx 승격」). 집단 도발이 그 첫 소비자로 어울리지만 이번엔
  상태만 만든다 — **연출로 사건을 만들지 않는다**(unit 4 교훈).
- **도발 중에는 평타 어그로가 멈춘다** [S] · **사용자가 Play 에서 「어색하다」고 관측**
  (2026-08-16). 도발이 상한을 우회해 5기를 붙잡으면 `held`(5) > `max`(2)가 되고, 그 5초 동안
  `AggroPolicy.CanAcquire` 가 막혀 **배스티온이 때린 적이 새로 안 끌려온다.** 상한 우회의
  회계상 귀결이고 계약대로지만, 화면에서는 「배치 직후엔 평타 어그로가 죽는다」로 읽힌다.
  → 고치려면 **Pass 2 의 `held` 재계산에서 도발분(`remainingTime > 0`)을 빼면 된다** —
  `held` 가 «히트로 붙잡은 수» 만 세게 되어 도발 중에도 평시 상한이 정상 동작한다.
  대가: 도발 5기 + 히트 2기 = 동시 7기를 끄는 순간이 생긴다(그게 과한지는 밸런스 판단).
  사용자 판단 「일단 ok」 — 지시 없이 착수하지 않는다.
- **도발 만료 후 이전 가디언 복귀** [S] · 지금은 만료 시 완전 해제하고 다음 히트에 재획득한다.
- **패턴 selection rule 확장 / 무타겟 패턴** [S] · `projectile-emission-pattern` 후속 후보 그대로.
