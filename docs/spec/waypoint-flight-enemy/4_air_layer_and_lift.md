# unit 4 — `Air` 통행층 + 비행 비주얼 (비행 = 층이지 분기가 아니다)

## 목적

비행의 규칙 정체성(**길막 무시**)을 `Air` 통행층 하나로 세운다. 이동 코드는 한 벌로 남는다 — 같은 경로탐색이 «모든 칸이 열린 마스크» 위를 돌 뿐이다(계약 7). 뜬 느낌은 lift 비주얼.

**unit 3 이 라이브로 돈 것을 확인한 뒤 착수한다** — 아트가 잘못된 동작을 예쁘게 포장하지 않게.

## 변경 대상

- `Assets/_Project/Scripts/Data/PlacementLayer.cs` — `Air` 비트 + `CellBits` + `Derive` 전 타일 개방
- `Assets/_Project/Scripts/Battle/Effects/FlowFieldRebuildSystem.cs` + `SimFieldInstaller.cs` — **Air 슬롯은 장애물 오버레이 스킵**
- `Assets/_Project/Scripts/Battle/Effects/AggroStateSystem.cs` — 추격 필드 마스크의 층 인지(`:141~146` Temp walkMask 재계산)
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `flightLift`(view 높이, 0 = 지상) knob
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 뷰 sync 에서 `SetFlightHeight(flightLift)`
- 비행 적 SO 1종(`traversalLayers = Air`) + 이름 확정(README D4)

## 구현

### `Air` 비트 — `Derive` 단일 정의로만 연다

```csharp
Air = 1 << 2,   // CellBits |= Air
// Derive: Place → Ground|Air · Walk → Path|Air · default(Deco/Env) → Air
```

- **모든 타일 종류가 Air 를 연다** — «벽»이라는 개념이 Air 층에는 없다. 데코 칸 위 웨이포인트가 합법이 되고(계약 4는 이미 «그 경로를 쓰는 적의 층 기준»), unit 0 의 «지상 층 닫힘» 경고가 정확히 이 경우를 가리킨다.
- `default` 가 0 → Air 로 바뀌므로 **«cellLayers == 0 = 불가침» 을 전제한 소비자가 있는지 grep 으로 전수**한다(traversal-layers 계약 6 — 데이터 재사용 전 writer/reader 전수). 배치는 `placeMask`(저작) 기준이라 무관 — 방어유닛은 Ground 만 갖는다.

### 장애물 오버레이 — Air 슬롯만 스킵

`FlowFieldRebuildSystem` 슬롯 루프에서 `MaskAt(m) & Air != 0` 이면 장애물 없이 `FillWalkMask` → **차단 해저드가 비행을 못 막는다**(정체성). 충돌 쪽은 공짜다 — traversal-layers unit 5 가 충돌 `NavGrid` 를 층별로 이미 조립하므로 Air 층엔 벽이 없다.

### 어그로 추격 — 층 인지 확인

`AggroStateSystem` 이 어그로 획득 시 **지상 walkMask** 로 추격 필드를 굽는다. Air 적은 자기 층 마스크로 굽지 않으면 **유인당한 비행이 벽을 돌아 걸어간다.** 유닛 층을 읽어 마스크를 선택한다(Effects 가 Movement 소유 층 컴포넌트를 RO 로 읽는 것은 합법).

### lift — 재사용만

`flightLift > 0` 이면 뷰 sync 가 `SpineUnitView.SetFlightHeight` 호출 → `UnitLiftVisual.Resolve` 가 확대·그림자 축소·페이드를 파생(공짜). ⚠ 오버헤드 체력 UI 가 lift 를 따라가는지 확인 — 지상 기준이면 몸과 분리돼 보인다.

## 완료 기준 — 라이브 카운터 2개 + 회귀

- [ ] 컴파일 에러 0 · EditMode 전량 그린 · `Derive` 테스트 갱신(전 타일 Air 포함)
- [ ] **카운터 ⑴ 차단을 실제로 넘는가**: 경로를 차단 해저드로 완전히 막은 판에서 — 비행 적이 차단 셀 위를 통과한 프레임 > 0 · **같은 판 지상 적은 0**(벽을 때리거나 우회)
- [ ] **카운터 ⑵ 여전히 맞는가**(음성 대조군): 근접·투사체·캐스터 계열 대표 각 1종이 비행 적을 실제 타격(`IncomingDamage` 유입 > 0). Air 층을 열면서 **실수로 «안 맞는 적»** 이 되지 않았는가를 세는 축 — 이 spec 의 양방향 검증 실체
- [ ] 유인당한 비행이 가디언까지 **직선(벽 무시)** 으로 이동(어그로 추격 층 인지 확인)
- [ ] lift 적용 시 그림자·체력바가 몸을 따라감(육안)
- [ ] 지상 적 회귀 0 — 기존 맵 웨이브 EditMode·Play 스모크 무변화
