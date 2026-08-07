# 5 — 검증 체크리스트 (세션 복구용)

> units 0~3 은 **컴파일만 검증된 상태로 커밋**됐다(`a7d1b015`). Unity 실행 검증이 통째로
> 남아 있다. 세션이 끊겼다 돌아오면 **이 파일부터 읽고 위에서부터 지운다.**
>
> ⚠ 검증 전 필수 확인: `ReadMcpResourceTool(unityMCP, mcpforunity://project/info)` 의
> `projectRoot` 가 `D:/projects/dreamsquad-demo` 인가? 다른 클론
> (`dreamsquad-demo-new`)에 붙어 있으면 **다른 코드베이스가 통과**해서 거짓 검증이 된다.

## 1. 자동 테스트

- [ ] EditMode 전량 (기준선: 개편 전 2890 passed / 0 failed / 1 skipped)
- [ ] PlayMode `GoalStabilityTest` — 신규. 만피 시작 → 유출 시 감소(낙폭 ≤ 5 · 유출 카운터
      동반 증가) → 0 바닥 → Battle 이탈
- [ ] PlayMode `TallyFlowTest` — 탤리 제거 후에도 Battle → Tally → Result 도달, 총점 == HUD
- [ ] PlayMode `EndlessModeSmokeTest` — 웨이브 100/상한 20초 단언으로 갱신됨
- [ ] 아래 4개는 **정적 판독상 호환**이지만 실행 미확인 — 실패하면 여기부터 본다
      (`WaveSpawnLeadInTests` · `SpawnAlertForecastTests` · `WavePatternGeneratorTests` ·
      `WaveSpawnForecastTests`)

## 2. Play 육안 (한 판 = 3분)

- [ ] 안정도 바가 골 위에 뜨고 **만피에도 보인다**, 바 위에 숫자
- [ ] 바가 골 구조물 메쉬를 뚫지 않는다 (`BattleBridge.goalStabilityBarLift` 초기 1.6)
- [ ] 골 2개 맵에서 두 바가 같은 값
- [ ] 유출 1기 → 안정도가 그 적의 `stabilityDamage`(일반 1 / 엘리트 2 / 보스 5)만큼 감소
- [ ] 안정도 0 → 즉시 패배. 스트레스가 10에 닿아도 패배하지 않는다
- [ ] 스트레스 배지가 분모·위기색 없이 개수만
- [ ] 도크: 다음 웨이브 **버튼 없음**, `웨이브 N / 100` + `다음 N초` 갱신
- [ ] 웨이브 적 전멸 → 리드인 뒤 **바로** 다음 웨이브
- [ ] 안 잡고 두면 트리거 후 20초에 다음 웨이브가 겹쳐 들어옴
- [ ] 종료 시 탤리 없이 바로 결과 화면, 총점 == 전투 중 마지막 HUD 숫자
- [ ] 결과 3줄(처치 N기 / 남은 안정도 X / Max (Y%) / 도달 웨이브 N)이 실제와 일치
- [ ] 드래프트 브리핑 스트립 인트로가 1초 이내 (카드 상한 12)
- [ ] 콘솔 에러/경고 0 — 특히 `ScoreRules.asset`·`ScoreTallyView` 관련 missing 없음
      (씬에서 ScoreTallyView 오브젝트를 지웠고 ScoreRulesData 는 빈 SO 로 남겼다)

## 3. 서버 왕복

- [ ] 제출 로그에 인코딩 값 확인 — 처치 47 + 안정도 62% → `1000047619`
- [ ] 결과 화면 리더보드에 `47` 로 표시(디코딩)
- [ ] 히스토리 패널의 **구 기록**이 가짜 점수로 디코딩되지 않는다(원값 그대로)

## 4. 밸런스 실측 (수치 기록 후 판단)

- [ ] 3분 도달 웨이브 수 = ____ (설계 목표 10~16)
- [ ] 종료 시 남은 안정도 = ____ / 20 (정상 플레이면 절반 이상, 방치하면 3분 전 0)
- [ ] 마지막 도달 웨이브의 적 수 = ____ (base 5 의 2배 이상이어야 성장이 체감)
- [ ] 한 판 총점 = ____ (처치 점수 스케일이 화면에서 읽히는 크기인가)

조정 손잡이: `Deck_*.asset` 의 `minUnitsPerWave`(base) / `unitGrowthPerWave` / `maxUnitsPerWave`
(cap) / `goalStabilityMax`. **cap 을 올리면 `intraWaveSpacingSec` 을 함께 내려야** 스폰 창
불변식(`leadIn + (cap−1)×spacing < maxWaveIntervalSec`)이 유지된다.

## 5. 실패 시 되돌릴 곳

| 증상 | 첫 의심 |
|---|---|
| 웨이브가 항상 20초로만 온다 | 스폰 창 불변식 위반 — 생성기 경고 로그 확인 |
| 첫 두 웨이브가 같은 프레임에 | `QueueDueWaves` 의 `first` 가드(`_nextWaveIndex == 0`) |
| 안정도가 안 깎인다 | `_enemyTypeByEntity` 조회 실패 → "등록부에 없다" 경고 1회 |
| 리더보드에 10억대 숫자 | 표시 지점이 `ScoreMath.DisplayScore` 를 안 거침 |
| 버스트 플래시 안 터짐 | 씬 `burstScoreThreshold`(4) 가 되돌아갔는지 |
| 타이틀 화면 missing script | 씬에서 지운 ScoreTallyView 블록이 되살아났는지 |
