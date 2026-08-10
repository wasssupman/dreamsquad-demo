# unit 11 — 공성 라이브 검증

## 목적

**공성 판이 실제로 승패를 내는가**를 라이브에서 확인한다. units 8~10 은 각각 EditMode 로 자기 계약을 고정하지만, 세 개가 한 판에서 맞물려 도는 것은 여기서만 관측된다.

구 후속 후보 «본능 발사의 라이브 검증»(A-L4)도 여기서 결론이 난다 — 다만 **흡수가 아니라 사유 확정**이다. 구현 중 실측한 사실:

> **적 본능은 배치된 방어유닛을 사거리에 담을 수 없다.** 9×9 배치 배제가 체비셰프 ≤ 4 를 닫으므로 최근접 합법 칸은 체비셰프 5(직선 거리 5, tileSize 1)이고, `Structure_TestInstinct.attackRange = 4` 다. 4 < 5 → **정의상 미도달**. 본능은 `targetFactions = DefenderUnit` 단독이라 다른 대상도 없다.

즉 이 저작에서 본능은 «부술 수 있는 벽» 이고 **한 발도 쏘지 않는다.** 미검증이 아니라 저작 수치의 결과이며, 발사 로직 자체는 EditMode 가 실 `AttackSystem` 으로 이미 고정했다(`ArmedInstinct_FiresProjectileRequest…`). 밸런스 판단은 README 후속 후보로 이관한다 — 코드가 아니라 값(배제 여유 3 또는 사거리 4)의 문제다.

반대 방향(**방어유닛이 적 본능을 깎는다**)은 사거리 6인 저격수면 체비셰프 5에서 닿는다. 테스트가 **런타임** `AttackState.range` 로 도달 여부를 판정해 닿을 때만 단정한다 — SO 값은 스탯 시트가 덮을 수 있어서다.

## 변경 대상

- `Assets/_Project/Tests/PlayMode/StructureLivePlayTest.cs` — 공성 판 테스트 추가

## 구현

배치 하네스는 **이미 공개 API 로 있다** — `bridge.CanPlaceDefenderAt(x, y, unit, out _)` + `bridge.PlaceDefenderAs(x, y, unit)`. PlayMode 6개 파일이 이미 그 패턴을 쓴다(`ActiveAllyZoneTest` 의 `TryFindPlaceableCell` 이 가장 가까운 선례). reflection 불필요.

시나리오 — `MapDocument_SiegeTest`(dev[2], 적 마음 (15,25) · 적 본능 (15,12) · 방어 골 (15,0)):

1. 부팅 → `BeginPlacement` → 풀 주입(`SetDefenderPool`) + 코스트 확보
2. **적 마음 인접 8칸을 채운다.** 적 마음은 1칸만 닫히므로(footprint 1) 인접 배치가 가능하다 — 그 사실 자체가 「공성이 새 메커닉 없이 성립한다」의 관측치다. 배치 0 이면 계약이 깨진 것이므로 실패로 잡는다
3. 본능에 가장 가까운 합법 칸(체비셰프 오름차순 탐색)에 저격수
4. `StartBattle` → **축 활성**(`EnemyCoreMax > 0`) + **저작 대칭**(`EnemyCoreMax == GoalStabilityMax`)
5. `EnemyCoreCurrent` 가 **줄어드는 것**을 관측 (unit 8 이 라이브에서 발효 — 이전엔 영구 무적)
6. 저격수가 사거리 안이면 적 본능 `Health` 감소도 관측(조건부 — 위 사유)
7. 적 마음 잔여를 0 으로 만들어 **승리 판정** 확인 (`GamePhase.Result`)

⚠ **7 을 800 HP 실그라인딩으로 재지 않는다.** 축이 재는 것은 «잔여 0 → 승리» 이고 피해 출처는 그 판정과 무관하다. 라이브 피해 경로는 5 가 이미 증명하므로, 둘을 묶으면 검증이 늘지 않고 소요 시간과 흔들림만 늘어난다. 치명 피해를 `IncomingDamage` 에 직접 넣는다.

⚠ 체력·사거리 값은 전부 **읽어서** 쓴다(하드코딩 금지) — 적 마음 HP 는 덱/SO 저작이고 사거리는 스탯 시트가 덮을 수 있다. 셀 좌표만 저작물 상수다.

## 완료 기준

- PlayMode 신설 1개 그린 — 위 5단계 전부
- 기존 PlayMode 골 3종(`GoalStabilityTest` · `EndlessModeSmokeTest` · `StructureLivePlayTest` 기존 2개) 그린 = 침략 맵 무회귀
- EditMode 전량 무회귀 (기준선 2049 / 실패 0 / 의도적 스킵 3)
- 콘솔 에러 0 (`read_console`)
