# 13 — 소비자 재배선 (82파일 → 세션 계약)

## 목적

스왑 커밋이 "구현체 교체 1곳"이 되려면 **소비자가 이미 세션만 보고 있어야** 한다. 구 sim 위에서
재배선을 끝내 회귀를 여기서 소진한다(sim 회귀와 재배선 회귀를 분리 검증 — 설계 정본 M1-3).

## 변경 대상

실측 소비자 82파일. 성격별 3묶음으로 나눠 **묶음당 독립 커밋**한다:

- **A. 폴링 → 읽기 모델** (`NextWaveDock` · `SpawnAlertPresenter` · `CostDisplay` ·
  `DefenderSelector` · `ScoreHudView` 등): `bridge.X` 직독 → `session.ReadModel.X`.
  ⚠ 청사진 ① §6 실측 — `TryGetSpawnAlertForecast` 는 **캐시 배열 참조**를 넘기므로 읽기 모델은
  복사본/read-only span 을 준다. `TryGetUnitViewAnchor` 를 생존 프로브로 겸용하던 곳은
  **명시 `IsAlive(simId)`** 로 교체.
- **B. push → 이벤트 구독** (`ScoreHudView.OnEnemyKilled` · `SetLeakStatus` · `BossWarningView` ·
  `ResultScreen.ShowVictory/ShowDefeat` · `ScoreTallyView.Play`): Bridge 가 뷰 메서드를 호출하던
  방향을 **뷰가 세션 이벤트를 구독**하는 방향으로 뒤집는다. 승패는 `MatchEnded` 이벤트.
- **C. 입력 → 커맨드** (`DefenderDragPlacementController` · `DefenderRelocationController` ·
  `DirectionAimController` · `DreamcatcherCardDragSlot` · `NextWaveDock` 버튼 ·
  `PlacementPhaseView.FinishPlacement` · `MenuPopup` pause): 직접 호출 → `session.SendCommand`.
  preflight(`CanPlaceDefenderAt` 등)은 **커맨드 검증과 같은 함수를 공유**해 이중 계산을 없앤다.

## 구현

- 순서는 A → B → C. A 는 읽기만이라 가장 안전하고, C 는 게임 상태를 바꾸므로 마지막.
- 각 묶음 후 **PlayMode 스모크**로 그 화면이 살아 있음을 확인한다(A: HUD 수치 갱신, B: 승패·집계
  연출, C: 배치·카드·웨이브 호출).
- **드림캐쳐 손패는 C 에서 가장 무겁다** — 현재 `DreamcatcherHandController` 가 덱·게이지·부착
  등록부를 소유하므로, 이 unit 에서는 컨트롤러를 **커맨드 발신자로만** 바꾸고 소유권 이동은
  unit 16 이 한다(범위 분리).
- 이 unit 이 끝나면 어댑터가 **유일한 drain 소유자**가 된다(unit 12 에서 유보한 것) — Bridge 의
  기존 drain 은 어댑터 호출로 대체하고 중복 소비를 제거한다.

## 완료 기준

- 묶음별 독립 커밋 3개, 각각 compile 0 · EditMode 회귀 0 · 해당 PlayMode 스모크 통과.
- `Assets/_Project/Scripts/{UI,Presentation}` 에서 **게임 상태를 바꾸는** `bridge.*` 호출 0
  (grep 증명. 좌표·픽 서비스 등 뷰 질의는 잔존 허용 — 청사진 ① §6 "계약 밖").
- 골든 7종 byte diff 0 — **재배선은 sim 을 건드리지 않는다**가 이 unit 의 핵심 계약이고 골든이 그
  증인이다. diff 가 나면 재배선이 규칙을 옮겼다는 뜻이므로 되돌린다.
