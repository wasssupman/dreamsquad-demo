# 2 — 히스토리 패널 + 로비 버튼

## 목적

로비 "히스토리" 버튼 → 내 (진행 중) 토너먼트 목록 페이지. `GetUnclaimedEntries` 로 목록을 받아 행으로 그리고, 행 탭 시 상세 팝업(unit 3)을 연다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/TournamentHistoryPanel.cs` (신규)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` (버튼 라우팅 + onClose 배선)
- 씬: `OutgameScene` 로비에 "히스토리" 버튼 + HistoryPanel GameObject (UnityMCP 부재 → 수동 배선 잔여)

## 구현

- `TournamentHistoryPanel`(MonoBehaviour): `ResultScreen` 선례대로 **자체 캔버스 절차적 빌드**(`UiCanvasSetup.Ensure`, sorting 2500). 헤더(제목+뒤로) + ScrollRect 목록 + 상태 라벨.
  - `OnEnable` 에서 `LoadEntries()`. `OnDisable`/재진입은 epoch 증가로 in-flight 응답 폐기.
  - 게스트(`IdToken==""`) → API 스킵, "로그인이 필요합니다". baseUrl 없음/실패/빈 목록 각각 상태 문구.
  - 각 행 = Button(이름/날짜/순위/점수), 클릭 → `EnsurePopup().Show(entry.tournamentEntryId)`. 상세 팝업은 자식으로 lazy 생성.
  - `event Action onClose` — 뒤로 버튼이 발화. 컨트롤러가 구독해 `ClosePanels`(메뉴 복원).
- `OutgameMenuController`: `[SerializeField] GameObject historyPanel` + `OnOpenHistory() => RaiseExclusive(historyPanel)` + `ClosePanels` 에 historyPanel 포함 + Awake 에서 `TournamentHistoryPanel.onClose += OnClosePanels`(OnDestroy 해제).

## 완료 기준

- [x] compile: Unity 콘솔 무에러 (2026-07-20).
- [x] 씬 배선 (`9717b460`, 2026-07-20): HistoryButton.onClick → `OnOpenHistory`(persistent 확인), HistoryPanel(+`TournamentHistoryPanel`) → `historyPanel` 할당, 초기 비활성. OutgameScene 저장.
- [x] Play 스모크: 패널 강제 활성 진입 시 `OnEnable→BuildCanvas→LoadEntries` 무예외(Canvas/타이틀/스크롤/상태 빌드), 미로그인=게스트 브랜치, `ClosePanels` 로 정상 비활성.
- [x] Play e2e (2026-07-20, 사용자 스샷): 로그인 상태에서 히스토리 버튼 클릭 → 실서버 `unclaimed` 3건 로드·렌더 확인. 버튼 화면밖/라벨/패널높이 버그 수정(`9964aa93`,`54f86223`).
