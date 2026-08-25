# 9 — 조준 은퇴: 가까운 적 직격 (rework)

## 목적

폭탄맨의 **배치 2스텝(놓기 → 방향 지정)을 없애고**, 다른 방어유닛과 같은
「사거리 안 가장 가까운 적을 노린다」로 바꾼다. 폭탄은 그 적이 **서 있는 칸**에
떨어진다. 이 단위 후 `DeployedFacing`·착지 후보 4칸 UI 는 폭탄맨에서 사라진다.

```
(전) 배치 → 방향 탭 → 쿨다운마다 무조건 방향×3칸에 낙하
(후) 배치 → 쿨다운마다 사거리(3타일) 안 최근접 적의 칸에 낙하 (없으면 안 던짐)
```

사용자 결정 2026-08-21: 착지 = **최근접 적의 칸 직격**, 사거리 = **3타일**.

## 변경 대상

- `Assets/_Project/Scripts/Data/Abilities/BombThrowAbility.cs` — `landingTiles` 삭제 · `RequiresFacing => false`
- `Assets/_Project/Scripts/Battle/Combat/BombLauncherState.cs` — `landingTiles` 삭제
- `Assets/_Project/Scripts/Battle/Combat/BombLanding.cs` — **삭제**(소비처 0)
- `Assets/_Project/Tests/EditMode/BombLandingTests.cs` — **삭제**
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 폭탄 분기 재작성 · `PickFallbackTarget` 진영 마스크 파라미터화
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — bake 게이트 · `PaintLandingCells` 삭제 · 프리뷰/조준 폭탄 분기 삭제
- `Assets/_Project/Scripts/UI/DirectionAimController.cs` — 폭탄 range 분기 삭제
- `Assets/_Project/Data/Abilities/Ability_Bomb_BombMan.asset` · `Data/Defenders/Defender_BombMan.asset`(attackRange 5→3)
- `Assets/_Project/Tests/EditMode/AttackSystemUnifiedLoopTests.cs` — 폭탄 헬퍼/테스트 재작성

## 구현

- **사거리의 집은 `attackRange` 하나다.** `landingTiles`(구 착지 거리)는 은퇴하고
  에셋 값을 5→3 으로 내린다. 두 필드가 같은 숫자를 갖는 순간 갈리며, 배치 프리뷰
  (네모 사거리)·시트 밸런스 knob·시뮬이 전부 이 한 값을 읽어야 한다.
- **타겟 선정 = 기존 `PickFallbackTarget`**(→`NearestTargeting.SelectNearest`).
  진영 필터가 `EnemyUnit` 하드코딩이었으므로 `factionMask` 파라미터를 열고
  폭탄은 `AttackState.targetMask`(= 에셋 `targetFactions`, 적 거점 포함)를 넘긴다.
  기존 두 호출부(니들 폴백·캐스트 드레인)는 `(int)Faction.EnemyUnit` 을 넘겨 무변경.
- **착지 칸 = 그 적의 현재 칸**(`WorldToCell` → `CellToWorldCenter`). 발사 시점
  스냅샷이라 적이 걸어 나가면 빗나간다 — 폭탄의 성질이며 유도 아님(계약 1 유지).
- **blind bombardment 폐기**: 사거리 안에 적이 없으면 던지지 않고 **쿨다운도
  리셋하지 않는다**(만료 상태 대기 → 적이 들어온 프레임에 즉시 투척). 소환사의
  닫힌 게이트와 같은 규율.
- **dc 사건 지점은 그대로** — 「폭탄이 실제로 손을 떠난 프레임 = 1카운트」(계약
  attack-decoupling 2). 못 던지는 사유가 off-grid 에서 **적 없음**으로 바뀔 뿐이다.
- `DcApplicability` **무변경**: `HostProvidesTarget(BombThrow)=false` 유지. 카드
  부착 가능 범위를 넓히는 것은 이 단위의 요청이 아니다(후속 후보).

## 완료 기준

- [x] compile 0 에러.
- [x] EditMode 회귀 없음. 신규/개작 단언:
  - 사거리 안 최근접 적의 칸으로 `impact` 가 잡힌다(먼 적 아님).
  - 사거리 밖에만 적이 있으면 발사 0 + dc 카운터 0.
- [x] (Play) 폭탄맨을 놓으면 **방향 지정 없이 즉시** 전투에 참여한다. 배치 프리뷰는
  다른 유닛과 같은 네모 사거리(3타일). 적이 사거리에 들어오면 그 자리에 폭탄이
  굴러가 터지고, 적이 없으면 던지지 않는다.
- [ ] **시트 미반영**: `Defender_BombMan.attackRange` 5→3 · `desc` 문안(「지정 방향으로」→
  「가까운 적에게」). 시트가 정본이라 에셋만 고치면 로비 진입 임포트가 되돌린다.

확인 2026-08-22 · 신규 단언 2건(최근접 적 칸 직격 · 사거리 밖이면 발사 0 + 쿨다운 대기) green.
Play 에서 조준 페이즈 없이 드래그 한 번으로 배치되고 즉시 전투에 참여하는 것을 확인했다.
