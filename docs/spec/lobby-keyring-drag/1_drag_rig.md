# 1 · 드래그 세션 + 키링 리그 + 캐릭터 접점

## 목적

캐릭터를 드래그하면 키링 모드로 전환: 고리(손가락)·줄·캐릭터(스프링 스윙)를
캔버스에서 구동한다. 캐릭터 본체 로직(로밍/리액션)은 드래그 동안 정지한다.
이 unit 에서 EndDrag 는 **임시로 즉시 바닥 스냅** — 낙하/바운스는 unit 2.

## 변경 대상

- 신설: `Assets/_Project/Scripts/UI/Outgame/LobbyKeyringDrag.cs` (`Wassup.UI`)
- 신설: `ILobbyKeyringTarget` (같은 파일 또는 `Assets/_Project/Scripts/UI/Outgame/ILobbyKeyringTarget.cs`)
- 수정: `HelloLobbyRoamer.cs`, `WorldLobbyCharacter.cs` — 인터페이스 구현

## 구현

**LobbyKeyringDrag** — `IBeginDragHandler/IDragHandler/IEndDragHandler`,
캐릭터 GameObject 에 부착. `[SerializeField] LobbyKeyringSettings settings`.

- 상태: `Idle / Dragging` (unit 2 에서 `Falling` 추가). `IsBusy` public.
- Awake: 바닥 y = `anchoredPosition.y` 캡처, `GetComponent<ILobbyKeyringTarget>()`.
- OnBeginDrag: `SuspendForKeyring()` 호출 → 리그 생성 → Dragging. 다른 포인터가
  이미 세션 중이면 무시 (pointerId 기록).
- 좌표: 손가락 screen → 부모 RectTransform 로컬
  (`RectTransformUtility.ScreenPointToLocalPointInRectangle`, overlay 라 camera null).
- Update(=Tick(dt)): 머리 목표 = 손가락 - (0, ropeLength). 스프링+감쇠+maxSpeed 로
  머리 위치 지연 추종(README 계약 3). 기울임 θ = 줄(머리→고리) 방향에서
  atan2 유도, maxAngle 클램프. 최종 `anchoredPosition = 머리 + Rotate(머리→피벗
  오프셋, θ)`, `localRotation = Euler(0,0,θ)` (계약 4). 머리 로컬 오프셋은 rect
  height·pivot 에서 계산.
- 리그: 런타임 생성 UI Image 2개 — 줄(단색 스프라이트, 고리→머리 중점에 위치·회전,
  `sizeDelta=(cordWidth, 거리)`), 고리(절차적 annulus 텍스처 → Sprite, static 캐시
  1회 생성). 부모는 캐릭터와 같은 컨테이너, sibling index 는 캐릭터 바로 앞(줄이
  캐릭터 뒤에 깔림). `raycastTarget=false`.
- OnEndDrag(임시): 리그 파괴 → x 클램프(landingMinX/MaxX) → 바닥 y 스냅 →
  회전 0 → `ResumeFromKeyring()` → Idle.
- OnDisable: 세션 중이면 위와 동일한 정리 (CleanupSession 패턴, 멱등).

**ILobbyKeyringTarget** — `void SuspendForKeyring()`, `void ResumeFromKeyring()`.

- **HelloLobbyRoamer**: Suspend = 걷기 중단(`IsWalking` false, `_walking` false),
  진행 중 리액션 즉시 종료(`_reactionRemaining=0` + `LobbyReactionLock.Release`),
  suspended 플래그로 Tick 로직 정지. Resume = 플래그 해제 + idle 타이머 재추첨
  (새 위치에서 자연 재개).
- **WorldLobbyCharacter**: Suspend = 리액션 즉시 종료 + 락 해제 + Tick 정지.
  Resume = 플래그 해제.

## 완료 기준

- compile 클린, 콘솔 에러 0.
- (unit 3 와이어링 후 확인 항목이지만 코드 완결 조건): 드래그 중 고리가 손가락에,
  캐릭터가 줄 끝에 매달려 스윙. 놓으면 즉시 바닥 스냅 + 행동 재개.
- 드래그 중 hello 로밍/리액션이 개입하지 않는다.

확인 2026-07-07 — compile 클린. 커밋 `2643383b`. (시각 확인은 unit 3 에서 사용자 통과.)
