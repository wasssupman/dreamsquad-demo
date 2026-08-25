# unit 4 — 팬 제스처 (손가락이 판을 민다)

## 목적

비armed 상태의 보드 드래그를 **팬**으로 연다. 그리고 그 과정에서 **이미 갈려 있던 임계값과
소유권**을 정리한다 — 새 포인터 소비자를 만들면 안 되기 때문이다.

## 지금 상태 (이게 이 unit 의 난이도다)

「비armed 보드 제스처」는 **존재하지 않는다.** `DefenderDragPlacementController.UpdateBoardGesture`
는 armed 일 때만 돈다. 비armed 탭(인스펙트)은 **다른 컴포넌트**인 `DcInspectController` 가
자기 포인터 폴링으로 처리한다.

포인터를 각자 폴링하는 소비자가 이미 다섯이다 — `DcInspectController`(order −50),
`PlacementInput`(−50), `DefenderDragPlacementController`(0), `DefenderRelocationController`(0),
`DirectionAimController`(0). **중재자가 없고** 각자 전역 플래그로 눈치를 본다.

그리고 임계가 갈려 있다: 보드 드래그 승격 **16px**, 인스펙트 탭 취소 **24px**.
**그 사이 8px 밴드에서 둘이 동시에 산다** — 팬이 시작됐는데 탭도 살아 있어, 릴리즈 시점의 화면
좌표로 유닛을 고르면 **손가락 밑에 있던 유닛이 아니라 판이 밀린 뒤 그 자리에 온 다른 유닛**의
패널이 열린다. 게다가 16px 는 약 6dp 로 플랫폼 터치 슬롭(8dp)보다 작아 **탭이 일상적으로 팬으로
승격**한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs`
- `Assets/_Project/Data/Config/DragSwaySettings.asset` (임계 통일 — 정본)
- `Assets/_Project/Data/Camera/CameraDirectionConfig.asset` (`placementFocusLead` 0.15 → 0)
- `Assets/_Project/Scenes/BattleScene.unity` (`DcInspectController` 의 씬 직렬화 임계 정리)
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` (press 우선순위 참여)

## 구현

**팬은 `DefenderDragPlacementController` 가 소유한다.** 이미 보드 제스처 상태기계와
**터치 id 로 판정하는 UI 가드**를 가진 유일한 컴포넌트다(`PlacementInput` 의 가드는 no-arg 라
터치에서 UI 를 못 거르고, `DcInspectController` 는 실행 순서 때문에 그 API 를 의도적으로 안 쓴다).
새 컨트롤러를 만들면 `placement-armed-board-drag` README 「보드 press 소유권」(*"두 소비자가 같은
press 를 노리는 race 재생산 금지"*, 원 계약은 `unit-dreamcatcher-inspect` 계약 11)을 그대로 재현한다.

- `UpdateBoardGesture` 의 진입 조건에서 armed 요구를 떼고, **armed 여부로 의미를 가른다**:
  armed → 사거리 스카우트(기존 그대로) / 비armed → 팬.
- `DcInspectController` 의 기존 양보 seam(현재 `IsDragging || HasArmedUnit`)에 **`IsPanning` 을
  추가**한다. 팬이 승격하면 열려 있던 선택 패널은 **닫는다**(팬 = 「이 유닛 얘기는 끝났다」).
- **임계를 하나로 통일**하고 플랫폼 터치 슬롭(8dp) 이상으로 올린다. 두 값이 남으면 반드시 밴드가 생긴다.
  ⚠ 인스펙트 임계는 `DcInspectController` 의 **`[SerializeField]`(씬 직렬화)** 다 — C# 기본값만 고치면
  씬 인스턴스는 옛 값을 유지한다(컴파일도 테스트도 통과하는 조용한 실패). **정본을
  `DragSwaySettings.boardDragThreshold` 하나로 옮기고** `DcInspectController` 가 그 값을 읽게 한 뒤,
  씬에 남은 필드를 제거한다. 씬 값을 손으로 맞추는 것은 최후 수단이다 — 다음 사람이 다시 갈라놓는다.
  ⚠ px↔dp 환산은 기기 밀도 가정에 의존한다. **실기 1대에서 재고 그 기기를 이 문서에 적는다.**
- 팬 출력은 `CameraDirector.SetPanDelta` 로만 나간다(unit 1). 컨트롤러는 bounds 를 모른다.
- **보드 press 우선순위를 표로 못박는다** — 소비자가 다섯인데 중재자가 없어서 각자 전역 플래그로
  눈치를 보는 것이 현재 상태다. 순서: **armed 드래그 > 이동모드 목적지 지정 > 카드 조준 > 팬 >
  인스펙트 탭.** 특히 `DefenderRelocationController` 는 `Pointer.current` 를 직접 폴링하고 가드가
  `IsDragging || HasArmedUnit` 뿐이라, **이동모드 중 비armed 프레스가 팬과 목적지 지정 양쪽으로
  갈 수 있다** — 그 컨트롤러도 이 표에 참여시킨다(새 핸들러가 아니라 양보 seam 확장으로).
- ⚠ **배치 페이즈에는 화면 전면 raycast 블로커가 있다**(`PlacementPhaseView` 의 `InputBlocker`).
  UI 가드를 통과해야 하는 팬은 그 페이즈에서 전량 차단된다. 라이브는 `placementPhaseEnabled: 0` 이라
  오늘 안 보이지만, 페이즈를 켜는 순간 조용히 재현된다 — 블로커를 **「배치 입력만 차단」으로 좁힌다.**
- **게인은 초점면 고정**(월드/스크린px 상수)으로 둔다. 「지면이 손가락에 붙는」 매핑은 직관적이지만
  pitch 때문에 화면 위쪽에서 판이 1.6배 빨라진다. 관성이 붙으면 미끄러짐은 거의 안 읽힌다.
- 클램프 도달을 **하드 스톱으로 두지 않는다** — 플릭 중 벽에 부딪히면 프레임 드랍으로 오독된다.
  짧은 러버밴드 + 복귀.

**카드 조준의 화면 밖 도달은 이 unit 이 풀지 않는다.** 손패 카드 드래그 중에는 팬이 발동하지
않는다(위 표). 조준은 손가락이 카드에 묶인 드래그(수명 = press~release)라 **팬을 겸할 수 없다** —
대신 unit 5 의 오버뷰 홀드가 조준 중에도 열려 있어(2점 터치) 그 경로로 화면 밖을 겨눈다.
unit 5 완료 기준이 이 흐름을 검증한다.

**`placementFocusLead` 처분.** 배치 상태의 드래그 포커스 채널은 이미 **화면을 미는 팬**이다
(`FocusDelta` 가 아니라 `PanDelta` 로 해석되고 저작값 0.15). 새 팬과 이중 적용되면 실효 감도가
두 배가 되고, 손을 뗀 뒤 이 채널만 페이드아웃하며 화면이 되돌아온다.
**base 팬이 이 채널의 존재 이유를 흡수하므로 0 으로 저작해 은퇴시킨다**(코드 삭제가 아니라 데이터로
끄는 것 — camera-direction 계약 「채널을 끄는 것은 그 채널의 데이터다」).

## 완료 기준

- **이 커밋에서 전투 가시 칸수를 실제 값으로 저작한다**(unit 2 의 폴백값 → 목표 칸수). 이 커밋부터
  판이 화면보다 커지고, 같은 커밋의 팬이 도달성을 회복한다.
- **`DragPlacementReachTest` 를 팬 포함 도달성으로 개정한다** — 「상단 행에 바로 닿는다」 →
  「팬을 끝까지 민 상태에서 상단 행에 닿는다」(삭제가 아니라 계약 교체). 초록 확인.
- **남쪽 클램프 극단에서 판의 모든 배치 가능 행에 손가락이 닿는다** — 노치/무노치 기기 각 1대.
  실패하면 `hudInsetBottom` 저작값을 조정한다(README 스파이크 2 와 같은 축).
- **20px 미세 팬에서 선택 패널이 0회 열린다.** 프로토콜: 유닛 위에서 ① 의도한 탭 ② 10px 드리프트 탭
  ③ 20px 미세 팬 ④ 큰 팬을 각 20회, 실기 터치로 시행한다.
- armed 드래그의 사거리 스카우트가 **오늘과 동일**하다(팬으로 새지 않는다).
- 이동모드 중 비armed 프레스가 팬으로 새지 않는다(우선순위 표 검증).
- UI(트레이·손패·독) 위에서 시작한 프레스가 팬으로 새지 않는다 — **실기 터치 확인 필수**
  (에디터 마우스로는 안 잡히는 계열의 버그다).
- 팬 중 배치 하이라이트가 손가락 셀과 어긋나지 않는다 — **실행 순서를 정리해서** 해결한다
  (셀 판정을 카메라 갱신 뒤로 옮기거나, 그 프레임의 팬 델타를 반영한 카메라로 판정).
  관측 단언: 최대 속도 플릭 20회에서 **하이라이트와 손가락 셀 불일치 0회**.
