# unit 1 — 규칙 경로에 방향 바인딩 발사 개통

## 목적

배치 스킬(`OnPlace × EmitProjectilePattern`)이 **방향으로 쏘는 탄**을 발사할 수 있게 한다.
지금은 못 한다: arm 이 패턴 템플릿을 **그대로 복사**하는데, 방향 바인딩 탄이 요구하는
**원점·방향·최대거리**는 트리거 시점에 스냅샷해야 하는 값이라 템플릿에 비어 있다
(캐논은 적 조준이라 이 축이 필요 없어 드러나지 않았다).

이 unit 은 단독으로는 아무 동작도 안 한다(캐논의 unit 0·1 선례). unit 3 이 첫 소비자다.

## 변경 대상

- **신규** `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/OnPlaceFireAim.cs` — 순수 함수
- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — `EmitProjectilePattern` arm
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ResolveForwardBurstDirection` 이 순수 함수를 쓰도록
- **신규** `Assets/_Project/Tests/EditMode/OnPlaceFireAimTests.cs`

## 구현

**① 방향 결정 규칙은 새로 만들지 않는다.** 이미 결정돼 있다(사용자 결정 2026-08-15,
`defender-on-place-skills` unit 4): **조준(`DeployedFacing`)이 있으면 그 방향, 없으면 가장 가까운
적 방향.** 조준이 최근접보다 세다. 지금 이 규칙은 브리지 레거시 경로에만 있어 순수 함수로 뽑고
두 경로가 같은 것을 보게 한다(제약 10 의 «2+ 호출처», 타겟팅은 sim-critical).

```
bool TryResolve(float2 hostXZ, bool hasAim, float2 aim,
                NativeArray<float2> candidateXZ, out float2 dir, out int pickedIndex)
```
- **동률은 pool index 가 깬다**(`<` 비교 + 선택 index 반환). 좌표가 연속이라 도달은 드물지만 이
  프로젝트는 같은 자리에서 두 번 결정론을 못박았다(자장가의 월드 거리, fan-out 의 row-major).
- ⚠ **브리지 호출처는 `false` 일 때 기존 폴백 `(0,1)` 을 유지한다.** 레거시는 후보가 전부 중심에
  겹친 퇴화에서도 발사했다 — 갈아타며 그 동작까지 바꾸면 무회귀가 아니다.

**② 후보는 «사거리 안 합법 후보» 다.** `BuildEnemyPool` 은 `WithAll<AttackUnitTag, LocalTransform>`
전부라 **필터가 하나도 없다**(이 풀을 쓰는 기존 arm 셋은 전부 소비 지점에서 자기 필터를 건다).
안 걸면 **시체나 비행 적이 총구를 가져가고** 산탄은 통행 층 게이트에 막혀 아무도 못 맞힌다.
사거리를 안 걸면 맵 반대편 적 하나가 방향을 정해 "허공에 쏜다"가 된다.

후보 조건 4개 — 전부 이 시스템이 이미 가진 lookup, 추가 쿼리 0:
`DeadTag` 없음 · `UltimateLeapState` 없음 · `PlacementLayers.CanTarget(host 공격 통행 층,
후보 `PathFollowState.traversalLayers)` 통과 · host 로부터 **`slot.tileRange` 안**.
레거시 경로가 이미 같은 계약이다(`CollectEnemiesInTileRange`).

**③ 이미 조준된 템플릿은 건드리지 않는다** (구현 중 발견 — 기존 테스트가 잡았다).
이 arm 은 **방향을 미리 실어 보내는 소비자와 공유**된다: 무타겟 방향 패턴은 원점·방향·사거리를
스냅샷한 템플릿으로 오고 **후보가 0이어도 발사한다**. `ProjectileEmitterIntegrationTests
.DirectionPattern_FiresWithoutTargets_OnTriggerFrame_WithSnapshotPayload` 가 그 계약을 고정하고
있으며, 특히 「host 현재 위치로 snapshot 원점을 덮으면 안 된다」를 명시 단정한다.
→ **«방향이 비어 있다» 를 «아직 조준되지 않았다» 의 표식으로 쓴다.** 유닛 능력 bake 는
origin·direction·maxDistance 를 하나도 채우지 않으므로 이 표식이 정확히 배치 스킬 경로만 고른다.
초판은 무조건 덮어써서 그 테스트를 깼다 — 되돌리지 말 것.

**④ 방향 바인딩이고 조준이 비었을 때만 스냅샷한다.** `MovementBinding.Of(...)` 가 `Direction` 인 인스턴스에 한해
push 직전 템플릿을 채운다: `origin` = host 위치 · `direction` = ①의 결과(`false` 면 **push 하지
않는다**) · `maxDistance` = `slot.tileRange × tileSize`(평타·Move·Hit 과 같은 월드 단위).
**`damage` 는 채우지 않는다** — emitter 가 `req.damage = order.damage`(패턴 SO 값)로 항상 덮는다.
적 후보 풀은 **조준이 없을 때만** 빌드한다(지연 플래그 공유, Dispose 는 기존 경로가 처리).

## 완료 기준

- [ ] `OnPlaceFireAimTests` — 조준 우선 / 최근접 / 둘 다 없으면 false / 겹친 후보(거리 0) 배제 /
      **동률 시 낮은 index 승** 5케이스 통과
- [ ] 순수 함수가 Burst 안에서 합법이다(arm 은 `[BurstCompile]` ISystem). 브리지(managed)는 Temp
      `NativeArray` 로 넘긴다 — 규칙이 두 벌 되는 것보다 이 변환이 싸다
- [ ] EditMode 전체 초록. 적 조준 패턴(캐논)과 **스냅샷 방향 패턴**은 이 분기를 타지 않는다
- [ ] 브리지 레거시 전방 발사(머신거너·마크스맨·피어서·스나이퍼)가 **그대로 동작**한다
- [ ] 이 unit 만으로는 화면에 아무 변화가 없다(정상)
