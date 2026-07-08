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

## 완료 기준

- [ ] 컴파일 + 기존 EditMode/PlayMode 무회귀 (bounce 필드 0 경로)
- [ ] execute_code Play: state 에 bounceRemaining 수동 주입한 투사체가 히트 후 파괴되지 않고 다른 적으로 재비행 → 소진 후 파괴 (unit 3 전에 프리미티브 단독 검증)
