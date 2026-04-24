# 26. Sort Order Unification

## 목적

프랍 / Spine defender / enemy (또는 fallback defender) 가 **모두 MonoBehaviour Renderer 체계**에 들어온 상태에서, 공통 sortingOrder 공식을 유틸로 단일화하고 매 프레임 갱신 훅을 붙인다. 이로써 audit V-010 ("캐릭터가 프랍에 의해 무조건 가려짐") 해소.

## 전제

- `24` Enemy Mono 이관 완료.
- `25` fallback defender Mono 수렴 완료.
- 모든 캐릭터 view 가 `Renderer.sortingOrder` 를 지원하는 GameObject 형태임.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — 공식 유틸
- `Assets/_Project/Scripts/Core/MapView.cs::ApplyPropSorting` — 유틸 호출로 치환
- `Assets/_Project/Scripts/Presentation/SpineDefenderView.cs` — sortingOrder 갱신 훅
- `Assets/_Project/Scripts/Presentation/EnemyView.cs` (또는 `QuadUnitView`) — sortingOrder 갱신 훅
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — view sync 루프에서 sortingOrder 계산 호출
- `Assets/_Project/Scripts/Presentation/PropBillboard.cs` — 초기 Awake sortingOrder 세팅 제거 (이중 source 제거)
- `Assets/_Project/Tests/EditMode/BoardSortOrderTests.cs` (신규)
- `docs/spec/board-visualization/audit/VISUAL_AUDIT.md` — V-010 해소 기록

## 공통 공식

```csharp
public static class BoardSortOrder
{
    public const int CharacterOffset = 1; // 캐릭터가 같은 셀의 프랍 앞
    public const int HealthBarOffset = 2;

    public static int Compute(int2 gridSize, int cellX, int cellY, int offset = 0)
        => (gridSize.y - cellY) * 10 + cellX + offset;

    public static int ComputeFromWorld(int2 gridSize, Vector3 world, float tileSize, int offset = 0)
    {
        int cx = Mathf.RoundToInt(world.x / tileSize);
        int cy = Mathf.RoundToInt(world.z / tileSize); // board 는 XZ plane
        return Compute(gridSize, cx, cy, offset);
    }
}
```

- 프랍: `offset = 0`
- 캐릭터 (defender, enemy): `offset = CharacterOffset`
- 체력바: `offset = HealthBarOffset`

## 구현 가이드

### Step 1. `BoardSortOrder` 유틸 도입

위 코드. 별도 namespace `Wassup.Presentation` 또는 `Wassup.Data`.

### Step 2. `MapView.ApplyPropSorting` 치환

기존:
```csharp
int order = prop.sortingOrder + (plan.gridSize.y - placement.y) * 10 + placement.x;
```

→
```csharp
int order = prop.sortingOrder + BoardSortOrder.Compute(plan.gridSize, placement.x, placement.y);
```

### Step 3. 캐릭터 view 에 갱신 훅

`SpineDefenderView` 와 `EnemyView` 양쪽에:

```csharp
public void UpdateSortingOrder(int2 gridSize, float tileSize)
{
    int order = BoardSortOrder.ComputeFromWorld(
        gridSize, transform.position, tileSize, BoardSortOrder.CharacterOffset);
    foreach (var r in GetComponentsInChildren<Renderer>(true))
        r.sortingOrder = order;
}
```

Spine 의 `SkeletonAnimation` 은 내부에 `MeshRenderer` 가 여러 개일 수 있으므로 `GetComponentsInChildren<Renderer>(true)` 로 일괄 설정.

### Step 4. BattleBridge view sync 루프

매 프레임 ECS → view 위치 sync 시점에 sortingOrder 도 함께 갱신:

```csharp
foreach (entity in livingCharacters)
{
    var pos = _em.GetComponentData<LocalTransform>(entity).Position;
    if (spineDefenderPool.TryGet(entity, out var spine))
    {
        spine.transform.position = new Vector3(pos.x, pos.y, pos.z);
        spine.UpdateSortingOrder(plan.gridSize, _tileSize);
    }
    else if (defenderFallbackPool.TryGet(entity, out var quad))
    {
        quad.transform.position = ...;
        quad.UpdateSortingOrder(plan.gridSize, _tileSize);
    }
}
// enemies 도 동일 루프
```

### Step 5. `PropBillboard.Awake` 의 초기 sortingOrder 제거

현재:
```csharp
spriteRenderer.sortingOrder = data.sortingOrder;
```

이 초기값은 이후 `MapView.ApplyPropSorting` 이 덮어쓰므로 **무의미 + 혼란 요인**. 제거 또는 `PropData.sortingOrder` 를 "offset only" 로 재해석하고 유틸에서 흡수.

### Step 6. Health bar 처리

Health bar 는 현재 RenderMesh 기반. 이번 spec 에서는 건드리지 않고 **별도 후속 spec 으로 미룸**. 단 Mono 로 이관할 때 `HealthBarOffset` 을 쓰면 자연스럽게 편입 가능.

### Step 7. 테스트

- `BoardSortOrderTests`:
  - 같은 셀에서 프랍/캐릭터 offset 비교
  - gridSize 경계값
  - 월드→셀 변환 round-off
- PlayMode (선택): 캐릭터가 프랍 뒤/앞으로 자연스럽게 전환되는지 integration

## 완료 기준

- `BoardSortOrder` 유틸이 존재하고 프랍/캐릭터 양쪽에서 호출됨.
- 공식 중복 (MapView 안의 raw 계산 + 캐릭터에서 별도 계산) 이 없음 (grep 확인).
- Play 에서 이동 중 프랍 앞뒤 전환이 자연스러움 (캐릭터 y 좌표 < 프랍 y 좌표 → 앞).
- 해적/궁수 screenshot 재캡처에서 sorting artifact 해소.
- `VISUAL_AUDIT.md` 의 V-010 이 **해소 상태** 로 기록 + 커밋 해시.
- Unity console error 0.

## 주의

- 24/25 완료 전 본 spec 시작 금지. Mono view 가 없으면 `Renderer.sortingOrder` 를 호출할 대상이 없음.
- `PropData.sortingOrder` 기본값 0 유지. 튜닝 필요 시 offset 으로만 사용.
- Transparency Sort Mode (`Graphics Settings`) 가 기본값 (Default) 이어야 `sortingOrder` 가 먼저 작동. `Custom Axis` 로 되어 있으면 world position 기반 정렬이 우선하므로 결과가 달라짐 — 설정 확인.
- health bar sort 는 후속 spec. 이 단계에선 오염시키지 않는다.

확인 일자: 2026-04-24 / 커밋 해시: PENDING
