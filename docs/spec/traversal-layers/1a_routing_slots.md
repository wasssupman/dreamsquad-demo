# unit 1a — 라우팅을 슬롯 stride 로 (바이트 동일)

## 목적

`flow`/`dist` 를 **슬롯별 flat stride** 로 재배치하고, 모든 소비처를 **슬롯 뷰**로 옮긴다. 슬롯은 1개뿐이라 **판이 한 톨도 바뀌지 않는다** — unit 1b 가 슬롯 수만 늘리면 되게 만드는 것이 전부다.

## 왜 싱글턴을 여러 개로 쪼개지 않나

rev 1 초판은 *"마스크별로 N벌 생성. 라이프사이클도 N벌"* 이라고 썼는데 **그대로 구현하면 게임이 죽는다** — `SystemAPI.GetSingleton<T>()` 는 매치가 2개 이상이면 throw 한다.

그리고 실측하면 그럴 이유도 없다. `FlowFieldSingleton` 소비처 **15곳 중 라우팅을 읽는 건 4곳뿐**이고 나머지 11곳은 `tileSize`/`gridSize`/`origin`(= 기하)만 읽는다 — 투사체 3종 · 존/해저드/실드 캐스트 · 픽업 · 보스주기 · 적 FSM · 어그로 · 순찰.

그래서 **기하는 1벌, 라우팅만 N벌**이고 둘은 **한 컴포넌트 안**에 산다. 이러면 그 11곳은 이 spec 내내 손댈 일이 없다.

## 왜 stride 인가

`NativeArray<NativeArray<T>>` 는 **불법**이다(nested native container — Burst 비호환). 그래서 `[slot * CellCount + cell]` 평면 배열이다. 200셀 규모에서 가장 단순하고 Burst 친화적이다.

## 변경 대상

- 수정: `Battle/Effects/FlowFieldSingleton.cs` — `maskValues` + 슬롯 접근자
- 수정: `Bridge/SimFieldInstaller.cs` · `Battle/Effects/FlowFieldRebuildSystem.cs` — 슬롯 뷰에 빌드
- 수정: 라우팅 소비처 5파일 — `MovementSystem` · `AgentSeparationSystem` · `AttackSystem` · `HealthThresholdSystem` · `BattleBridge`
- 수정: `Assets/_Project/Tests/EditMode/CellLayersInstallTests.cs` — 슬롯 계약 5건 추가

## 구현

### 슬롯 뷰가 stride 를 감춘다

```csharp
public NativeArray<float2> FlowSlot(int slot) => flow.GetSubArray(slot * CellCount, CellCount);
public NativeArray<int>    DistSlot(int slot) => dist.GetSubArray(slot * CellCount, CellCount);
```

뷰는 길이가 `CellCount` 라 **소비자가 stride 를 모른다** — 순수 함수(`FlowRecovery.RecoveryDir` · `PathSmoothing.TryStepTarget` · `BlinkMath.TryFindLandingCell`)는 배열을 통째로 받는데, 시그니처를 하나도 바꾸지 않고 뷰만 넘기면 된다. 빌더도 마찬가지로 뷰에 쓴다.

**직접 인덱싱을 남기지 않았다** — 지금은 슬롯 0 이라 `flow[idx]` 가 우연히 맞지만, 슬롯이 늘어나는 순간 조용히 다른 슬롯을 읽는다. 전수 확인해 잔여 0건이다.

### `CellCount` 는 필드가 아니라 파생이다

`gridSize.x * gridSize.y` 로 계산한다. `FlowFieldSingleton` 을 **직접 초기화하는 EditMode 픽스처가 수십 개**라, 새 필드를 요구하면 그것들이 전부 깨진다. `MaskCount` 도 `flow.Length / CellCount` 로 파생하고, 미생성이면 1 로 떨어진다.

### 슬롯 0 이 라우팅하는 마스크 = `Path`

`maskValues[0] = PlacementLayer.Path`. 지금 라우팅은 `walkMask`(= `tiles == Walk`)로 굽는데 `Walk` 는 `Path` 층을 연다(`PlacementLayers.Derive`). 즉 **두 집합이 같다** — unit 1b 가 «(cellLayers & mask) != 0» 기반으로 갈아탈 때 이것이 무변경의 논거이고, 테스트로 고정해뒀다.

## 완료 기준

- [x] compile 에러 0 · EditMode **2013 중 2010 통과 · 실패 0**
- [x] **기존 테스트 기대값 갱신 0건** — 바이트 동일의 증거. unit 1a 직전 실행이 2008/2005/0 이었고, 늘어난 5건은 이 unit 이 추가한 슬롯 계약 테스트다
- [x] 직접 인덱싱 잔여 **0건**(전수 grep)
- [x] 신규 5건: 슬롯 1개·뷰 길이 = `CellCount` / **슬롯 0 = Path 층이고 `walkMask` 와 같은 집합**(1b 논거) / 미등록 마스크 → primary 폴백 / 등록 마스크 → 그 슬롯 / 픽스처가 `flow` 를 안 채워도 `MaskCount` 1

---

**완료 기준 확인**: 2026-08-09 · EditMode 2013 중 2010 통과 · 실패 0 · 행동 변화 0
