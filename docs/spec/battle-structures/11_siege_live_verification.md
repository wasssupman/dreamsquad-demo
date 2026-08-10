# unit 11 — 공성 라이브 검증

## 목적

**공성 판이 실제로 승패를 내는가**를 라이브에서 확인한다. units 8~10 은 각각 EditMode 로 자기 계약을 고정하지만, 세 개가 한 판에서 맞물려 도는 것은 여기서만 관측된다.

동시에 구 후속 후보 «본능 발사의 라이브 검증»(A-L4)을 흡수한다. 그것이 미검증으로 남았던 이유는 «방어유닛을 배치하지 않아 본능이 쏠 대상이 없다» 였고, 이 unit 은 배치를 하므로 자연히 커버된다. 게다가 unit 8 로 **반대 방향도** 열려 «상호 교전»(내가 거점을 깎고 거점이 나를 쏜다)이 한 판에서 관측된다.

## 변경 대상

- `Assets/_Project/Tests/PlayMode/StructureLivePlayTest.cs` — 공성 판 테스트 추가

## 구현

배치 하네스는 **이미 공개 API 로 있다** — `bridge.CanPlaceDefenderAt(x, y, unit, out _)` + `bridge.PlaceDefenderAs(x, y, unit)`. PlayMode 6개 파일이 이미 그 패턴을 쓴다(`ActiveAllyZoneTest` 의 `TryFindPlaceableCell` 이 가장 가까운 선례). reflection 불필요.

시나리오 — `MapDocument_SiegeTest`(dev[2], 적 마음 (15,25) · 적 본능 (15,12) · 방어 골 (15,0)):

1. 부팅 → `BeginPlacement`
2. **적 마음 인접 칸에 방어유닛을 배치한다.** 적 마음은 `CloseCellLayers` 로 1칸만 닫히므로(footprint 1) 인접 배치가 가능하다 — 그 사실 자체가 「공성이 새 메커닉 없이 성립한다」의 관측치다
3. `StartBattle` → 적 마음 `Health` 가 **줄어드는 것**을 관측 (unit 8 이 라이브에서 발효)
4. 같은 판에서 **적 본능이 방어유닛을 쏘는 것**을 관측 (unit 5 의 미검증분 — `AttackState` 쿨다운 진행 또는 방어유닛 피해)
5. 적 마음 HP 0 까지 태워 **승리 판정** 확인 (`GamePhase.Result` + win)

⚠ 3~5 는 시간이 걸린다. 기존 테스트의 `TimeoutSec = 90f` 프레임 루프 패턴을 따르고, 필요하면 유닛을 여러 기 배치해 DPS 를 올린다(적 마음 HP 는 덱 저작이라 테스트가 그 값을 읽어 기대치를 계산한다 — 하드코딩 금지).

## 완료 기준

- PlayMode 신설 1개 그린 — 위 5단계 전부
- 기존 PlayMode 골 3종(`GoalStabilityTest` · `EndlessModeSmokeTest` · `StructureLivePlayTest` 기존 2개) 그린 = 침략 맵 무회귀
- EditMode 전량 무회귀 (기준선 2049 / 실패 0 / 의도적 스킵 3)
- 콘솔 에러 0 (`read_console`)
