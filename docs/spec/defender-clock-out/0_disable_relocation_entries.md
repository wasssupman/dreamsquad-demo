# 0 — 이동 진입구 차단

## 목적

`defender-relocation` 의 진입구를 끈다. 기능 코드는 손대지 않는다 — 팀 판단이 다시 뒤집히면
상수 한 줄로 돌아와야 한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 상수 + `Relocation` 접근자
- `Assets/_Project/Tests/PlayMode/RelocationMoveModeTest.cs` — 슬롯 제스처 단정 반전

> **rev 2 (리뷰 반영)**: 초안은 여기서 패널 이동 버튼도 껐지만, **unit 2 가 같은 파일 같은 줄을
> 즉시 덮어쓴다**(액션 슬롯 중립화 + 퇴근 콜백). 곧 지워질 2줄로 커밋과 육안 검증 항목을 만드는
> churn 이라 걷어냈다. 게다가 초안의 완료 기준은 **중간 상태**(액션 버튼이 아예 없는 패널)를
> 눈으로 확인하라고 요구했는데, 그 상태는 다음 커밋에서 사라진다. 패널은 unit 2 가 통째로 소유한다.

## 구현

```csharp
// defender-clock-out unit 0 — 이동/재배치는 퇴근으로 대체됐다(팀 리뷰 2026-08-13).
// 기능 코드·설정·테스트는 남기고 **진입구만** 끈다. true 로 되돌리면 그대로 부활한다.
//
// ⚠ [SerializeField] 로 노출하지 않는다 — 인스펙터에서 켜고 씬을 저장하면 값이 조용히
// 리포에 박힌다(이 프로젝트에서 반복된 사고). 진실원은 코드 하나다.
// const 가 아니라 static readonly 인 이유: const 면 분기가 상수 폴딩돼 "도달 불가 코드"
// 경고가 뜬다.
private static readonly bool RelocationEnabled = false;

// 접근자 하나가 트레이 초상화 드래그를 막는다.
public DefenderRelocationController Relocation => RelocationEnabled ? relocationController : null;
```

⚠ **`DefenderDragSlot` 은 건드리지 않는다.** 폴백이 이미 그 파일 안에 있다 —
`TryBeginRelocationFromSlot` 이 `_inspect.Relocation == null` 이면 스스로 `GoToDeployedUnit()` 으로
빠진다. 접근자만 막으면 board-limit 계약 5(소진 셀의 모든 제스처 = 판 위 그 유닛 선택)로
**저절로 되돌아간다**. 슬롯에 두 번째 상수를 심으면 진실원이 둘이 된다.

**남기는 것**: `DefenderRelocationController` · `RelocationSettings` · `BattleBridge.Relocation.cs`
전부 · EditMode `RelocationCheckTests` · PlayMode 재배치 스위트. 이동모드는 **API 로는 계속 살아
있고** 씬 배선도 그대로다 — 끊긴 것은 사람이 만지는 경로뿐이다.

**테스트 반전 1건.** `ExhaustedSlot_Drag_PicksUpUnit_Tap_GoesToUnit` 은 relocation unit 10 이 만든
"드래그 = 집어들기" 단정이다. 지금은 **드래그도 탭도 판 위 그 유닛 선택**이 정답이므로 뒤집는다.
지우지 않는 이유: 이 단정이 곧 **"배선이 실제로 끊겼다"는 유일한 증거**이고, 되살릴 때 다시
뒤집을 지점을 표시해 둔다.

나머지 재배치 PlayMode 는 `BeginMoveModeFor` 를 직접 부르므로 **영향 없다** — 그게 이 설계의
요점이다(진입구와 기능이 갈려 있어 진입구만 끌 수 있다).

## 완료 기준

- 컴파일 통과.
- **PlayMode**: 소진 슬롯에 `OnBeginDrag` 를 줘도 이동모드로 들어가지 **않고** 판 위 그 유닛
  선택으로 간다. 같은 슬롯 `OnPointerClick` 도 동일(두 제스처가 다시 합쳐졌다).
- **회귀**: 재배치 스위트 나머지 + `BoardLimit*` 전부 통과. `RelocationCheckTests` 8/8 불변.
- 육안: 트레이의 소진된 초상화를 끌어도 슬로모가 걸리지 않고 그 유닛으로 카메라만 간다.

> 이 unit 은 이동 버튼을 건드리지 않으므로 **패널에는 아직 "이동" 이 떠 있다**(누르면 종전대로
> 동작). unit 2 가 그 슬롯을 퇴근으로 교체한다.

> **자동 검증 2026-08-13** — 컴파일 통과(에러 0).
> PlayMode `RelocationMoveModeTest` **4/4**(뒤집은 `ExhaustedSlot_BothGestures_GoToUnit_NotMoveMode` 포함) ·
> `RelocationSmokeTest`+`RelocationPlacementSessionTest`+`BoardLimitPlacementTest`+`BoardLimitTrayStateTest` **9/9** ·
> EditMode `RelocationCheckTests` **8/8**. 육안(트레이 초상화 드래그) 확인 대기.
>
> 검증 중 겪은 것: 다른 세션이 매치 타이머 HUD 를 편집 중이라 `ScoreHudView.cs` 18건 +
> `BattleBridge.cs` 1건이 빨간 상태였다. **그쪽 파일을 건드리지 않고** 초록이 될 때까지 기다렸다.
> 또 `run_tests` 를 `test_names` 로 필터하면 `total: 0` 인 채 `Passed` 가 나온다(거짓 통과) —
> **`group_names`(클래스명)로 돌려야** 실제로 잡힌다.
