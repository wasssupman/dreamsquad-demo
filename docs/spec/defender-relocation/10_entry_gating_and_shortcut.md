# 10 — 진입 다듬기 (코스트 잠금 · 선택 칸 드래그 지름길)

## 목적

코스트가 없으면 **이동모드에 들어가지 못하게** 하고(들어가서 아무 데도 못 놓는 최악을 막는다),
선택 중에 그 유닛 칸을 **끌면** 버튼을 거치지 않고 바로 이동모드로 넘어가게 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectPanelView.cs` — 이동 버튼 잠금/코스트 표기
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 매 프레임 잠금 피드 + 드래그 handoff
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — `CanBeginMoveModeFor` + carried-press

⚠ **진입구는 플립북이 아니라 선택 패널이다.** 재배치 unit 5 가 만든 좌측 부채꼴 플립북
(`DcActionFlipbookView`)은 `selection-hand-attach` unit 15 에서 **은퇴**했고, 이동 버튼은
`DcInspectPanelView` 안으로 옮겨졌다. 이 spec 의 unit 5 문서는 그 시점 기준이라 stale 하다.

## 구현

**버튼 잠금** — 패널은 `Show(...)` 때 이동 콜백을 한 번 받고 끝난다. 코스트는 슬로모 중에도 계속
차므로 **매 프레임 갱신**이 필요하다. 패널은 유닛도 Bridge 도 모르므로 코스트를 같이 싣는다.

```csharp
public void SetMoveState(bool enabled, int cost);   // 선택 스탯 피드와 같은 프레임 경로에 태운다
```

조건은 **진입 가드를 두 벌로 적지 않는다** — `BeginMoveModeFor` 의 가드를 read-only 판본으로
빼고 양쪽이 그것 하나를 본다.

```csharp
public bool CanBeginMoveModeFor(Entity entity, Vector2Int cell);   // BeginMoveModeFor 가 그대로 탄다
```

코스트 검사는 `CanRelocateDefender(cell, cell)` 재사용으로 끝난다 — unit 9 이후 **제자리 재정비가
항상 유효**하므로 "코스트만 있으면 어딘가엔 놓을 수 있다"가 참이다. 별도 코스트 술어를 만들지 않는다.

잠금은 숨김이 아니라 **흐림**(라벨·배경 알파) — 왜 못 누르는지가 코스트 숫자로 읽혀야 한다.
값이 바뀔 때만 다시 칠하도록 래치를 둔다(매 프레임 TMP 재빌드 방지).

**드래그 지름길** — unit 5 가 홀드 진입을 없애면서 carried-press 개념도 같이 지웠다
(`EnterMoveMode` 는 `_targetPressActive=false` 로 시작). 그 절반을 되살린다.

```csharp
public bool BeginMoveModeFor(Entity entity, Vector2Int cell, bool carriedPress = false, Vector2 pressScreen = default)
```

`carriedPress` 면 `EnterMoveMode` 가 `_targetPressActive=true`, `_targetPressDown=pressScreen` 으로
시작해 **누르고 있던 손가락이 그대로 목적지 드래그로 이어진다**. 기본값 false 라 버튼 진입은 불변.

**입력 소유권** — `DcInspectController` 는 이미 press 를 탭 후보로 물고 있다가 **이동량이
`tapMoveThreshold` 를 넘으면 "탭이 아니다" 로 후보를 취소**한다. handoff 는 정확히 그 지점이다:
취소하는 대신 "그럼 이동 드래그다" 로 넘긴다.

⚠ **새 임계를 만들지 않는다.** 이미 있는 탭 취소 임계를 그대로 쓴다. 다른(더 큰) 임계를 쓰면
두 임계 사이에 **탭도 이동도 아닌 죽은 구간**이 생긴다.

조건은 **누른 지점의 칸 == 선택된 유닛의 칸**(Strict 판정 — 보드 밖에서 시작한 드래그가 가장자리
칸으로 clamp 돼 오인되지 않게). 그 밖의 press 는 종전 그대로다. 순서는
`Close()` → `BeginMoveModeFor(..., carriedPress: true, pressScreen: 누른 지점)` — 선택이 슬로모/줌을
먼저 반납해야 재배치가 자기 lease 를 겹치지 않게 잡는다.

기준점으로 **현재 위치가 아니라 원래 누른 지점**을 넘긴다. 손가락이 이미 임계를 넘었으므로
재배치 쪽에서 다음 프레임 곧바로 드래그로 승격돼 조준 오프셋이 끊기지 않는다.

## 완료 기준

- 컴파일 통과.
- **PlayMode**: 코스트를 바닥내면 `CanBeginMoveModeFor` 가 false 이고 `BeginMoveModeFor` 도 실제로
  막힌다. 코스트를 유닛 코스트만큼 채우면 둘 다 다시 열린다.
- **드래그 지름길은 자동 검증 대상이 아니다** — 실제 포인터 press/이동/릴리즈가 필요해
  `Pointer.current` 없이는 재현되지 않는다(기존 재배치 테스트도 목적지 제스처만 `Step` 으로
  구동하고 진입은 API 직호출로 우회한다). 육안 확인 항목으로 둔다.
- 육안: 코스트가 부족하면 이동 버튼이 흐리고 코스트 숫자가 보인다. **코스트가 차면 패널이 떠 있는
  동안 실시간으로 활성화**된다.
- 육안: 선택 상태에서 그 유닛 칸을 눌러 끌면 → 슬로모 + 이동모드 진입 + **손가락을 떼지 않은 채**
  목적지 스카우트가 따라온다. 떼면 그 칸에 확정된다.
- 회귀: 선택 상태에서 유닛을 **짧게 탭**하면 종전대로 선택이 닫힌다(이동모드 진입 아님).
- 회귀: 이동 버튼 경유 진입은 종전대로 새 press 를 기다린다(carried-press 아님).
