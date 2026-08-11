# unit 3 — 켜기 (부착·주입·소비를 한 커밋에)

## 목적

적이 실제로 웨이포인트를 따라 걷는다. 컴포넌트 + SO 저작 + 스폰 주입 + `MovementSystem` 목적지 교체를 **한 커밋**으로 — 쪼개면 «부착됐는데 안 움직인다» 가 되고, 그 증상은 순수 함수 테스트가 전부 초록인 채로 발생한다(traversal-layers unit 5 사고, 계약 5).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/WaypointFollow.cs` — 신규 컴포넌트(Movement 소유)
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `waypointPathIndex`(int, **-1 = 미사용**)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnUnit` 부착 분기 + 스폰 시 검증
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — goal 분기의 목적지 슬롯 교체
- 검증용 적 SO 1종(기존 `Enemy_Basic` 복제 저작 — 신규 아키타입 아님)
- `Assets/_Project/Tests/EditMode/` + 라이브 계측

## 구현

### 컴포넌트 — 인덱스만, 경로 사본 없음

```csharp
public struct WaypointFollow : IComponentData { public int pathIndex; public int index; }
```

경로 데이터는 필드 슬롯(unit 1)에 있다 — rev 1 의 `DynamicBuffer` 복사는 불요가 됐다.
인덱스는 배열·순수 함수와 같은 `int`를 쓴다. 수십 개 엔티티의 몇 바이트를 줄이려고
별도 255개 제한·검증·캐스팅을 만드는 조기 압축은 하지 않는다.

### 스폰 주입 + **조용한 폴백의 경고 지점** (계약 9)

`SpawnUnit` 에서 `waypointPathIndex >= 0` 이면: 맵에 그 경로가 있는지 검증 → 있으면 부착, **없으면 경고 로그 + 미부착(골 직행)**. 런타임 `SlotFor` 폴백은 침묵 안전망으로 두고, **사람이 보는 경고는 여기 한 곳**이다 — 매 프레임 아니라 스폰 1회라 스팸이 없다.

### `MovementSystem` — 목적지 슬롯만 갈아끼운다

goal 분기(4번째 방향 출처)에서:

```
활성 웨이포인트 있음 → slot = SlotFor(웨이포인트 셀, 유닛 층)
                      reachable = dist[currentCell] != MaxValue
                      WaypointProgress.Step(...) → 전진/done 을 WaypointFollow 에 기록
없음/done            → slot = SlotFor(골 센티널, 유닛 층)   ← 현행
```

- **방향 계산·평활화·충돌·분리는 한 줄도 안 바뀐다** — 읽는 슬롯만 다르다.
- 어그로 추격(Chasing)·사냥(hunting)·순찰 분기는 **웨이포인트보다 우선**(기존 분기 순서 그대로). 어그로가 풀리면 웨이포인트로 복귀 — 인덱스가 컴포넌트에 남아 있으므로 공짜.
- `WaypointFollow` 쓰기는 Movement 소유 시스템 안 — 맥락 경계 위반 없음.
- **골 판정(`IsGoalCell`→`PastGoalTag`)은 건드리지 않는다.** 경로가 골 셀을 지나면 유출되는 것이 맞고, 그건 unit 0 의 저작 경고가 잡는다.

### 확인 지점 (수정 아님 — 확인만)

- `FrontmostTargeting`·블링크·스폰 예고·`AttackSystem:1460` 이 **PrimarySlot(골)** 을 계속 읽는지 — unit 1 이 슬롯 0 을 고정했으므로 무수정이어야 정상. 바꾸고 싶어지면 계약 2 위반이다.

## 완료 기준

- [x] 컴파일 에러 0 · EditMode 전량 그린(경로 미저작 적 = 현행 무변경)
- [x] **라이브 계측 — 통과 순서를 센다**(계약 6): 검증용 적이 저작된 지점들을 **저작 순서대로** 통과(각 웨이포인트 셀 진입 프레임 기록, 순서 역전 0) → 마지막 지점 후 골 도달
- [x] 음성 대조군: 같은 판의 경로 미저작 적은 현행 최단 경로 유지(웨이포인트 셀 진입이 우연이 아닌 한 0)
- [x] 어그로 왕복: 유인 → 가디언으로, 해제 → **남은 웨이포인트부터** 재개(인덱스 보존 확인)
- [x] 존재하지 않는 경로 인덱스 저작 → 스폰 시 경고 1회 + 골 직행(무한 스팸·정지 없음)

완료 확인: 2026-08-11 — 사용자 D1 MovementLab 플레이 확인. EditMode 2,140건 중
실패 0(2,137 통과·기존 Ignore 3), PlayMode 라이브 계측 1/1 통과(경로 0·1 스폰,
활성 가이드, 순서 통과, 골 도달). 코드 리뷰에서 `byte` 조기 압축을 `int`로 되돌리고
죽은 1축 슬롯 호환 API를 제거한 뒤 재검증했다. 이 문서와 동일 커밋.
