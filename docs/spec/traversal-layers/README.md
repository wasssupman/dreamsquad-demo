# traversal-layers — 통행 층 비트필드 (배치 층의 대칭)

> ## 목표 3줄
>
> 1. **셀이 이미 갖고 있는 «칸의 종류» 층 비트(`placeMask`)를 sim 이 볼 수 있게 넘긴다** — 지금 sim 은 «Walk 냐» 1비트만 안다.
> 2. **유닛에 «어느 층을 지날 수 있나»를 주고, 경로 계산이 그 교집합만 보게 한다** — 알고리즘은 그대로 두고 «어느 마스크를 먹이나»만 바꾼다.
> 3. **그 위에서 지금 켜는 것은 «방어유닛이 자기 배치 영역 안에서만 움직인다» 하나**이고, 물타일은 나중에 비트 하나로 같은 기계에 합류한다.
>
> 1·2 는 **행동 변화 0**(마스크 1종이면 현행과 바이트 동일). 판이 실제로 바뀌는 건 3 뿐이며, 3 도 이 spec 은 **전제조건까지만** 책임진다(좌표 권위·어그로 정적 전제·재배치 공존은 별도 spec — §9-2).

상태: **rev 2 · 2026-08-09 · 기반 검증 완료 · 착수 승인 대기**
(rev 0 = 적 전용 초안 / rev 1 = 이동하는 모든 주체로 확장 + 파급 실측 / rev 2 = **물타일을 만들지 않기로 확정**하고 «미래 융합 기반»으로 목표 재정의)

> **rev 2 사용자 결정 (2026-08-09)**
> 1. **물타일은 지금 만들지 않는다.** 시각 요소를 포함한 «물타일» 기능 전체는 현재 작업 대상이 아니다.
> 2. 원하는 것은 **기반**이다 — 지금 마스크가 그러하듯 타일을 맥락별로 구분해 두고, 나중에 물타일이 왔을 때 **쉽게 융합**되는 구조.
> 3. **배치 가능한 타일이 곧 이동 가능한 타일**이고, 유닛은 **그 영역 안에서만** 움직인다.
>
> 3번이 이 spec 의 성격을 바꾼다 — 통행 층의 첫 소비자는 «물 적»이 아니라 **«방어유닛 이동»** 이고, 그것이 §9-2 의 C1(배치칸이 non-walkable)을 **해소한다**. 아래 §0 참조.

## 0. 기반 검증 결과 (2026-08-09)

> **질문**: "타일을 맥락별로 구분하고 나중에 물타일이 쉽게 융합되는 기반이 되어 있나?"

세 층으로 나눠 코드를 확인했다.

| 층 | 상태 | 근거 |
|---|---|---|
| **셀 데이터 — 맥락별 구분** | ✅ **이미 완료** | 아래 §0-1 |
| **유닛 데이터 — 층 보유** | ◐ **절반** | 방어유닛엔 있고(`DefenderUnitData.placementLayers`) 적 SO 엔 없다 |
| **라우팅 — 층별 경로** | ✕ **없음. 그리고 이게 유일한 진짜 갭이다** | 아래 §0-2 |

### 0-1. 셀 쪽은 이미 «칸의 종류»를 구분한다 — 이름만 «배치»다

`placeMask` 는 **배치 전용 데이터가 아니다.** `PlacementLayer.cs:8-9` 의 주석이 스스로 그렇게 못박는다:

> *"층 이름은 **공간** 기준(어떤 종류의 칸인가)이지 유닛 클래스 기준이 아니다 — 런타임은 `DefenderClass`/role 을 일절 보지 않는다."*

실제로 이미 두 맥락을 담고 있다 — `Ground`(= `Place` 파생) 와 `Path`(= `Walk` 파생). 그리고 그 주위에 필요한 기계가 **전부 갖춰져 있다**:

| 기계 | 위치 |
|---|---|
| 파생의 **단일 정의** (빌더·재파생·폴백·페인터가 전부 이걸 씀) | `PlacementLayers.Derive` |
| 미정의 비트 제거 | `PlacementLayers.Sanitize` |
| 직렬화 + 길이 불일치 시 파생 폴백 | `MapDocument.cs:15,33,85-88` |
| 왕복 (문서 ↔ 런타임) | `MapDocumentBuilder.cs:19-32, 81-125` |
| 절차적/폴백 맵도 생성 | `BattleMapBuilder.cs:53-61` |
| 타일 변경 시 재파생 | `ObstaclePlacer.RederivePlaceMask` |
| 페인터 **층별 브러시** + 저작본 테두리 표시 + 스폰/골 경고 | `MapPainterWindow.cs:22-23, 190-194, 287-288, 388-393` |

**→ 물타일이 왔을 때 셀 쪽에 필요한 변경은 3줄이다**: `PlacementLayer` 에 `Water = 1 << 2`, `Derive` 에 `case MapTileType.Water`, 페인터 브러시 목록에 한 항목. 나머지 소비자는 그 비트를 모르므로 영향받지 않는다(비트필드가 직교인 덕).

**따라서 «맥락별 구분» 기반은 새로 만들 것이 없다. 재사용하면 된다.**

### 0-2. 진짜 갭은 하나 — `placeMask` 가 sim 으로 넘어가지 않는다

`SimFieldInstaller` 는 `walkMask` (= `tiles == Walk` 인 0/1)만 만들어 `FlowFieldSingleton` 에 넣는다. **`placeMask` 는 `GeneratedMap`(Mono 쪽)에만 존재하고 sim 은 그것을 볼 수 없다.**

그래서 sim 의 모든 경로 계산은 «Walk 냐 아니냐» 1비트만 안다. 층이 의미를 가지려면 **셀 층 비트필드가 sim 으로 넘어가야 한다** — 이것이 이 spec 이 실제로 해야 할 최소 작업이고, 나머지(마스크별 라우팅)는 그 위에 얹힌다.

### 0-3. 세 번째 결정이 여는 것 — 방어유닛 이동이 «공짜에 가깝다»

「배치 가능 = 이동 가능, 그 영역 안에서만」을 코드에 대보면:

- `PatrolAreaMath.FillAreaMask(walkMask, gridSize, anchorCell, tileRadius, outMask)` 는 **임의 마스크를 받아** 앵커 박스와 교집합한다(`:29-47`). «영역 안에서만»이 이미 이 함수의 정의다.
- 지금 그 자리에 들어가는 건 `MovementCellTrim.FillWalkMask` 가 채운 **Walk 마스크**다(`PatrolFieldSystem.cs:50`).
- 여기에 **Place 파생 마스크**를 먹이면 방어유닛이 자기 배치 영역 안에서 움직인다. **알고리즘은 한 줄도 안 바뀐다.**

그리고 이것이 §9-2 의 **C1 을 해소한다** — 방어유닛이 Walk 칸을 찾아 나가는 게 아니라 애초에 자기 층 칸 위에서만 도니, 배치 타일을 떠나지 않는다.

**결론**: 이 spec 의 최소 코어는 «셀 층 비트필드를 sim 으로 넘긴다» 하나이고, 그 첫 소비자는 물이 아니라 방어유닛 이동이다. 물타일은 그 위에 비트 하나로 합류한다.

## 1. 상위 목표

**타일이 «어떤 종류의 칸인가»를 층 비트로 갖고, 유닛이 «어느 층을 지날 수 있는가»를 층 비트로 갖는다. 통행은 그 교집합이다.**

```
통행 가능  ⇔  (셀 층 & 유닛 통행 층) != 0
```

배치 판정(`placement-mask` unit 4)과 **같은 셀 데이터를 공유**하고 유닛 쪽 축만 다르다(§7 계약 1). 지금은 sim 이 «Walk 냐 아니냐» 1비트만 보므로 이 표현이 불가능하다.

이 구조가 서면 «배치 영역 안에서만 움직이는 방어유닛»(지금 원하는 것)과 «물만 지나는 적»(나중)이 **같은 기계에 데이터로 합류**한다.

## 2. 왜 적/방어유닛을 구분할 필요가 없나

**이미 둘 다 같은 이동 파이프라인을 탄다.** `PathFollowState` 를 받는 곳은 `BattleBridge` 에 두 군데이고 — 순찰 방어유닛 스폰(`CreatePatrolEntity:6173`)과 적 스폰(`SpawnUnit:7546`) — 그 뒤로는 `MovementSystem` 이 둘을 같은 루프에서 돈다.

따라서 **통행 층을 `PathFollowState` 에 실으면 한 메커니즘이 둘 다 덮는다.** 적용 대상을 유닛 종류로 분기하지 않는다(placement-mask 의 «클래스 비종속» 원칙 승계). 미래에 움직이는 주체가 늘어도 `PathFollowState` 를 받는 순간 자동으로 편입된다.

## 3. 지금 구조에서 얼마나 바뀌나

### 3-1. **바뀌지 않는 것** (이게 이 계획의 핵심)

| 무엇 | 왜 안 바뀌나 |
|---|---|
| `FlowFieldBuilder` | **이미 `walkMask` 를 인자로 받는다.** 다익스트라·옥타일 비용·코너컷 전부 그대로 |
| `AgentCollision` · `PathSmoothing` · `Separation` · `GridMath` · `FlowRecovery` | `NavGrid` 와 plain 값만 본다. 통행 정의가 어디서 왔는지 모른다 |
| `NavGrid` 구조 | 이미 «프레임 뷰» 다. 마스크가 여러 벌이 되면 **뷰를 여러 개 조립할 뿐** 타입은 그대로 |

**경로·충돌·평활화 알고리즘은 한 줄도 안 바뀐다.** 바뀌는 건 «어느 마스크를 먹이나» 뿐이다.

### 3-2. **이미 있는 선례** (새로 발명하지 않는다)

| 패턴 | 어디에 이미 있나 |
|---|---|
| 층 비트필드 + 파생 폴백 + Sanitize | `PlacementLayer` / `PlacementLayers.Derive·Sanitize` — **복제가 아니라 재사용**(rev 2 계약 1) |
| 유닛 SO 의 층 필드 + None 폴백 | `DefenderUnitData.placementLayers` / `EffectivePlacementLayers` |
| 셀 마스크 직렬화·왕복·페인터 브러시 | `MapDocument.placeMask` · `MapPainterWindow._placeMask` / `_maskBrushLayer` |
| **제한된 마스크로 필드를 굽기** | **순찰**(`PatrolAreaMath:79`, 거점 박스 ∩ walk) — **이것 하나뿐이다** |
| 유닛마다 **자기 필드 버퍼**를 따르기 | **어그로 추격**(`AggroChaseCell`, `AggroStateSystem:163`). 어그로는 **전체 walkMask** 를 쓰고 제한되는 건 **소스 집합**(가디언 사거리 디스크)이라 마스크 선례가 아니다 |

두 줄은 **서로 다른 선례**다: 마스크를 바꿔 굽는 것(순찰)과 유닛별 필드를 따르는 것(어그로). 이 spec 은 **둘을 합친 형태**이고, 각각은 이미 프로덕션에서 돈다.

### 3-3. **새로 만드는 것**

rev 2 계약 1(셀 데이터 공유)에 따라 **셀 쪽은 새로 만들지 않는다.** 신규는 두 가지뿐이다.

| 무엇 | 크기 | 내용 |
|---|---|---|
| **셀 층을 sim 으로 전달** | **S** | `SimFieldInstaller` 가 `GeneratedMap.placeMask` 를 `FlowFieldSingleton` 에 함께 설치(`walkMask` 옆에 `cellLayers`). §0-2 의 유일한 갭 |
| 유닛 `traversalLayers` (SO + 컴포넌트) | **S** | `DefenderUnitData` / 적 SO + 스폰 주입. 미지정 폴백은 계약 2 |
| **라우팅 다중화** | **M** | 마스크 값별 flow/dist를 **한 싱글턴 안 flat stride** 로. 로스터에서 결정론적 수집(오름차순). 값 1종이면 지금과 바이트 동일 |

**~~`TraversalLayer` 신규 enum~~ · ~~셀 `traverseMask` 직렬화~~ — rev 2 에서 삭제.** `PlacementLayer` 비트필드와 `placeMask` 직렬화를 **그대로 재사용**한다(§0-1). 이것이 «나중에 물타일이 쉽게 융합되는 구조»의 실체다 — 저작·직렬화·페인터가 한 벌뿐이라 비트 하나만 늘면 된다.

### 3-4. **손대는 것** (파일별)

| 파일 | 무엇을 |
|---|---|
| `Bridge/SimFieldInstaller.cs` | 유일한 `walkMask` 생산 지점(`:58`)이 마스크별 라우팅을 만든다. **싱글턴 엔티티는 1개를 유지**한다(아래 상자) |
| **`Movement/MovementCellTrim.cs`** | `BuildNavGrid`/`FillWalkMask` — `NavGrid` 를 조립하는 **유일한 지점**인데 시그니처가 `in FlowFieldSingleton` 이라 «어느 마스크»를 고를 수단이 없다. 호출처 4곳. 계약 4를 실제로 이행하는 파일 |
| `Combat/AttackSystem.cs:1465` | **넉백 방향**을 골 flow 에서 뽑는다 — 물 적이 육지 필드 방향으로 밀린다 |
| `Bridge/BattleBridge.cs:903` | 스폰 레인 측면 분산이 골 flow 기반(`ComputeSpawnLateralOffset`) |
| `Bridge/BattleBridge.cs` 의 `MapTileType.Walk` 6곳 | `:2247,:2256`(`TryGetNearestWalkCell` — **순찰 앵커 스냅**이 쓴다) · `:2292,:2306` · `:3912` · `:4404` |
| `Data/BattleMapBuilder.cs:39,42` | `BuildFallbackLinear` — §3-3 직렬화 목록에 없던 **네 번째 맵 생산자**(라이브 폴백에서 실제로 불림) |
| `Effects/FlowFieldRebuildSystem.cs` | 장애물 변경 시 N벌 재빌드 |
| `Movement/MovementSystem.cs` | `field.flow/dist` → **그 유닛의 필드**. `NavGrid` 도 그 마스크 것 |
| `Movement/AgentSeparationSystem.cs` | unit 13 의 `RejectForwardPush` 가 `field.flow` 를 읽는다 — **rev 0 목록에 없던 신규 소비처** |
| `Effects/DefenderFieldSystem.cs` | 보스 사냥 필드를 보스의 마스크로 |
| `Effects/PatrolFieldSystem.cs` · `PatrolAreaMath` | 거점 마스크 ∩ 통행 마스크 |
| `Effects/AggroStateSystem.cs` · `Combat/AggroChaseMath.cs` | 추격 필드를 그 적의 마스크로 |
| `Combat/BlinkMath.cs` 호출부 (`HealthThresholdSystem:309`) | **착지 셀 판정이 `ff.dist` 기반** — 물 적이 육지로 순간이동하면 안 된다 |
| `Combat/FrontmostTargeting` 호출부 (`AttackSystem:589`) | dist 비교 의미 — **§5 미결** |
| `Data/MapConnectivity.cs` | `AllSpawnsReachGoal` 이 `MapTileType.Walk` 하드코딩(`:35,52,62`) → 마스크별 검증 |
| `Editor/MapPainterWindow.cs` | 통행층 브러시(배치층 브러시와 같은 UI) |
| `Data/DefenderUnitData.cs` + 적 SO | `traversalLayers` 필드 + `None` → `Ground` 폴백 |
| `Bridge/BattleBridge.cs` | 스폰 2곳에서 `PathFollowState.traversalLayers` 주입 + 예고 라인(`:1911`)이 그 적의 필드 |

**총 파일 ~20개 · 신규 3개.**

> ### 「N벌」이 아니라 **「기하 1벌 + 라우팅 N벌, 한 컴포넌트 안」** 이다
>
> rev 1 초판은 *"마스크별로 N벌 생성. 라이프사이클도 N벌"* 이라고 썼다. **그대로 구현하면 게임이 죽는다** — `SystemAPI.GetSingleton<T>()` 는 매치가 2개 이상이면 throw 한다.
>
> 실측: `FlowFieldSingleton` 소비처 **15곳 중 라우팅(`flow`/`dist`/`walkMask`)을 읽는 건 4곳뿐**이다(`MovementSystem` · `AgentSeparationSystem` · `DefenderFieldSystem` · `HealthThresholdSystem`). 나머지 **11곳은 `tileSize`/`gridSize`/`origin` 만** 읽는다 — 투사체 3종 · 존/해저드/실드 캐스트 · 픽업 · 보스주기 · 적 FSM · 어그로 · 순찰.
>
> 따라서 올바른 shape 는 **싱글턴 엔티티 1개 안에서 기하는 1벌로 두고 라우팅만 마스크 수만큼** 갖는 것이다. 이러면 **11곳은 손댈 필요가 없다** — 파급이 오히려 줄어든다.
>
> 그리고 `NativeArray<NativeArray<T>>` 는 **불법**이다(nested native container). 라우팅은 **flat stride** 여야 한다 — `flow[m * n + i]` + `maskValues[m]`. 200셀 규모에서 가장 단순하고 Burst 친화적이다.

## 4. 작업 단위

> **⚠ 아래 표는 rev 2 재작성 대상이다.** 디스크의 `0~3_*.md` 는 **rev 0(적 전용) 그대로**이고 README 와 갈렸다 — 존재하지 않는 `IsWallCell` 시그니처 변경을 지시하고, 생산 지점을 `BattleBridge.BuildFlowField`(실제는 `SimFieldInstaller:58`)로 적고, 셀 데이터를 새로 만들라고 한다(계약 1과 충돌). 번호도 3번이 둘이다. **착수 전 재작성이 블로커다.**

rev 2 스코프의 작업 단위 (재작성할 목표 형태):

| # | 작업 구분 | 목적 | 행동 변화 |
|---|---|---|---|
| 0 | 셀 층 전달 | `SimFieldInstaller` 가 `placeMask` 를 sim 으로 넘긴다(`FlowFieldSingleton.cellLayers`). 소비자 0 | **0** |
| 1a | 싱글턴 shape | 라우팅을 flat stride 로 재배치. 마스크 1종 고정, 전 소비처 primary. **빌드 그린 · 바이트 동일** | **0** |
| 1b | 라우팅 다중화 | 로스터 수집(오름차순) + 마스크별 BFS + 도달 실패 폴백 | 0 (1종이면) |
| 2a | 유닛 축 | 유닛 `traversalLayers`(SO+컴포넌트) + 스폰 주입 + `MovementSystem`·`AgentSeparationSystem` 이 그 슬롯을 읽음 | 0 (폴백이 현행) |
| 2b | 벽 술어 | `MovementCellTrim.BuildNavGrid` 를 마스크 인지로 (**D2 가 자동 해결**) + `AggroChaseMath`·`PatrolAreaMath` | 0 |
| 3 | **첫 소비자 — 방어유닛 이동** | 배치 영역 마스크로 순찰 필드를 굽는다(§0-3). **여기서 처음 판이 바뀐다** | **있음** |

**순서 근거**: 0~2b 는 전부 «행동 변화 0 · 빌드 그린»이라 언제든 멈춰도 판이 안 바뀐다. 실제 게임 변화는 3 에서만 일어나고, 그것이 사용자 결정 3(배치 가능 = 이동 가능)의 구현이다. 물타일은 이 spec 밖이며, 오면 계약 2의 파생표에 비트 하나가 는다.

**⚠ unit 3 은 이 spec 단독으로 끝나지 않는다** — §9-2 의 C2(좌표 권위 split-brain 8곳)·C3(어그로 정적 전제)·재배치 공존이 남는다. 통행 층은 C1 만 해소한다. 방어유닛 이동을 실제로 켜려면 별도 spec 이 필요하고, 이 spec 은 그 **전제조건**까지만 책임진다.

## 5. 미결 결정 (착수 전 확정 필요)

**D1 — 마스크가 다른 유닛끼리 «앞선 적» 순서를 어떻게 정하나? → 기본값 채택** (2026-08-09)

`FrontmostTargeting` 은 `dist` 오름차순으로 "골에 가장 가까운 적"을 고르는데, 필드가 여러 벌이면 **물 적의 dist 와 육지 적의 dist 는 다른 그래프 위의 값**이라 직접 비교가 의미를 잃는다. 물길이 돌아가면 골 코앞의 물 적(dist 180)이 5칸 뒤 육지 적(dist 50)보다 뒤로 밀린다.

**⚠ 범위 정정**: 이 규칙은 **일반 타겟팅이 아니다.** `AttackSystem:470` 이 요구하는 조건은 ⑴ 방어유닛 ⑵ `FrontmostAttackLock` 보유 ⑶ 살아있는 `FrontmostTarget` 슬롯 — 즉 **드림캐쳐 카드 「끝을 보는 눈」이 걸린 동안에만** 발동한다. 평소 타겟팅은 이 경로를 타지 않는다. 착수를 막을 결정이 아니다.

**채택(재검토 중): `dist / maxFiniteDist(그 마스크의 필드)` 정규화.** 리뷰 반론 — raw `dist` 는 «그 유닛 통행 집합 위의 실제 남은 이동 비용»이라는 방어 가능한 의미가 있고, 정규화는 그것을 «그 마스크 최악치 대비 비율»로 바꾼다. 발동 조건이 (카드 × 마스크 2종 공존 × 둘 다 사거리 내)로 희소하므로 **코드 0줄 + 계약 한 줄(«마스크 간 dist 비교는 근사»)** 이 더 나을 수 있다. 착수 시 재확정.

정규화안의 장점은 0~1 잔여 진행률이라 그래프가 달라도 비교되고 엔티티별 상태가 필요 없다는 것, 그리고 **마스크가 1종이면 단조 변환이라 순서가 지금과 완전히 동일**하다는 것이다. 어느 쪽이든 unit 3 소관.

**D2 — 통행 불가 지형에 넉백/견인으로 밀려 들어가면?** 지금은 벽이면 `AgentCollision` 이 막는다. 물 적이 육지로 밀리는 건 같은 규칙으로 막히지만, **자기 층이 아닌 칸에 갇히는 경우**(맵 편집 실수·층 변경)의 탈출 규칙이 필요하다. 후보: `FlowRecovery` 를 그 마스크 dist 로 돌리면 자동 해결 — 기본값으로 채택 제안.

**D3 — 필드 개수 상한?** rev 0 계약 7 은 «3종 초과 시 경고». 유지할지, 하드 상한으로 바꿀지.

## 6. rev 0 에서 정정된 전제

- **정정 ①(쉬워짐)**: rev 0 은 *"런타임 벽 판정도 필드의 `flow==0` 에서 파생된다(`MovementCellTrim.IsWallCell`)"* 를 전제로 계약 4를 세웠다. **`IsWallCell` 은 존재하지 않는다** — continuous-agent-movement unit 2 에서 은퇴하고 벽 술어가 `NavGrid`(정적 마스크 + 동적 장애물)로 이관돼 **flow 와 분리**됐다. 벽과 경로가 이미 따로 놀므로 이 작업이 rev 0 가정보다 단순하다. 계약 4를 «`NavGrid` 가 유일한 벽 술어이며, 마스크별로 조립된다»로 대체한다.
- **정정 ②(늘어남)**: rev 0 의 소비처 목록에 `AgentSeparationSystem`·`BlinkMath` 호출부·`FrontmostTargeting` 호출부·`MapConnectivity` 가 빠져 있었다. `AgentSeparationSystem` 의 flow 소비는 rev 0 작성 **이틀 뒤**(unit 13)에 생겼다.

## 7. Feature-wide 계약

1. **셀 층 비트필드는 하나를 공유하고, 유닛 쪽 축만 둘이다** (rev 2 에서 rev 0/1 의 «별도 enum» 결정을 뒤집음).
   - 셀은 «어떤 종류의 칸인가»를 **한 벌**의 비트로 갖는다 — 이미 있는 `placeMask` 가 그것이다(§0-1). 셀 배열을 두 벌 만들면 저작이 두 배가 되고 페인터·직렬화·파생 폴백도 두 벌이 되는데, 실제로 «배치 층»과 «통행 층»이 서로 다른 값을 가져야 할 칸의 예가 지금 하나도 없다(제약 8).
   - 유닛은 두 축을 갖는다 — `placementLayers`(설 수 있는 칸) / `traversalLayers`(지날 수 있는 칸). 사용자 결정 3에 따라 방어유닛은 기본적으로 **둘이 같고**(배치 가능 = 이동 가능), 적은 `traversalLayers` 만 의미가 있다.
   - 이 결정으로 `placeMask` 는 이름과 실제가 어긋난다. **리네임은 하지 않는다** — 참조가 40곳 이상이고 이름 변경은 이 spec 의 검증 질문과 무관하다. 대신 «셀 층 = 칸의 종류이며 배치·통행이 함께 읽는다»를 `PlacementLayer.cs` 헤더 주석에 명시한다.
2. **파생 기본값 = 현행 재현** — 셀 파생은 이미 있는 `PlacementLayers.Derive` 를 그대로 쓴다(`Place → Ground`, `Walk → Path`, 나머지 0). 유닛 통행 층 미지정(`None`)은 **적 = `Path`**(현행 재현) / **방어유닛 = 자기 `placementLayers`**(사용자 결정 3) 로 폴백한다. **아무것도 저작하지 않으면 지금 판과 완전히 동일**(옵트인).
3. **클래스 비종속** — 적/방어유닛/보스를 코드가 구분하지 않는다. `PathFollowState` 를 받는 모든 주체가 같은 규칙.
4. **벽 술어 단일 정의** — `NavGrid` 하나. 마스크별로 **조립**될 뿐 술어가 두 벌이 되지 않는다.
5. **결정론** — 필드 집합의 순서·내용은 로스터에서 결정론적(마스크 값 오름차순). 같은 시드·같은 덱 = 같은 필드 집합 = 같은 경로.
6. **B-1 승계** — 유닛과 적은 여전히 서로를 막지 않는다. 이 spec 은 «어디를 지날 수 있나»만 데이터화하며 충돌/블로킹을 도입하지 않는다.
7. **스폰·골 칸은 그 맵에 등장하는 모든 마스크에 대해 통행 가능**해야 한다 — 빌드 시 강제 + 페인터 검증(`MapConnectivity` 확장).
8. **성능 상한** — 필드는 마스크 값 종류만큼만. 종류가 3 을 넘으면 경고(콘텐츠 설계 실수 신호).

## 8. 파이프라인 커버리지

**N/A** — 플레이 오브젝트 신설·생성→렌더 경로 변경 없음(이동 판정의 데이터 소스 교체 + 길찾기 다중화). 렌더 정거장 불변.

## 9. 인접 결정 — 사용자 입력 (2026-08-09) 과 현재 구현의 갭

이 spec 밖이지만 **맞물려서** 함께 판단해야 하는 두 가지. 사용자가 제시한 원칙을 코드와 대조한 결과를 그대로 적는다.

### 9-1. 타겟팅 철학 — **적 쪽은 구현됨, 방어유닛 쪽은 없음**

> 사용자 원칙: 방어유닛 기준 결정된 타겟은 특수조건(범위이탈 등) 외에 바뀌지 않고, 적 기준 결정된 타겟은 범위 이탈·어그로 끌림 외에는 바뀌지 않는다.

| 주체 | 현재 구현 | 원칙 대비 |
|---|---|---|
| **적** — `FocusUntilDead` | `FocusTarget.current` 락. 대상이 죽거나 사라질 때까지 유지하고, **사거리는 발사만 게이팅(락은 유지)** — `AttackSystem:637~667` | **일치** |
| **적** — 어그로 | `aggroLookup` sticky override. 필터·우선순위·nearest·focus 를 전부 무시하고 가디언만, 사거리 안일 때만 — `:669~` | **일치** |
| **적** — `Nearest` 모드 | 매 프레임 재선정 | **불일치** — 원칙대로면 이 모드도 락이 필요하거나, 「의도된 예외」로 문서화돼야 한다 |
| **방어유닛** | **지속 락이 없다.** 정정 — `EnemyBehavior` 는 순찰 방어유닛도 갖는다(`:6165`, 주석에 "faction-agnostic"). 적 전용을 만드는 건 클래스가 아니라 **SO 의 `targetMode` 값 + `FocusTarget` 부착**(`:7533`) = **데이터로 갈린다**(계약 3과 일관). 유일한 방어유닛 락 `FrontmostAttackLock` 은 카드 한정 + **공격 1회 안에서만** 유효하다(지속성 장치가 아니라 wind-up desync 방지). 방어유닛은 `EnemyTargetFilter` 를 안 받아 priority 경로가 없고, 힐러만 lowest-health 랭킹 | **불일치 — 이게 가장 큰 갭** |

**traversal-layers 와의 접점**: 방어유닛 타겟이 고착이면 frontmost 순서(D1)는 **획득 시점에만** 영향을 준다. 지금처럼 매 프레임 재선정이면 매 프레임 영향을 준다. 즉 **철학을 구현하면 D1 의 중요도가 더 떨어진다.**

**판정: 별도 spec 후보.** 이 spec 에 넣지 않는다 — 통행 층과 인과가 없고(층이 없어도 갭은 존재한다), 타겟 락은 그 자체로 밸런스 변경이라 한 커밋에 묶으면 원인 분리가 안 된다(제약 9 · 「버그 픽스 ≠ 기능」).

### 9-2. 방어유닛 이동 — **부품은 이미 클래스 비종속, 막는 건 제품 결정**

> 사용자 질문: 현재 구현된 소환물의 이동방식을 모든 방어 유닛에게 적용 가능한가.

현재 상태:
- **소환 순찰병**은 `PatrolAnchor`(거점) + `PatrolFieldSystem`(거점 박스 ∩ walk 마스크로 필드) + `PatrolStep`(방향) 으로 움직이고, `MovementSystem` 이 그 dir 을 **적과 같은 루프**에서 소비한다.
- **일반 방어유닛**은 배치 타일 위에 고정. 예외는 `DefenderRelocationController`(이동모드 → 목적지 지정 → 비행/재전개)인데 이건 **플레이어가 지시하는 텔레포트성 재배치**이지 지속 이동이 아니다.

**부품은 비종속이다.** `PatrolAnchor`·`PatrolStep`·`PatrolFieldSystem`·`PatrolAreaMath` + `MovementSystem` 의 patrol 분기(`patrolStepLookup.HasComponent`) 전부 유닛 종류를 묻지 않는다. 소환 종속은 수명 링크(`SummonedBy`/`PatrolLifecycleSystem`)와 스폰 경로뿐이다. 부착은 **3개** 필요하다(`PatrolAnchor` + `PathFollowState` + `PatrolStep` — `PatrolFieldSystem` 은 `PatrolStep` 을 쿼리만 하고 생성하지 않는다, `BattleBridge:7565`).

**그러나 현재 로스터에서는 켤 수 없다** (2026-08-09 조사):

- **C1 — 배치 셀이 walkable 이 아니다.** `NavGrid.staticWalk = (tiles == Walk)` 인데 배치칸은 `Place` 라 마스크 0 이다. 실측 저작: `placementLayers: 2`(Path)인 유닛은 **`Defender_Guardian` 하나뿐**이고 나머지 25종은 `Ground` 폴백 = 비-walkable 위에 서 있다. 이동을 켜면 `PatrolAreaMath` 가 **의도적으로** `RecoveryDir` 탈출을 시켜(`:140-147`) 전투 시작 즉시 배치 타일을 떠난다. 앵커를 배치 셀에 두면 반대로 도달 불가 목적지를 향해 영원히 전진한다.
- **C3 — 유일하게 Path 층인 가디언이 하필 어그로 보유자다.** `AggroCapacity: 2` = Guardian·Bastion·ShieldShuttle. `AggroChaseCell` 은 **어그로 획득 시점의 가디언 셀로 1회만** 굽고(`AggroStateSystem:148-165`), 그 1회성의 근거가 *"어그로는 목적지가 정적이라"*(`PatrolStep.cs:11-14`)다. 가디언이 움직이면 적이 옛 자리로 계속 걷는다.

즉 **C1 이 없는 유일한 유닛이 C3 에 정면으로 걸린다** — 두 제약이 현재 로스터에서 상호 배타적이다.

그 외 HIGH: 좌표 권위 split-brain(`DefenderTile` vs `LocalTransform` — 사직서·레드불·아군장판·해저드검증·시너지·효과타일·점유격자 8곳), 재배치의 앵커 재스냅 경로 부재, `SyncPatrolViews` 이중 동기화로 재배치 비행 파괴, `AgentSeparationSystem` 자동 편입(배치 격자가 물리적으로 흐트러짐), `DeployedFacing` 축 무의미화, walk 애니 전 유닛 미저작.

막는 것은 코드가 아니라 제품 축이다. 함께 결정해야 하는 것:
1. **배치 위치의 의미** — 자유 이동하면 "어디에 놓나"가 "어느 구역에 넣나"로 바뀐다. 배치 게임의 핵심 축 변경.
2. **사거리 밸런스** — 이동하면 실효 커버 범위가 `range + 순찰 반경` 이 된다. 전 유닛 재튜닝.
3. **재배치 기능과의 중복** — 자유 이동이 생기면 `DefenderRelocationController` 의 존재 이유(정밀 재배치)가 약해진다. 남길지 통합할지.
4. **통행 층과의 관계** — 방어유닛이 움직이면 **이 spec 이 곧바로 그들에게도 적용된다**(계약 3 클래스 비종속). 즉 «땅 방어유닛은 물을 못 건넌다»가 공짜로 따라온다. **이 spec 을 먼저 하는 게 순서상 유리하다.**

**판정: 별도 spec 후보(제품 결정 선행).** 이 spec 은 «어디를 지날 수 있나»만 데이터화하고, «누가 움직이나»는 건드리지 않는다. 다만 계약 3 이 그 확장을 **미리 막지 않도록** 설계돼 있음을 여기 명시해 둔다.

## 10. 후속 후보

- **비행(공중) 유닛** [M] · 통행 층에 `Air` 비트를 추가하면 모든 칸을 지나는 유닛이 데이터로 생긴다. 착지/그림자·타겟팅 규칙은 별도 결정.
- **통행 층별 시각 어포던스** [S] · 어떤 적이 어느 길로 오는지 배치 페이즈에 보여줄지.
- **통행 층별 이동 비용**(선호) [S] · 지금 계획은 **하드 제한**(못 감)만 다룬다. "가능하면 물길로, 정 안 되면 육지로" 같은 **선호**는 다익스트라 가중치 문제라 **필드를 늘리지 않고** 비용만 바꾸면 된다 — 별개 축이니 섞지 않는다.
- **footprint 오브젝트 모델과의 통합** [L] · 장애물이 오브젝트가 되면 통행 층은 그 오브젝트에서 파생된다.
