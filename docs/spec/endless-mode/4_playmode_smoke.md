# 4 — PlayMode 스모크 (배관 + 리스크/리워드 어서션)

## 목적

무한 모드 통합 검증 + **모드의 핵심 가설(누수가 점수 긴장을 만든다)**을 테스트로 고정한다.
(critic MAJOR#4: 배관만 검증하지 말 것.)

## 변경 대상

- 신규 `Assets/_Project/Tests/PlayMode/EndlessModeSmokeTest.cs`

## 구현

원격 Play 검증 = run_tests (Play 중 MCP 메뉴·코루틴 동결 주의). 입력은 reflection/상태머신 구동.

**배관 어서션**:
1. `DevMapOverride.Endless=true` → 엔드리스 부팅, `Deck_Endless` 로드.
2. 웨이브가 **10초 간격**으로 스폰(초반 트리거타임 = i×10 확인).
3. `ForceNextWave()` → 다음 웨이브 당겨지고 남은 스케줄 리베이스(기존 로직 재사용 확인).
4. 누수해도 **패배 안 함**(`_goalReachedCount` 가 예산 근처여도 `_running` 유지).
5. 타이머(가속) 180초 → 결과 표시, **시간점수=0**, 킬>0.
6. 토너먼트 리포트 **미발생**. `mapPool.Count` 불변(회귀 가드).

**리스크/리워드 어서션 (가설 검증)**:
7. 누수 N 개 → 스트레스 점수가 `N × stressScorePerPoint` 만큼 감소(예산 이내).
8. **saturation 명시**: 누수가 예산(`defeatGoalReachedCount`)을 넘으면 스트레스 점수가 0 에서
   멈춤(더 안 깎임). 이 값이 180초 내 도달 불가하도록 예산을 높게 잡았음을 테스트가 문서화.

## 완료 기준

- PlayMode 테스트 green, 콘솔 에러 0.
- 배관 6개 + 리스크/리워드 2개 어서션 통과.
- (밸런싱 — 킬 vs 스트레스 가중 실튜닝 — 은 후속. 이 테스트는 산식 성립만 고정.)

✅ 확인 2026-07-25 — 가설(시간0·누수 saturation)은 결정론 EditMode `EndlessScoreTests` 3/3 로,
통합(부팅·10초간격·30웨이브·mapPool 불변·누수무사망)은 PlayMode `EndlessModeSmokeTest` 1/1 로 분리 검증.
full EditMode 1298/1298(회귀 0). 커밋 해시는 handoff(unit 5) 참조.
