# 5. Tap Skip — 연출 중 터치 스킵 (rev1: 2단 스킵)

## 목적

선물 페이즈 연출(~6.9s)을 이미 아는 유저가 터치로 건너뛰게 한다. 단 **받은 카드 확인(리빌 포커스)은 건너뛰지 않는다** — 서사 계약(README 계약 9)의 "존재의 개입" 비트는 정보이기도 하기 때문. 덱 최종 순서는 `OnGiftDeckReady` 시점에 데이터로 확정돼 있으므로(계약 1) 스킵은 연출만 끊는다.

rev1 (2026-07-14 사용자 결정): 착지점을 배치가 아니라 **리빌 포커스**로 변경.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` — 탭 캐처 + 2단 스킵 + 시퀀스 전/후반부 분리
- `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` + `GiftConfig_Default.asset` — `tapSkipEnabled` / `tapSkipGraceSec`

## 구현

1. `_panel` 풀블리드 Dim Image 에 `IPointerClickHandler` 경량 컴포넌트 부착(코드빌드, 씬 배선 불변 — 계약 7).
2. 시퀀스를 전반부(인트로~플립~과시)와 후반부 `PlayFromRevealFocus()`(홀드→스택→리플→부채꼴→흡수)로 분리. 자연 진행은 전반부 말미 `ChainCallback` 으로 후반부에 진입 — 탭 스킵과 같은 경로를 공유한다.
3. **2단 스킵**: 리빌 전(`_stage == 0`) 탭 → 전반부 정지 + 리빌 포커스 상태 직접 세팅(그리드 정착·선물 앞면·`revealScale`·앰비언스 온) + 후반부 시작. 리빌 이후(`_stage == 1`) 탭 → `StopSequence()` + `ProceedToPlacement()`.
4. grace(`tapSkipGraceSec`, 기본 0.35s)는 시퀀스 시작과 **리빌 착지 시점에 각각 리셋** — 연속 탭이 리빌 확인까지 날리는 것을 방지.
5. 후반부가 쓰는 판 컨텍스트(`_n/_baseN/_keyByF/_finalPos/_kindColor/_amb`)는 전반부 구축 시 필드로 보관.

## 완료 기준

- 컴파일 클린 + EditMode 무회귀.
- Play 스모크: ①리빌 전 탭 → 리빌 포커스 착지(선물 앞면·과시 스케일) 후 후반부 자연 진행 ②리빌 이후 탭 → 즉시 Placement, 잔여 FX 0 ③grace 구간 내 탭 무시. 콘솔 에러/PrimeTween ignored callback 0.
- 스킵해도 배치/사이클 덱 순서가 완주와 동일(데이터 확정 선행이므로 자명 — 로그로 1회 확인).
- 사용자 Play 확인: 2단 스킵 반응감(grace 포함).

확인: 2026-07-14 — 스모크(리빌 착지·재-grace 무시·2차 탭 배치행·자연 완주·FX 0) + 사용자 확인("오케 마무리"). 커밋 `3a9e20f4` + rev1 `c87f8060`.
