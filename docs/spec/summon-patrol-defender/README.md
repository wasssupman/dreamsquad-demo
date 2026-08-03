# summon-patrol-defender — 소환사 & 순찰병 (Patrol)

> 상태: 구현 중 2026-08-03 · units 0~6 코드 완료 · unit 7 Play 검증 중 · unit 8 대기

## 목표

이 게임의 **3번째 유닛 유형** 인 *거점 순찰 아군(Patrol, 이하 순찰병)* 과, 그 첫 생성 경로인 **소환사**(id `summoner`)를 추가한다.

순찰병은 **walk 타일 위를 이동하며 거점 셀 ± N타일 박스를 지키는 아군**이다. 박스 안에 적이 들어오면 마중 나가 근접 교전하고, 없으면 거점으로 복귀한다. 소환은 순찰병의 정체가 아니라 **판에 올리는 경로 하나**다 — 그래서 이동 규칙(계층 A)과 소환 유지 규칙(계층 B)을 분리해 작업 단위를 나눈다.

**검증 질문**: *"소환사를 뒤에 두고 순찰병을 앞세우는 것이, 타일에 유닛을 직접 놓는 것과 다른 배치 결정을 만드는가?"*

### 왜 새 유형인가

기존 두 유형은 각각 반대쪽 극단이다 — 방어유닛은 `MapTileType.Place` 에 고정(`PathFollowState` 미부여), 적은 goal flow 를 따라 이동. 순찰병은 **아군인데 walk 위를 이동**하는 첫 유닛이라, `object-pipeline-map.md` 가 "이동은 적 전용"이라고 적어둔 전제를 깬다. 그 전제에 기대고 있던 코드가 실재하므로(unit 0), 새 유형 신설은 유닛 하나가 아니라 **불변식 하나를 여는 작업**이다.

## 작업 단위

| # | 계층 | 문서 | 목적 |
|---|---|---|---|
| 0 | 선행 | `0_zone_faction_gate.md` | `ZoneApplySystem` 적 전용 게이트 — 아군 오폭 차단 (기존 결함 교정) |
| 1 | A | `1_patrol_anchor_and_field.md` | `PatrolAnchor`/`PatrolStep` + 박스 마스크·타겟 선정 순수 함수 + `PatrolFieldSystem` + EditMode |
| 2 | A | `2_movement_and_bridge_spawn.md` | `MovementSystem` dir 분기 + goal 게이트 + `CreatePatrolEntity` + 디버그 메뉴 + 매치 경계 정리 |
| 3 | B | `3_summon_ability_and_fire.md` | `SummonPatrolAbility` + `SummonerState` + 소환 발화(초회 구역 게이트) + 스폰 요청 캐리어 |
| 4 | B | `4_owner_link_and_respawn.md` | `SummonedBy` 연쇄 소멸 + 재소환 순환 + 재배치 anchor 재스냅 |
| 5 | 뷰 | `5_view_and_leash_preview.md` | 순찰병 walk 애니 + 소환 VFX + 거점 반경 프리뷰 · **소환사 대기 모션은 unit 8 로 이관**(재생할 트랙이 없음) |
| 6 | 뷰 | `6_ally_readability.md` | **아군 식별 표식** — 표식 종류가 육안 판정이라 unit 5 에서 분리 |
| 7 | 에셋 | `7_unit_assets_and_play.md` | 유닛 에셋 저작 + 카탈로그 등록 + Play 검증 |
| 8 | 에셋 | `8_unique_spine_swap.md` | 고유 스파인 2종 임포트 + 스왑 |

**unit 2 까지 끝나면 소환 없이 순찰병이 판에서 동작한다**(디버그 메뉴 스폰으로 검증). 계층 B 는 그 위에 얹힌다. 커밋 경계 = 계층 경계.

작업 단위를 가르는 기준은 **"독립적으로 구현·검증·커밋되는가"** 하나다. unit 1 은 EditMode 로 자기 검증되고, unit 2 부터는 각각 Play 로 검증된다. 자기 검증을 다음 유닛에 미루는 단위는 두지 않았다.

## Feature-wide 계약

1. **순찰병 = `FactionTag{Defender}` + `DefenderUnitTag` + `DefenderClassTag` 를 갖되 `DefenderTile` 은 갖지 않는다.**
   - `DefenderUnitTag` 부착은 선택이 아니다 — `BattleBridge.DestroyBattleEntities` 가 **타입 기반 파괴**라, 이 태그가 없으면 매치 경계에서 안 지워지고 앱 수명 default world 에 잔존한다(사직서·AllyBuffField 캐리어가 같은 사고를 겪은 기록이 그 함수 주석에 남아 있다). 부착의 귀결로 실드/온천 열기/번아웃 피로/보스 스킬 대상에 편입된다 — "완전한 유닛" 결정과 정합. **레드불 픽업은 편입되지 않는다** — `PickupConsumeSystem` 의 소비 루프가 `DefenderTile` 을 함께 요구하기 때문이다(태그가 아니라 타일 게이트).
   - `DefenderClassTag` 도 붙인다. 태그 없음 면제는 `EnemyTargetFilter` 주석대로 **무생물(blocking hazard)** 용이다. 생물을 태그 없이 태우면 클래스 하드 타게팅 적(킨들러 = 레인저 전용 마스크)이 레인저 대신 순찰병을 쏴서 그 적의 정체가 무력화된다.
   - `DefenderTile` **미부착이 계약이다.** 이것이 배치 점유·재배치·`DefenderDeathEvent`·사직서 드랍(`ResignationDropSystem` 이 `DefenderTile` 을 함께 요구)을 한 번에 차단한다. 나중에 누가 이 태그를 붙이면 **사직서 무한 드랍(반복 사망 파밍)** 이 조용히 열린다.
   - `AttackUnitTag` 미부착. 붙이면 누수 카운트·`EnemyKilledEvent`·투사체 AOE 적 풀에 섞인다.

2. **소환수 스탯은 `DefenderUnitData` 를 재사용한다.** 신규 SO 타입을 만들지 않는다 — `ISpineUnitVisualData` 는 멤버 11개이고 지금까지 네 번 커졌다(구현체 3번째는 이후 모든 확장 비용을 늘린다). 시트는 안전하다: `UnitStatApplier` 가 id 미매칭 행을 스킵하므로 시트에 행이 없으면 덮이지 않는다. **단 `CreateDefenderEntity` 를 재사용하지 않는다** — 그쪽은 `_defenderByTile` 등록과 `DefenderTile` 부착을 한다. 별도 `CreatePatrolEntity` 를 유지하고, `DefenderCatalog` 에 등록하지 않는다(미등록 = 로스터 미노출).
   - 재사용의 갭은 **필드 2개**다(스펙 작성 시점엔 1개로 봤으나 구현에서 하나 더 드러났다): `moveSpeed`(unit 2 에서 추가 — 방어유닛은 타일 고정이라 애초에 없던 값) · `SpineWalkAnimation`(unit 5 — 현재 `""` 하드코딩). 둘 다 **맨 뒤에 덧붙여** 직렬화 순서를 흔들지 않고, 기존 에셋은 초기값을 유지해 무영향이다.

3. **이동은 기존 BFS·하강을 그대로 쓴다. 그리디 스텝을 쓰지 않는다.**
   - 박스 제약은 **walkMask 마스킹**으로 표현한다 — 박스 밖 셀을 0 으로 지운 walkMask 를 `AggroChaseMath.BuildChaseField` 에 넘기면, 목적지 BFS·도달 불가 판정·cardinal 하강(`FlowRecovery.RecoveryDir`)을 전부 재사용한다.
   - 8-이웃 그리디는 금지다. `aggro-tile-chase` 가 직선 greedy 를 벽 고착(좀비버그)으로 폐기했고, 대각 이웃은 미수리 결함("대각 코너 슬립 차단", 백로그)에 걸린다. 현행 이동이 cardinal 인 것은 의도다.

4. **거점(anchor)은 소환사 셀이 아니라 그 셀의 최근접 walk 셀이다.** 방어유닛은 `MapTileType.Place` 에만 놓이고 `Place` 는 walkable 이 아니다 — 소환사 셀을 그대로 anchor 로 쓰면 순찰병이 절대 설 수 없는 칸을 향해 영원히 전진한다. `BattleBridge.TryGetNearestWalkCell` 로 스냅하고, 스냅 실패 시 소환을 취소한다. 스폰 셀 = anchor. 소환사 재배치 시 `BattleBridge.Relocation` 에서 재스냅한다.

5. **실효 교전 반경 = `leashTileRadius + attackRange`.** `EnemyAiStateSystem` 은 구역을 모르므로, 구역 경계 바로 밖의 적이 순찰병 사거리에 들면 `Engaging`+`Halt` 가 거점 복귀보다 우선한다(`MovementSystem` 이 `PatrolStep.dir` 을 읽기 전에 `continue`). 순수 함수는 "구역 밖 적 무시 → 복귀"를 보증하지만 **시스템 레벨에서는 그 자리에서 교전한다**. 의도로 채택 — 막으려면 `AttackSystem` 타겟 선정에 구역 필터가 필요해 스코프가 넓어진다.

6. **박스 이탈은 자기주도 이동에만 불가하다.** 포털 텔레포트·토네이도 pull·임펄스 넉백은 faction 을 보지 않으므로 순찰병을 박스 밖으로 민다. 계약은 *"자기주도 이동은 박스를 벗어나지 않는다. 외력은 벗어날 수 있고, 다음 틱에 복귀 경로가 잡힌다"* 이다 → 필드 계산이 **박스 밖 시작** 입력을 다뤄야 하고, 그 케이스가 EditMode 대상이다.

7. **goal 셀이 박스 안에 있을 수 있다.** `MovementSystem` 의 goal 판정은 dir 분기보다 **앞**이라 patrol 분기가 우회하지 못한다. 붙으면 `PastGoalTag` 로 영구 동결되고(파괴 루프는 `AttackUnitTag` 요구), 그 결과 `SummonerState.current` 가 계속 유효해 **소환사가 남은 판 내내 재소환하지 못한다**. 보스 `hunting` 과 같은 형태로 `!patrolling &&` 게이트를 건다. 맵은 매판 랜덤이고 배치는 플레이어가 하므로 "저작으로 피한다"는 해법은 쓰지 않는다.

8. **첫 소환만 구역 게이트, 이후 재소환은 무게이트** (사용자 결정 2026-08-03).
   - 첫 순찰병은 **거점 구역 안에 적이 있을 때만** 낸다. 판정 중심은 **소환사 셀**이다 — 실제 거점은 Bridge 가 walk 셀로 스냅해 정하는데 첫 소환 전엔 아직 없고, 스냅 상한이 leash 반경이라(계약 4) 소환사 기준 구역이 실제 구역을 보수적으로 감싼다. 구역 술어는 `PatrolAreaMath.IsInArea` 단독 소유(정의가 갈리지 않게).
   - **게이트가 닫혀 있으면 쿨다운을 리셋하지 않는다.** 만료 상태로 대기하다 적이 들어온 프레임에 즉시 소환한다 — 리셋하면 "구역에 들어오면 부른다"가 최대 한 쿨 늦게 반응해 규칙이 거짓이 된다. 대기 중 스캔은 이미 만든 타겟 스냅샷 위의 짧은 루프라 비용이 없다.
   - **재소환에는 게이트를 다시 걸지 않는다.** 순찰병이 죽는다는 건 곧 적이 있다는 뜻이라 재게이트는 같은 사실을 두 번 묻는 것이고, 교전 중 적이 잠깐 구역을 벗어난 프레임에 재소환이 끊기면 순환이 덜컥거린다.
   - **게이트 소비(`SummonerState.hasSummonedOnce`)는 Bridge 가 순찰병을 실제로 생성한 시점에** 켠다. stage 시점에 켜면 스냅 실패로 취소된 경우에도 게이트가 닳아 이후 적 없이 소환되는 상태로 넘어간다.
   - 유출 대기(`PastGoalTag`) 적은 게이트를 열지 않는다.
   - 훅 위치는 그대로 폭탄맨과 같은 **발사 분기**(타겟을 요구하는 RESOLVE 가 아님) — 게이트는 "적이 구역에 있나"만 보고 타겟을 고르지 않는다.
   - **이력**: 초기 구현은 `bomb-thrower-defender` 의 blind bombardment 를 따라 적 유무 무관 발화였다. 실플레이 확인 중 "적이 와야 첫 소환"으로 뒤집혔다.

9. **생존 술어는 양방향 대칭이다.** `SummonerState.current` 유효 = `Exists && !DeadTag && HP>0`, `SummonedBy.owner` 동일. `Entity` 는 version 을 포함하므로 `Exists` 가 재활용 id 를 막는다. 한쪽만 검사하면 stale 핸들로 소환사가 영구 대기한다.

10. **순찰병은 각성치를 주지 않고 코스트가 0이며 재배치 대상이 아니다.** 각성치는 계약 1의 `DefenderTile` 미부착이 `DefenderDeathEvent` 를 막아 자동으로 성립한다(반복 사망 파밍 차단). 적을 죽이면 킬 점수는 정상 집계한다.

11. **드림캐쳐/시너지 버프는 순찰병에 걸지 않는다.** `CreatePatrolEntity` 가 `ApplyActiveDcEffectsTo` 를 호출하지 않는다. 효과 타일은 위치 기반이라 자동으로 걸릴 수 있으므로 Play 에서 확인한다(unit 6).

12. **모든 수치는 SO 에서 나온다** — 거점 반경·소환 쿨다운·순찰병 스탯 전부. 하드코딩 금지(제약 6).

## 파이프라인 커버리지 (Defender 아키타입 대조)

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Defender_Summoner.asset`(소환사) + `Defender_PatrolSoldier.asset`(순찰병, **카탈로그 미등록**) + `SummonPatrolAbility` 서브에셋(Data/Abilities/). 소환사만 `DefenderCatalog` 등록 |
| 스폰 진입점 | 소환사 = 기존 `PlaceDefenderAs`→`CreateDefenderEntity`. 순찰병 = **신규 `CreatePatrolEntity`**(소환 발화 + 디버그 메뉴 2 경로) |
| ECS 컴포넌트 | **신규 4**(순찰병/소환사 본체): `PatrolAnchor`(Movement) · `PatrolStep`(Effects) · `SummonerState`(Combat) · `SummonedBy`(Units). **+ 요청 캐리어 2**(Combat, 수명 1프레임): `PatrolSpawnRequest` · `PatrolRequestCarrier`. 재사용: `EnemyAiState`·`EnemyBehavior{Halt}`·`PathFollowState`·`AttackState`·`DefenderUnitTag`·`DefenderClassTag`. **`DefenderTile` 은 의도적 미부착**(계약 1) |
| 시뮬 시스템 | **신규 2**: `PatrolFieldSystem`(Effects) · `PatrolLifecycleSystem`(Units, owner 연쇄 소멸). 수정 3: `MovementSystem`(dir 분기+goal 게이트) · `ZoneApplySystem`(진영 게이트) · `AttackSystem`(소환 발화 + 초회 구역 게이트) |
| 이벤트 큐 | **신규 채널 0.** 순찰병 스폰은 `ProjectileRequestCarrier` 와 같은 **캐리어 엔티티** 관용구를 쓴다(AttackSystem 에서 Bridge 스폰을 요청하는 관용구가 이미 그 자리에 있다) — 싱글턴 배선도 CLAUDE.md 채널 목록 갱신도 불요. `DefenderDeathEventsSingleton` 은 계약 1로 미발행 |
| View/Pool | 기존 `SpineUnitPool` 재사용(스폰·회수는 `DespawnMissing` 이 자동). **위치 sync 는 전용 루프가 필요하다** — `SyncMonoUnitViews` 의 두 루프는 각각 `AttackUnitTag` 쿼리(적)와 `_defenderByTile` 순회(방어유닛)인데 순찰병은 **둘 다 아니다**. 없으면 뷰가 스폰만 되고 영원히 제자리에 선다 → `SyncPatrolViews`(unit 5) 신설. walk 애니는 `SpineWalkAnimation` 을 채워 활성화(방어유닛은 `""`) |
| 체력 표시 | **노출한다** — `UnitOverheadUiLayer`/`UnitOverheadView` 기존 폴링 경로를 `SyncPatrolViews` 안에서 호출. HP 보유 완전 유닛이고 죽고 다시 나는 것이 이 유닛의 핵심 피드백이므로 숨기지 않는다 |
| 매치 경계 정리 | **`DestroyBattleEntities` 에 등재**(unit 2 = 순찰병, unit 3 = 요청 캐리어). 계약 1의 `DefenderUnitTag` 부착으로 순찰병은 자동 포함되나, 회귀 방지를 위해 완료 기준에 명시 |
| 씬 wiring | **N/A — 신규 SerializeField 없음.** 기존 `spineUnitPool`/`unitOverheadUiLayer` 를 그대로 쓴다 |

## 후속 후보

- **배치형 이동 아군** [M] · `PatrolAnchor` 만 붙이고 `SummonedBy` 를 안 붙이면 성립한다. 지금은 생성 경로가 소환 하나뿐이라 만들지 않는다(제약 9).
- **드림캐쳐 소환 페이로드 배선** [M] · `docs/spec/README.md` 의 "드림캐쳐 복합 효과 · lowcost-summon" 과 `dreamcatcher-unit-trigger` 의 "프리미티브 밖 페이로드(소환 — 해당 효과의 파이프라인 신설이 본체)" 가 이 파이프라인을 선결 조건으로 예약해 뒀다. **이 spec 이 그 본체를 만든다.** 카드 배선은 범위 밖 — 종료 시 백로그 항목을 "파이프라인 완료, 남은 것은 카드 배선" 으로 갱신할 것.
- **다중 순찰병** [S] · `SummonerState.current` 를 버퍼로 바꾸고 "빈 슬롯이 있으면 소환"으로 규칙 전환. 지금은 1기 고정(사용자 결정 2026-08-03).
- **거점 이동 명령** [M] · 플레이어가 드래그로 거점을 재지정. 배치 결정의 성격이 달라지므로 별도 결정.
- **`ZoneApplySystem` 아군 대상 존** [S] · unit 0 은 "존은 적에게만" 게이트 하나만 넣는다. 아군 대상 존(회복 장판 등)이 실제로 생기면 그때 `HazardEffect` 에 진영 축을 연다 — 지금 여는 것은 투기(제약 8).
- **순찰병 어그로 보유** [M] · `AggroCapacity` 를 주면 적을 붙잡아 세우는 성격이 강해진다. 현재는 어그로 없이 `EnemyAiState.Engaging`+`Halt` 로 적이 멈추는 것에 의존한다.
- **영구 봉쇄 밸런스 감시** [S] · 순찰병이 경로 위에 서면 적이 멈추고, 죽어야 다시 간다. 재소환이 빠르면 봉쇄가 성립한다. 봉쇄를 막는 knob 은 HP 가 아니라 **재소환 쿨다운**이다 — Play 관찰 항목.
- **아군 이동체 가독성 일반 규칙** [S] · unit 5 는 순찰병 하나에만 식별 처리를 넣는다. 이동형 아군이 늘면 규칙으로 승격.
