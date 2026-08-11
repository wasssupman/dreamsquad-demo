# unit 2 — 순서 관리 순수 함수 (웨이포인트는 이동을 모른다)

## 목적

«지금 몇 번째 지점인가 · 도달했나 · 다음은 어디인가» 를 plain 값 순수 함수로 세운다. **이동 방식(직선/경로탐색)은 이 함수에 들어오지 않는다** — 계약 1 의 본체. 소비자 0 — 행동 변화 0.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/WaypointProgress.cs` — 신규(순수 static)
- `Assets/_Project/Tests/EditMode/WaypointProgressTests.cs` — 신규

## 구현

```csharp
// 반환: 이번 프레임의 목적지. done = 경로 소진(이후 골 슬롯).
public static void Step(
    int2 currentCell,
    int2 waypointCell,     // 현재 인덱스의 웨이포인트
    bool reachable,        // 호출자가 판정: 그 슬롯의 dist[currentCell] != MaxValue
    int index, int count,
    out int nextIndex, out bool advanced, out bool done)
```

규칙 세 줄:

1. **도달 = 셀 일치**(README D2). 반경 판정은 안 만든다 — 필드가 이미 그 칸으로 데려간다.
2. **도달 불가면 건너뛴다.** 웨이포인트 셀이 장애물로 막히면(`dist == MaxValue`) 그 지점에서 영원히 맴돌게 된다. `reachable == false` → `advanced = true` 로 다음 지점으로. 판정 자체(dist 조회)는 호출자 몫 — 순수 함수는 ECS 를 모른다.
3. `index >= count` → `done`. 이후 호출자는 골 슬롯을 쓴다(현행 이동과 동일).

**넣지 않는 것**: 반경·보간·이동 방향. 이동 방향은 `MovementSystem` 이 슬롯 필드에서 읽는다(unit 3). 이 함수에 «어떻게 가나»가 스며드는 순간 rev 1 의 축 융합이 재발한다.

## 완료 기준

- [ ] 컴파일 에러 0 · EditMode 전량 그린
- [ ] 테스트: 미도달 → 유지 / 셀 일치 → 전진 / 마지막 도달 → done / **도달 불가 → 건너뜀** / count 0 → 즉시 done
- [ ] `WaypointProgress.cs` 에 Unity 참조 0 (`Unity.Mathematics` 만 — battle-sim-extraction 이식 어휘 준수)
