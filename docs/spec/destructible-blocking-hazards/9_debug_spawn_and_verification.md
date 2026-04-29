# Debug Spawn Entry + PlayMode Verification

**작업 구분**: 9

## 목적

본 spec 의 **feature 게이트** — 디버그 spawn 메뉴 + PlayMode 에서 사용자가 게임감 검증.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/BlockingHazardDebugMenu.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`DebugSpawnBlockingHazardAt` 헬퍼)

## 구현

### BattleBridge.DebugSpawnBlockingHazardAt

```csharp
public Entity DebugSpawnBlockingHazardAt(BlockingHazardSO so, int2 cell)
{
    if (!Application.isPlaying || _em == default) return Entity.Null;
    return SpawnBlockingHazardWithVisual(so, cell);
}
```

### BlockingHazardDebugMenu.cs

path-zone-hazards 의 `HazardDebugMenu` 패턴:
- Unity Editor 메뉴: `Wassup/Battle/Spawn Blocking Hazard (Rock)`
- 클릭 시 `Mouse.current.position` (Input System) 으로 cell 계산 → `DebugSpawnBlockingHazardAt(rockSo, cell)` 호출.
- 클릭 셀이 walkable 아니거나 충돌이면 가장 가까운 walkable cell 로 스냅 (path-zone 패턴 참조).
- 또는 fixed cell (예: `int2(3, 3)`) 사용으로 단순화.

### 적 공격 발동 조건 (prerequisite)

검증 시나리오에서 적이 hazard 공격하려면 적 SO 가 `attackDamage > 0` + 적당한 `attackRange` (≥ 1.5) 필요. **prerequisite**: 본 unit 시작 전 `Assets/_Project/Data/Enemies/` 의 기존 enemy SO 들을 점검 — `attackDamage > 0` 인 SO 가 없으면 디버그 enemy SO (`Enemy_Debug_Melee_Attacker.asset`, attackDamage=5, attackRange=1.5, attackCooldown=1.0) 한 종을 추가 작성. 본 unit 의 spawn 메뉴 / wave 시나리오는 이 enemy SO 를 사용한다.

## PlayMode 검증 시나리오

| # | 시나리오 | 기대 결과 |
|---|---|---|
| V1 | Rock_3x3 spawn 후 적 wave 진행 | 적이 rock 앞에서 멈춤 (path-block) → 자동 공격 시작 → HP bar 감소 → HP 0 시 destruction VFX + visual destroy → 다음 프레임 적이 통과 |
| V2 | Rock_3x3 + 디펜더 (rock 뒤) | 적이 rock 부수기 진행 — rock 부서지기 전엔 디펜더 사거리 안 안 들어감 (path-block 효과). 부서진 후 디펜더가 적 공격 시작 |
| V3 | Rock_3x3 + 골 cell 인접 spawn | spawn 거부 (충돌 검증) — `Entity.Null` 반환 + 경고 로그 |
| V4 | Rock_3x3 spawn 후 즉시 같은 cell 다시 spawn | 두 번째 거부 (blockedCells 충돌) |
| V5 | Rock_3x3 + path-zone Fire (같은 cell) | 둘 다 spawn 성공 — zone 과 blocking 양립 |
| V6 | 회귀: 디펜더 적 공격 / 적 디펜더 공격 (Unit 2 게이트 보강) | 동작 동일. knockback / projectile / synergy 동작 동일 |

## 로깅 (선택)

Battle JSON logger 에 hazard destruction 이벤트 추가 (path-zone-hazards 의 `dot_damage` 패턴):
- `blocking_hazard_spawn { cell, soName, maxHp }`
- `blocking_hazard_destroyed { cell, hp_at_destroy=0, attackerCount }` (optional)

cap: 세션당 200건.

## 완료 기준

- 컴파일 성공.
- Editor 메뉴 spawn 동작.
- V1~V6 모두 사용자 PlayMode 확인 통과 (게임감 + 회귀 안정성).
- 콘솔 에러/경고 0.
- 본 spec 의 검증 질문 1 (게임감) + 2 (회귀 안정성) 모두 답변됨.

검증: 2026-04-29 — `BlockingHazardDebugMenu` 및 `BattleBridge.DebugSpawnBlockingHazardAt` 구현. 골 cell 인접 클릭은 유효한 3x3 walk cell 로 스냅하도록 보강. 사용자 PlayMode 확인 완료, 콘솔 에러 없음. 커밋 미작성.
