# waypoint-flight-enemy — 비행 적 (지형을 무시하고, 저작된 궤도로 온다)

> ## 목표 3줄
>
> 1. **복도를 안 쓰는 적을 만든다** — 지형·벽·장애물을 전부 무시한다.
> 2. **그런데 최단거리로 오지 않는다** — **맵에 그려진 궤도**를 따라 날아온다. 궤도가 맵마다 다르므로 방어선도 맵마다 달라진다.
> 3. **flow field 를 아예 안 본다** — 그래서 `traversal-layers` 의 미완 배선(슬롯 다중화)을 **건드리지 않고** 끝난다.

상태: **작성됨 2026-08-10 · 리뷰 반영 rev 1 (2026-08-11)** · unit 0 착수 대기 · 선행 spec 없음(`structure-hunter-enemy` 와 독립)

## 상위 목표

적 12종이 전부 **같은 길**로 온다 — goal flow field 를 따라 복도를 걷는다. 그래서 배치 판단은 «어느 길목을 막나» 하나로 수렴한다.

비행 적은 그 전제를 깬다. 지형을 무시하되 **저작된 궤도**를 따르므로, 「이 맵의 비행은 좌측 상단으로 크게 돌아 들어온다」가 맵의 성질이 되고 플레이어는 그에 맞춰 다른 자리에 유닛을 세운다.

## 검증 질문

> 비행 적이 맵에 그려진 궤도를 실제로 따라 날아오는가?
> 그리고 그 궤도가 맵마다 달라서 플레이어가 **다른 자리에** 방어를 세우는가?

이 질문에 답하는 데 필요하지 않은 것은 이 spec 에 넣지 않는다.

---

## 왜 최단거리가 아닌가 (기각 근거)

비행을 통행 층으로 표현하면(`PlacementLayer` 에 `Air` 비트 + 라우팅 슬롯 2개) 벽 없는 flow field 가 나오고, 그건 곧 **스폰→골 직선**이다. 그러면:

- 모든 맵에서 비행 대응이 **똑같아진다**(직선 위에 세우면 끝). 맵의 개성이 사라진다.
- 그리고 `traversal-layers` 의 미완 배선을 전부 열어야 한다 — 실측 결과 `SimFieldInstaller.Install(..., slotMasks = default)` 에 **넘기는 호출자가 하나도 없어** 슬롯이 영구 1개이고, `SlotFor` 는 완전일치라 다중 비트 유닛이 **조용히 primary 로 폴백**한다(그 spec 의 D1 이 예고한 함정).

**웨이포인트 방식은 필드를 안 보므로 이 둘을 동시에 피한다.** 통행 층 축은 물 적이 올 때 그쪽이 자기 맥락에서 푼다(`traversal-layers` §6 이 그렇게 남겨뒀다).

## 이미 있는 것 (선례 대조 완료)

**「골이 아닌 지점으로 가서 도착하면 멈춘다」는 라이브에서 이미 돈다.**

| 무엇 | 어디 | 이 spec 이 쓰는 방식 |
|---|---|---|
| 목적지 갈아끼우기 | `MovementSystem` — `PatrolStep.dir`(순찰) · `AggroChaseCell`(추격) · `huntField.flow`(사냥) · `goalFlow`(기본) **4가지 분기** | **5번째 방향 출처**로 합류. 구조적으로 새 개념이 아니다 |
| 특정 지점으로 이동 후 정지 | `AiState.Chasing` 분기(`MovementSystem.cs:117~145`) | **형태만** 선례로 삼는다 — 위치는 따르면 안 된다(아래 §골 도달) |
| 맵에 배열 저작 | `MapDocument.structures[]` → `GeneratedMap.structures`(`battle-structures` unit 3) | `flightPath` 를 **같은 형태**로 |
| 뜬 높이 시각 반응 | `UnitLiftVisual.Resolve(lift → 확대·그림자축소·그림자페이드)` 순수 함수 + `SpineUnitView.SetFlightHeight(float)` public API | 비행 적에 **상시 lift**. 뷰 코드 최소 |

흔적도 있다 — 포탈 주석에 *"exitWaypointIndex 제거됨"* 이라 적혀 있어, 예전엔 웨이포인트 인덱스 방식이 있다가 flow field 로 대체됐다.

---

## 착수 전 확인된 배선 함정 3개

리뷰에서 코드 대조로 나왔다. **셋 다 unit 2 의 실질**이며, 이 셋을 놓치면 「부착은 됐는데 이상하게 도는」 상태가 된다.

### ① 궤도를 sim 으로 넘기는 경로 — 엔티티 버퍼로 복사한다

**`GeneratedMap` 은 ECS 시스템이 못 읽는다.** Mono 측 데이터이며(`PatrolSpawnRequest.cs:14` 주석이 *"보는 Mono 측 API(GeneratedMap)"* 라고 명시), `MovementSystem` 이 맵을 보는 창구는 `FlowFieldSingleton` 하나뿐이다.

**해법: 싱글턴을 새로 만들지 않고 스폰 시 엔티티에 `DynamicBuffer<FlightWaypoint>` 로 복사한다.**

- 궤도는 스폰 시점에 확정되고 점이 3~5개라 복사 비용이 없다.
- 신규 싱글턴 0개 — 라이프사이클(할당·해제·재빌드) 계약을 하나 더 만들지 않는다.
- 「맵당 궤도 여러 개」로 확장할 때 **엔티티마다 다른 궤도**가 자연스럽다(싱글턴이면 인덱싱 규약이 또 필요하다).
- **브리지가 셀→월드 변환을 끝내서 담는다.** 그러면 순수 함수(`FlightPathStep`)가 월드 좌표만 다루고 격자 개념이 안 들어온다.

### ② 분리 시스템이 비행 적을 자동으로 잡아간다

`AgentSeparationSystem` 의 쿼리가 **`PathFollowState` + `LocalTransform`** 이다. 비행 적이 `PathFollowState` 를 가지면 아무 선언 없이 **밀어내기 대상이 되어** 지상 적과 서로 밀치고 궤도를 이탈한다.

이것이 `traversal-layers` unit 5 의 재현 형태다 — 그때도 계약 문장은 맞았고 **배선이 반쪽**이었다(라우팅만 층 인지, 충돌 그리드는 아님). unit 2 의 변경 대상에 이 시스템을 **명시**한다.

### ③ 골 도달을 건너뛰면 적이 골 위에 떠 있는다

골 도달 처리는 `MovementSystem` 안의 `field.IsGoalCell(cell)` 게이트가 `PastGoalTag` 를 붙여서 한다(`:180`). 그런데 형태 선례인 `AiState.Chasing` 분기는 **그 판정보다 앞에서 `continue`** 한다.

**비행 분기는 골 판정 «뒤에» 방향만 갈아끼우는 형태여야 한다.** `Chasing` 의 배치를 그대로 베끼면 비행 적이 골에 닿아도 아무 일이 일어나지 않는다.

---

## 작업 단위

| # | 작업 구분 | 내용 | 행동 변화 | 크기 |
|---|---|---|---|---|
| **0** | **저작 축** | `MapDocument.flightPath`(Vector2Int[]) + `GeneratedMap.flightPath`(NativeArray\<int2\>) + `ToGeneratedMap` 전달 + Dispose. **검증용 맵 1장에 궤도 수기 저작**(unit 2 가 볼 것이 있어야 한다) | **0** — 읽는 코드 0 | S |
| **1** | **순수 함수** | `FlightPathStep` — 현재 위치·웨이포인트(월드)·인덱스·도달 반경 → 방향 + 다음 인덱스 + 종료 여부. EditMode 테스트(제약 10) | **0** | S |
| **2** | **켜기** | `FlightPathFollow` 컴포넌트 + `DynamicBuffer<FlightWaypoint>`(둘 다 Movement 소유) + `AttackUnitData` 비행 저작 축 + 스폰 주입(셀→월드 변환) + `MovementSystem` 분기(`AgentCollision`·`MovementCellTrim` 우회, **골 판정 뒤**) + **`AgentSeparationSystem` 제외**. **한 커밋** — 계약 4 | **큼** | M |
| **3** | **뷰** | 상시 lift(`SetFlightHeight`) → `UnitLiftVisual` 이 확대·그림자를 파생. 오버헤드 체력 UI 가 lift 를 따라가는지 확인 | 시각 | S |
| **4** | **저작 도구 + 검증 맵** | `MapPainterWindow` 궤도 모드 + **맵 2~3장** 저작 + `MapDocument.OnValidate` 검증(궤도가 격자 안인가 등) | 콘텐츠 | M |
| **5** | **handoff** | 커밋·검증·주의점 | — | S |

**순서 근거**:

- **0·1 은 소비자 0 이라 안전하게 먼저 넣는다.** 0 이 데이터를, 1 이 계산을 세우고 둘 다 아무도 안 읽는다.
- **2 를 쪼개지 않는 것이 이 spec 의 핵심 판단이다.** `traversal-layers` 가 라우팅만 층 인지로 바꾸고 충돌 그리드를 unit 5 로 미뤘다가 «컴포넌트는 붙었는데 한 칸도 못 움직이는» 상태를 만들었다. 부착·소비·제외를 한 커밋에 둔다(계약 4).
- **4 를 2 보다 뒤에 두되 unit 0 이 맵 1장을 미리 저작한다.** 페인터 툴 없이 수기로 한 장만 찍어두면 2 의 라이브 검증이 성립한다. 툴과 추가 맵은 동작이 확인된 뒤.
- **맵 2~3장이 상한이다.** 검증 질문(「맵마다 다른 자리에 세우는가」)은 서로 다른 궤도 2~3개면 답한다. **9장 전부 저작은 검증이 아니라 콘텐츠**이며 후속으로 뺀다(제약 9).

---

## Feature-wide 계약

1. **비행은 필드를 안 본다.** flow field · `NavGrid` · 라우팅 슬롯을 **건드리지 않는다.** 비행을 통행 층으로 표현하려는 유혹을 거부한다 — 그건 최단경로가 되고 이 spec 의 검증 질문과 정반대다.
2. **미저작 폴백 = 골로 직선.** `flightPath` 가 없는 맵에서 비행 적이 나오면 골을 향해 **직선**으로 난다(지상 경로가 아니라). 조용한 정지·조용한 지상화가 생기지 않게 한다.
3. **웨이포인트 추종은 순수 함수다.** plain 값 입력 → plain 값 출력, **월드 좌표만** 본다(셀→월드 변환은 스폰 시 브리지가 끝낸다). EditMode 테스트 필수 — 제약 10 의 (a)비자명·(c)sim-critical 을 둘 다 충족한다.
4. **켜는 커밋은 하나다.** 컴포넌트·버퍼·스폰 주입·`MovementSystem` 분기·`AgentSeparationSystem` 제외를 나눠 커밋하지 않는다. 반쪽 배선은 «부착됐는데 이상하게 돈다»로 나타나고, 그 증상은 순수 함수 테스트가 전부 초록인 채로 발생한다.
5. **검증 축은 «라이브에서 궤도를 실제로 따라가는가»다.** 순수 함수 그린은 증거가 아니다(`traversal-layers` 계약 7 승계). 웨이포인트 통과 순서를 **세어** 확인한다.
6. **모든 방어유닛이 비행 적을 때릴 수 있다.** 대공 개념을 만들지 않는다. 방어유닛 26종에 저작 축이 붙고 밸런스가 크게 흔들린다. **비행의 정체성은 «안 맞는다»가 아니라 «다른 길로 온다»다.**
7. **충돌·분리 양쪽에서 빠진다.** `traversal-layers` 계약 8 승계(유닛과 적은 서로를 막지 않는다)에 더해, 비행은 `AgentCollision`(지형)과 **`AgentSeparationSystem`(에이전트 간 밀어냄)** 둘 다 건너뛴다. 계약 문장에 시스템 이름을 박는 이유는 §②의 사고를 반복하지 않기 위해서다.
8. **격자 밖으로 나가지 않는다.** 셀 트림을 건너뛰므로 궤도가 격자 밖을 가리키면 적이 맵을 벗어난다. 저작 검증(unit 4 `OnValidate`)이 **정본**이고 런타임 클램프는 안전망이다.
9. **비행 축은 `traversalLayers` 와 다른 축이다.** 저작 필드 주석에 «비행은 통행 층을 보지 않는다»를 못박는다. 안 박으면 다음 사람이 `traversalLayers = All` 로 비행을 만들려다 `SlotFor` 완전일치 때문에 **조용히 기본 슬롯으로 폴백**당한다.
10. **결정론.** 궤도가 저작이라 자동으로 성립한다. 절차적 오프셋·랜덤 지터를 넣지 않는다.
11. **무장 없이 시작한다.** 골 도달이 목적(`Runner`·`Swift` 선례 — `attackMethod None`). 무장이 필요해지면 저작으로 켠다.

---

## 파이프라인 커버리지

`object-pipeline-map.md` **적(Enemy)** 아키타입 대조. 이 spec 은 실제로 정거장을 바꾼다.

| 정거장 | 이 spec 에서 | 비고 |
|---|---|---|
| 데이터 SO | 신규 `AttackUnitData` 에셋 + **비행 저작 필드 신설** + `EnemyCatalog.units` + `AttackDeck.attackUnitPool` | 등록 3곳 누락 시 조용히 안 나온다 |
| 스폰 진입점 | `BattleBridge.SpawnUnit` 에 **`FlightPathFollow` + `FlightWaypoint` 버퍼 부착 분기** | 궤도를 `GeneratedMap.flightPath` 에서 읽어 **셀→월드 변환 후** 버퍼에 담는다 |
| ECS 컴포넌트 | **신규 2개** — `FlightPathFollow`(인덱스) + `DynamicBuffer<FlightWaypoint>`(궤도 사본). 둘 다 Movement 맥락 소유 | ⚠ **싱글턴 신설 0** — `GeneratedMap` 은 sim 이 못 읽으므로 엔티티로 복사하는 것이 경로다 |
| 시뮬 시스템 | `MovementSystem` **분기 추가**(5번째 방향 출처, **골 판정 뒤**). `AgentCollision`·`MovementCellTrim` 우회 + **`AgentSeparationSystem` 쿼리에서 제외** | 맥락 경계 변화 없음 — 위치 쓰기는 그대로 Movement |
| 이벤트 큐 | **무변경** — 신규 채널 0 | `GoalReachedEventsSingleton` 기존 경로(§③ 지켜야 성립) |
| View/Pool | 기존 `SpineUnitPool` + **상시 lift**(`SetFlightHeight`) | `UnitLiftVisual` 이 확대·그림자를 파생 |
| 체력 표시 | 기존 `UnitOverheadUiLayer` — ⚠ **lift 를 따라 올라가는지 확인**(unit 3) | 지상 기준으로 붙어 있으면 몸과 분리돼 보인다 |
| 씬 wiring | **N/A** — 신규 SerializeField 0, 신규 GameObject 0 | |

---

## 결정

**결정 1 — 마지막 웨이포인트 이후는 골까지 직선이다.** 계약 1 과 정합한다(비행은 끝까지 필드를 안 본다). 골을 마지막 웨이포인트로 저작하게 하는 안은 저작자가 매번 골을 찍어야 해서 실수 지점이 늘어난다.

**결정 2 — 웨이포인트 사이는 직선이다.** 곡선 보간은 넣지 않는다.

**결정 3 — 맵당 궤도 1개.** 여러 개를 저작할 수 있게 하면 배정 규칙이 필요하고, 그때는 스폰 레인 선례를 따라 **index 기반 결정론**(seeded RNG 아님)으로 나눈다. unit 4 에서 실제로 필요한지 판단 — 미리 만들지 않는다(제약 8).

## 미결 (착수 시 확정)

**D1 — 이름.** 코드명 후보: `Enemy_Drifter` / `Enemy_Wisp` / `Enemy_Skimmer`. unit 2 에서 확정.

**D2 — 도달 반경.** 너무 작으면 웨이포인트 주변에서 진동한다. SO knob 으로 노출하고 하드코딩하지 않는다(제약 6). 초기값은 unit 2 라이브에서.

---

## 후속 후보 (이 spec 범위 밖)

- **비행 궤도 예고** [S] — 스폰 예고에 궤도를 그린다. 없으면 첫 조우가 «갑자기 옆에서 나타남»이 되지만, **이 spec 의 검증 질문(궤도를 따르는가 / 맵마다 다른가)은 예고 없이 답한다.** UX 개선이라 범위 밖으로 뺀다. 붙일 곳은 `SpawnAlertPresenter` 이고, 경로 소스가 `TryGetSpawnPathSim`(goal flow 추적)이라 **다른 소스**가 필요하다 — 예고는 `flightPath` 를 그대로 그린다(맵 표의 "표시 루트 = 실제 루트" 유지).
- **맵 나머지 6~7장 궤도 저작** [S] — unit 4 가 2~3장까지만 한다. 콘텐츠 작업.
- **비행 적의 무장** [S] — 계약 11 이 무장 없이 시작한다고 못박았다. 저작으로 켜는 형태.
- **궤도 곡선 보간** [S] — 결정 2. 직선 꺾임이 거슬리면.
- **맵당 궤도 여러 개 + 배정 규칙** [S] — 결정 3.
- **대공 축** [L] — 계약 6 을 뒤집는 제품 결정. 방어유닛 26종 저작 + 밸런스 재조정을 동반한다.
- **비행 적의 지상 착륙** [M] — 궤도 끝에서 내려앉아 지상 적으로 전환. `LeapFlight`·`leap-flight-state` 가 상태 전환 선례를 갖고 있다.
- **통행 층 슬롯 다중화 켜기** [M] — 이 spec 은 **의도적으로 피한다**(계약 1). 물 적 spec 이 `traversal-layers` §6 의 표를 따라 푼다.
- **거점 사냥꾼** → `docs/spec/structure-hunter-enemy/` (이 브레인스토밍의 나머지 절반)
