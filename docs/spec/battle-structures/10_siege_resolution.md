# unit 10 — 공성 승패 축

## 목적

공성 판의 승패를 낸다. **모드에 종속된 분기를 만들지 않는다** (2026-08-10 사용자 지시) — 종료 조건을 각각 독립 축으로 두고, 축마다 「저작된 상한 > 0 이면 이 축이 산다」로 켜고 끈다.

**그 패턴은 이미 이 코드베이스에 있다:**

```
_timerDuration    <= 0 → 타이머 축 없음     (CheckTimer)
StressLimit       <= 0 → 유출 축 없음       (CheckStressDefeat)
_goalStabilityMax <= 0 → 골 타워를 안 세운다 (SpawnStructureEntities)
```

적 마음은 **같은 형태의 축 하나**를 더할 뿐이다.

| 축 | 활성 조건 | 판정 |
|---|---|---|
| 방어 마음 | `_goalStabilityMax > 0` | HP 0 → 패배 (기존) |
| **적 마음** (신설) | `_enemyCoreMax > 0` | HP 0 → 승리 |
| 유출 | `StressLimit > 0` | N회 → 패배 (기존) |
| 타이머 | `_timerDuration > 0` | 만료 → **방어 잔여 ≥ 적 잔여 면 승리** |

**타이머 축의 비교식 하나가 두 경우를 통합한다.** 적 축이 비활성이면 「적 잔여 = 0」이므로 `방어 ≥ 0` 은 항상 참 → 승리. 기존 `victory_timeout` 동작이 특수 케이스로 그대로 재현된다. 침략 맵과 공성 맵이 **같은 코드**를 탄다.

⚠ 축의 활성 조건은 「적 마음 엔티티가 없다」가 **아니다** — 그러면 침략 맵이 첫 프레임에 승리한다. 「저작된 상한이 있었는데 지금 잔여가 0」이다. 위 4축이 전부 그 모양이므로 새 패턴이 아니다.

⚠ `MapMode`(`StructureAuthoringRules.DeriveMode`)는 **런타임에 쓰지 않는다.** 그것은 페인터 배지·저작 검증 전용이다. 승패에 모드를 읽으면 축 구성이 무너지고, 적 마음이 부서지는 순간 «공성 맵이 아니게» 되는 함정이 생긴다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_enemyCoreMax`/`_enemyCoreCurrent` · `SyncGoalStability` 집계 확장 · 적 마음 축 판정 · `CheckTimer` 만료 분기
- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentPool.cs` — `OnValidate` 체력 대칭 경고
- 에셋: `Deck_SiegeTest`(신설) + `MapDocumentPool` dev[2] 엔트리에 연결

## 구현

**집계는 기존 순회에 얹는다.** `SyncGoalStability` 가 이미 `_structureRegistry` 를 훑어 방어 마음 최저를 구한다(`:5236` 이 `faction != DefenderCore` 를 걸러낸다). 같은 루프에서 적 마음 잔여 합을 모은다 — 새 순회 0.

**승리 판정이 들어갈 자리는 이미 표시돼 있다** — `:5260` 의 «본능·적 마음 붕괴 — v1 은 연출·로그만(README 결정 2)» 분기. unit 10 은 적 마음에 한해 그 자리에 승리 축을 얹는다(본능은 계속 연출·로그만). 붕괴 감지가 이미 그 루프에 있으므로 새 감지 기계를 만들지 않는다.

**적 마음 max 는 스폰 시 1회 확정한다** (`SpawnStructureEntities`). 축 활성 = `_enemyCoreMax > 0`.

**타이머 만료** — `BeginTally(win: _goalStability >= _enemyCoreCurrent, ...)`. 로거 결과 문자열은 패배 시 `"defeat_timeout"` 을 추가한다(기존 `"victory_timeout"` 과 짝).

**두 마음의 체력은 저작으로 맞춘다.** 「덱 스칼라로 통일」은 채택하지 않았다 — 적 마음만 `StructureData.health` 를 무시하는 예외와 그 조용한 무시를 알리는 장치가 필요해져 순이득이 없다. 대신 **문서와 덱을 둘 다 아는 유일한 자리**인 `MapDocumentPool.Entry` 에서 어긋남을 잡는다:

```
엔트리 문서에 적 마음이 저작돼 있고 deck != null 이면
  deck.goalStabilityMax != (적 마음 StructureData.health) → 경고
```

**에러가 아니라 경고**다 — 비대칭 체력도 난이도 저작일 수 있다. 판정이 한쪽에 유리해진다는 사실만 알린다.

**공성 맵 전용 덱** — 맵 풀 엔트리는 자기 덱을 들고 온다(`BattleBridge:980`). 그래서 코드 분기 0으로 그 맵에만 저작할 수 있다:

- `defeatGoalReachedCount = 0` — 유출 축을 끈다. 유출은 이미 `stabilityDamage` 로 안정도를 깎아 방어 마음 축에 흡수되므로 「N회」는 중복 규칙이다.
- `goalStabilityMax` = 적 마음 SO `health` 와 같은 값
- `timerDurationSec = 180` (현행 동일)

dev[2] 엔트리에 덱이 없으면 레거시 `deck` 폴백(1000 / 유출 10)이라 이 저작이 **필수**다.

## 완료 기준

- 컴파일 0
- **별도 순수 함수·EditMode 축 조합표를 만들지 않는다.** 판정이 비교 한 줄이라 함수 추출은 과잉이고(제약 10 — 호출처 1곳, 자명), 진짜 위험은 «축이 비활성일 때 기존 동작이 보존되나» 인데 그것은 기존 PlayMode 가 침략 맵을 태워 이미 잡는다
- 기존 PlayMode 골 3종(`GoalStabilityTest` · `EndlessModeSmokeTest` · `StructureLivePlayTest`) 그린 = **침략 맵 무회귀**. 타이머 만료 승리가 여전히 승리여야 한다
- EditMode 전량 무회귀 (기준선 2049 / 실패 0 / 의도적 스킵 3)
- `MapDocumentPool` 저작 경고가 어긋난 조합에서 뜨고 맞춘 조합에서 안 뜬다 (인스펙터 확인)
- 공성 3경로의 라이브 확인은 **unit 11** 이 담당한다
