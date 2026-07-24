# 6 — PlayMode 스모크 + 결과 라벨

## 목적

무한 모드가 실제 배틀에서 끝까지 도는지 통합 검증한다. 결과 팝업이 엔드리스 성격을 반영한다.

## 변경 대상

- 신규 `Assets/_Project/Tests/PlayMode/EndlessModeSmokeTest.cs`
- (선택) `BattleBridge.cs` — 엔드리스 종료 시 `SetResult` 라벨 (예 `"endless_complete"`)

## 구현

1. **결과 라벨**: 엔드리스가 타이머 만료(또는 전멸)로 끝날 때 `SetResult` 라벨을 엔드리스 전용으로.
   최소한으로 — 기존 `"victory_timeout"` 재사용도 허용(팝업이 점수만 보여주면 됨). 라벨 신설 시
   로거/리포트 소비처 확인.
2. **PlayMode 스모크** (원격 Play 검증 = run_tests, MCP 메뉴/코루틴은 Play 중 동결 주의):
   - `DevMapOverride.Index = 6` 설정 → 엔드리스 부팅.
   - 웨이브가 **10초 간격**으로 스폰(초반 몇 개 트리거타임 확인).
   - `ForceNextWave()` 호출 시 다음 웨이브가 당겨지고 남은 스케줄이 리베이스(unit 재사용 검증).
   - 타이머(가속) 180초 도달 → 결과 표시. 점수: **시간항 0, 킬>0, 스트레스=예산 기반**.
   - 토너먼트 리포트 **미발생**.

## 완료 기준

- PlayMode 테스트 green.
- 콘솔 에러 0.
- 위 4개 관찰(간격·당기기·시간0·리포트없음)이 테스트 어서션으로 고정.
