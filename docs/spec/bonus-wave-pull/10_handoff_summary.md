# 10 — 인계 요약 (보너스 당기기)

## Commit

- `<pending>` — feat(bonus-wave-pull): 보너스 당기기 (units 0~9)

## Implemented

- **일반 당김 알약 위에 조건부 두 번째 알약**(보라). 누르면 보드에 저작된 포탈 2개가 열리고
  보너스 적 10기가 순차로 나온다 — 1초 뒤 포탈, 2초 뒤 첫 적, 0.35초 간격.
- **등장 조건 = 일반 처치 30기(크레딧) AND 스트레스 30 이하(창).** 두 축의 성격이 다르다 —
  크레딧은 쌓이는 자원이라 창이 닫혀 있어도 소멸하지 않고, 창은 **등장 순간에만** 판정한다.
- **보너스 적은 방어유닛을 사냥하다 전멸시키면 거점으로 간다.** 신규 이동 코드 0 —
  `boss-defender-field` 의 필드를 `BossTag` → `DefenderHunterTag` 게이트 교체로 열었다.
- **기존 웨이브 생성과 코드 경로가 갈린다** — `WavePatternGenerator`·`AttackDeck`·`_wavePlan`·
  `_pending` 무접촉. 자기 큐(`_bonusPending`)와 자기 타임라인.
- **보너스 웨이브는 일반 판 진행을 멈추지 않는다** — 전멸 판정 **전용 쿼리**로 제외.
- 결정론은 seeded RNG 가 아니라 구조: 포탈 `i % portalCount` · 시각 등차 · 링 각도 `2π·i/count`.
- 신규 ISystem 0 · 신규 NativeQueue 채널 0 · 신규 FSM 상태 0.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.BonusWave.cs` — 스케줄러·트리거·포탈 뷰(신규 partial)
- `Assets/_Project/Scripts/Data/BonusPullTrigger.cs` · `BonusWaveSchedule.cs` — 순수 함수 2종
- `Assets/_Project/Scripts/Data/BonusWaveData.cs` + `Assets/_Project/Data/BonusWaveData.asset` — 수치 정본
- `Assets/_Project/Scripts/Battle/Combat/DefenderHunterTag.cs` · `Battle/Units/BonusWaveTag.cs`
- `Assets/_Project/Scripts/Data/MapGrid/BonusSpawnAuthoringRules.cs` — 저작 검증 단일 소유자
- `Assets/_Project/Scripts/UI/NextWaveDock.cs` — rev 9(두 번째 알약)
- `Assets/_Project/Data/Enemies/Enemy_DreamShard.asset` · `Data/Maps/MapDocument_Duel.asset`

## Verified

- EditMode 코어 **2455 · 실패 0 · 스킵 3**(기존 문서화 ignore)
- EditMode Assets — 신규 가드 2개 통과. `UnitKitCatalogTests`(malphite 30자) 1건 실패는 **무관·기존**
- PlayMode `BonusWavePullTest` **10/10**
- 사용자 Play 확인 통과 (2026-08-24)
- 투트랙 리뷰(code + ecs) 반영 완료

## Notes — 되돌리면 안 되는 판단

1. **`_aliveAttackersQuery` 에 필터를 걸지 마라.** 11곳이 공유하고 거기엔 광역기 사전집계와
   **배치 스킬 대상 수집**이 들어 있다. 전멸 판정용 `_aliveNormalAttackersQuery` 는 별개다.
   두 쿼리는 `CreateAliveAttackerQueries()` 로 **항상 함께** 만든다 — 한쪽만 되살리면 그 판의
   웨이브 진행이 멎는다(리뷰 H-2).
2. **`DefenderHunterTag` 는 `CreateEnemyEntity` 본문에서 붙인다.** `BakeNightmareMechanics` 는
   메커닉이 비면 조기 반환하므로 `BossTag` 옆에 두면 메커닉 없는 사냥꾼이 태그를 못 받는다 —
   보스는 무회귀이고 테스트도 초록인 채 사냥만 죽는다. `EnemyTierBakeTests` 가 이걸 고정한다.
3. **트리거 판별은 SO 동일성이다**(태그 아님) — 킬 드레인 시점엔 엔티티가 이미 파괴됐다.
   그 동치는 계약 4 가 보장하고 `BonusEnemyNotInDeckTests` 가 지킨다. 덱 풀에 넣는 날 함께 깨진다.
4. **소비는 한 회분(`consumed += killThreshold`)** — `= normalKills` 로 두면 밀린 크레딧이 증발한다.
5. **스트레스는 등장 조건이지 유지 조건이 아니다**(래치). 매 프레임 재평가하면 문턱에서 깜빡인다.
6. **포탈 개수의 소유자는 맵이다** — SO 에 `portalCount` 를 되살리지 마라(읽히지 않는 값이 된다).
7. **통행 판정은 `== Walk`** — `!= Place` 로 쓰면 Env 기둥이 통과하고 벽에 스폰된다.
8. 겹침 오프셋은 **분열 레시피 복제**(셀 중심 + 반경 0.25 · 각도 `2π·i/count`). 레인 스폰의
   `ComputeSpawnLateralOffset` 은 래퍼 전용이라 이 경로에 없다.

## Follow-up

- **골든 코퍼스 재생성** — `configHash` 에 키 3종이 추가돼 7건 전부 「조건 드리프트」로 빨개진다.
  회귀가 아니다. ⚠ **무관 dirty 격리 후 별도 커밋** — 지금 재생성하면 남의 WIP 가 기준선에 구워진다.
- **R-별 헌터 필드 분리** — 보스와 근접 보너스 적이 겹치면 R 이 1 로 내려가 보스가 사냥을 멈추고
  골로 향한다. 보스는 5웨이브마다·보너스는 30킬마다라 **이 겹침이 상시에 가깝다** — 체감 확인 필요.
- **포탈 전용 비주얼** — 레인 스폰과 같은 프리팹이라 「혼동되지 않는가」가 남는 질문.
- **임계 30 실측 튜닝** — 판당 1~4회 추정. **잘하는 플레이어일수록 자주 온다**(스트레스 게이트가
  이 성질을 증폭한다). 반대 설계(위험할 때 구원)로 뒤집는 건 `maxStressToOffer` 를 하한으로 바꾸는 한 줄.
- **`BonusPullBlockedByStress` 소비처** — 「크레딧은 찼는데 스트레스 때문에 막혔다」 신호가 API 로만
  있고 도크는 안 읽는다. 힌트 문안을 붙일지 판단.
- **L5** 등록부 miss 시 보너스 킬이 일반 킬로 계수(유계, 수용).
