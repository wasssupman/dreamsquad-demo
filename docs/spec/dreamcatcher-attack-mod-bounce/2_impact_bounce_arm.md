# 2 — ImpactSystem 튕김 분기 + bridge 필드 피핑

## 목적

SingleSplash 해결 후 조건부 생존/재비행을 넣는다. 계약 1(후처리 원칙): 기존 해결 로직은 한 줄도 옮기지 않는다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs`
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnProjectile` 에서 request→state 로 bounce 3필드 복사

## 구현

ProjectileHitSystem:

- **주의: 루프 하단의 `ecb.DestroyEntity(entity)` 는 switch 밖에 있고 TileAoe/default payload 의 파괴도 담당한다.** DestroyEntity 를 SingleSplash case 안으로 옮기면 다른 payload 가 파괴되지 않는 leak — 대신 SingleSplash case 의 재타겟 성공 지점에서만 `bool bounced = true` 를 세우고, 하단에서 `if (!bounced) ecb.DestroyEntity(entity)` 로 감싼다:
  - `payload == SingleSplash && bounceRemaining > 0 && 직전 해결이 유효 타겟이었음` 일 때 `BounceRetarget.FindNext(targetPos, target, aoeEntities, aoeTransforms, bounceTileRange, ...)` 호출.
  - 인덱스 ≥ 0 → **생존**: `ProjectileState` 를 ecb.SetComponent 로 갱신 — `target = 새 대상`, `impactReached = false`, `bounceRemaining--`, `damage *= bounceDamageMul`. outputs 버퍼가 있으면 Damage-kind magnitude 도 `*= bounceDamageMul` (RW BufferLookup — 계약 3).
  - 인덱스 < 0 또는 조건 미충족 → 기존대로 `DestroyEntity`.
- `impactReached` 를 MoveSystem 이 세우는 방식 확인 후, 리셋만으로 재비행이 성립하는지 (홈잉 arm 은 target 추적이므로 origin 갱신 불필요 — 현 위치에서 이어 날아감) 검증. 타겟이 비행 중 죽으면 기존 홈잉 소실 규칙(파괴) 그대로.
- ProjectileState 쓰기는 ImpactSystem 소유 확장 (계약 7). MoveSystem 무변경.

BattleBridge `SpawnProjectile`: `state.bounceRemaining/bounceTileRange/bounceDamageMul = req.*` 복사 한 줄씩. request 기본값 0 이므로 기존 스폰 전부 무영향.

## 주의 (되돌리지 말 것)

- **`FindNext` 는 float3 위치를 받는다 — `NativeArray<LocalTransform>` 로 바꿔 aoePositions 할당을 없애지 말 것.** 순수함수의 아키텍처-중립(Entity/Transforms 무참조)이 Temp 할당 1개보다 우선(사용자 확정). aoePositions 는 매 OnUpdate Temp 할당 후 Dispose — 의도된 비용.
- **감쇠는 state.damage 와 outputs Damage magnitude 둘 다** ×mul. outputs 있으면 데미지 소스는 outputs 지만, splash/fallback 이 state.damage 를 쓰므로 양쪽 유지가 맞다(이중 차감 아님 — 리뷰 확인).

## 완료 기준

- [x] 컴파일 + 기존 EditMode 무회귀 (bounce 필드 0 경로) — EditMode 588 그린
- [x] 프리미티브 동작: bounceRemaining 주입 투사체가 히트 후 재비행 (라이브 시뮬 중 8발 arrow×bounce4 가 적 6→1 킬 = 튕김 없인 불가능한 attrition). ecs-reviewer 6/6 CONFIRMED SAFE (RefRO+ecb.SetComponent / outputs RW / 이중감쇠 없음 / excludeIndex 항상 유효 / Temp 무누수 / bounce=0 무회귀).
- 시각 e2e(재비행 궤적 육안)는 **unit 4 카드 e2e 로 이관** — 에디터 비포커스 시 sim frame 정지라 자율 라이브 스냅샷 불가(projectile-trajectory-payload unit 6 이 authored 유닛 필요로 Play e2e 를 이관한 것과 동형).

완료 확인: 2026-07-09 — 컴파일 클린, EditMode 588 그린, ecs-reviewer CRITICAL/HIGH 0. 이 문서와 동일 커밋.
