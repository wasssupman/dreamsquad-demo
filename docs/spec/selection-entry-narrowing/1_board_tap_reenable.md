# 1 — 보드 유닛 탭을 다시 켠다

## 목적

unit 0 이 끈 셋 중 **보드 유닛 탭(선택 → 유닛 상세)** 하나만 되켠다. 판 위 유닛을 찍으면
다시 상세와 손패가 뜬다. 항아리 탭과 선택 줌은 **끈 채로 둔다**.

사용자 요청 2026-08-20 — 트레이 셀 하나로 좁혀 놓으니 판 위 유닛을 만지는 길이 사라져,
"저 유닛이 지금 어떤 상태인가" 를 보는 동작이 화면에서 멀어진다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — `BoardTapSelectEnabled`
- `Assets/_Project/Scripts/UI/Tutorial/FirstRunTutorialController.cs` — `TryResolveHost` 주석의 전제

## 구현

`BoardTapSelectEnabled = true`. 게이트가 서 있던 두 지점(`HandleTap`, `OnBoardTapped`)의
분기는 **그대로 둔다** — unit 0 의 계약 1(스위치로만 켜고 끈다)이 그대로 유효해야 다음에
또 내릴 수 있다. `static readonly` 라 `true` 여도 `if (!BoardTapSelectEnabled)` 가 상수
폴딩되지 않아 컴파일 경고가 없다.

되켠 뒤에도 **해제 어휘는 사라지지 않는다**: 빈 보드 탭(`TryPick` 실패)과 선택 유닛
재탭(`entity == _selected`)이 종전대로 선택·손패를 걷는다. 항아리가 히트를 놓고 있는 것도
그대로라(unit 0 A) 손패가 열린 동안 항아리 위 탭도 여전히 dismiss 로 닿는다.

트레이 소진 셀 경로(`DefenderDragSlot.GoToDeployedUnit` → `SelectDeployed`)는 이 게이트를
지나지 않는 별도 입구라 **함께 산다**. 입구가 둘이 되는 것이 이 변경의 결과다.

### 튜토리얼 전제 정정

`FirstRunTutorialController.TryResolveHost` 는 부착 대상을 «트레이 셀로 선택할 수 있는 배치
유닛» 으로 판정한다. 그 근거로 적혀 있던 "보드 탭은 유닛을 고르지 않는다" 는 이제 거짓이다.
판정 자체는 **바꾸지 않는다** — 4.1 이 뚫는 구멍이 트레이 셀 rect 하나라서 소진 조건이 여전히
필요하다. 주석의 근거만 그 사실로 고쳐 쓴다. 플레이어가 보드 탭으로 선택해도 완료
이벤트(`DreamcatcherHandView.SelectionTargetSet`)는 같으므로 4.1 은 어느 쪽으로도 통과한다.

## 완료 기준

- [x] compile 클린 (2026-08-20 — `dotnet build Wassup.Runtime.csproj` 오류 0)
- [x] Play: 판 위 유닛을 탭하면 그 유닛이 선택되고 상세·손패가 뜬다
- [x] Play: 다른 유닛을 탭하면 선택이 그쪽으로 전환된다(손패 유지)
- [x] Play: 선택 유닛 재탭 · 빈 보드 탭은 종전대로 선택과 손패를 걷는다
- [x] Play: 하단 트레이 소진 셀 탭도 여전히 그 유닛을 선택한다
- [x] Play: 항아리는 여전히 눌리지 않고, 선택해도 카메라는 당겨지지 않는다

> 사용자 Play 확인 2026-08-20
