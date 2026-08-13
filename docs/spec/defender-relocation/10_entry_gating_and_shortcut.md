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

**드래그 지름길 — 표면은 트레이 슬롯(유닛 초상화)이다.**

> ⚠ 처음엔 이걸 **보드 타일**로 읽고 거기에 만들었다가 통째로 버렸다. 사용자가 말한 "선택 셀"은
> 보드 칸이 아니라 **트레이의 그 유닛 칸**이다("배치된 유닛의 **초상화**를 D&D"). 보드 타일 판본은
> 두 번 죽었다 — ⑴ 선택하면 각성 손패가 열리고 `DcInspectController` 의 탭 경로는 손패가 열리면
> 통째로 게이트되므로(`TapGated`) **이 기능의 전제 상태에서 100% 안 돌았고**, ⑵ 게이트 위로 올린
> 뒤에도 보드 칸 위를 다른 UI 가 덮어 press 가 무장되지 않았다. 표면을 잘못 고르면 어디를 고쳐도
> 안 된다.

소진 슬롯의 제스처를 **가른다**:

| | 종전 (board-limit 계약 5) | 지금 |
|---|---|---|
| 탭 | 판 위 그 유닛 선택 | **그대로** |
| 드래그 | 판 위 그 유닛 선택 | **집어들기(이동모드)** |

계약 5 는 재배치에 대가가 없던 시절 것이다 — 그땐 두 제스처의 속마음이 "이 유닛 쓰고 싶다"
하나였다. 이제 드래그에는 "저기로 옮긴다"는 자기 의미가 생겼다. **임계는 UGUI 가 이미
`OnPointerClick`/`OnBeginDrag` 로 갈라주므로 새로 만들지 않는다.**

```csharp
// DefenderDragSlot.OnBeginDrag — 소진 분기에서 GoToDeployedUnit() 대신
TryBeginRelocationFromSlot(eventData.position);   // 실패 시 GoToDeployedUnit() 로 폴백
```

`carriedPress: true` 로 들어가 **누르고 있던 손가락이 그대로 목적지 제스처**가 된다.
진입 실패(코스트 부족·쿨다운·페이즈)는 **종전 동작으로 폴백** — 끌었는데 아무 일도 안 일어나는
것보다 그 유닛을 보여주는 편이 낫다.

**씬 배선은 늘지 않는다.** 슬롯은 런타임 생성이라 인스펙터 배선이 없고 이미 `DcInspectController`
참조를 `Bind` 로 받는다 — 거기에 `Relocation` 접근자 하나만 노출한다.

## 완료 기준

- 컴파일 통과.
- **PlayMode**: 코스트를 바닥내면 `CanBeginMoveModeFor` 가 false 이고 `BeginMoveModeFor` 도 실제로
  막힌다. 코스트를 유닛 코스트만큼 채우면 둘 다 다시 열린다.
- **PlayMode**: 소진 슬롯에 `OnBeginDrag` 를 주면 이동모드로 들어가고 대상은 판 위 그 유닛이다.
  같은 슬롯에 `OnPointerClick` 은 이동모드로 들어가지 **않는다**(두 제스처가 갈렸다).
  슬롯은 UGUI 핸들러라 `PointerEventData` 를 만들어 직접 부르면 손가락 없이 구동된다 —
  보드 타일 판본이 자동 검증 불가였던 것과 대비된다(표면을 옳게 고르면 테스트도 쉬워진다).
- **회귀**: `BoardLimit*` 스위트가 계속 통과해야 한다 — 계약 5 를 가른 쪽이 그 spec 이다.
- 육안: 코스트가 부족하면 이동 버튼이 흐리고 코스트 숫자가 보인다. **코스트가 차면 패널이 떠 있는
  동안 실시간으로 활성화**된다.
- 육안: 초상화를 끌면 슬로모 + 이동모드 진입 + **손가락을 떼지 않은 채** 목적지 스카우트가
  따라온다. 떼면 그 칸에 확정된다.
- 회귀: 초상화를 **짧게 탭**하면 종전대로 판 위 그 유닛으로 데려간다.
- 회귀: 이동 버튼 경유 진입은 종전대로 새 press 를 기다린다(carried-press 아님).
