# 0 — 방어유닛 사냥을 보스 밖으로 개방한다

## 목적

「배치된 방어유닛을 찾아다니다 전멸시키면 거점으로」는 이미 구현돼 있다
(`boss-defender-field`). 다만 `BossTag` 로 잠겨 있어 보스만 쓴다. 그 README 의 후속 후보
「두 번째 수요가 생기면 SO 플래그로 게이트 교체」를 실행한다.

**이 unit 은 라이브 보스 동작을 건드리는 유일한 unit 이다.** 신규 데이터가 하나도 없을 때
먼저 착지시켜야 「보스 회귀」와 「보너스 웨이브 버그」가 커밋 단위로 갈린다(README 순서 근거).

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `huntsDefenders` 필드 추가
- `Assets/_Project/Scripts/Battle/Combat/DefenderHunterTag.cs` — 신규
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CreateEnemyEntity` 본문에 부착
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — 게이트 1곳
- `Assets/_Project/Scripts/Battle/Effects/DefenderFieldSystem.cs` — 게이트 2곳
- `Assets/_Project/Tests/EditMode/EnemyTierBakeTests.cs` — 단언 추가

## 구현

1. `AttackUnitData.huntsDefenders : bool`(기본 false)을 **필드 목록 맨 뒤에** 추가한다.
   기존 에셋 17종은 false 로 남아 무회귀.

2. `DefenderHunterTag : IComponentData`(빈 태그)를 **Combat 맥락**에 둔다 — `BossTag` 와
   같은 자리, 같은 읽기 패턴(Movement·Effects 가 RO 소비, 브리지가 유일 writer).
   주석에 **스폰 이후 불변**임을 명시한다. 전투 중 떼는 시스템이 생기면 맥락 소유권 질문이
   다시 열린다.

3. **부착 지점은 `CreateEnemyEntity` 본문**이고, `BakeNightmareMechanics` 호출 **앞**이다.
   조건은 `unitType.tier == EnemyTier.Boss || unitType.huntsDefenders`.

   ⚠ `BossTag` 옆(`BakeNightmareMechanics` 안)에 두면 안 된다. 그 메서드는
   `nightmareMechanics` 가 비면 **조기 반환**하므로 메커닉 없는 보너스 적이 태그를 못 받는다.
   보스는 무회귀이고 테스트도 전부 초록인 채 **사냥만 조용히 죽는다** — 증상은 「보너스 적이
   방어유닛을 무시한다」이고 원인은 `huntsDefenders` 와 완전히 무관한 곳에 있다.

4. 게이트 교체 **3개 지점**:
   - `MovementSystem` — `bossLookup` → `hunterLookup`(`GetComponentLookup<DefenderHunterTag>`)
   - `DefenderFieldSystem` — 재빌드 skip 쿼리, R 산출 쿼리 둘 다 `WithAll<DefenderHunterTag>`

5. **바꾸지 않는 5곳**: 넉업 면역(`AttackSystem`) · 어그로 면역(`AggroStateSystem`) ·
   CC 면역(`CcApplySystem`·`EffectSpawner`) · 보스 bake. 전부 **보스 특권이지 사냥 성질이
   아니다.** 이 구분이 이 unit 의 핵심이므로 코드 주석으로 남긴다.

6. R-min 한계 주석: 근접 헌터와 원거리 보스가 동시에 살아 있으면 R 이 근접 쪽으로 내려가
   보스가 일시적으로 사냥을 멈추고 골로 향할 수 있다. 알려진 제한이며 R-별 필드 분리로만
   해소된다(README 후속 후보). `DefenderFieldSystem` 의 R 산출 옆에 적는다.

## 완료 기준

- [x] 컴파일 에러 0
- [x] `EnemyTierBakeTests` — `tier=Boss` 적이 `BossTag` **와** `DefenderHunterTag` 를 둘 다 받는다
- [x] `nightmareMechanics` 가 **빈** SO + `huntsDefenders=true` → `DefenderHunterTag` 부착 (H1 가드)
- [x] `huntsDefenders=false` + `tier=Normal` → 태그 없음
- [x] `DefenderFieldSystem` 신규 테스트 — 헌터 + 방어유닛 → hunt-dist 유한 / 헌터 없으면 재빌드 skip
- [x] EditMode 전체 green (기존 보스 테스트 무회귀)

**확인 2026-08-24** — EditMode 2455 · 실패 0. `DefenderHunterGateTests`(5) 가 게이트 3곳을,
`EnemyTierBakeTests` 의 부착 지점 가드 3개가 `BakeNightmareMechanics` 조기 반환 함정을 고정한다.
