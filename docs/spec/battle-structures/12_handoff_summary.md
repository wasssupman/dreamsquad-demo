# 종료 인계 — battle-structures units 8~11 (공성 승패)

2026-08-10 · **구현 완료.** units 0~6 의 인계는 [`7_handoff_summary.md`](7_handoff_summary.md). 계약의 정본은 README.

## Commit

- `69d6b1b4` units 8~11 설계 문서 (계약 15 신설, 결정 2 를 본능 한정으로 정정)
- `a63f4351` unit 8 방어 저작 마스크 (+ `368edbdf` 누락 `.meta`)
- `9537a91d` unit 9 광역 피해풀 진영 대칭
- `dd9cea08` unit 10 공성 승패 축 (+ `Deck_SiegeTest` 저작)
- `5f7d1ed5` unit 11 라이브 검증 → `0e0a62c4` 유클리드 정정 + 검증 결과
- (다음 커밋) 종료 문서 + 파이프라인 맵

## 선행 사실 — 적 거점은 무적이었다

units 0~6 이 끝난 시점에 «적 마음을 파괴하면?» 이라는 질문은 **파괴가 불가능해서** 성립조차 안 했다. 경로 3개 중 하나만 열려 있었다:

| 경로 | 상태 |
|---|---|
| `AttackSystem` 후보 풀(`FactionTag+Health+LocalTransform`) | ✅ 이미 거점을 담고 있었다 |
| 방어 측 `targetMask` | ❌ `(int)Faction.EnemyUnit` **리터럴** 2곳 |
| 광역 피해풀 | ❌ `WithAll<AttackUnitTag>` — 거점은 그 태그가 없다 |

광역 쪽은 **재발**이었다: `ProjectileHitSystem:95` 가 «골은 `AttackUnitTag` 가 없어서 보스 AreaBarrage 가 골에 떨어져도 안정도가 안 줄었다» 를 기록하고 방어 풀만 고쳤다. 적 풀은 그대로여서 같은 증상이 진영만 바꿔 살아 있었다.

## Implemented

- **저작 마스크 대칭** — `DefenderUnitData.targetFactions`(기본 `AnyEnemy`) + `DefenderTargetDefaults.Resolve(mask, targetAllies)`. unit 1(적)의 거울이고 같은 «0 = 미저작» 폴백. 호출처 = 배치 방어유닛 + 순찰 아군
- **`targetAllies` 는 오버라이드로 존치** — 승격하면 기존 힐러 에셋의 `targetAllies: 1` 이 죽고 새 이니셜라이저(`AnyEnemy`)가 이긴다. 그리고 아군 타게팅을 `AnyDefender` 로 넓히면 `IncomingHeal` 버퍼 없는 거점이 후보에 들어 ECB playback 에서 던진다
- **`IsHostileInstinct` 술어** — 배치 배제가 `EnemyInstinct` 리터럴 대신 「방어 진영이 아닌 본능」. 상수도 `HostileInstinctPlacementPadding` 으로 개명
- **광역 피해자 풀 한 벌 + 진영 비트 필터** — `GoalTowerTag` 특례 은퇴, 스냅샷 2벌→1벌. 범위는 **TileAoe 한정**(splash·bounce·경로 스윕은 기존 «적 유닛 풀만» 의도 유지)
- **승패 4축** — 적 마음 축(`_enemyCoreMax > 0`) 신설. 판정은 `CheckEnemyCoreDestroyed`, 잔여 집계는 `SyncGoalStability` 의 기존 순회에 얹음
- **타이머 축 비교식 하나가 두 모드를 통합** — `_goalStability >= _enemyCoreCurrent`. 적 축 비활성이면 적 잔여 0 이라 항상 참 = 기존 `victory_timeout` 동치
- **공성 전용 덱 저작** — `Deck_SiegeTest`(유출 축 off · HP 800 = 적 마음 SO) → `devEntries[2].deck`. 맵 엔트리가 자기 덱을 들고 오므로 코드 분기 0
- **`MapDocumentPool.OnValidate`** — 두 마음 HP 어긋남 경고(문서와 덱을 둘 다 아는 유일한 자리)
- **`EnemyCoreCurrent`/`EnemyCoreMax`** 공개 — `GoalStabilityCurrent/Max` 대칭

## Key Files

`Data/DefenderUnitData.cs` · `Battle/Combat/DefenderTargetDefaults.cs`(신설) · `Battle/Combat/Projectile/ProjectileHitSystem.cs`(victim 풀) · `Bridge/BattleBridge.cs`(`_enemyCoreMax`/`CheckEnemyCoreDestroyed`/`CheckTimer`/`SyncGoalStability`) · `Data/StructurePlacement.cs`(`IsHostileInstinct`) · `Data/MapGrid/MapDocumentPool.cs`(OnValidate)
테스트: `AuthoredTargetMaskTests`(unit 1 과 같은 파일 — 8개 추가) · `GoalProjectileTests`(3개 추가) · PlayMode `StructureLivePlayTest.SiegeMap_DefendersBreakEnemyCore_AndCoreDeathWins`

## Verified

- EditMode **2060 / 실패 0 / 의도적 스킵 3** — 기준선 2049 에서 +11
- PlayMode **5/5** — 공성 승패 · 공성 스폰 파생 · 본능 우회 · 골 안정도 · 엔드리스 스모크(뒤 3개 = 침략 맵 무회귀)
- 콘솔 실질 에러 0
- **손저작 GUID 와이어링이 라이브에서 검증됐다** — `EnemyCoreMax == GoalStabilityMax == 800` 단정 통과 = `Deck_SiegeTest` 가 실제로 로드됨(미연결이면 레거시 1000 vs 800 으로 실패). Unity 임포트가 손저작 YAML 을 한 줄도 다시 쓰지 않았다
- 저격 도달 로그 증거: 저격수 (15,17) 사거리 6 → 본능 (15,12) 거리 5

## Notes — 되돌리면 안 되는 것

- **`targetAllies` 를 마스크로 승격하지 말 것** (에셋 마이그레이션 없이는 힐러가 적을 때린다). 아군 타게팅은 `DefenderUnit` **단독** — 넓히면 크래시
- **`HazardCastState.targetMask`(`BattleBridge:6223`) 는 이 축이 아니다** — «누구를 때리나» 가 아니라 «장판을 어디에 깔까» 의 조준점
- **광역 풀을 두 벌로 되돌리지 말 것** — 이어 붙이면 중복 제거가 없어 광역 1발이 골을 2번 때린다(goal-stability 가 그렇게 실패했다)
- **splash·bounce·경로 스윕을 같이 넓히지 말 것** — 모든 호밍 투사체의 거동이 바뀐다(범위 제약: 승패 공식 외 게임플레이 불변)
- **`MapMode` 를 런타임에서 읽지 말 것**(계약 15). 축 조건은 「저작된 상한 > 0」이지 「엔티티가 없다」가 아니다 — 후자면 침략 맵이 첫 프레임에 승리한다
- **`CheckEnemyCoreDestroyed` 는 `SyncGoalStability` 다음**이어야 한다. Sync 안에 넣으면 조건부 `return` 에 걸려 붕괴 프레임에 건너뛰어지고, 이 순서가 같은 프레임 패배의 우선권을 만든다
- **테스트 픽스처에 `FactionTag` 를 빠뜨리지 말 것** — 광역 풀이 그것으로 진영을 가른다. 프로덕션 스폰 5경로 전부 붙인다

## Follow-up

- ⚠ **적 본능의 사거리 vs 배치 배제 여유 — 의도 확인 필요.** 배제 여유(체비셰프 4)가 본능 사거리(4)와 같아서 **본능이 아무도 못 쏜다**(최근접 합법 칸 거리 5). 반대로 저격수는 그 칸에서 본능을 깎는다 → «저격으로 안전하게 철거되는 벽». 값의 문제이고 방향 3개(README 후속 후보)
- 커버리지 공백: 「공성 맵 3분 만료 + 방어 < 적 → 패배」 경로는 자동 검증이 없다(3분 소요). `EnemyCoreCurrent`/`GoalStabilityCurrent` 공개로 검산은 가능
- README 후속 후보: 본능 광역 투사체(범위 제약으로 되돌림) · `targetAllies` bool 은퇴 · `GoalCollapsedEventsSingleton` 재정의 · 거점 수복 · 중립 콘텐츠 · footprint 일반화
