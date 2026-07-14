# 5. Tap Skip — 연출 중 터치로 즉시 배치 진입

## 목적

선물 페이즈 연출(~6.9s)을 이미 아는 유저가 터치 한 번으로 건너뛰게 한다. 덱 최종 순서는 `OnGiftDeckReady` 시점에 데이터로 확정돼 있으므로(계약 1) 스킵은 **연출만 끊는다** — 상태 재구성 없음. 스킵 목적지 = 흡수 연출 이후 = 즉시 `ProceedToPlacement()`.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` — 패널 탭 캐처 + 스킵 핸들러
- `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` + `GiftConfig_Default.asset` — `tapSkipEnabled` / `tapSkipGraceSec`

## 구현

1. `_panel` 의 풀블리드 Dim Image(raycastTarget 기본 on)에 `IPointerClickHandler` 경량 컴포넌트를 부착(코드빌드, 씬 배선 불변 — 계약 7).
2. 탭 핸들러: `_seq.isAlive` 가드 → `tapSkipEnabled` → 시퀀스 시작 후 `tapSkipGraceSec`(오탭 방지) 경과 확인 → `StopSequence()` + `ProceedToPlacement()`.
3. 기존 중단 계약(계약 6) 재사용: `StopSequence()` 가 트윈/일회성 FX 를 전부 정리하고, `Sequence.Stop()` 은 남은 콜백(진동·링 빌드 등)을 실행하지 않는다. 패널 숨김은 기존 `OnPhaseChanged(Placement)` 경로.
4. 수치는 전부 GiftConfig(계약 4). 기본 grace 0.35s.

## 완료 기준

- 컴파일 클린 + EditMode 무회귀.
- Play 스모크: 연출 중간(딜-인/리빌/셔플 각 구간) 탭 → 즉시 Placement 진입, 잔여 FX 0, 콘솔 에러/PrimeTween ignored callback 0. grace 구간 내 탭은 무시.
- 스킵해도 배치/사이클 덱 순서가 완주와 동일(데이터 확정 선행이므로 자명 — 로그로 1회 확인).
- 사용자 Play 확인: 탭 반응감(grace 포함).
