# 5 — BackdropMounter board center 에 origin 반영

## 목적

배경/엣지 프롭(BackdropMounter)이 계산하는 board center 가 월드 원점 기준이라, MapView 를 옮기면 배경 데코가 보드를 따라오지 않는다. board center 에 origin 을 더해 정렬한다. (시각 정렬용 — 게임플레이엔 영향 없음.)

## 변경 대상

- `Assets/_Project/Scripts/Presentation/Backdrop/BackdropMounter.cs` (66~67)

## 구현

BackdropMounter 가 board origin 을 얻는 경로 확인 필요:
- BattleBridge 가 BackdropMounter 를 호출/초기화한다면 origin 을 인자로 전달(권장).
- 또는 BackdropMounter 가 mapView/bridge 참조를 가지면 `bridge.BoardOrigin` 읽기.
- **새로 mapView.transform 을 직접 읽는 경로를 만들지 말 것**(계약: origin 단일 소스는 BattleBridge).

```csharp
Vector3 origin     = /* 전달받은 board origin */;
var boardCenter    = origin + new Vector3(gs.x * tileSize * 0.5f, 0f, gs.y * tileSize * 0.5f);
var boardHalfWorld = new Vector2(gs.x * tileSize * 0.5f, gs.y * tileSize * 0.5f); // half-extent 는 origin 무관
```

`BackdropAnchorTable.Resolve(...)` 가 boardCenter 를 받으므로 그 하위는 자동 정렬.

배경이 MapView 의 자식이라면 이미 local 로 따라오므로 **이 작업이 불필요**할 수 있다 — 구현 직전 BackdropMounter 가 만든 GameObject 의 부모(root)가 MapView 자식인지 월드 루트인지 확인하고, 자식이면 본 작업 skip(README 후속 후보로 강등).

## 완료 기준

- [ ] (필요한 경우) compile green.
- [ ] MapView 이동 상태 Play: 엣지 프롭/배경이 옮겨진 보드 둘레에 정렬.
- [ ] 배경이 이미 MapView 자식이라 자동 정렬되면, 그 사실을 handoff 에 기록하고 작업 생략.

## 주의

- 게임플레이 비영향 작업. 시간이 부족하면 우선순위 최하位 — 핵심(0~4)이 끝나면 검증 질문은 이미 충족된다.
