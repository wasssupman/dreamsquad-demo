# Debug Obstacle Spawn

**작업 구분**: 9 (feature 검증 게이트)

## 목적

Obstacle entity 를 만드는 spawn API 와, Play 모드에서 사용자가 호출 가능한 디버그 진입점을 만든다. 본 단위 commit 후 큐브 동작이 처음 관측 가능하며, spec 의 두 검증 질문에 대한 사용자 manual 평가가 시작된다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- Add: `Assets/_Project/Scripts/Battle/Debug/ObstacleDebugMenu.cs` (Editor 전용, `#if UNITY_EDITOR`)

## EffectSpawner.SpawnObstacle

```csharp
public static Entity SpawnObstacle(EntityManager em, int2 cell, float3 worldPos, float lifetime)
{
    var e = em.CreateEntity();
    em.AddComponentData(e, new Obstacle
    {
        cell = cell,
        worldPosition = worldPos,
        remainingLife = lifetime,
    });
    return e;
}
```

## BattleBridge 디버그 메서드

```csharp
public Entity DebugSpawnObstacleAt(int2 cell, float lifetime = 5f)
{
    var em = World.DefaultGameObjectInjectionWorld.EntityManager;
    float3 worldPos = GridToWorldXZ(cell);  // 기존 grid math 재사용 (정확 함수명은 BattleBridge 검색)
    return EffectSpawner.SpawnObstacle(em, cell, worldPos, lifetime);
}
```

## 디버그 진입점 (택 1, 본 단위 PR 에서 1개 선택)

### 옵션 A: Editor 메뉴 (추천 — Input System 의존 0)

`ObstacleDebugMenu.cs`:
```csharp
[MenuItem("Wassup/Battle/Debug/Spawn Obstacle Under Mouse")]
static void SpawnUnderMouse()
{
    if (!Application.isPlaying) return;
    // Scene/Game view 마우스 hover 셀 계산. 기존 마우스→셀 변환 utility 재사용 (없으면 BattleBridge 의 hit-test 함수).
    int2 cell = MouseToCell();
    BattleBridge.Instance.DebugSpawnObstacleAt(cell, 5f);
}
```

### 옵션 B: 키 바인딩

`BattleDebugInput` MonoBehaviour 또는 기존 디버그 input 처리 코드에 `Q` 키 → 마우스 hover 셀 spawn.

## 시각

- 큐브 시각 표시는 *본 단위 범위 밖*. 적이 빈 공간에서 멈추는 것만으로도 feature 검증 가능.
- 시각 prefab 추가가 필요하면 후속 작업 (Presentation 측 ObstaclePresenter) 으로 분리. 본 단위 완료 기준에 포함하지 않음.

## 검증 (PlayMode, feature 게이트)

### 시나리오 1: 기본 차단 + 시간 소멸
1. 적 1마리 wave 시작, 경로 따라 진행 중.
2. 디버그 메뉴/키로 적 진행 방향 앞 1셀에 큐브 spawn (lifetime 5초).
3. 적이 큐브 셀 경계에서 멈춤. 5초 동안 정지.
4. 5초 후 큐브 사라짐 (`remainingLife <= 0` → destroy → blockedCells 갱신).
5. 적이 다시 flow 따라 진행 → 골 도달.

### 시나리오 2: 다중 적 정지
- 같은 큐브에 두 적이 도달 → 같은 셀 경계에서 둘 다 정지. (시각 겹침은 후속 후보)

### 시나리오 3: knockback × cube 상호작용 (Q6=B 검증)
- 디펜더 (`knockbackDistance > 0`) 가 적을 큐브 방향으로 밀침 → 적이 큐브 셀 안으로 박히지 않고 셀 경계에서 멈춤.

### 시나리오 4: on-place push × cube 상호작용
- 디펜더 (`onPlacePushDistance > 0`) 배치로 적을 큐브 방향으로 밀침 → 동일하게 셀 경계 정지.

## 완료 기준

- 컴파일.
- PlayMode 시나리오 1~4 사용자 확인 통과.
- 콘솔 에러/경고 0.
- 본 spec 의 검증 질문 두 가지에 대한 사용자 판정 수령:
  - (1) 게임감 만족 → spec 종료
  - (2) Slow 회귀 없음 → Unit 2 의 사후 재확인
- 종료 후 `10_handoff_summary.md` 작성 + commit. README 상단에 "상태: 완료 YYYY-MM-DD" 기재.

완료: 2026-04-29 — 2ee808d (fix: 431726c) PlayMode 시나리오 1~4 통과
