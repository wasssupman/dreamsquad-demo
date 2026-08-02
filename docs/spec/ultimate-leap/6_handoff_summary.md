# 6 — Handoff Summary

## Commit

| 해시 | 제목 |
|---|---|
| `69ade30b` | docs — leap-flight-state + ultimate-leap spec 신설 |
| `a8e62138` | leap-flight-state unit 0 — `LeapFlight` 태그 + 시뮬 게이트 |
| `e4514d60` | leap-flight-state unit 1 — 브리지가 비행 창을 여닫는다 |
| `46a2df32` | unit 0 — payload kind + `UltimateLeapState` |
| `d0c2521b` | unit 1 — 발동 arm + `UltimateLeapSystem` |
| `92063b29` | unit 2 — 피격·타겟팅 완전 차단 |
| `9628f365` | unit 3 — 이탈/강습 연출 채널 |
| `78760f0d` | unit 4 — 착지 예고 빨간 타일 |
| (이 커밋) | unit 5 — 에셋 배선 + CLAUDE.md 채널 등재 |

## Implemented

- **`LeapFlight`**(Combat 태그) — 공격·자기주도 이동 불가, **피격 가능**. 일반 도약(브리지 창)과
  궁극기(sim 시퀀스)가 공유한다. 부수효과로 `flight-lift-feel` 의 "보스 착지점 드리프트" 해소.
- **`UltimateLeapState`**(Combat) — 존재 = 판 밖. 무적 축만 소유하고 잠금은 `LeapFlight` 가 담당하는
  **레이어 분리**. 두 컴포넌트는 함께 붙고 함께 떨어진다.
- **`DcPayloadKind.UltimateLeap`(=18)** — 신규 슬롯 필드 0. 기존 필드 재사용:
  `duration`=예고 초 · `magnitude`=밀집 탐색 반경 · `tileRange`=착지 링 상한 ·
  `slamDamage`/`slamTileRange`=착지 피해와 예고 범위.
- **발동**(`HealthThresholdSystem`) — 밀집 최대 셀을 발동 프레임에 고정. **시퀀스**
  (`UltimateLeapSystem`, Battle 도메인) — 카운트다운 → `BlinkRequest`(기존 seam) → 슬램 캐리어 →
  상태 해제. `[UpdateBefore(BlinkApplySystem)]` 로 같은 틱 착지.
- **차단** — 후보 풀 3곳(`AttackSystem` 타겟·`ProjectileMoveSystem` 재조준·`ProjectileHitSystem`
  aoe/bounce) + `DamageApplicationSystem` 버퍼 드랍.
- **연출** — 신규 채널 `UltimateLeapVisualEvents`(Ascend/Descend 2종) + `BattleBridge.UltimateLeap`
  partial. 상승 → 화면 밖 대기 → 강하 → 슬램 VFX·스쿼시.
- **예고** — `TilemapMapView` 전용 타일맵(`SetTelegraphCells`), 빨강 tint.
- 짱쎈놈 슬롯: `fraction 0.7`(체력 30%, 생존당 1회) · 예고 2초 · 슬램 100 / 반경 2.

## Key Files

- `Battle/Combat/LeapFlight.cs` · `UltimateLeapState.cs` · `UltimateLeapSystem.cs` · `UltimateLeapVisualEvents.cs`
- `Battle/Combat/HealthThresholdSystem.cs` (발동 arm, `TryResolveBlinkDest` out cell)
- `Battle/Units/DamageApplicationSystem.cs` (버퍼 드랍)
- `Bridge/BattleBridge.UltimateLeap.cs` (연출·예고) · `BattleBridge.BossLeap.cs` (일반 도약 창)
- `Core/TilemapMapView.cs` (예고 타일맵)
- `Data/Enemies/Enemy_Boss_Jjangssen.asset` (슬롯 4개)

## Verified

- **EditMode 1809 중 1807 통과 · 실패 0 · skip 2**(기존 Ignored). 각 유닛마다 실행.
- compile 클린(유닛마다 `read_console` CS 에러 0).
- 에셋 배선을 Unity 런타임이 인식하는 것까지 확인(`SerializedObject` 로 슬롯 4개 판독:
  `[3] trigger.kind=5 fraction=0.7 | payload.kind=18 dur=2 slamDmg=100 slamR=2 proj=Projectile_JjangssenLeap`).
- 구현 중 테스트가 설계 오류 1건을 반려 — `DcApplicability` 의 `Unclassified` 오용
  (`EvaluateMechanic_IsTotalOverAllKindAndArchetypePairs`).

**미검증(남김)**: **사용자 Play 감각 확인 미완** — unit 5 의 체크리스트 9항목이 전부 대기 상태다.
PlayMode e2e 미작성.

## Notes (되돌리면 안 되는 의도)

- **`LeapFlight` 를 `DamageApplicationSystem`·타겟 후보 풀에 넣지 말 것.** 넣으면 일반 도약이
  비행 내내 무적이 된다. 무적은 `UltimateLeapState` 축 전용이다.
- **피격 차단은 쿼리 제외가 아니라 버퍼 `Clear()`.** 쿼리로 빼면 2초치 피해가 적립됐다가 착지
  프레임에 터져 무적이 아니라 지연 폭탄이 된다.
- **착지점은 발동 프레임 고정.** 예고는 약속이다.
- **브리지는 예고 시간을 복제하지 않는다.** 상승 후 Descend 신호를 기다린다 — 복제하면 두 시계가 갈린다.
- **예고 타일은 전용 타일맵.** `_rangeTilemap` 은 매 호출이 `ClearPlacementRange()` 로 시작해서,
  예고 중 드래그 배치가 서로를 지운다.
- **드레인은 `SyncMonoUnitViews` 앞.** 뒤면 이탈·착지 프레임에 sim 좌표가 한 프레임씩 샌다.
- 슬램은 `projectileDataIndex` 필수 — 없으면 bake 가 loud 거절한다(없으면 피해까지 사라진다).

## Follow-up

1. **사용자 Play 감각 확인** — `5_asset_wiring.md` 체크리스트 9항목.
2. **밸런스 튜닝** — 슬램 100 / 반경 2 / 예고 2초는 전부 초안값. 특히 예고 2초가 회피에 충분한지,
   30% 발동이 너무 늦지 않은지.
3. **`liftScaleMax` 상호작용** — 이탈 상승이 `flight-lift-feel` 의 lift 확대를 그대로 타므로
   화면 밖으로 나가기 전 크게 부푼다. 과하면 상승 구간만 예외 처리가 필요할 수 있다.
4. **예고 펄스 케이던스** — 잔여 시간에 비례해 빨라지는 점멸(README 후속 후보).
5. **다른 보스 재사용** — 에셋 슬롯만으로 성립하는지가 이 설계의 진짜 검증.
6. **행동트리 재검토 트리거** — 보스 스킬이 우선순위 경쟁·인터럽트·다페이즈를 요구하면 규칙
   목록의 한계다. 그전까지 BT 도입은 제약 8 위반.
