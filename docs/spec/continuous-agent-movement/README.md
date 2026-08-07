# continuous-agent-movement — 격자 저작 · 연속 이동

상태: **초안 2026-08-07 (rev 4: BattleBridge 감량 unit 0 편입 — 필드 설치 코드 추출) · 사용자 승인 대기**

## 결정 요약

- **D1 = (b) 확정** (2026-08-07 사용자) — 장애물 변경 시 flow field 재빌드. **막으면 돌아간다.**
- **D2 = (a) 제안, unit 6 착수 시 확정** — 평활화 후보는 필드 전방 K 셀 전진으로 생성.
- **D3 = (B) 확정** (2026-08-07 사용자) — 필드 설치 코드를 `BattleBridge` 밖 `SimFieldInstaller` 로 추출. unit 0.

## 상위 목표

맵은 지금처럼 타일 격자로 저작하고 배치·사거리·AoE 도 타일 단위를 유지하되, **적 이동에서 격자가 보이지 않게** 한다. 목표 상태는 "격자 위를 걷는 유닛"이 아니라 "격자로 만들어진 지형 위를 자유롭게 걷는 유닛"이다.

### 검증 질문

> 45°가 아닌 기울기(예: 8×6)의 열린 구역에서, 적이 축 정렬 구간 없이 **한 줄기 직선**으로 골까지 걸어가는가? 그 직선 위에 장애물을 놓으면 모서리를 스치는 **두 직선**이 되는가?

이 질문에 답하는 데 필요하지 않은 것은 이 spec 에 넣지 않는다.

## 아키텍처 위치 — 순수 함수 우선

**이 feature 의 계산은 전부 아키텍처 중립이다.** 벽 판정, 거리 필드, 가시성 레이캐스트, 원형 충돌은 격자 · 위치 · 반지름이라는 plain 값만 필요로 하며 `EntityManager` / `SystemAPI` / `Time` 을 요구하지 않는다. 제약 10 의 직접 사례다.

따라서 각 작업 단위는 **2단 구조**로 만든다:

1. **순수 static 함수 + 자료구조** (Burst 호환, unmanaged) — 로직의 전부. EditMode 단위 테스트 대상.
2. **호출 지점** — 기존 ISystem 이 값을 넣고 결과를 쓴다. 규칙을 소유하지 않는다.

`FlowFieldBuilder` 가 이미 이 형태다(순수 static + `FlowFieldBuilderTests`). 신규 코드도 같은 모양으로 맞춘다.

### 이식성의 정확한 범위

**"수정 없이 이식된다"고 주장하지 않는다.** 이 spec 의 함수는 `NativeArray` / `Unity.Mathematics` 를 쓰며, 이는 `battle-sim-extraction` 이 요구하는 "순수 관리 C#(Burst-off)" 어휘가 아니다.

이 spec 의 의무는 그보다 좁다 — **sim 이 이미 쓰는 어휘를 벗어나지 않는 것.** 즉 `EntityManager` / `SystemAPI` / `MonoBehaviour` / `Time` 을 계산 안으로 들이지 않는다. 컨테이너 어휘(`NativeArray`→배열) 교체는 `battle-sim-extraction` 이 시뮬 전체에 대해 일괄 수행하며, 그때 이 함수들은 **시그니처의 컨테이너 타입만 치환**되고 로직은 그대로다.

### `NavGrid` 는 저장 상태가 아니라 프레임 뷰다

벽은 이 게임에서 **두 층**이다 (아래 "실제 ECS 접점" 참조): 맵 빌드 시 1회 굽는 정적 벽과, 매 프레임 재구축되는 동적 장애물. 하나로 구울 수 없다.

그래서 `NavGrid` 는 **정적 마스크 + 동적 오버레이를 합친 읽기 전용 뷰**로 정의한다. 조립은 호출자 책임이고, 순수 함수는 조립된 `NavGrid` 하나만 받는다. 이 분리가 아키텍처 호환의 실체다 — ECS 는 싱글턴 둘에서, Mono 는 자기 자료구조에서 조립해 **같은 함수를 부른다.**

## 착수 근거 (현재 상태)

- `FlowFieldBuilder` 는 **4-이웃 BFS** 다. dist = 맨해튼 거리라 열린 공간의 모든 계단 경로가 **동률**이고, 방향 선택 루프의 타이브레이크(`+x, -x, +y, -y` 순서로 첫 감소 이웃 채택)가 그중 가장 극단인 L 자를 뽑는다 — `FlowFieldBuilder.cs:101-109`.
- 그래서 **복도 폭 제한을 풀어도 L 자가 유지된다** (2026-08-07 사용자 실측). 폭이 아니라 알고리즘의 정의다.
- 위치는 이미 연속이다 — `MovementSystem` 이 `LocalTransform.Position`(float3)을 갱신하고 셀 중심 스냅은 없다. 격자가 보이는 원인은 두 가지: **방향 양자화(4방향)** 와 **셀 경계 clamp**(`MovementCellTrim.Apply`).
- 현재 맵 6종은 가로 연속 Walk 폭이 1타일 위주다(Twin = 폭1×54회, Spiral = 폭1×28회). 이 spec 은 **맵 저작을 바꾸지 않는다** — 복도에서는 코너 품질로, 열린 구역(Serpent·Zig·Coil 의 폭 7~14 구간)에서는 직선 경로로 이득이 난다.

## 작업 단위 목록

| 파일 | 작업 구분 | 순수 로직 (신규/변경) | 호출 지점 |
|---|---|---|---|
| `0_sim_field_installer.md` | 게이트웨이 감량 | — (순수 이동) | `BuildFlowField`/`TeardownFlowField`/`BuildPickupSpawnState` → `SimFieldInstaller`. **BattleBridge −180줄, 동작 불변** |
| `1_nav_grid.md` | 벽 질의 통합 | `NavGrid` 프레임 뷰 struct + `NavGrid.IsBlocked(cell)` | 정적 마스크를 `DefenderFieldSingleton` 에서 `FlowFieldSingleton` 으로 이관·공유 + 동적 오버레이는 `ObstacleSingleton` 그대로. **`FillWalkMask` 임시할당 호출부 2곳**(`AggroStateSystem`·`PatrolFieldSystem`) 제거 |
| `2_agent_circle_collision.md` | 충돌 교체 | `AgentCollision.Resolve(pos, desired, radius, in NavGrid)` | `MovementSystem` — `MovementCellTrim.Apply` 대체 |
| `3_weighted_8_neighbor_field.md` | 필드 확장 | `FlowFieldBuilder` 8-이웃 가중(1 / √2) + 코너컷 | 호출부 변경 없음 (시그니처 유지) |
| `4_field_rebuild_on_obstacle_change.md` | 동적 재빌드 | 순서 무관 dirty 판정(개수 + 교환법칙 결합) | Effects ISystem 신설 — `[UpdateAfter(ObstacleLifetimeSystem)]`·`[UpdateBefore(MovementSystem)]`. 기존 배열에 **in-place** 기록이라 할당 없음. `AggroChaseCell` 무효화 포함 (**D1-b**) |
| `5_dist_consumer_revalidation.md` | 파급 정리 | — | 소비처 4곳 기대값 확정 + **전투 중 dist 변동** 대응 |
| `6_los_path_smoothing.md` | 평활화 | `PathSmoothing.FurthestVisible(from, candidates, in NavGrid)` | `MovementSystem` — flow 방향 대체. **후보 생성 방식 미결 (D2)** |
| `7_agent_separation.md` | 분리 | `Separation.Accumulate(...)` → 일괄 적용 | **이웃 질의는 ECS 판단 필요 (아래 참조)** |
| `8_handoff_summary.md` | 인계 | — | 커밋·검증·주의점 |

**순서 근거**:

- **unit 0 은 사용자 요청(2026-08-07)으로 편입**했다. 기능이 아니라 **이 spec 이 손댈 코드의 사전 정리**다 — unit 1 이 어차피 `BuildFlowField` 를 편집하므로, 먼저 옮겨두면 unit 1 의 diff 가 읽힌다. 순수 이동이라 동작 변경 0.
- **unit 1 이 나머지의 전제다.** 2·3·6 이 공유할 벽 술어를 세우고, 동시에 **D1-b 의 전제조건**이다 — zero-flow=벽 판정을 떼어내지 않으면 봉쇄 시 차단 구역 전체가 벽이 된다.
- **unit 2 를 3 보다 앞에 두는 이유**는 역순이면 중간 상태가 더 나빠지기 때문이다 — 대각 이동이 열린 채 셀 경계 clamp 가 남아 있으면 벽 모서리에서 거칠게 걸린다.
- **unit 4 가 5 보다 앞인 이유**는 재빌드가 dist 를 *런타임에* 변하게 만들어, unit 5 가 검증해야 할 범위를 넓히기 때문이다.
- unit 3 단독으로는 L 자만 사라진다. **"직선 이동"은 unit 6 에서 완성된다.**

## BattleBridge 총 변경량

이 spec 은 게이트웨이를 **키우지 않는다. 순감이다.**

| 항목 | 델타 |
|---|---|
| unit 0 — 필드 설치 코드 추출 | **−180줄** |
| unit 1 — walkMask 이관 | 소폭 감소 (`DefenderFieldSingleton` 중복 개념 해소) |
| unit 4 — 재빌드 | **0줄** (ISystem 이 기존 배열에 in-place 기록) |
| unit 2·6·7 — 이동/평활화/분리 | **0줄** (`MovementSystem` 전용) |
| 반지름·분리 계수 knob | ScriptableObject 신설 + 전달 몇 줄 |

`AggroStateSystem` 의 어그로 획득 시 `Allocator.Temp` walkMask 재계산(`:141-146`)도 unit 1 에서 사라진다.

## 실제 ECS 접점 (5곳, 그 외는 전부 순수)

계산은 ECS 를 모르지만 통합 지점은 존재한다. 정직하게 열거한다.

1. **필드 할당/해제의 소유자 이전** (unit 0) — `BattleBridge` → `SimFieldInstaller`. **소유 주체는 그대로 Mono 측**이다(설치자를 부르는 건 여전히 브리지). 싱글턴 3종의 라이프사이클 공유 계약도 그대로 옮긴다.
2. **정적 마스크를 `FlowFieldSingleton` 으로 이관** (unit 1) — Effects 맥락 소유. **새 싱글턴을 만들지 않는다.** 현재 `DefenderFieldSingleton.walkMask` 에 얹혀 있는 것을 제자리로 옮겨 두 필드가 공유한다.
3. **동적 장애물은 `ObstacleSingleton` 에 그대로 둔다** (unit 1) — `ObstacleLifetimeSystem` 이 매 프레임 `blockedCells` 를 Clear 후 재수집하며 `[UpdateBefore(MovementSystem)]` 로 돈다. 이 갱신 주기를 정적 마스크에 접을 수 없다. `NavGrid` 가 두 싱글턴을 프레임 단위로 합쳐 순수 함수에 넘긴다.
4. **`MovementSystem` 의 쓰기 지점** (unit 2·6) — `LocalTransform.Position` 은 Movement 맥락 소유. 맥락 경계 변화 없음. 순수 함수를 호출해 결과를 대입할 뿐.
5. **재빌드 시스템 신설** (unit 4, D1-b) — Effects 맥락. `FlowFieldSingleton` 의 *내용* 갱신이므로 소유 맥락이 쓴다(`DefenderFieldSystem` 선례). 할당·해제는 계속 설치자가 소유해 라이프사이클을 이원화하지 않는다. `AggroChaseCell`(Effects 소유 버퍼) 무효화도 여기서.
6. **unit 7 의 이웃 질의** — "반경 안의 다른 적"은 엔티티 순회이므로 순수 함수로 뺄 수 없다. **밀어내는 계산 자체는 순수**로 두고 이웃 수집만 ISystem 이 한다. 공간 분할 자료구조가 필요한지는 unit 7 착수 시 실측으로 판단한다 — 미리 만들지 않는다(제약 8).

**리뷰 판정**: unit 0·1·4·7 은 ECS 계약(싱글턴 라이프사이클 · 맥락 소유 · 시스템 순서 · 이웃 질의)을 건드리므로 `ecs-reviewer` 대상. unit 2·3·5·6 은 순수 로직 + 기존 호출부 대입이라 일반 코드 리뷰로 충분하다.

**⚠ unit 0 의 위험**: `TeardownFlowField` 는 호출처 4곳(`BattleBridge.cs` 583·797·1023·1798)이고, 멱등성·누수 방지 계약이 주석에 "CRITICAL #1 (Codex 2차 리뷰)"로 박혀 있다(`:790`). **추출 커밋에서 로직을 한 줄도 바꾸지 않는다** — 옮기기만 하고, 개선은 후속 커밋으로 분리한다. 누수는 조용히 발생해 나중에 원인 추적이 오래 걸린다.

## Feature-wide 계약

- **모든 계산은 plain 입력 → plain 출력 static 함수다.** ISystem 안에 산식을 인라인하지 않는다. 각 함수는 EditMode 단위 테스트를 갖는다 (sim-critical: 이동·경로).
- **타일은 저작·규칙의 단위로 남는다.** `placeMask`, `tileRange`(189 참조), `TileAoe`(체비셰프), 존·해저드·드림캐쳐는 이 spec 에서 건드리지 않는다. **이동만** 격자에서 풀린다.
- **`FlowFieldSingleton` 을 유지한다.** 9개 시스템이 `RequireForUpdate<FlowFieldSingleton>()` 로 하드 요구하고, 그중 6개는 `tileSize`/`gridSize`/`origin` 만 읽는다. 싱글턴을 없애면 존·해저드·실드·픽업·투사체발사·보스주기·체력임계가 **에러 없이 조용히 멈춘다**. 이 spec 은 필드의 *내용*만 바꾼다.
- **전역 필드를 유지한다.** 평활화(unit 6)는 필드 **위에 얹는 것**이지 대체가 아니다. 순수 스티어링 회피는 오목 지형(U자 벽)에서 갇힌다.
- **필드는 정적 벽과 동적 장애물을 모두 반영한다** (D1-b). 장애물 집합이 바뀌면 재빌드한다 — **막으면 돌아간다**가 게임 규칙이다. 완전 봉쇄 시의 거동은 "적이 벽면에서 차단 해저드를 부순다"이며, 이는 `destructible-blocking-hazards`(구현 완료)가 담당한다. 새 연결성 가드를 만들지 않는다.
- **벽 질의는 한 진입점으로 모은다.** unit 1 이후 "이 칸을 걸을 수 없는가"를 묻는 곳은 `NavGrid` 하나다. 단 그 *내용*은 정적/동적 두 출처의 합성이며, `NavGrid` 는 이를 감추지 않고 **프레임마다 조립**한다. flow 값에서 벽을 파생하지 않는다 (현행 `IsWallCell` = zero-flow 는 경로 *결과*에 벽 정의를 얹은 형태라, 평활화 레이캐스트가 쓸 수 없다).
- **결정론 유지.** 순수 관리 C#, 부동소수 연산 순서 고정, `Date`/`Random` 미사용. 현행 `+x` 우선 타이브레이크는 코드에 "결정론 계약"으로 명시돼 있으므로(`FlowFieldBuilder.cs:12`), unit 3 이 이를 **대체하는 새 계약을 명시 정의**한다. Unity NavMesh 를 쓰지 않는 이유가 이것이다 — bake 데이터·엔진 내부에 묶여 엔진-프리 이식과 양립하지 않는다.
- **순서 의존 연산은 누적 후 일괄 적용한다.** unit 7 의 상호 밀어내기는 순회 순서에 따라 결과가 갈린다(A→B 먼저 처리하면 B→A 가 갱신된 위치를 본다). 모든 밀어냄을 **먼저 누적하고 그 뒤에 한 번 적용**해 순서 무관을 보장한다. unit 4 의 dirty 판정도 같은 이유로 순서 무관이어야 한다(개수 + 교환법칙 결합). unit 2·6 은 에이전트 간 독립이라 이 문제가 없다.
- **유닛 반지름 = 0.35 타일** (기본값, 사용자 미지정으로 채택). 지름 0.7 < 1.0 이라 1타일 복도 통과 가능 + 벽 여유 0.15. ScriptableObject knob 으로 노출하고 하드코딩하지 않는다(제약 6).
- **겹침은 소프트 분리**로 처리한다 (기본값, 사용자 미지정으로 채택) — 서로 밀어내되 관통을 하드 블록하지 않는다. 1타일 복도에서 하드 블록은 교착을 만든다.
- **맵 저작은 이 spec 밖이다.** 복도 폭 확장·열린 아레나 전환은 후속 후보로 둔다.

## 미결 결정 (착수 전 확정 필요)

### D1 — 동적 장애물이 *경로*를 바꾸는가? → **(b) 재빌드로 확정** (2026-08-07 사용자 결정)

**결정**: 장애물 집합이 바뀌면 flow field 를 재빌드한다. **장애물이 적 경로를 바꾼다** — 막으면 돌아간다.

**배경**: flow field 는 맵 빌드 시 1회만 굽는 구조였다(`BattleBridge:1214`). 해저드·장애물이 셀을 막아도 필드는 몰랐고, 유닛이 장애물 쪽으로 걸어가다 clamp 될 뿐이었다. 평활화(unit 6)를 넣으면 직선으로 처박혀 정체하고 오목 배치에서 지역 최소값에 갇힌다.

**이 결정이 성립하는 근거** — 봉쇄 대응이 이미 있다:

- `destructible-blocking-hazards`(구현 완료, 2026-04-29 PlayMode 확인)로 **적이 차단 해저드를 공격해 부순다**(`Faction` + `targetMask`). 완전 봉쇄는 영구 교착이 아니라 "부수는 시간"이 된다.
- 스폰 충돌 거부 정책 존재 — 골 셀 · 기존 `blockedCells` · `DefenderTile` · OOB 겹침이면 스폰 거부.
- 연결성 검사는 **없다**(`MapConnectivity.AllSpawnsReachGoal` 은 맵 빌드 시 1회만). 완전 봉쇄 자체는 가능하며, 그때 의도된 거동은 "적이 벽면에 멈춰 서서 때려 부순다"이다.

**⚠ unit 1 은 이 결정의 전제조건이다.** `MovementCellTrim.IsWallCell` 이 zero-flow 를 벽으로 판정하므로, 봉쇄로 필드가 끊기면 차단 구역의 **모든 셀이 dist=MaxValue / flow=zero → 전부 "벽"** 이 된다. 적이 벽 위에 서 있는 상태가 되어 clamp 거동이 무너진다. 벽 술어를 flow 파생에서 떼어내는 unit 1 없이는 (b)를 켤 수 없다.

**파생 작업**: unit 4(재빌드 시스템) 신설. 아래 함의를 함께 다룬다.

- **재빌드 주체** — 할당은 `BattleBridge`(생성/`Teardown`), **내용 갱신은 Effects 맥락 ISystem.** `DefenderFieldSystem` 이 이미 이 형태다(`BattleBridge.cs:386` 주석의 확립된 분업).
- **변경 감지** — `ObstacleLifetimeSystem` 이 매 프레임 `blockedCells` 를 Clear 후 재수집하므로 매 프레임 재빌드하지 않도록 dirty 신호가 필요하다. 판정은 **순서 무관**이어야 한다(개수 + 교환법칙 결합). 청크 순서에 의존하면 결정론이 깨진다.
- **업데이트 순서** — `[UpdateAfter(ObstacleLifetimeSystem)]` + `[UpdateBefore(MovementSystem)]`.
- **`AggroChaseCell` 무효화** — 어그로 획득 시 1회 계산해 부착하는 필드라(`AggroChaseCell` 주석), 장애물이 생기면 stale 해진다. 재빌드 시 함께 무효화한다. **(b) 이전에도 있던 결함이나 (b)가 가시화한다.**
- **`dist` 가 전투 중 변한다** — (a)였다면 판 내내 고정이다. `FrontmostTargeting` 의 "앞선 적" 순서가 장애물 생성/파괴 시점에 바뀐다. unit 5 검증 범위에 포함.

### D2 — 평활화의 후보 지점을 어떻게 만드는가? (unit 6 핵심)

flow field 는 **명시 경로를 주지 않는데** string-pulling 은 경로가 있어야 한다. 후보 생성 방식이 unit 6 의 실질이다.

| | 내용 | 비용 | 결과 |
|---|---|---|---|
| (a) | 필드를 따라 앞으로 K 셀 전진시켜 후보 생성 → 가장 먼 가시점 | agent당 K 스텝 (K≈8) | 코너 스치기 정상 동작 |
| (b) | agent별 A\* 경로를 실제로 뽑고 funnel | agent당 경로 유지 | 가장 정확, 가장 비쌈 |
| (c) | 골로 직접 레이캐스트 → 보이면 직행 | 최저 | 열린 공간만 개선, 중간 장애물에서 코너 스치기 안 나옴 |

**기본값은 (a)** 로 제안한다 — 필드를 유지한 채 경로를 국소적으로 물질화하는 형태라 D1-b 의 재빌드와 자동으로 정합하고(재빌드된 필드를 그대로 따라가므로), 검증 질문의 "두 직선" 요구를 만족한다. unit 6 착수 시 확정한다.

## 파이프라인 커버리지

**N/A** — 새 플레이 오브젝트 아키타입을 만들지 않고, 생성→렌더 정거장(스폰·뷰 풀·정렬·VFX)을 변경하지 않는다. 기존 적 엔티티의 **위치 갱신 방식**만 바뀐다. `docs/reference/object-pipeline-map.md` 갱신 불필요.

## 알려진 파급 (unit 5 에서 확정)

`dist`/`flow` 를 읽는 곳은 이동 외에 4곳이다. 4-이웃 → 8-이웃 가중으로 바뀌면 값이 전부 달라진다.

| 소비처 | 무엇에 쓰나 | 예상 영향 |
|---|---|---|
| `FrontmostTargeting` | "골에 가장 가까운 적" 순서 | 타겟 우선순위 변동 (동률 처리 포함) |
| `HealthThresholdSystem:309` (`BlinkMath.TryFindLandingCell`) | 블링크 착지 셀 링 탐색 | 착지 후보 순서 변동 |
| `BattleBridge:1957-1959` | 경로 추적(스폰 예고 라인) | 필드 경로 ≠ 평활화 경로 → unit 6 후 육안 재확인. **D1-b 로 경로가 전투 중 바뀌므로 예고 라인의 갱신 시점도 함께 본다** |
| `AttackSystem:1460` | flow 방향 참조 | 8방향으로 확장 |

기대값 갱신 대상 테스트: `FlowFieldBuilderTests`, `FlowRecoveryTests`, `MovementCellTrimTests`, `MovementCellTrimApplyTests`, `MovementSystemTests`, `MovementCompositionTests`. **일괄 갱신 금지** — 하나씩 "왜 이 값이 바뀌는가"를 판단한다. 일괄 갱신하면 진짜 회귀가 섞인다.

## 검증 시 함정

- **검증 맵을 45° 기울기로 잡지 말 것.** 45°에서는 8방향 양자화가 우연히 진짜 방향과 일치해, unit 6(평활화) 없이도 직선으로 보인다. 8×6 또는 7×3 처럼 45°가 아닌 기울기를 쓴다.
- **D1-b 검증에는 봉쇄 시나리오를 반드시 넣을 것.** 차단 해저드로 경로를 **완전히** 막았을 때 (1) 적이 얼어붙지 않고 벽면으로 모여 해저드를 때리는가, (2) 해저드 파괴 직후 필드가 재빌드되어 이동이 재개되는가. unit 1 이 제대로 안 됐으면 여기서 "차단 구역 전체가 벽" 증상이 드러난다.
- **대각 비용을 1 로 세지 말 것.** 단순 BFS 로 8-이웃을 돌리면 dist 가 체비셰프가 되어 대각이 공짜가 되고, 이번엔 반대로 불필요한 대각선을 선호하는 왜곡이 생긴다 (우회 비용을 실제보다 싸게 계산).
- **코너컷 방지 필수.** 대각 이웃은 인접한 두 직교 이웃이 **둘 다** walkable 일 때만 허용한다. 아니면 유닛이 벽 모서리를 관통한다.

## 후속 후보 (이 spec 범위 밖)

- **맵 복도 폭 확장** — 폭 2~3 + 광장/교차로 2~3개. 이 spec 의 이득이 실제로 드러나는 지형이며, 경로 단축에 따른 밸런스 재튜닝(사거리·DPS·웨이브 간격)을 동반한다. 열린 아레나 전면 전환은 배치 게임의 축을 바꾸므로 별도 제품 결정.
- **항법 격자 세분화(2x)** — 게임플레이 격자 1타일을 유지한 이중 해상도. unit 6 까지 끝낸 뒤 코너 표현이 실제로 부족할 때만 착수한다. 맵이 15×12=180셀로 작아 비용은 문제되지 않지만, 세분화는 계단을 **없애지 못하고 잘게 만들 뿐**이라 평활화의 대체가 아니다.
- **방어유닛·투사체의 연속화** — 이 spec 은 적 이동만 다룬다.
- **경로 예고 UI 재표현** — 평활화 후 예고 라인이 실제 이동선과 어긋나면 표현 갱신.
