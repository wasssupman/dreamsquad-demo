# 2 — GimmickGuideView (배치 페이즈 안내 카드)

## 목적

배치 페이즈 동안 배정된 기믹을 상시 안내 카드(제목+설명)로 표시한다. 좌상단 메뉴버튼을 가리지 않고, 배치 입력을 막지 않는다.

## 변경 대상

- (신규) `Assets/_Project/Scripts/UI/GimmickGuideView.cs` — `Wassup.UI`.
- `Assets/_Project/Scenes/BattleScene.unity` — `GimmickGuideView` GameObject 추가(같은 유닛에서 배선해 독립 검증 가능하게).

## 구현

MonoBehaviour, 자체 캔버스. `PlacementPhaseView`/`GiftPhaseView` 의 절차 캔버스 패턴 미러.

1. **캔버스**: `Awake` 에서 `UiCanvasSetup.Ensure(gameObject, sortingOrder: 8)` (메뉴버튼 1000 아래). 카드 패널 빌드 후 `SetActive(false)`. `UiLayer.Apply`.
2. **카드 레이아웃** (좌상단 메뉴버튼 회피):
   - SafeAreaRoot 자식, 상단 중앙 앵커(0.5,1), pivot(0.5,1), `anchoredPosition ≈ (0, -180)` — 카운트다운 배너(y=-90, h=72) **아래**. 폭 ~640, 높이는 내용에 맞춤(세로 레이아웃).
   - 배경: `UiRoundedSprite.Make(...)` 반투명 다크 플레이트, `raycastTarget=false`.
   - 제목 TMP(displayName, 굵게) + 본문 TMP(description, 자동 줄바꿈). 둘 다 `raycastTarget=false`.
   - "이번 판 특수 룰" 같은 머리말 라벨 1줄(선택, 하드코딩된 UI 문구는 허용 — 데이터 아님).
3. **페이즈 구동**:
   - `OnEnable`: `GameManager.Instance != null` 이면 `PhaseChanged += OnPhaseChanged` **그리고 현재 페이즈로 즉시 동기**(`OnPhaseChanged(gm.CurrentPhase)`) — 재시작/late-enable 로 Placement 전이를 이미 놓쳤어도 카드가 뜬다. Instance 가 null 이면 `Start`/다음 프레임에 재시도(GameManager `DefaultExecutionOrder(-100)` 라 보통 선행하지만 방어).
   - `OnPhaseChanged(GamePhase p)`: `p == Placement && GameManager.Instance?.AssignedGimmick != null` → `Populate()+Show()`, 아니면 `Hide()`.
   - `Populate()`: `AssignedGimmick.displayName`/`.description` 를 라벨에 세팅.
   - `OnDisable`: 구독 해제(중복 구독 방지 — OnEnable/OnDisable 짝).
4. **null 안전**: `AssignedGimmick==null`(기믹 비활성) 이면 Placement 라도 표시 안 함.

## 완료 기준

- [ ] 컴파일 통과, 콘솔 에러 0.
- [ ] 씬에 `GimmickGuideView` 배선 완료(자체 캔버스라 추가 참조 불필요 — GameManager.Instance 로 조회).
- [ ] Play: 배치 페이즈 진입 시 카드 표시(제목=기믹명, 본문=설명). 전투 시작 시 사라짐.
- [ ] 카드가 좌상단 메뉴버튼과 겹치지 않음(스크린샷 확인). 카드 위로 배치 드래그/탭 입력 통과(raycast 비차단).
- [ ] 기믹 비활성(pool 없음/enabled=false) 시 카드 미표시.
- [ ] 재시작(Restart)로 배치 페이즈 재진입 시에도 카드 정상 표시(enable-sync 확인).
