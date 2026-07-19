# 1 — 모바일 가로형 안내 UI

## 목적

전체 화면 설명창 없이 짧은 문구와 대상 펄스로 다음 행동을 알린다. 화면을 가리거나 별도 `다음` 버튼을
요구하지 않고, 실제 조작은 기존 UI가 그대로 받는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceView.cs` (신규)
- `Assets/_Project/Scripts/UI/Tutorial/TutorialGuidanceStyle.cs` (신규 SO)
- `Assets/_Project/Data/Config/TutorialGuidanceStyle_Default.asset` (신규)

## 구현

`TutorialGuidanceView`는 `UiCanvasSetup.Ensure`로 런타임 UI를 만들고 아래 기능만 제공한다.

- Safe Area 상단 중앙의 한 줄/두 줄 말풍선
- 전달받은 `RectTransform`을 따라가는 비레이캐스트 펄스 링과 손가락 표시
- 월드 위치를 카메라로 투영한 출발/목표 지점 지속 마커와 라벨
- 우상단 `건너뛰기` 버튼과 `SkipRequested` 이벤트
- `ShowMessage` / `FocusUi` / `ShowWorldMarker` / `ClearWorldMarkers` / `Hide`의 작은 concrete API
- 핵심 안내에서만 보이는 `건너뛰기` 가시성 토글

Canvas sortingOrder는 10으로 고정해 Placement/Gimmick/손패 HUD 위, 메뉴 팝업 아래에 둔다. 한글 TMP
폰트는 style asset에서 필수로 받으며 미할당이면 경고 후 TMP 기본 폰트로 폴백한다.

말풍선·펄스·손가락은 `unscaledDeltaTime`을 사용한다. Skip 버튼만 raycast를 받고 나머지 Graphic은 전부
`raycastTarget=false`다. UI 대상은 `RectTransform`의 화면 중심을 SafeAreaRoot 로컬 좌표로 변환해 따라가고,
월드 대상은 `Camera.WorldToScreenPoint` 후 `RectTransformUtility.ScreenPointToLocalPointInRectangle`으로
변환한다. 카메라 뒤/화면 밖 대상은 링을 숨긴다. 대상이 파괴되거나 비활성화되면 링만 숨기고 문구는 유지한다.
16:9와 20:9에서 말풍선은 상단 카운트다운과 대상 위를 피하도록 safe rect 안에서 clamp한다.

한글 폰트·색·크기·펄스 주기·목표 beat 시간(전체 4~6초, 기본 5초)·각성 노출 시간(3~4초)은
`TutorialGuidanceStyle`에서 가져온다. 신규 Canvas 싱글톤이나 범용
튜토리얼 프레임워크는 만들지 않는다.

## 완료 기준

- [ ] compile clean, 콘솔 오류 0.
- [ ] 1920×1080과 2400×1080에서 말풍선·Skip이 Safe Area 안에 있다.
- [ ] 펄스 링이 슬롯 이동/트레이 리사이즈를 따라가며 터치를 가로채지 않는다.
- [ ] target null/파괴/비활성에서 예외 없이 폴백한다.
- [ ] 16:9/20:9에서 UI·월드 펄스 중심이 실제 대상과 일치하고 화면 밖 대상은 숨는다.
- [ ] 한글 문구가 누락 글리프 없이 표시되고 안내 Canvas가 HUD 위·메뉴 팝업 아래다.
- [ ] 반복 Show/Hide와 OnDisable에서 코루틴·동적 오브젝트가 남지 않는다.
- [ ] 외부 이미지 없이 절차적 플레이트/링 폴백으로 완전 동작한다.
