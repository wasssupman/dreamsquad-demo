# unit 4 — `Air` 통행층 + 비행 비주얼 + 방어 타겟층

## 목적

비행의 규칙 정체성(**길막 무시 + 일반 방어 공격 회피**)을 `Air` 통행층 하나로 세운다. 이동 코드는 한 벌로 남고, 방어 공격도 별도 고도 enum 없이 같은 비트와의 교집합을 본다(계약 7). 뜬 느낌은 lift 비주얼.

**unit 3 이 라이브로 돈 것을 확인한 뒤 착수한다** — 아트가 잘못된 동작을 예쁘게 포장하지 않게.

## 변경 대상

- `Assets/_Project/Scripts/Data/PlacementLayer.cs` — `Air` 비트 + `CellBits` + `Derive` 전 타일 개방
- `Assets/_Project/Scripts/Battle/Movement/MovementCellTrim.cs` — 모든 소비자가 공유하는 조립점에서 **Air 는 장애물 오버레이 스킵**
- `Assets/_Project/Scripts/Battle/Movement/AgentSeparationSystem.cs` — 분리 변위도 유닛 통행층 NavGrid 로 해결
- `Assets/_Project/Scripts/Battle/Effects/AggroStateSystem.cs` — 추격 필드 마스크의 층 인지(`:141~146` Temp walkMask 재계산)
- `Assets/_Project/Scripts/Data/ObstaclePlacer.cs` — 구 placeMask 의 Air 비트 부재를 수동 배치 저작으로 오인하지 않음
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `flightLift`(view 높이, 0 = 지상) knob
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `attackTargetLayers`(기존 미저작 = Path)
- `Assets/_Project/Scripts/Battle/Combat/{AttackState,AttackSystem}.cs` — 타겟층 스냅샷 + 후보 게이트
- `Assets/_Project/Scripts/Battle/Combat/Projectile/` — 요청→상태 스냅샷 + 실제 피해/재조준 게이트
- `Assets/_Project/Scripts/Battle/Effects/{HazardCastState,HazardCastSystem,HazardEffect,ZoneApplySystem}.cs` — 캐스터 선정과 장판 적용 게이트
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 뷰 sync 에서 `SetFlightHeight(flightLift)`
- `Enemy_Skimmer` SO(`traversalLayers = Air`) + `EnemyCatalog`/`Deck_WaypointLab` 등재
- `Defender_AntiAir` SO(`attackTargetLayers = Path | Air`, `targetFactions = EnemyUnit`) + `DefenderCatalog` 등재

## 구현

### `Air` 비트 — `Derive` 단일 정의로만 연다

```csharp
Air = 1 << 2,   // CellBits |= Air
// Derive: Place → Ground|Air · Walk → Path|Air · default(Deco/Env) → Air
```

- **모든 타일 종류가 Air 를 연다** — «벽»이라는 개념이 Air 층에는 없다. 데코 칸 위 웨이포인트가 합법이 되고(계약 4는 이미 «그 경로를 쓰는 적의 층 기준»), unit 0 의 «지상 층 닫힘» 경고가 정확히 이 경우를 가리킨다.
- `default` 가 0 → Air 로 바뀌므로 **«cellLayers == 0 = 불가침» 을 전제한 소비자가 있는지 grep 으로 전수**한다(traversal-layers 계약 6 — 데이터 재사용 전 writer/reader 전수). 배치는 `placeMask`(저작) 기준이라 무관 — 방어유닛은 Ground 만 갖는다.

### 장애물 오버레이 — Air 슬롯만 스킵

`FlowFieldRebuildSystem`·`MovementSystem`·어그로·스폰 가이드가 함께 쓰는 `MovementCellTrim.FillWalkMask` 에서 `Air` 마스크면 장애물 합성을 생략한다. 소비처별 분기를 복제하지 않는다. 분리 시스템도 유닛별 층 NavGrid 로 해결해, 실제 이동 뒤 Path 벽으로 다시 밀려나는 예외를 막는다.

### 어그로 추격 — 층 인지 확인

`AggroStateSystem` 이 어그로 획득 시 **지상 walkMask** 로 추격 필드를 굽는다. Air 적은 자기 층 마스크로 굽지 않으면 **유인당한 비행이 벽을 돌아 걸어간다.** 유닛 층을 읽어 마스크를 선택한다(Effects 가 Movement 소유 층 컴포넌트를 RO 로 읽는 것은 합법).

### lift — 재사용만

`flightLift > 0` 이면 뷰 sync 가 `SpineUnitView.SetFlightHeight` 호출 → `UnitLiftVisual.Resolve` 가 확대·그림자 축소·페이드를 파생(공짜). ⚠ 오버헤드 체력 UI 가 lift 를 따라가는지 확인 — 지상 기준이면 몸과 분리돼 보인다.

### 방어 타겟층 — 기존 비트 재사용

- `DefenderUnitData.attackTargetLayers`의 이니셜라이저와 `None` 폴백은 모두 `Path`다. 기존 방어 에셋을 25개 손마이그레이션하지 않아도 전부 지상 전용으로 바뀐다.
- `AttackState`가 Combat 런타임 스냅샷을 소유한다. Combat/Effects는 Movement 소유 `PathFollowState.traversalLayers`를 RO로만 읽는다. 새 태그·시스템·이벤트·인터페이스는 만들지 않는다.
- `0` 마스크는 레거시 무필터다. 적의 공격, 플레이어 스킬, 구조물처럼 통행층이 없는 대상은 기존 동작을 유지한다.
- 타겟 선정만 막지 않는다. 지상 투사체가 splash/bounce/PathHit/TileAoe로 Air에 번지거나 지상 장판이 Air에 적용되는 경로도 같은 마스크로 닫는다.
- 배치 즉발 공격/CC도 같은 SO 마스크를 읽는다. 지원형 아군 효과는 `targetAllies`/진영 규칙이 우선하며 고도 필터 대상이 아니다.
- 신규 `대공사수`는 **Path | Air**를 모두 공격하는 양성 대조다. 사용자 결정에 따라 발당 피해 7·공격 주기 0.2초·타격 지연 0으로, 낮은 공격력 대신 매우 빠른 연사가 정체성이다. 별도 Air 우선순위나 추가 피해는 이번 검증 범위가 아니다.
- `Enemy_Skimmer`의 공격은 단일 타겟(`attackTargetCount = 1`)·0.2초 빠른 주기다. 피해 10은 유지하며, 반경 스플래시나 다중 타격을 추가하지 않는다.

## 완료 기준 — 라이브 카운터 2개 + 회귀

- [x] 컴파일 에러 0 · EditMode 전량 그린 · `Derive` 테스트 갱신(전 타일 Air 포함)
- [x] **카운터 ⑴ 차단을 실제로 넘는가**: 경로를 차단 해저드로 완전히 막은 판에서 — 비행 적이 차단 셀 위를 통과한 프레임 > 0 · **같은 판 지상 적은 0**(벽을 때리거나 우회)
- [x] **카운터 ⑵ 타겟층이 실제 피해까지 닫히는가**: 같은 사거리에서 기존 근접·투사체·캐스터 대표는 Skimmer에 피해/CC 0이면서 Path 적에는 피해 > 0. 신규 대공사수는 Skimmer와 Path 적 모두 피해 > 0. splash/bounce/PathHit/TileAoe·장판의 우연한 Air 피격도 0
- [x] 유인당한 비행이 가디언까지 **직선(벽 무시)** 으로 이동(어그로 추격 층 인지 확인)
- [x] lift 적용 시 그림자·체력바가 몸을 따라감(육안)
- [x] 지상 적 회귀 0 — 기존 맵 웨이브 EditMode·Play 스모크 무변화

자동 검증 2026-08-11: EditMode 2,150건(실패 0, 기존 Ignore 3) · MovementLab PlayMode 1/1. Play 테스트가 같은 웨이브의 Air/지상 적에 각자 차단 waypoint를 두고 Air 통과 프레임 > 0 / 지상 진입 0을 계측했으며, 실제 뷰 루트가 지상 대조군보다 `flightLift`만큼 상승하는 것도 확인했다.

타겟층 최초 자동 검증 2026-08-11: EditMode 14/14(공격→투사체 요청 스냅샷, 직격/스플래시/재조준/PathHit/TileAoe, 캐스터 선정·장판 적용) · WaypointLab PlayMode 2/2(실제 BattleScene 런타임 베이크 + 기존 경로/가이드/차단/lift 회귀). 전체 EditMode 2,163건 중 1건은 공유 작업트리의 `boss-mamemo` 미완 `GrantShield × None × Standard` 분류 실패이며 본 변경 대상 테스트는 전부 통과했다. 이후 대공사수를 `Path | Air`로 바꾼 계약은 관련 EditMode 2/2(카탈로그 비트·두 층 타겟 선정)와 런타임 베이크 PlayMode 1/1로 재검증했다. 카운터 ⑵ 체크는 자동 검증과 별개로 실제 웨이브 플레이 체감 확인 뒤 닫는다.

리뷰 마감 검증 2026-08-11: `EnemyAiStateSystem`도 타겟층을 미러하도록 수정해 Path 전용 순찰병이 Air 적만 보고 멈추는 회귀를 차단했다. 관련 EditMode 18/18 · WaypointLab PlayMode 2/2 · 콘솔 에러 0.

체감 조정 2026-08-11: 사용자 확인에서 `flightLift = 0.7`은 비행 판독성이 부족해 Skimmer SO만 `1.4`로 올렸고, 그림자·체력바를 포함한 최종 Play 체감을 재확인했다.

사용자 완료 확인 2026-08-11 · 구현 commit `258c96ec`.
