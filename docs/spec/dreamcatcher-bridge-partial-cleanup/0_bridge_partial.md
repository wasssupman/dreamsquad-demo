# 0. BattleBridge.Dreamcatcher.cs partial 분리

## 목적

BattleBridge 안의 드림캐쳐 카드 번역자 구간(~380줄)을 `BattleBridge.Dreamcatcher.cs` 로
물리 분리한다. 새 payload/카드 부류가 추가될 때마다 자라는 파일을 본체와 격리해
가독성과 리뷰 단위를 개선한다. **동작 변화 0 — 순수 이동.**

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `partial` 키워드 + 구간 제거(포인터 주석 잔류)
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — 신규 (+ Unity 생성 .meta)

## 이동 멤버 (전부, 그 외 금지)

- `ActiveDcEffect` struct · `_activeDcEffects` · `_dcStackCounter` · `_dcHandleCounter` ·
  `DcDuration` · `_activePlacementSleeps` · `_dcInstanceCounter`
- 3중1 트리거 이벤트: `FirstDefenderPlaced` · `WaveMilestoneReached` ·
  `_firstDefenderPlacedFired` · `FireFirstDefenderPlacedOnce` (unit 1 에서 삭제 예정이나
  이동은 원문 그대로)
- 각성 경제 릴레이: `EnemyKilledAwakening` · `DefenderDied` 이벤트 선언
- `ApplyDreamcatcherCardHosted` · `ApplyDreamcatcherCardInternal` · `RevokeDreamcatcherEffects` ·
  `ApplyActiveDcEffectsTo` · `ApplyPlacementSleep` · `ApplyDreamcatcherCardToUnit` ·
  `RegisterPlacementAura` · `MapDcEffect` · `MatchesDcAxis`

## 이동하지 않는 것 (경계 결정)

- `TryScreenToCell`/`TryGetDefenderAt`/`SetDefenderHoverHighlight`/`TryPickDefenderAtScreen`
  — 범용 입력/픽킹 유틸(드래그 UX 가 쓸 뿐 번역자 아님)
- 드림스톤 블록(`SetDreamstones`/`ApplyPendingDreamstones`) — 같은 레지스트리를 공유하지만
  드림스톤 도메인. partial 이라 cross-file 멤버 접근 무비용.
- `BakeNightmareMechanics` — DcMechanic 번역자이지만 적 스폰 파이프라인 소속(BossTag/ThreatEntry
  동반). 보스 도메인 분리는 별도 판단.
- `BeginPlacement` 의 dc 레지스트리 리셋 5줄 — 매치 수명주기 소유는 본체.

## 완료 기준

- [x] compile 클린 (Unity 콘솔 에러 0)
- [x] `BattleBridge.cs` 와 신규 파일 diff 가 이동만임을 확인 (라인 범위 스크립트 이동 + 멤버 카운트 검증)
- [x] EditMode 테스트 그린 (678/678, 스킵 2 = 기존 의도적 ignore)
- [x] 신규 `.cs` + `.meta` 짝 커밋

확인 2026-07-12 · 커밋 `cc4f26b9`
