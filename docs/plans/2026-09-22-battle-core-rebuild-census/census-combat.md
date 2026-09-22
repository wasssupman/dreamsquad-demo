# 전투 판정 — 키워드 census

> 도메인: 전투 판정 (attack → reach → damage). 읽기 전용 조사, 2026-09-22.
> 모든 포인터는 grep 으로 존재를 확인했다. 결정 이력을 못 찾은 것은 「이력 미발견」으로 적었다.

| 키워드 | 한 줄 정의 (게임 언어로) | 현행 구현 포인터 | 판정 | 애매하면: 결정 이력 포인터 + 미결 요지 |
|---|---|---|---|---|
| 통합 공격자 루프 | 방어유닛·적·도발받은 적·순찰병이 **같은 한 루프**에서 때린다. 진영별 분기는 「이 컴포넌트를 갖고 있나」로만 갈린다 | `AttackSystem.OnUpdate` 단일 foreach | 확정 | |
| 후보 스냅샷 | 때릴 수 있는 것들을 프레임 시작에 한 번 모은다. **전 진영 통합 풀**이고 진영 판정은 공격자마다 따로 | `AttackSystem` `targetCandidatesQuery` | 확정 | ⚠ 「미리 걸러져 있다」고 읽으면 아군 오사. 2026-08-12 리뷰가 초판 스펙의 그 거짓 전제를 잡은 이력이 코드 주석에 박혀 있다 |
| 후보에서 빠지는 것 3종 | 배치 중 · 사망 대기 · 궁극기로 판 밖에 나간 자. 일반 도약은 **비행 중에도 맞는다** | `WithNone<PendingDeployment, DeadTag, UltimateLeapState>` | 확정 | 마음은 본능이 살아 있는 동안 추가 제외(`CoreShielded`, heart-stress-axis unit 6) |
| 타겟 진영 마스크 | 「내가 누구를 때리나」의 저작 비트 | `AttackState.targetMask` | 확정 | attack-system-loop-unify 계약: 마스크가 source of truth |
| 타겟 통행층 | 지상 전용 공격이 하늘로 안 번진다 | `AttackState.targetTraversalLayers` · `PlacementLayers.CanTarget` | 확정 | waypoint-routing unit 4 rev 4 |
| 근접은 지상 전용 | 사거리 1 유닛은 공중을 못 때린다(손이 안 닿는다) | `DefenderUnitData` targetTraversalLayers 툴팁 + 에셋 lane 불변식 | 확정 | 2026-09-03 결정(커밋 bc37b57c). 원거리는 Path\|Air, 지상 전용 원거리는 아틸러리·폭탄맨 둘 |
| 획득 / 유지 / 정지 | 「이제 때릴 수 있나」 「문 것을 놓나」 「멈춰도 되나」 셋을 구분한다 | `AttackReach` 헤더의 소비처 12 목록 | 확정 | distance-based-range 4d. 이동 정지에 유지 임계를 쓰면 적이 사거리 밖에서 멈춘다 |
| START / RESOLVE | 공격은 두 박자다. START 는 애니·쿨다운·선딜 시작, RESOLVE 는 실제 타격 | `AttackSystem` `doResolve` 분기 | 확정 | attack-hit-delay unit 1 |
| 선딜(히트 딜레이) | 휘두르기 시작부터 맞기까지의 시간. 0 이면 같은 프레임 즉발 | `AttackState.hitDelaySec` / `hitDelayRemaining` | 확정 | |
| 실주기 = max(간격, 선딜) | 선딜이 공격 간격보다 길면 **선딜이 실제 발사 주기** | `AttackSystem:1010` `attackAnimPeriod` | **애매** | 공속 배율(`attackSpeedMul`)은 `attackInterval` 에만 곱해져, 선딜이 긴 유닛은 공속 버프가 무효인데 **실효 스탯 표시는 전량을 보여준다.** 이력: `battle-core-architecture` 재정합 미해결 4건 중 4번(코드 대조 전, 수용 확정 아님) |
| 쿨다운은 CC 중에도 돈다 | 묶여 있어도 쿨은 차고, 풀리는 즉시 때린다 | `AttackSystem` 쿨다운 틱이 `actionLocked` 판정 **앞** | 확정 | combat-action-lock. 쿼리에서 빼면 진행 중 스윙까지 얼어 CC 와 규약이 갈린다 |
| 행동 잠금 | 기절·수면 ‖ 도약 비행. **START 만 막고** 이미 시작한 스윙은 완료된다 | `CcActionLock.IsLock` · `UnitActionPhase.Resolve` | 확정 | leap-flight-state unit 0 rev 가 도약을 같은 술어에 합류시켰다. 넉백은 외력이라 이 표에 없다 |
| 행동 상태(랭크) | 배치중 > 잠김 > 교전중 > 유지중 > 대기. 공격을 **시작**할 수 있는 것은 대기·유지중 둘 | `Wassup.UnitAi` `DefenderAi.Resolve` · `DefenderAiStateSystem` | 확정 | defender-autobattle-ai unit 2. 상태는 밖에서 정하고 안에 저장(`DefenderAiStatus`) |
| 적 FSM | Marching / Engaging / Chasing / Standoff. 적은 Engaging·Standoff 에서만 발사 | `EnemyAi.Evaluate` · `EnemyAiStateSystem` | 확정 | enemy-ai-fsm 3a. `AttackSystem` 과 **같은 술어 함수**를 봐야 「락은 있는데 Marching」 데드락이 안 난다 |
| 한 공격 안의 타겟 커밋 | 겨눈 대상은 그 공격이 끝날 때까지 안 바뀐다 | `AttackState.committedTarget` / `committedDirection` | 확정 | target-persistence unit 0. 없으면 애니는 A 를 향하는데 피해·투사체·넉백·카드 payload 는 B 로 간다 |
| 지속 락 | 한 번 문 대상은 죽거나 사거리를 벗어날 때까지 유지 | `FocusTarget` · `TargetPersistence.KeepsLock` | 확정 | 사거리 이탈이 해제 사유(D2, 2026-08-09 사용자 확정). 예전엔 이탈해도 락을 붙들어 적이 옆의 방어유닛을 무시하고 골로 걸어갔다 |
| 락 제외 4종 | facing · frontmost · 힐러 · 가디언은 지속 락을 **안 받는다** | `AttackSystem` 방어유닛 락 블록의 게이트 4개 | 확정 | target-persistence unit 4 계약 2. 「빠뜨렸네」 하고 채우면 그 유닛의 정체성이 조용히 망가진다 |
| 히스테리시스 h = 0.1칸 | 유지 판정만 0.1칸 넓게 봐서 경계 진동을 막는다 | `TargetPersistence.HysteresisTiles` | 확정 | `4d_hysteresis.md`. 실측 지터 0.047·0.051 의 약 2배. `sceneKnobs` 등재 금지(configHash 가 움직인다) |
| 최전방(frontmost) | 골에 가장 가까운 적을 먼저 친다. 락이 깨지면 **그 공격은 그냥 날아간다** | `FrontmostTargeting` · `FrontmostAttackLock` | 확정 | dreamcatcher-content-2 「끝을 보는 눈」 계약 2/3. strict lapse 가 카드 계약이다 |
| 동률 해소 | 같은 거리면 **먼저 스폰된 쪽**(SimEntityId 오름차순) | `SimEntityId` · `FrontmostTargeting.RanksBefore` · `LowestHealthTargeting` | 확정 | battle-sim-extraction M0 unit 1. `Entity.Index` 축을 교체한 것 — 할당기 번호는 신 sim 에서 재현 불가 |
| 우선 클래스 / 최저체력 | 특정 클래스 우선 · 힐러는 가장 다친 아군 | `AttackSystem` `prioClass` 분기 · `LowestHealthTargeting` | 확정 | 힐러가 frontmost/facing 카드를 같이 들면 **범위 밖 집합체**(spec follow-up) |
| 어그로 sticky | 도발당한 적은 **가디언만** 본다. 사거리 밖이면 쏘지 않고 걸어간다 | `AttackSystem` aggro override · `AggroPolicy` | 확정 | aggro-targeting unit 5. 배타성이 load-bearing — 풀면 가는 길에 만난 유닛과 싸우다 가디언에 영영 도착 못 한다(`AggroAoeWidthTests`) |
| 어그로는 폭을 안 줄인다 | 붙잡힌 광역 적도 원래 대상 수 그대로 때린다 | `AttackSystem` outputs 경로 주석(elite-whirlpot unit 0) | 확정 | 예전 aggro-targeting unit 8 이 `attackTargetCount` 를 1 로 접어 **어그로가 공격 형태를 바꾸는** 숨은 방어 버프였다 |
| 가디언 다중 선정 | 여유가 있으면 아직 안 물린 적부터, 상한이 차면 이미 겹친 팩을 정리 | `AggroTargeting.SelectTargets` | 확정 | aggro-targeting unit 11 |
| 도발 부여/회수 | 도발이 걸리면 공격 자체가 없던 적에게 **공격을 붙여 준다** | `TauntAttackGrantSystem` · `AggroAttackProfile` · `TauntAttackGranted` | 확정 | 원래 마스크가 있으면 Defender 비트 OR, 없으면 `AttackState` 통째 부여. 해제 시 각각 원복/제거 |
| 집단 도발 | 배스티온이 한 번에 여러 적을 끌어온다 | `on-place-skill-rework/4_taunt_bastion.md` · `AreaTaunt` 페이로드 | 확정 | 히트 구동 도발과 부하 규모가 다르다(같은 spec 3_taunt_state.md) |
| 도달 산식 | `도달 = \|좌표 차\| ≤ 범위 + 원점 항 + 대상의 몸` | `SkillMath.Reach`(private) | 확정 | CLAUDE.md 절대 제약 13 · `battle-core-architecture` §8 불변식 7. 같은 결함이 두 번 나 제약으로 승격 |
| 효과의 형 | **몸에서 나오는 것**(사거리·자기중심 광역·자폭·시체폭발·오라·도발) / **자리에 떨어지는 것**(운석·투사체 착탄·장판·회오리·착지 슬램) | `SkillMath.TryOriginRadius` · `RangeMetric` | 확정 | 2026-09-06 사용자 결정. 「원점이 무엇이냐」가 아니라 **형**이 정한다. 같은 스킬이 두 형을 겸할 수 있고 형을 정하는 것은 **감지자** |
| 공개 진입점 4 | 호출부가 **원점이 무엇인지 선언**하게 만든다 | `SkillMath.ReachFromUnit` / `ReachFromCell` / `ReachWithOrigin` / `ReachFromImpact` | 확정 | distance-based-range unit 23a. 5번째 `ReachFromUnitToCell` 은 2026-09-07 신설 당일 철거(전제가 뒤집혀 소비처 0) |
| 칸 반폭 0.5 | 자리형의 **도형 보정항**. 몸이 아니다 | `SkillMath.CellHalfWidthTiles`(private) · `CellShapePaddingTiles`(표기 전용 접근자) | 확정 | private 이 1차 방어. ⚠ 리터럴 `0.5f` 를 몸 슬롯에 넘기는 것은 여전히 컴파일된다(리뷰 M-1b) |
| 몸 반경의 출처 | 방어유닛 = footprint **가로/2** · 적 = 크기 티어 저작 · 구조물 = 점유 내접원 | `HitRadius` · `DefenderUnitData.BodyRadiusTiles` | 확정 | rev 3(2026-09-01) 원 하나로 회귀. 세로 깊이는 몸에 기여하지 않는다(행 배제, b59e1008) |
| `InReach` / `InCellReach` | 사거리 판정의 단일 술어. 배치 프리뷰 표기도 **같은 본체**를 지난다 | `AttackReach.InReach` · `InCellReach` | 확정 | 표기가 따로 그리면 「밝은 칸인데 안 때린다」가 되고 화면이 규칙을 틀리게 가르친다 |
| 방향 도형 | 원 안 + 「주 대상 쪽 부채꼴/띠」. **부가 타격에만** 곱해진다 | `AttackReach.InReachShaped` · `SkillMath.SectorGate`/`BandGate` | 확정 | directional-attack-shape rev 3(2026-09-12 사용자 결정 「안 1」). **도형은 넓히지 못한다**, 획득·유지·정지는 도형을 아예 안 본다. rev 2(좌/우 축·획득부터 도형)는 폐기 |
| 도형 bake | 각도는 저작 1회에 (sin, cos) 가 된다. sim 은 각을 모른다 | `AttackShapeBaked` · `AttackShapeBake` | 확정 | `AttackState.shape` default = Omni = 항등원이라 코드가 만드는 AttackState 는 안전 |
| 체비셰프 폴백 타겟팅 | 폭탄맨·캐스터 4종이 아직 **사각 자**로 대상을 고른다. 양쪽 몸 0 | `AttackSystem.PickFallbackTarget` · `NearestTargeting` · `GridMath.ChebyshevDistance` | **애매** | `docs/spec/README.md` Follow-up Backlog 「[높음] … 아직 체비셰프 사각」. ⚠ **이름과 달리 폴백이 아니라 유일 경로**(그 아키타입은 RESOLVE 를 안 탄다). 2026-09-06 사용자 지시로 보류 |
| 부채꼴 거리 게이트의 몸 누락 | `SkillCone` 이 양쪽 몸을 0 으로 본다 | `SkillCone` | **애매** | 같은 Backlog [중]. 프리필터만 고치면 변화 0 이라 콘 자체를 고쳐야 한다 |
| 착지 슬램의 형이 데이터로 안 실린다 | sim·뷰·판정 셋이 각자 독립적으로 「자리형」을 선언한다 | `UltimateLeapSystem` · `ReachFromImpact` 의 0→칸 반폭 승격 | **애매** | Backlog [중]. 오늘은 슬램 저작이 짱쎈 하나뿐이라 소스 정규식으로 충분. 활성화 트리거 2개가 명시돼 있고 **그 전에 필드를 만들지 말 것** |
| 저작 툴팁이 아직 "Chebyshev" | 저작자가 툴팁을 믿고 값을 정하면 실제 자(원)와 어긋난다 | `DcMechanic` · `ProjectilePatternData` · `BattleBridge.Dreamcatcher` | **애매** | 같은 Backlog [중]. 코드 동작은 맞고 **문서/툴팁만 stale** |
| 광역 멤버십 | 착탄 칸 반경 안 전원. 모양은 **모서리가 둥근 원** | `TileAoe.IsInRadius`(정본) / `IsInTileRange`(격자 통계로 강등) | 확정 | `4b_area_membership.md`. 시그니처를 안 바꾼 것이 결론 — 소비처 중심이 전부 이미 칸에 물려 있다. `PayloadKind.TileAoe` 주석은 아직 "Chebyshev" 라고 적혀 stale |
| 궤적 9 × 바인딩 3 | 엔티티 바인딩 / 셀 바인딩 / 방향 바인딩. **도착 조건은 궤적이 소유** | `MovementKind` · `MovementBinding` · `ProjectileMoveSystem` | 확정 | projectile-trajectory-payload. 기존 바인딩으로 분류되는 새 궤적은 발사 코드 변경 0 |
| 페이로드 4 | SingleSplash · TileAoe · PathHit · SpawnBlocker | `PayloadKind` · `ProjectileHitSystem` | 확정 | PathHit 에게 「도착」은 착탄이 아니라 **비행 종료**(직선·궤도·부메랑 공유) |
| 저작 토큰 8 → 런타임 2축 | 저작은 1축, 런타임은 (궤적, 페이로드) 로 번역. **번역은 전사가 아니다** | `ProjectileFlightMode` → `ProjectileSpawnRequest` | 확정 | 수류탄·궤도 2 궤적은 저작 토큰이 아예 없다(코드 경로가 직접 고른다) |
| 한 탄에 조준은 하나 | 궤적이 칸을 겨누는데 페이로드가 적을 겨누면 예고 시간만큼 어긋나 헛방 | `MovementKind.SkyFallOnEntity` 헤더의 실측 기록 | 확정 | on-place-skill-rework unit 1·8 이 실제로 낸 사고(예고 0.40s × 속도 2.00 = 0.80타일 > 칸 소속 ±0.50) |
| 요청 = 값 스냅샷 | 쏘고 나면 사수 스탯이 변해도 탄은 안 변한다 | `ProjectileSpawnRequest` · `ProjectileRequestCarrier` | 확정 | 넉백은 **사수 저작**, 관통 예산·재타격 쿨다운은 **탄 SO** 에서 드레인이 채운다 |
| 원점의 몸은 경계를 넘어 실린다 | 즉발 폭발이 투사체 길을 타도 원점은 트리거 대상의 몸 중심이다 | `ProjectileSpawnRequest` 의 원점 몸 반경 필드 · `SkillMath.ReachFromImpact` | 확정 | distance-based-range unit 23. 국소 문맥만 보는 감사는 원리적으로 못 잡는다(unit 23 에서 실제로 놓쳤다) |
| 유효 피격 반경 | `탄의 관대함 + 대상의 몸`. 큰 몸은 큰 표적 | `ProjectileHitSystem:520` · `ProjectileMoveSystem:158` · `SweepHitMath.SegmentHits` | 확정 | 구 backlog 「적별 피격 반경」은 해소됨. ⚠ `hitThreshold` 는 월드, `HitRadius` 는 타일 — 환산 필수 |
| 재조준 | 대상이 맞기 전에 죽으면 같은 반경에서 다시 겨눈다 | `ProjectileState.retargetTileRange` · `BounceRetarget.FindNext` | 확정 | **방향 바인딩은 0 으로 명시**(겨눌 엔티티가 없다). 같은 필드가 두 뜻을 겸하지 않게 |
| 관통 · 재타격 쿨다운 | 경로 스윕은 피해자당 창 1회 | `PathHitRecord.CanHit` · `ProjectileState.pierceRemaining` | 확정 | |
| 튕김(통통구슬) | 맞은 대상을 빼고 가장 가까운 생존자로, 피해는 감쇠 | `BounceRetarget` · `DcAttackModKind.ProjectileBounce` | 확정 | 집계는 count 합 / range max / mul 곱. 방향 유닛 조합은 미개통(backlog) |
| 왕복(부메랑) | 발사 축은 **불변**. 「지금 어느 다리인가」를 저장하지 않는다 | `Boomerang` · `MovementKind.BoomerangReturn` 헤더 | 확정 | direction 되먹임이 초판의 실제 결함(발사점 뒤로 날아갔다) |
| 궤도 = 주인 종속 | 주인이 사라지면 구슬도 사라진다 | `MovementKind.OrbitAroundPoint` · `ProjectileMoveSystem` 궤도 arm | 확정 | 2026-08-17 사용자 결정이 content-4 계약(자기 수명)을 뒤집었다 — 화면에서 빈 자리를 도는 구슬이었다 |
| 발사 명세(패턴) | 「누구를·몇 발·어떤 간격·얼마나 벌려」. **탄의 성질은 복제하지 않는다** | `ProjectilePatternData` · `PatternSlot` · `ProjectileEmitterSystem` · `EmitterTick` | 확정 | projectile-emission-pattern |
| 탄막 난수 씨앗 | `hash(사수 SimEntityId, 발사 카운터)` | `AttackSystem:1326` · `PatternShotRandomizer.Apply` | 확정 | 구 축이 `attackerEntity.Index` 였다(M0 unit 1 에서 교체) |
| 발사 카운터는 durable | 인스턴스는 transient 라 카운터를 못 갖는다 | `PatternSlot.fireCountBase` · `EmitterTick.Begin` | 확정 | 0 에서 다시 시작하면 RoundRobin 이 영원히 같은 rank 를 고른다(spec-review C2) |
| 버스트 중 쿨다운 연장 | 다음 트리거는 마지막 탄이 나간 뒤부터 기다린다 | `EmitterTick.TotalDuration` · `cooldownRemaining +=` | 확정 | 계약 8 |
| 패턴 선정 순위 | 후보 index 로만 지칭하고, 순위 축은 **SimEntityId 오름차순** | `PatternTargeting.Select` | 확정 | 구 row-major 셀 키는 격자 없이 정의되지 않아 unit 18 에서 교체 |
| 「한 칸에 몇 발」 | 발사가 아니라 **착탄의 성질** | `on-place-skill-rework/1_pattern_scope.md` · `PatternScope` | 확정 | 반경 필터는 셀 중복 제거를 하지 않는다 |
| 피해 인박스 | 피해는 버퍼에 쌓였다가 다음 단계가 한 번에 적용 | `IncomingDamage` · `DamageApplicationSystem` | 확정 | Combat→Units 이벤트 채널로 쓰는 Units 소유 버퍼 |
| 실드 흡수 · 파열 | 실드 합이 양수에서 0 이 되는 **그 순간**이 사건 | `ShieldMath.Absorb`/`Sum`/`Merge` · `ShieldBreakEventsSingleton` | 확정 | dreamcatcher-shield-break unit 0. 완전 흡수 히트 = 「피격 아님」(계약 3). 시간 만료는 구조적으로 배제 |
| 킬 귀속 | 그 프레임 피해 중 **source 가 있는 최대치**가 킬러. 동점이면 버퍼 순서 앞 | `KillAttribution.Consider` | 확정 | dreamcatcher-kill-and-threshold unit 2 계약 4. 지속 피해·배치 스킬·환경(source 없음)은 미귀속 = OnKill 미발동(의도) |
| 받는 피해 배율 | 피해 합에 곱해지고, **실드 흡수는 그 뒤** | `ModifierStats.dmgTakenMul` | 확정 | 계약 2 — 표시 데미지 = 흡수량 |
| 데미지 숫자 | 버퍼 엔트리당 폰트 1개, 체력 비율은 **프레임 최종값** | `DamageNumberEvent` · `pierceRatio` 비례 배분 | 확정 | 적 전용(`AttackUnitTag` 필터). 같은 프레임 폰트 전부가 같은 최종 비율을 나른다 |
| 초당 재생 | 모디파이어 재생은 힐 버퍼를 안 타고 직접 더한다 | `ModifierStats.regenPerSec` · `DamageApplicationSystem.OnUpdate` | **애매** | `state.RequireForUpdate<IncomingDamage>()` 때문에 그 버퍼를 가진 엔티티가 하나도 없으면 **재생도 같이 멈춘다.** 「이력 미발견」 — 코드에만 있는 암묵 결합 |
| 힐 펄스 vs 재생 | 펄스만 연출하고 재생은 조용히 | `HealAppliedEventsSingleton` · `hasPulse` 게이트 | 확정 | 마음은 힐 펄스에서 제외(heart-stress-axis unit 2) |
| 피격 시 수면 해제 | 실제로 피해를 입으면 잠이 깬다. 기절은 안 깬다 | `CcClearRequestsSingleton` · `CcClearSystem` | 확정 | combat-action-lock unit 3. Units 가 CC 를 직접 못 지워 이벤트로 Effects 에 위임 |
| 무적이 아니라 드랍 | 궁극기 이탈·마음 방패는 피해 버퍼를 **비운다** | `UltimateLeapState` / `CoreShielded` 분기 | 확정 | 쿼리에서 빼면 적립됐다 착지 프레임에 통째로 터진다. 힐 버퍼도 같이 비운다(코드 리뷰 발견) |
| 위협표 | 보스를 누가 제일 많이 때렸나 | `ThreatTable.Accumulate` · `ThreatHitEventsSingleton` · `TryCredit` | **애매** | `ThreatTable.Leader` 는 **런타임 소비자 0**(blink 목적지가 `HealthThresholdSystem` 으로 떠났고 누적만 돈다). 감쇠 없음도 nightmare-catcher spec follow-up |
| 넉백 | 미는 방향은 **적이 가던 방향의 반대** 하나 | `DefenderCcData.knockbackDistance` · `PathFollowState.lastMoveDir` | 확정 | 2026-08-17 사용자 결정 B. 상대속도 합산은 은퇴(지나쳐 가는 적을 골 쪽으로 밀어줬다) |
| 착탄 넉백 | 유도탄은 넉백을 착탄까지 미룬다 | `knockbackAtImpact` · `ProjectileHitSystem` SingleSplash arm | 확정 | defender-knockback-on-impact unit 1 |
| 보스 면역 집합 | 기절·수면·넉백은 보스에게 안 걸린다(출처 불문) | `CcActionLock.IsBossImmune` | 확정 | boss-jjangssen unit 3·8. 스택 임계발 예외는 DoT 분리 후 폐기 |
| 넉업 | 심에서는 **짧은 기절**이고, 띄우는 연출은 띄운 쪽이 따로 신호한다 | `KnockupVisualEventsSingleton` · `DefenderCcData.knockupOnHitSec` | 확정 | knockup-fighter-defender unit 3. 뷰가 `CcEffect.kind` 로 판단하면 일반 스턴까지 떠오른다 |
| 수면 부여 | 주 대상 1체만. 넉백과 같은 스코프 | `DefenderCcData.sleepOnHitSec` | 확정 | 자기 히트가 자기 Sleep 을 안 깨우는 것은 **시스템 순서**가 보장 |
| 더블파이어 충전 | 다음 공격 한 번을 즉시 더 쏜다 | `NextAttackDoubleFire` · `DamagedCounter` · `SimIntentKind.GrantCharge` | 확정 | 각 발이 온전한 공격이라 카드 틱·CC·로그가 발마다 한 번씩(critic H3/H4 회피) |
| 공격 연출 신호 | 애니 주기는 **실주기**를 싣는다 | `UnitAttackVisualEvent.attackAnimPeriod` · `UnitAttackVisualEventsSingleton` | 확정 | attack-anim-speed-match unit 1. 애니가 실발사보다 빨리 끝나지 않게 |
| 빔 세션 | 고속 틱을 뷰가 세션으로 뭉친다. **심 개념이 아니다** | `BeamPresenter` · `BattleBridge.BeamSessionTtlMargin` | 확정 | |
| 지연 히트 VFX | 착탄 연출을 배틀 도메인 델타로 깎아 늦게 튼다 | `BattleBridge._pendingHitVfx` | 확정 | 슬로모에서 연출이 시뮬과 갈리지 않게 |
| 공격 출력 로그 | 출력 하나마다 한 줄. 로그·트레이스의 첫 축 | `AttackOutputLogEventsSingleton` · `LegacyTraceRecorder.Ev` | 확정 | CLAUDE.md 「로깅은 마지막이 아니라 첫 축」 |
| 출력 목록 | 피해·힐·스탯·스택을 한 공격이 여러 개 낸다 | `AttackOutputElement` · `AttackOutputKind` | 확정 | modifier-framework unit 5 |
| 캐스트 = 그 유닛의 공격 사건 | 사거리 0 캐스터는 RESOLVE 에 못 가므로 캐스트 성사가 공격이다 | `CastEventsSingleton` · `HazardCastSystem`(`UpdateBefore(AttackSystem)`) | 확정 | attack-decoupling unit 4. 같은 프레임 소비가 순서 계약 |
| 공격 사건 1회 = 1카운트 | 한 host 가 같은 프레임에 두 번 세지 않는다 | `AttackSystem` `castCountedHosts` | 확정 | 계약 2. 시트가 캐스터 사거리를 3 으로 확정하며 「attackRange 0 이라 우연히 성립」이 깨진 이력 |
| 강공 pre-scan | 「이번이 N번째인가」를 미리 보고 배율을 접는다 | `DcTrigger.WouldFire` ↔ `DcTrigger.Tick` 합성 불변식 | 확정 | trigger-gates unit 1. 게이트 실패면 카운터도 안 오른다 |
| 배럴 폭발 | 길막 설치물이 **부서지는 순간** 그 칸에 광역 | `BarrelExplosionSystem` | 확정 | 폭발 로직 없음 — 즉발 투사체 요청 하나를 놓고 `ProjectileHitSystem` 이 해결 |
| 호접몽 | 완주하면 보상, 맞으면 파탄 | `DreamCocoonSystem` · `SimIntentKind.BeginDreamCocoon` | 확정 | 마지막 프레임 동시(피격+만료)는 **파탄 우선**(critic M2) |
| 궁극기 도약 | 이탈 → 예고 → 강습. 2초는 **회피 창이자 피해 게이트** | `UltimateLeapSystem` · `BlinkRequestEventsSingleton` · `UltimateLeapVisualEventsSingleton` | 확정 | 시퀀스를 sim 이 소유(일반 도약의 비행 창이 브리지 소유인 것과 비대칭이 맞다). 착지 슬램은 **자리형**(2026-09-07 정정) |
| 순간이동 적용 | 위치는 Movement 소유라 요청으로 넘긴다 | `BlinkApplySystem` · `BlinkMath` | 확정 | nightmare-catcher unit 3 |
| 주기 트리거 | N초마다 도는 슬롯. **진영 중립** | `BossPeriodicTriggerSystem` | 확정 | dreamcatcher-content-4 unit 0 — 「디펜더는 period 0 이라 건너뛴다」는 bake 누락이 만든 우연이었다 |
| 라스트런 | 시간이 끝나면 최대 체력의 절반을 스스로 깎는다 | `LastRunSystem` · `RedBullGimmickConfig` | 확정 | source 없음(자해, 킬 미귀속) |
| 감지 반경 | 「저 놈을 발견했나」. 사거리와 **같은 술어·다른 반경** | `DetectionSystem` · `DetectionRange` · `AttackReach.InReach` 소비처 12 | 확정 | enemy-detection-range. 관성(grace) 1초 중에는 `hunting` 이 유지돼 연속 사냥이 표식을 한 번만 낸다 |

**행 수: 88** (확정 81 · 애매 7)

---

## ECS 고유라 새 설계에서 개념 자체가 사라지는 것

- **`RequireForUpdate<T>` 게이트** — `AttackSystem`은 `AttackState`, `DamageApplicationSystem`은 `IncomingDamage`, `ProjectileHitSystem`은 `ProjectileTag`, `LastRunSystem`은 `RedBullGimmickConfig`를 요구한다. 나르는 규칙은 「이 종류가 판에 하나도 없으면 이 단계 전체를 건너뛴다」이고, **그 단계가 겸하던 무관한 일까지 같이 멈춘다**(재생이 그 사례). 새 설계에서는 단계 실행 조건을 명시로 쓰고, 한 단계에 성격이 다른 일을 겸직시키지 않아야 한다.
- **요청 캐리어 엔티티 + ECB** — `ProjectileRequestCarrier`는 「host가 같은 프레임에 자기 공격으로도 요청을 올릴 수 있다」는 이유만으로 존재한다(dc-trigger 계약 6). 나르는 규칙은 **한 프레임에 같은 주체가 서로 독립인 발사를 여러 개 낼 수 있다**이고, 새 설계에서는 요청 리스트로 충분하다.
- **`[UpdateBefore]`/`[UpdateAfter]` 순서 어트리뷰트** — 캐스트를 같은 프레임에 소비하기(`HazardCastSystem` → `AttackSystem`), 사망 표시가 붙은 뒤 파괴 전 창에서 터지기(`BarrelExplosionSystem`), 피격 해제 뒤 자연 만료 전에 판정하기(`DreamCocoonSystem`), 도발 부여를 같은 프레임에 보이게 하기(`TauntAttackGrantSystem`). 어트리뷰트는 사라져도 **이 순서들은 게임 규칙이라 명시 단계 목록으로 살아남아야 한다.** 순서 어트리뷰트가 없는 시스템(`MovementSystem` 등)이 토폴로지 tie-break에 얹혀 있는 상태도 같이 정리 대상이다.
- **`HasComponent` 자연 분기 = 아키타입 판별** — 폭탄맨·소환사·가디언·힐러·frontmost·방향탄·캐스터가 전부 「이 컴포넌트를 갖고 있나」로 갈린다(`AttackSystem` 안에만 `defenderTagLookup` 게이트가 7곳). 나르는 규칙은 **「이 유닛은 어떤 종류의 공격자인가」**이고, 새 설계에서는 타입 체크가 아니라 **정책 값**으로 가야 한다. `DefenderAttackPolicy`(Target/Bomb/Summon)가 이미 그 형태의 선례다.
- **`DynamicBuffer` 인박스의 암묵 타이밍** — `IncomingDamage`/`IncomingHeal`/`IncomingShield`는 「이번 프레임에 쌓고 다음 단계에서 비운다」가 계약이고, 실드 부여만 **다음 프레임 드레인이 의도**다. 버퍼가 사라지면 이 한 칸 차이가 조용히 없어진다.
- **NativeQueue 싱글턴 채널** — 이 도메인만 `UnitAttackVisual`·`Knockup`·`Cast`·`ThreatHit`·`AttackOutputLog`·`DamageNumber`·`HealApplied`·`ShieldBreak`·`ShieldGranted`·`EnemyCc`·`CcClear`·`Detection`을 쓴다. 나르는 규칙은 **맥락 경계를 넘는 쓰기 금지**이고, 새 설계에서 맥락이 하나가 되면 채널 자체는 불필요하지만 **「사건이 났다」와 「그것을 실행한다」의 분리**는 트레이스·로그의 첫 축이라 남겨야 한다.
- **`Entity.Null` 센티널과 엔티티 동등성** — 락 유효성·킬 귀속·주인 생존·커밋 타겟이 전부 이것에 기댄다. `SimEntityId`가 이미 프로세스 밖으로 나가는 유일한 ID이므로 그쪽으로 수렴해야 한다(불변식 17).
- **쿼리 랭크가 우선순위 표의 위 두 칸을 담당** — `UnitActionPhase.Resolve`가 Dead·Deploying을 **인자로 안 받는** 이유가 `WithNone<PendingDeployment, DeadTag>`가 이미 걸러서다. 쿼리가 없어지면 그 두 랭크를 함수가 직접 받아야 하고, 안 그러면 표가 조용히 3단으로 줄어든다.
- **Burst/lookup 함정** — `OnUpdate` 안의 `GetComponentLookup`을 지우면 Burst가 조용히 깨지는 재발이 5회라 소비처 0인 `_facingRetiredLookup`이 좀비 필드로 남아 있다. 이것은 **옮길 규칙이 아니라 사라져야 할 것**이다.

## 코드에만 박혀 있고 문서에 없는 규칙 (rebuild가 놓치기 쉬운 것)

- **폭탄맨은 던진 프레임에만 쿨다운을 리셋한다.** 사거리에 적이 없으면 쿨을 만료 상태로 **대기**시켜, 적이 들어온 프레임에 즉시 투척한다. 리셋 위치를 옮기면 최대 한 쿨 늦는다.
- **소환사는 소환물이 살아 있어도 쿨을 리셋한다.** 스폰만 건너뛴다. 이 리셋이 없으면 소환물이 죽는 즉시 재소환이 되어 동작이 바뀐다. 그래서 `Sustaining` 상태도 공격 START를 **시도**한다.
- **폭탄맨·소환사는 타겟 선정 앞에서 처리하고 루프를 빠져나간다.** RESOLVE에 두면 소환사의 근접 사거리 안에 적이 들어와야만 소환돼 순찰병이 마중 나갈 시간이 없어진다.
- **카드 슬롯이 발동했는데 실행할 팔이 없으면 경고 로그를 낸다.** 카운트는 이미 소비된 채로. 조용한 no-op 금지가 계약이고 `AttackSystem`에 세 군데(캐스트 드레인·폭탄 발사·RESOLVE) 있다.
- **니들 캐리어의 피해는 flat이다.** 공격자 `damageMul`이 안 붙는다(계약 7).
- **가디언은 primary를 「실제로 때린 적」으로 정렬한다.** 넉백·카드 캐리어·공격 로그가 `bestTarget`/`bestTargetPos`를 쓰기 때문이고, frontmost 카드가 있으면 중복 타격 없이 **자리 교환**(swap)한다.
- **데미지 숫자의 체력 비율은 그 프레임 최종값이다.** 같은 프레임의 폰트 셋이 전부 같은 최종 비율을 나르고, 실드가 일부만 막으면 폰트 값은 관통분 비례로 배분된다.
- **넉백은 방향을 모르면 안 민다.** `lastMoveDir`이 없는 대상(스폰 직후·고정 구조물)은 밀리지 않는다.
- **자기 히트가 자기 수면을 안 깨우는 것은 시스템 순서가 보장한다.** 피해가 프레임 N, 수면 적용이 N+1이라 성립하는 것이지 명시 가드가 없다.
- **`AttackReach.InCellRange`는 순찰 이동 전용이다.** 추격 필드 소스 수집이 셀 디스크라 그와 같은 자여야 해서 남았다. `PatrolAreaMath`만 격자 자와 원 자를 **분해해서** 쓴다(셀 통과 + 몸 거리 실패 = 한 칸 더 밀어 준다). 2026-08-12에 한 곳만 조였다가 182프레임 교착이 난 이력이 있다.
- **어그로 sticky의 배타성은 load-bearing이다.** 「가디언 없으면 최근접」으로 풀면 가는 길에 만난 방어유닛과 싸우느라 가디언에 영영 도착하지 않는다.
- **공중에서 죽는 일이 없다는 것이 착지 보장의 근거다.** `UltimateLeapState` 보유자의 피해 버퍼를 통째로 비우기 때문이고, 그 가드가 사라지면 착지 예고 미해제 3경로가 한꺼번에 열린다.
- **락은 CC 중에 비워지고 재잠금도 건너뛴다.** `else`로 감싸지 않으면 해제 분기가 그 프레임 최근접으로 즉시 다시 잠근다(초판이 그랬고 테스트가 잡았다). 진행 중 스윙의 커밋 타겟은 별개 층이라 안 건드린다.
- **`PastGoal`은 더 이상 타겟 제외 사유가 아니다.** 골에 붙어 타워를 때리는 적은 살아 있는 유효 대상이고, 경로상 가장 앞선 적이라 frontmost 정의에 정확히 부합한다.
- **거점에 대한 타입 기반 특별 취급이 없다.** 마스크에 들어온 후보는 종류를 묻지 않고 거리로만 경쟁한다(2026-08-09 사용자 확정으로 예외 제거).

## 이 영역에서 rebuild가 결정해야 할 열린 질문 (5)

1. **선딜이 공속의 천장인 것을 유지할 것인가.** 공속 배율은 공격 간격에만 곱해지는데 실주기는 `max(간격, 선딜)`이라, 선딜이 긴 유닛은 공속 버프가 사실상 무효다. 그런데 실효 스탯 표시는 버프 전량을 보여준다. 「실주기」를 누가 소유하고 공속이 어디에 걸리는지가 정해져 있지 않다.
2. **진행형 상태를 「취소」하는 어휘가 없다.** 개시 의도는 다섯이다(`BeginUltimateLeap` · `BeginDreamCocoon` · `StartLethalTimer` · `GrantCharge` · `DelaySelfAttack`). 대응하는 취소는 `ClearCc` 하나뿐이고, 도중에 죽음·퇴근·CC가 오면 무슨 일이 일어나는지는 각 시스템이 제각각 알고 있다.
3. **체비셰프 사각 자를 통일할 것인가.** 폭탄맨과 캐스터 4종의 일반 공격/캐스트가 아직 사각 자에 양쪽 몸 0으로 대상을 고른다. 이름은 폴백인데 그 아키타입의 **유일 경로**다. 2026-09-06에 사용자 지시로 보류된 상태라, rebuild가 그 보류를 승계할지부터 정해야 한다. 같은 성격으로 `SkillCone` 부채꼴 게이트의 몸 누락이 함께 남아 있다.
4. **트리거가 트리거를 낳을 때의 순서 계약이 없다.** 큐 스냅샷·세대 반복·깊이 예산·종료 통로가 전부 미정인데, 모디파이어 슬롯이 덮어쓰기라 **적용 순서가 곧 결과**다. 「같은 시드 = 같은 판」과 정면으로 부딪친다.
5. **wind-up 중 대상이 사라지면 그 공격은 어떻게 되나.** 지금은 strict lapse라 START 모션만 나가고 투사체가 생략된다. 방향탄만 예외로 커밋된 축을 따라 발사한다. START 커밋 발사 / 재타겟 / 빗나감 중 어느 것을 규칙으로 삼을지가 미결이고, 백로그에도 「별도 spec에서 결정」으로 남아 있다.
