# traversal-layers — 통행 층 비트필드 (배치 층의 대칭)

상태: **rev 1 · 2026-08-09 · 사용자 승인 대기** (rev 0 = 2026-08-07 적 전용 초안)

> **rev 1 변경**: 범위를 «적 이동» → **«이동하는 모든 주체»** 로 확장. 아래 §2 가 근거고, 결과적으로 **클래스 분기가 0** 이라 오히려 단순해진다. rev 0 이 전제한 두 가지도 정정했다(§6).

## 1. 상위 목표

배치를 `placeMask × 유닛 층` 교집합으로 바꿨듯(placement-mask unit 4), **통행도 같은 모양으로** 만든다.

```
통행 가능  ⇔  (셀 통행 층 & 유닛 통행 층) != 0
```

이걸로 «물만 이동» · «땅만 이동» · «둘 다 이동» 이 **데이터**가 된다. 지금은 통행 가능 = `tiles == Walk` 하나로 고정이라 저작으로 만들 수 없다.

## 2. 왜 적/방어유닛을 구분할 필요가 없나

**이미 둘 다 같은 이동 파이프라인을 탄다.** `PathFollowState` 를 받는 곳은 `BattleBridge` 에 두 군데이고 — 적 스폰(`:6173`)과 순찰 방어유닛 스폰(`:7546`) — 그 뒤로는 `MovementSystem` 이 둘을 같은 루프에서 돈다.

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
| 층 비트필드 + 파생 폴백 + Sanitize | `PlacementLayer` / `PlacementLayers.Derive·Sanitize` — **그대로 복제** |
| 유닛 SO 의 층 필드 + None 폴백 | `DefenderUnitData.placementLayers` / `EffectivePlacementLayers` |
| 셀 마스크 직렬화·왕복·페인터 브러시 | `MapDocument.placeMask` · `MapPainterWindow._placeMask` / `_maskBrushLayer` |
| **제한된 마스크로 필드를 굽고 특정 유닛만 따르기** | **순찰**(`PatrolAreaMath:79`, 거점 박스 마스크) · **어그로 추격**(`AggroChaseMath:49`, 적별 필드를 `AggroChaseCell` 버퍼에) |

마지막 줄이 중요하다 — **«유닛마다 다른 필드를 따른다»는 이미 프로덕션에서 돌고 있다.** 골 필드만 1벌 공유였을 뿐이다.

### 3-3. **새로 만드는 것**

| 무엇 | 크기 | 내용 |
|---|---|---|
| `TraversalLayer` enum + `TraversalLayers` 헬퍼 | **S** | `PlacementLayer` 복제. `Ground` / `Water` / … + `Derive(MapTileType)` + `Sanitize` |
| 셀 `traverseMask` 직렬화 | **S** | `MapDocument` · `GeneratedMap` · `MapDocumentBuilder` — `placeMask` 와 같은 형태(길이 불일치 = 파생 폴백) |
| **필드 집합** 싱글턴 | **M** | 마스크 값 → 필드 인덱스. 로스터에서 결정론적 수집(오름차순). 값 1종이면 지금과 동일한 1벌 |

### 3-4. **손대는 것** (파일별)

| 파일 | 무엇을 |
|---|---|
| `Bridge/SimFieldInstaller.cs` | 유일한 `walkMask` 생산 지점(`:58`)이 **마스크별로 N벌** 생성. 라이프사이클도 N벌 |
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

**총 파일 ~15개 · 신규 3개.** 대부분이 «인자를 하나 더 넘긴다» 수준이고, 실제 설계 부담은 **필드 집합 싱글턴의 라이프사이클** 하나에 몰려 있다.

## 4. 작업 단위

| 파일 | 작업 구분 | 목적 | 행동 변화 |
|---|---|---|---|
| [0_traverse_mask_data.md](0_traverse_mask_data.md) | 데이터 | `TraversalLayer` + 셀 `traverseMask` + 유닛 SO 필드 · 직렬화·왕복·파생 폴백 | **0** (파생값이 현행과 동일) |
| [1_flow_field_per_mask.md](1_flow_field_per_mask.md) | 길찾기 | 마스크별 필드 집합 + 로스터 수집 + 싱글턴/라이프사이클 | 0 (마스크 1종이면 동일) |
| [2_movement_consumers.md](2_movement_consumers.md) | 소비처 | 위 §3-4 의 소비처가 «그 유닛의 필드»를 보게 | 0 |
| **3_teleport_and_targeting.md** *(신규)* | 파급 | 블링크 착지 · frontmost 순서 · 스폰/골 도달성 검증 | §5 결정에 따름 |
| [3→4_painter_and_verify.md](3_painter_and_verify.md) | 저작·검증 | 페인터 통행층 브러시 + Play 검증(층 다른 유닛 2종) | — |

**순서 근거**: 0 은 행동 변화 0 인 데이터 레이어라 안전하게 먼저 들어간다. 1 이 필드를 N벌로 만들되 **마스크 1종이면 현행과 바이트 동일**이라 이 시점까지 판이 안 바뀐다. 2 에서 소비처가 갈아타고, 3 에서 파급을 정리한 뒤, 4 에서 처음으로 «물 적»을 실제로 저작한다.

## 5. 미결 결정 (착수 전 확정 필요)

**D1 — 마스크가 다른 유닛끼리 «앞선 적» 순서를 어떻게 정하나? → 기본값 채택** (2026-08-09)

`FrontmostTargeting` 은 `dist` 오름차순으로 "골에 가장 가까운 적"을 고르는데, 필드가 여러 벌이면 **물 적의 dist 와 육지 적의 dist 는 다른 그래프 위의 값**이라 직접 비교가 의미를 잃는다. 물길이 돌아가면 골 코앞의 물 적(dist 180)이 5칸 뒤 육지 적(dist 50)보다 뒤로 밀린다.

**⚠ 범위 정정**: 이 규칙은 **일반 타겟팅이 아니다.** `AttackSystem:470` 이 요구하는 조건은 ⑴ 방어유닛 ⑵ `FrontmostAttackLock` 보유 ⑶ 살아있는 `FrontmostTarget` 슬롯 — 즉 **드림캐쳐 카드 「끝을 보는 눈」이 걸린 동안에만** 발동한다. 평소 타겟팅은 이 경로를 타지 않는다. 착수를 막을 결정이 아니다.

**채택: `dist / maxFiniteDist(그 마스크의 필드)` 정규화.** 0~1 잔여 진행률이라 그래프가 달라도 비교되고, 엔티티별 상태가 필요 없으며 필드 재빌드 때 함께 갱신된다. **마스크가 1종이면 단조 변환이라 순서가 지금과 완전히 동일하다.** unit 3 소관.

**D2 — 통행 불가 지형에 넉백/견인으로 밀려 들어가면?** 지금은 벽이면 `AgentCollision` 이 막는다. 물 적이 육지로 밀리는 건 같은 규칙으로 막히지만, **자기 층이 아닌 칸에 갇히는 경우**(맵 편집 실수·층 변경)의 탈출 규칙이 필요하다. 후보: `FlowRecovery` 를 그 마스크 dist 로 돌리면 자동 해결 — 기본값으로 채택 제안.

**D3 — 필드 개수 상한?** rev 0 계약 7 은 «3종 초과 시 경고». 유지할지, 하드 상한으로 바꿀지.

## 6. rev 0 에서 정정된 전제

- **정정 ①(쉬워짐)**: rev 0 은 *"런타임 벽 판정도 필드의 `flow==0` 에서 파생된다(`MovementCellTrim.IsWallCell`)"* 를 전제로 계약 4를 세웠다. **`IsWallCell` 은 존재하지 않는다** — continuous-agent-movement unit 2 에서 은퇴하고 벽 술어가 `NavGrid`(정적 마스크 + 동적 장애물)로 이관돼 **flow 와 분리**됐다. 벽과 경로가 이미 따로 놀므로 이 작업이 rev 0 가정보다 단순하다. 계약 4를 «`NavGrid` 가 유일한 벽 술어이며, 마스크별로 조립된다»로 대체한다.
- **정정 ②(늘어남)**: rev 0 의 소비처 목록에 `AgentSeparationSystem`·`BlinkMath` 호출부·`FrontmostTargeting` 호출부·`MapConnectivity` 가 빠져 있었다. `AgentSeparationSystem` 의 flow 소비는 rev 0 작성 **이틀 뒤**(unit 13)에 생겼다.

## 7. Feature-wide 계약

1. **축을 겸직시키지 않는다** — `PlacementLayer`("설 수 있는 칸")와 `TraversalLayer`("지날 수 있는 칸")는 별도 enum. 도로는 적이 지나가지만 지면 유닛은 못 서는 칸이고 배치지면은 그 반대라, 두 축이 우연히 반대일 뿐 같은 값이 아니다.
2. **파생 기본값 = 현행 재현** — `Walk → Ground`, `Place/Deco/Env → 없음`. 유닛 SO 미지정(`None`) = `Ground` 폴백. **아무것도 저작하지 않으면 지금 판과 완전히 동일**(옵트인).
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
| **방어유닛** | **락이 없다.** `FocusTarget` 경로는 `behaviorLookup`(`EnemyBehavior`) 게이트라 **적 전용**이다. 방어유닛은 매 프레임 nearest/priority 재선정 | **불일치 — 이게 가장 큰 갭** |

**traversal-layers 와의 접점**: 방어유닛 타겟이 고착이면 frontmost 순서(D1)는 **획득 시점에만** 영향을 준다. 지금처럼 매 프레임 재선정이면 매 프레임 영향을 준다. 즉 **철학을 구현하면 D1 의 중요도가 더 떨어진다.**

**판정: 별도 spec 후보.** 이 spec 에 넣지 않는다 — 통행 층과 인과가 없고(층이 없어도 갭은 존재한다), 타겟 락은 그 자체로 밸런스 변경이라 한 커밋에 묶으면 원인 분리가 안 된다(제약 9 · 「버그 픽스 ≠ 기능」).

### 9-2. 방어유닛 이동 — **부품은 이미 클래스 비종속, 막는 건 제품 결정**

> 사용자 질문: 현재 구현된 소환물의 이동방식을 모든 방어 유닛에게 적용 가능한가.

현재 상태:
- **소환 순찰병**은 `PatrolAnchor`(거점) + `PatrolFieldSystem`(거점 박스 ∩ walk 마스크로 필드) + `PatrolStep`(방향) 으로 움직이고, `MovementSystem` 이 그 dir 을 **적과 같은 루프**에서 소비한다.
- **일반 방어유닛**은 배치 타일 위에 고정. 예외는 `DefenderRelocationController`(이동모드 → 목적지 지정 → 비행/재전개)인데 이건 **플레이어가 지시하는 텔레포트성 재배치**이지 지속 이동이 아니다.

**기술적으로는 가능하다.** 필요한 건 `PatrolAnchor` + `PathFollowState` 부착뿐이고, 두 컴포넌트 모두 유닛 종류를 묻지 않는다. `MovementSystem` 의 patrol 분기도 `patrolStepLookup.HasComponent` 로만 판별한다 — **클래스 분기가 이미 0 이다.**

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
