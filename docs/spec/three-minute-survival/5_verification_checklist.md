# 5 — 검증 체크리스트 (세션 인계용 · 통합)

> **이 파일이 두 spec(`three-minute-survival` + `goal-tower-siege`)의 단일 검증 입구다.**
> 두 spec 은 하나의 룰 개편이고 코드가 서로 맞물려 있어 따로 검증할 수 없다.
> 상태: 커밋 8개 전부 **컴파일만 검증**(`dotnet build` 3개 어셈블리 오류 0).
> **Unity 실행 검증 0** — EditMode·PlayMode·Play 모두 한 번도 안 돌렸다.
>
> 위에서부터 지우면 된다. 실패하면 §5 의 증상표를 먼저 본다.

## 0. 시작 전 (반드시)

- [ ] `ReadMcpResourceTool(unityMCP, mcpforunity://project/info)` 의 `projectRoot` 가
      **`D:/projects/dreamsquad-demo`** 인가?
      다른 클론(`dreamsquad-demo-new`)에 붙어 있으면 **다른 코드베이스가 통과**해 거짓 검증이
      된다. 이 세션에서 실제로 한 번 오인했다(EditMode 2890 통과를 내 변경의 검증으로 착각).
- [ ] 새 `.cs` 를 추가했다면 Unity 가 `.meta` 를 만들었는지. Unity 없이 만든 파일은
      손으로 넣은 2줄 meta 다(정상, GUID 중복 없음 확인됨).

## 1. 관련 커밋

| 커밋 | 내용 |
|---|---|
| `a7d1b015` | three-minute-survival units 0~3 — 점수 산식·웨이브 케이던스·골 안정도·탤리 제거 |
| `43f85107`·`15fc28f1`·`b01c748b` | goal-tower-siege units 0~2 (rev 1) |
| `b8f22ebd` | **rev 2 재설계** — 골 타워를 건물형 유닛으로 단순화(현재 코드) |
| `143584be` | endless-mode 계약 4 폐기 |

## 2. 자동 테스트

- [ ] **EditMode 전량** (개편 전 기준선: 2890 passed / 0 failed / 1 skipped)
- [ ] PlayMode `GoalStabilityTest` — 공성 지속 피해 경로로 갱신됨. 만피 시작 → 감소 →
      0 → Battle 이탈
- [ ] PlayMode `TallyFlowTest` — 탤리 제거 후에도 Battle → Tally → Result, 총점 == HUD
- [ ] PlayMode `EndlessModeSmokeTest` — 웨이브 100 / 상한 20초 / 안정도 패배로 갱신됨
- [ ] EditMode `UnitLifecycleSystemTests` — 자폭/공성 두 경로 + 마커 재발화 없음
- [ ] EditMode `FrontmostAttackLockTests` — 배제 단언 2건이 **뒤집혔다**(골에 붙은 적이 frontmost)
- [ ] 아래 4개는 **정적 판독상 호환**이지만 실행 미확인 — 실패하면 여기부터 본다:
      `WaveSpawnLeadInTests` · `SpawnAlertForecastTests` · `WavePatternGeneratorTests` ·
      `WaveSpawnForecastTests`

## 3. Play 육안 (한 판 = 3분)

**점수 · 웨이브**
- [ ] 도크에 다음 웨이브 **버튼이 없고** `웨이브 N / 100` + `다음 N초` 가 갱신된다
- [ ] 웨이브 적 전멸 → 리드인 뒤 **바로** 다음 웨이브
- [ ] 안 잡고 두면 트리거 후 20초에 다음 웨이브가 겹쳐 들어온다
- [ ] 드래프트 브리핑 스트립 인트로가 1초 이내(카드 상한 12)
- [ ] 종료 시 탤리 없이 바로 결과 화면, 총점 == 전투 중 마지막 HUD 숫자
- [ ] 결과 3줄(처치 N기 / 남은 안정도 X / Max (Y%) / 도달 웨이브 N)이 실제와 일치

**골 타워 · 공성**
- [ ] 안정도 바가 골 위에 뜨고 **만피에도 보인다**, 바 위에 숫자
- [ ] 바가 골 구조물 메쉬를 뚫지 않는다(`BattleBridge.goalStabilityBarLift` 초기 1.6)
- [ ] 골 2개 맵(Serpent·Twin·Zig)에서 **두 번째 바의 위치가 정확한가** — rev 2 에서
      sim→view 변환을 고친 지점이다
- [ ] 근접 적이 골에 도달해 **사라지지 않고** 타워를 때리고 안정도가 지속적으로 준다
- [ ] 공성 적의 뷰가 살아 있다(데미지 폰트가 허공에 뜨지 않는다)
- [ ] Runner·Swift 는 골에서 사라지며 안정도를 1회 깎는다(자폭 경로)
- [ ] 골 인접 배치칸에 유닛을 놓으면 공성 적이 죽고 **전멸 진행이 살아난다**
- [ ] 보스 AreaBarrage 가 골에 떨어지면 안정도가 준다(`TileAoe` 피해자 풀 수정 확인)
- [ ] 안정도 0 → 즉시 패배. 골 2개면 **한쪽만 부서져도** 패배
- [ ] 스트레스 배지가 분모·위기색 없이 개수만 표시된다
- [ ] 콘솔 에러/경고 0 — 특히 `ScoreRules.asset`·삭제된 `ScoreTallyView` 관련 missing 없음

**rev 2 로 새로 생긴 상호작용 (유지/차단 판단)**
- [ ] 원거리 적(Sniper·Needler)이 사거리에서 멈춰 타워를 쏜다 — **정상**이다. 그 적은 골 셀에
      안 들어와 스트레스 카운터에 안 잡힌다(수용된 대가)
- [ ] 힐러가 골을 수리한다 — 눈에 거슬리는가? 막으려면 아군 후보에서 `GoalTowerTag` 배제
- [ ] 보스가 방어유닛이 살아 있어도 골로 향한다 — `DefenderFieldSystem` 이 `Faction.Defender`
      로 필터하기 때문. rev 1 이 "의도된 구멍" 으로 남겼던 항목이 자연 해소된 것
- [ ] 도발된 공성 적이 무기한 정지하는가(guardian 이 사거리 밖일 때). 리뷰 M2 — 미결

## 4. 서버 왕복

- [ ] 제출 로그에 인코딩 값 확인 — 점수 47 + 안정도 62% → `1000047619`
- [ ] **결과 화면 리더보드**가 `47` 로 표시(디코딩). rev 2 에서 고친 지점 —
      랭킹 응답 도착 시 10억대 숫자가 뜨던 버그
- [ ] `LeaderboardList` · 히스토리 패널도 `47`
- [ ] 히스토리의 **구 기록**이 가짜 점수로 디코딩되지 않는다(원값 그대로)

## 5. 밸런스 실측 — **가장 큰 미지수**

공성 DPS 가 붙으면서 안정도 20 이 몇 초 만에 녹을 수 있다. 그러면 "3분 생존" 전제 자체가
성립하지 않는다. **이 항목을 먼저 재는 것을 권한다.**

- [ ] 3분 도달 웨이브 수 = ____ (설계 목표 10~16)
- [ ] 첫 골 돌파부터 안정도 0 까지 걸린 시간 = ____ 초 (너무 짧으면 `goalStabilityMax` ↑)
- [ ] 종료 시 남은 안정도 = ____ / 20 (정상 플레이면 절반 이상)
- [ ] 마지막 도달 웨이브의 적 수 = ____ (base 5 의 2배 이상이어야 성장이 체감)
- [ ] 한 판 총점 = ____ (처치 점수 스케일이 화면에서 읽히는 크기인가)

조정 손잡이: `Deck_*.asset` 의 `minUnitsPerWave`(base) / `unitGrowthPerWave` /
`maxUnitsPerWave`(cap) / `goalStabilityMax`, 적 SO 의 `attackDamage`·`stabilityDamage`.
**cap 을 올리면 `intraWaveSpacingSec` 을 함께 내려야** 스폰 창 불변식
(`leadIn + (cap−1)×spacing < maxWaveIntervalSec`)이 유지된다.

## 6. 증상 → 첫 의심 지점

| 증상 | 첫 의심 |
|---|---|
| 웨이브가 항상 20초로만 온다 | 스폰 창 불변식 위반 — 생성기 경고 로그 확인 |
| 첫 두 웨이브가 같은 프레임에 | `QueueDueWaves` 의 `first` 가드(`_nextWaveIndex == 0`) |
| 적이 골에 붙었는데 안정도가 안 준다 | 적에게 `AttackState` 가 있는가(없으면 자폭 경로). 타워 `FactionTag` 가 `Defender` 인가 |
| 골에 붙은 적이 안 죽는다 | `PastGoalTag` 배제가 되살아났는지(리뷰가 지목한 5곳) |
| 안정도가 안 깎인다(자폭) | `_enemyTypeByEntity` 조회 실패 → "등록부에 없다" 경고 1회 |
| 리더보드에 10억대 숫자 | 표시 지점이 `ScoreMath.DisplayScore` 를 안 거침(4곳) |
| 버스트 플래시 안 터짐 | 씬 `burstScoreThreshold`(4)가 되돌아갔는지 |
| 로비에서도 타워가 살아 있다 | `DestroyBattleEntities` 의 `GoalTowerTag` 정리 |

## 7. 코드리뷰 미처리 (전부 MEDIUM 이하 · 판단 필요)

투트랙 리뷰(code-reviewer + ecs-reviewer)에서 나왔고 **의도적으로 미루었다.** CRITICAL 2건과
HIGH 4건은 rev 2 재설계와 `b8f22ebd` 에서 처리됐다.

| 항목 | 상태 |
|---|---|
| 타워가 런타임에 `StatModifierSlot` 획득(Debuffer/Kindler 공격) | 현재 무해 확인. 막으려면 소비자 쪽 배제 + 테스트 1개 |
| `SyncGoalStability` 가 매 프레임 `CreateEntityQuery` | 누수 아님. `_aliveAttackersQuery` 처럼 캐시로 합류시키는 게 일관적 |
| 도발×공성 무기한 정지(M2) | Play 후 판단 |
| blink 로 골 이탈 시 영구 동결(M3) | 보스 한정 좁은 경로. 알려진 제약으로 둘지 |
| 죽은 `scoreRules` SerializeField + 빈 SO | Unity 에서 에셋+씬 참조를 함께 정리 |
| 작성 플랜 모드에서 도크 카운트다운 0 고정 | 개발용 테스트 모드 한정 |
| 공성 지속 피해를 고정하는 EditMode 테스트 없음 | 표준 경로라 전용 테스트 없이도 회귀는 잡히지만 한 번 잠글 가치 |
