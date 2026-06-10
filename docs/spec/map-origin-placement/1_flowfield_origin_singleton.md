# 1 — FlowFieldSingleton.origin + BattleBridge 캡처

## 목적

board origin 을 ECS 시뮬레이션 전체로 전파하는 단일 채널을 만든다. 모든 Burst 시스템이 이미 `FlowFieldSingleton` 을 읽으므로, 여기에 `float3 origin` 필드를 추가하는 것이 신규 싱글턴/NativeQueue 없이 전파하는 가장 가벼운 방법이다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs` (Effects 맥락 소유)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (origin 캡처 + 싱글턴 주입)

## 구현

`FlowFieldSingleton` 에 필드 추가:

```csharp
public int2   gridSize;
public int2   goalCell;
public float  tileSize;
public float3 origin;   // ← 추가. board 월드 원점 = MapView.transform.position. 기본 zero.
public int    version;
```

`BattleBridge`:

- 클래스 필드 `private float3 _boardOrigin;` 추가 (단일 소스 of truth).
- init 시 origin 캡처 — `mapView.Initialize(...)` 직전/직후, `mapView != null` 일 때:
  ```csharp
  _boardOrigin = mapView != null
      ? (float3)mapView.transform.position
      : float3.zero;
  ```
  (mapView 가 null 인 헤드리스/테스트 경로는 zero → 기존 동작 유지.)
- `FlowFieldSingleton` 을 생성하는 곳(BattleBridge.cs:432 의 `new FlowFieldSingleton { ... }`)에 `origin = _boardOrigin` 추가.
- FlowField 재빌드(BattleBridge.cs:609~611) 경로에서도 origin 이 보존되도록 확인. tileSize/gridSize 와 동일하게 취급.

## 완료 기준

- [ ] compile green.
- [ ] Play 시 `_boardOrigin` 이 씬의 MapView.transform.position 과 일치(Debug.Log 1회 또는 인스펙터 확인).
- [ ] FlowField 재빌드 후에도 `origin` 값이 유지됨.
- [ ] 이 단계까지는 시스템이 아직 origin 을 **사용하지 않으므로** 동작 변화 없음(origin=0 과 동일). 회귀 없음 확인.

> ✅ 확인 2026-06-10 — 컴파일 green, 콘솔 에러 0, 전체 EditMode 309개 중 307 passed / 0 failed / 2 skipped(기존 Ignored). `BuildFlowField` 에 `Debug.Log(boardOrigin=...)` 추가 — Play 로그 일치 검증은 작업 4 후 통합 Play 테스트와 함께 관측. 커밋: a852904

## 주의

- origin 캡처는 **반드시 BattleBridge 안에서만**. 다른 MonoBehaviour 가 mapView.transform 을 읽어 별도 origin 을 만들지 않는다(계약 위반).
- 캡처 시점이 MapView 가 씬에 배치/이동 완료된 이후여야 한다. init 순서상 `mapView.Initialize` 와 같은 프레임이면 안전.
