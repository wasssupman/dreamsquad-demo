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

---

## 종료 갱신 (2026-08-02)

**사용자 Play 확인 완료.** EditMode 1815 중 1813 통과·실패 0.

추가 커밋:

| 해시 | 내용 |
|---|---|
| `8ff9e766` | 착지점 해석 실패를 조용히 넘기지 않는다(조용한 fizzle 제거) |
| `c5e5ff9f` | 예고 전용 타일 `Tile_LandingTelegraph` 신설 |
| `7132f144` | 실사용 tileSet 배선 + 폴백 loud 경고 + tint 매 프레임 + 발동 20% |

**최종 발동값: `fraction 0.8`(체력 20%, 생존당 1회).** 진동갑주(0.2)의 20% 경계와 겹쳐 같은
프레임에 함께 터진다 — "터지면서 뛰어오른다" 가 궁극기다워 의도적으로 수용했다. 분리하려면
0.79(21%)로 옮기면 된다.

**되돌리면 안 되는 것 추가 2건**

- **`telegraphTile` 은 모든 `TileSetData` 에 배선한다.** 실사용본이 `Assets/_Project/Generated/`
  아래에 있어 `Data/` 만 보면 놓친다. 미배선 시 폴백이 `placeableTile`(자체 색 회색)로 떨어져
  "색이 안 먹는다" 로 위장한다 — 그래서 폴백에 1회 loud 경고를 달았다. 경고를 지우지 말 것.
- **예고 tint 는 매 프레임 반영이다.** 생성 시 1회 대입으로 되돌리면 Play 중 인스펙터 튜닝이
  먹지 않는다(타일맵 GameObject 가 맵 리빌드까지 살아남기 때문).

**진단 교훈**: "보이는데 색만 이상하다" 를 색 문제로 단정하지 말 것. 보이는 타일맵과 안 보이는
타일맵을 **같은 프레임에 나란히 덤프**하니 `tileCount` 가 1 vs 0 이었고, 그때서야 배선 누락이
드러났다. 그 전까지 flags·스프라이트·머티리얼·정렬을 차례로 의심한 것은 전부 헛다리였다.
