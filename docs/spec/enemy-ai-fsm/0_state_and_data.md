# 0 — 상태 enum/컴포넌트 + 이동정책 데이터 plumbing

## 목적

FSM 의 토대를 깐다. `EnemyAiState` 상태 enum/컴포넌트와 이동정책 `engageMovement` 필드를 추가하되, **레거시(`aimMode`/`movePause`)와 공존**시켜 컴파일·동작이 깨지지 않게 한다. 이 단계는 행동을 바꾸지 않는다(데이터만 추가).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/EnemyAiState.cs` — enum + 컴포넌트.
- 신규 `Assets/_Project/Scripts/Data/EngageMovement.cs` (또는 `EnemyBehaviorEnums.cs` 에 추가) — `EngageMovement` enum.
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `engageMovement` 필드 추가.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (적 스폰 ~3550–3676) — `EnemyAiState` 컴포넌트 초기값 부여 + `engageMovement` bake(임시: `aimMode` 에서 파생).

## 구현

```csharp
// Battle/Combat/EnemyAiState.cs  (Combat 소유 — struct 가 컴포넌트명, 프로젝트 컨벤션)
public enum AiState : byte { Marching, Engaging, Chasing, Standoff }
public struct EnemyAiState : IComponentData { public AiState value; }
```

```csharp
// Data/EngageMovement.cs
public enum EngageMovement : byte { Halt, Advance }   // aimMode StopToAttack→Halt, MoveAndShoot→Advance
```

- `AttackUnitData`: `public EngageMovement engageMovement = EngageMovement.Halt;` 추가. `aimMode` 는 아직 남겨둔다(3b 에서 제거).
- **engageMovement 런타임 위치 (H4 확정)**: 별도 컴포넌트 만들지 않고 `EnemyBehavior`(Combat 소유) 에 `EngageMovement engageMovement` 필드를 추가한다. `EnemyBehavior` 는 `aimMode` 제거 후에도 `targetMode` 가 남아 유지되므로 자연스럽고, Movement 는 RO 로 읽어 경계 위반 없음.
- BattleBridge 적 스폰: 모든 적 엔티티에 `EnemyAiState { value = AiState.Marching }` 추가. `EnemyBehavior.engageMovement` bake 는 이 단계에선 `unitType.aimMode == MoveAndShoot ? Advance : Halt` 로 파생(SO 직접 값은 4에서 마이그레이션).

## 완료 기준

- compile 통과, 콘솔 에러 0.
- 기존 동작 변화 없음(상태 컴포넌트는 추가만, 아직 아무도 읽지 않음 — 레거시 경로 그대로 작동).
- 적 스폰 시 `EnemyAiState` 와 `EnemyBehavior.engageMovement` 가 부여됨을 EditMode 또는 reflection 으로 확인.
