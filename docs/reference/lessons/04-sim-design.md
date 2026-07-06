# 전투 시뮬 설계 원칙

## 구조적 결정론 (seeded RNG 보다 index 기반)

전투 시뮬의 비주얼/배치 분산은 RNG(seeded 포함) 대신 **결정론 수열**을 쓴다. 목표 = 구조적 결정성(같은 입력 → byte-identical).

- **Why**: 비동기 토너먼트 리플레이/공정성. 사용자 명시 요구 "랜덤 있으면 안됨".
- **적용**: 분산/지터/선택은 index 기반 결정론으로. 예) 스폰 측면 분산을 `_spawnSpreadRng`(seeded) → `SpawnSpread.LaneFraction`(이산 N-레인 round-robin, 스폰 순번 % N)로 교체.
- **선호**: clever 한 저불일치(golden-ratio)보다 **단순·예측가능한 이산 N-레인 round-robin**(또렷한 N줄 대형 + 디버그 용이). seeded RNG 는 차선.

## 시간 제어는 TimeManager 만 — `Time.timeScale` 금지

시간 스케일 제어는 `Wassup.Core.TimeControl.TimeManager`(의도된 예외 싱글턴, TRD §5.2, 커밋 c2fe03d, spec `docs/spec/time-manager/`)만 담당. 코드에서 `Time.timeScale` 은 **절대 write 안 함(항상 1)**.

- **Why**: 글로벌 `Time.timeScale` 은 너무 blunt — 전투만 멈추고 UI·드래그·카메라는 실시간으로 두려면 도메인 분리 필요.
- **사용**: 정지 = `TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority:100)`, 슬로우 = `Request(Battle, 0.2f)`. 반환 `TimeLease` 를 보관 후 Dispose(멱등)로 해제.
- **전투 도메인 스케일**: ECS 는 `BattleSimGroup` 위 `BattleScaledRateManager`(scale 0=skip, >0=scaled delta). BattleBridge 가 `BattleTimeScale` singleton write + `_battleClock`(unscaledDeltaTime×scale)로 웨이브/타이머 구동.
- **되돌리면 안 되는 것**: `DestroyEcsInfrastructureEntities` 의 `DestroyEntitiesByType<BattleTimeScale>()`(빼면 StopBattle 후 orphan → 시간제어 무력화) · RateManager 로컬 `_elapsedTime` 누산(월드 elapsed 읽으면 정지 후 점프).
- **부작용**: `Time.timeScale=0` 으로는 이제 웨이브/타이머가 안 멈춘다(`_battleClock` 이 unscaledDeltaTime 기반). 검증 목적 완전 동결은 `TimeManager.Request(Battle,0)`. (→ `01-unity-mcp-operation.md` 애니 검증.)
