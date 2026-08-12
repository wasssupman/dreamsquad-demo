# 10 — 진입 다듬기 (코스트 잠금 · 선택 칸 드래그 지름길)

## 목적

코스트가 없으면 **이동모드에 들어가지 못하게** 하고(들어가서 아무 데도 못 놓는 최악을 막는다),
선택 중에 그 유닛 칸을 **끌면** 버튼을 거치지 않고 바로 이동모드로 넘어가게 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcActionFlipbookView.cs` — 이동 버튼 활성 갱신 + 코스트 표기
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 소스 칸 press 감시 → handoff
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — `BeginMoveModeFor` carried-press 인자

## 구현

**버튼 잠금** — 플립북은 지금 `Show(...)` 때 `moveEnabled` 를 한 번 받고 끝난다. 코스트는 슬로모
중에도 계속 차므로 **매 프레임 갱신**이 필요하다.

```csharp
// 코스트도 같이 싣는다 — 플립북은 Show(anchor, cam, moveEnabled, onMove) 만 받아서
// 유닛도 Bridge 도 모른다. 숫자를 띄울 소스가 지금은 없다.
public void SetMoveState(bool enabled, int cost);   // Show 이후 매 프레임 컨트롤러가 밀어 넣는다
```

조건 = 기존 `BeginMoveModeFor` 가드(활성 비행 없음 · 진입 쿨다운 경과 · Battle 페이즈 · 유닛
비-busy) **+ 코스트 충분**. 버튼에 그 유닛의 코스트를 숫자로 띄워 **누르기 전에 대가가 보이게**
한다. 잠금은 숨김이 아니라 **흐림** — 왜 못 누르는지가 코스트 숫자로 읽혀야 한다.

**드래그 지름길** — unit 5 가 홀드 진입을 없애면서 carried-press 개념도 같이 지웠다
(`EnterMoveMode` 는 `_targetPressActive=false` 로 시작). 그 절반을 되살린다.

```csharp
public bool BeginMoveModeFor(Entity entity, Vector2Int cell, bool carriedPress = false, Vector2 pressScreen = default)
```

`carriedPress` 면 `EnterMoveMode` 가 `_targetPressActive=true`, `_targetPressDown=pressScreen` 으로
시작해 **누르고 있던 손가락이 그대로 목적지 드래그로 이어진다**. 기본값 false 라 버튼 진입은 불변.

**입력 소유권** — `DcInspectController` 는 선택 중이고, 릴리즈에서 탭을 해석한다(재탭 = 닫기).
여기에 press 감시를 얹는다: **선택된 유닛의 칸**에서 press 가 시작되고 이동량이 드래그 임계를
넘으면 → `Close()` → `BeginMoveModeFor(..., carriedPress: true)`.

⚠ 임계를 **넘기 전 릴리즈**는 기존 재탭 토글(닫기) 그대로다. 임계 하나가 두 결과를 가르므로
`DragController.BoardDragThreshold`(배치와 같은 단일 소스)를 쓴다 — 별도 임계를 만들지 않는다.
소스 칸이 아닌 곳의 press 는 건드리지 않는다(빈 보드 탭 = 닫기 유지).

## 완료 기준

- 컴파일 통과.
- 육안: 코스트가 부족하면 이동 버튼이 흐리고 코스트 숫자가 보인다. **코스트가 차면 패널이 떠 있는
  동안 실시간으로 활성화**된다.
- 육안: 선택 상태에서 그 유닛 칸을 눌러 끌면 → 슬로모 + 이동모드 진입 + **손가락을 떼지 않은 채**
  목적지 스카우트가 따라온다. 떼면 그 칸에 확정된다.
- 회귀: 선택 상태에서 유닛을 **짧게 탭**하면 종전대로 선택이 닫힌다(이동모드 진입 아님).
- 회귀: 플립북 이동 버튼 경유 진입은 종전대로 새 press 를 기다린다(carried-press 아님).
