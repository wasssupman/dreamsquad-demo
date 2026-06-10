# 2 — BattleBridge grid↔world 변환에 origin 적용

## 목적

BattleBridge 가 수행하는 모든 셀↔월드 변환과 엔티티 스폰이 `_boardOrigin` 을 반영하도록 한다. 이 단계에서 **디펜더 배치 위치가 실제로 옮겨진 맵 위로 이동**한다(유닛이 화면에 나타나기 시작).

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

핵심 헬퍼 한 곳만 고치면 대부분 전파된다 (BattleBridge.cs:1239):

```csharp
private float3 GridToWorldCenter(Vector2Int cell, float y = 0f)
    => _boardOrigin + new float3(cell.x * tileSize, y, cell.y * tileSize);
```

`GridToWorldCenter` 를 경유하는 호출(1380/1419/1453/1454/1834/1890/2487/2585/2729/2767/3032/3176)은 자동으로 origin 반영됨 — 디펜더 스폰(2585 `CreateDefenderEntity`), 적 스폰(3176), 스킬 VFX 등.

WorldToCell 경로 (origin 을 named arg 로 전달):

- `InTileRange`(1250): `GridMath.WorldToCell(worldPos, tileSize, grid, origin: _boardOrigin)`
- `DebugWorldToCell`(1259): 동일하게 `origin: _boardOrigin` 추가 → 입력 MonoBehaviour 가 이 헬퍼만 쓰면 origin 일관 적용
- 1675/1676 의 srcCell/tgtCell 변환도 origin 추가

직접 월드 좌표를 만드는 비-헬퍼 지점 점검:

- 1799 `new float3(req.origin.x, spawnHeight, req.origin.z)` — `req.origin` 이 이미 월드 좌표(이벤트로 들어온 값)인지 셀인지 확인. 셀이면 origin 더함, 이미 월드면 그대로.
- 2736 `cube.transform.position = new Vector3(worldPos.x, ...)` — worldPos 가 GridToWorldCenter 산출물이면 이미 origin 포함.

## 완료 기준

- [ ] compile green.
- [ ] MapView.transform 을 (예: x+10, z+5) 옮긴 뒤 Play → 클릭/드래그 배치한 디펜더가 **옮겨진 타일 위**에 스폰. (이 단계의 핵심 가시 검증)
- [ ] 적 스폰 위치도 옮겨진 경로 시작점에 정렬.
- [ ] origin=0(MapView 원점 유지) 시 기존과 100% 동일 동작.

> ✅ 구현/컴파일 2026-06-10 — `GridToWorldCenter` 에 `_boardOrigin` 가산(12개 호출부 자동 정렬), WorldToCell 4곳(InTileRange/DebugWorldToCell/공격로그 src·tgt)에 `origin: _boardOrigin`. `req.origin`(투사체)·cube position 은 이미 월드 좌표라 무변경. 컴파일 green, EditMode 307 passed. 가시 스폰 검증은 입력(작업 4)·이동(작업 3) 완료 후 통합 Play 로 관측. 커밋: (다음 줄)

## 주의

- 이 단계 이후에도 **이동/타겟팅 시스템(작업 3)** 이 아직 origin 미반영이면, 스폰은 맞지만 적 이동/공격 판정이 어긋날 수 있다. 3 까지 마쳐야 완결. 2→3 을 연속 검증 권장.
- `req.origin` 의 의미(셀 vs 월드)를 코드에서 반드시 확인 후 결정. 추측 금지.
