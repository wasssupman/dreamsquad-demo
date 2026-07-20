# 1. 데이터 계약 — SO 필드 · DeployedFacing · SpawnRequest 확장 · Bridge API

## 목적

feature 전체가 쓰는 데이터 계약을 한 번에 깐다. 이 단계는 **컴파일 안전 뼈대만** — 신규 필드/컴포넌트를 소비하는 로직은 이후 작업 단위에서 붙는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs`
- `Assets/_Project/Scripts/Data/ProjectileData.cs`
- `Assets/_Project/Scripts/Battle/Units/DeployedFacing.cs` (신규)
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/MovementKind.cs` · `PayloadKind.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

**DefenderUnitData** 신규 필드 (기존 공격 필드 옆, tooltip 필수):
- `bool directionalAttack` — true = 배치 시 공격방향 페이즈 진입 + 영구 방향 고정 유닛.
- `int shotCount = 1` — 트리거당 발수. 1 = 현행 단발.
- `float shotIntervalSec = 0f` — 발 간 간격. >0 이면 버스트(시간차), 0 이면 동프레임.
- `float spreadAngleDeg = 0f` — 총 확산각. >0 이면 발마다 부채꼴 분배.

**ProjectileData**:
- flightMode enum 에 `Directional` 추가 (기존 Homing/BallisticToCell 뒤).
- `int pierceCount = 1` — 관통 예산. 1 = 첫 피격 소멸, N = N기 히트 후 소멸.

**DeployedFacing** (Units 맥락, 신규):
```
public struct DeployedFacing : IComponentData { public int2 value; } // cardinal 단위 벡터
```
쓰기는 BattleBridge(배치 확정) 1회뿐, 이후 불변. Combat 은 읽기 전용 — 맥락 간 통신 규칙 준수.

**ProjectileSpawnRequest**: `float2 direction`(발사 방향 단위 벡터) + `float maxDistance`(월드 단위 최대 비행 거리) 필드 추가. Homing/Ballistic request 는 zero 로 두면 무시된다(기존 경로 무영향).

**MovementKind / PayloadKind**: enum 값 `DirectionalLinear` / `PathHit` 추가. 이 단계에서는 switch arm 미구현 — 각 시스템의 default(무시/로그) 경로에 떨어져도 컴파일·기존 동작 무해.

**BattleBridge**:
- `ActivateDeployedDefender` 에 facing 전달 오버로드(또는 nullable 파라미터) 추가 — directionalAttack 유닛이면 `DeployedFacing` 을 엔티티에 기록. 기존 호출부는 무변경 시그니처 유지.
- `ResolveProjectileAxes` 에 `Directional → (DirectionalLinear, PathHit)` 매핑 추가.
- 유닛 스폰 시 `DefenderUnitData` 다연발 필드를 AttackState 확장 필드로 복사하는 건 unit 4 에서(AttackState 변경과 함께).

## 완료 기준

- [x] compile 통과, 기존 EditMode/PlayMode 테스트 회귀 없음
- [x] 기존 유닛(비 directional) 배치·전투 Play 스모크 무변화 — 신규 필드 기본값이 현행 동작과 동일함을 확인

확인 2026-07-17 — compile 클린 · EditMode 896/894 green(실패 0) · 신규 필드/enum/오버로드 소비처 0 으로 기존 경로 구조적 무변화(Play 스모크 대체 근거, 리뷰어 grep 교차 확인) · 리뷰 통과(MED 1 = 커밋 격리, 준수) · 커밋 98cf377b
