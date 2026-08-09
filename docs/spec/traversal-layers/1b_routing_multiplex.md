# unit 1b — 마스크 집합 → 슬롯 N개 (행동 변화 0)

## 목적

**«셀 층 ∩ 슬롯 마스크» 로 슬롯마다 라우팅을 굽는다.**

unit 1a 가 슬롯 자리를 만들었고, 여기서 그 자리를 **실제로 채우는 규칙**이 정의된다. 마스크 집합은 **인자로 받는다** — 지금 호출자가 아무것도 안 넘겨 슬롯 1개(`Path`)로 떨어지므로 판은 그대로다.

## ⚠ 착수 시 발견한 순서 정정

rev 2 초판은 1b 에 «로스터 수집»을 넣고 2a 에 «유닛 축»을 뒀는데 **의존성이 역전**돼 있었다 — 로스터에서 마스크를 모으려면 **유닛이 먼저 마스크를 가져야** 한다. 지금 순서대로면 1b 가 자기 입력을 못 구한다.

그래서 1b 는 «집합을 **받아** 슬롯을 굽는다»로 좁혔다(합성 집합으로 테스트 가능하고, 행동 변화도 0 이다). 수집은 축이 생기는 **2a 로 옮겼다**.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Effects/TraversalSlots.cs` — 교집합 규칙(순수)
- 수정: `Battle/Effects/FlowFieldSingleton.cs` — `MaskAt(slot)`
- 수정: `Bridge/SimFieldInstaller.cs` — `slotMasks` 인자 + 슬롯별 빌드
- 수정: `Battle/Effects/FlowFieldRebuildSystem.cs` — 슬롯별 재빌드
- 수정: `Assets/_Project/Tests/EditMode/CellLayersInstallTests.cs` — 3건 추가

## 구현

### 정의식은 한 곳에만 있다

```csharp
걸을 수 있다  ⇔  (셀 층 & 슬롯 마스크) != 0
```

`TraversalSlots.FillWalkMask` 가 이 spec 의 정의식 그 자체다. **장애물은 포함하지 않는다** — 그건 지형이 아니라 별개 층이고 `NavGrid` 가 합성한다(계약 4).

### 재빌드는 **모든** 슬롯을 돈다

장애물이 바뀌면 **모든 통행 층의 경로가 함께 바뀐다.** 한 슬롯만 갱신하면 다른 층 유닛이 이미 사라진 장애물을 계속 피해 돌게 된다. 슬롯마다 «층 마스크 → `NavGrid`(장애물 합성) → BFS» 를 한 번씩 돌린다.

여기서도 벽 술어를 인라인 재구현하지 않는다 — `NavGrid` 를 슬롯의 층 마스크로 조립해서 `MaterializeWalkMask` 를 시킨다(계약 4 유지).

### 픽스처 폴백

`cellLayers` 가 없으면(= `FlowFieldSingleton` 을 직접 초기화하는 EditMode 픽스처) 기존 `walkMask` 경로로 떨어진다. 픽스처 수십 개를 고치지 않기 위한 것이고, 프로덕션은 설치자가 항상 채운다.

## 행동 변화 0 의 논거

슬롯이 `DefaultMask = Path` 하나일 때, `(cellLayers & Path) != 0` 은 `tiles == Walk` 와 **같은 집합**이다 — `PlacementLayers.Derive` 가 `Walk → Path` 이기 때문이다. 이 등식을 테스트로 셀 단위 고정했다(`SingleDefaultSlot_MatchesWalkMaskRouting`).

## 완료 기준

- [x] compile 에러 0 · EditMode **2016 중 2013 통과 · 실패 0**
- [x] **기존 테스트 기대값 갱신 0건** — 늘어난 3건은 이 unit 이 추가한 것
- [x] 신규 3건:
  ① **슬롯 2개면 라우팅이 2벌**이고 각자 자기 층으로 굽는다 — 골이 `Walk` 라 `Path` 슬롯에선 `dist 0`, `Ground` 슬롯에선 도달 불가. **빌드 순서가 Ground → Path 이므로, 앨리어싱이었다면 나중 값이 앞을 덮어 둘 다 0 이 된다** → 다르게 나온 것이 비-앨리어싱의 증거
  ② 슬롯 1개(기본)의 walk 집합이 `walkMask` 와 **셀 단위로 동일**(무변경 축)
  ③ 교집합 규칙 진리표 — 층 하나만 여는 칸 / 둘 다 여는 칸 / 아무것도 안 여는 칸

  ⚠ ①에서 «다른 셀도 라우팅이 다르다»를 덧붙이려다 빨갛게 났다. 2x2 픽스처에서 `(0,0)`은 두 슬롯 모두 도달 불가다(골과 대각인데 **코너컷 방지**로 막힌다). 앞 두 줄이 이미 같은 것을 증명하고 있었다.

---

**완료 기준 확인**: 2026-08-09 · EditMode 2016 중 2013 통과 · 실패 0 · 행동 변화 0
