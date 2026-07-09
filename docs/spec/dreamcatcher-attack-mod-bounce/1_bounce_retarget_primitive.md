# 1 — BounceRetarget 순수함수 + EditMode

## 목적

재타겟 선정을 Burst-호환 static 순수함수로 고정한다. ImpactSystem 이 이미 들고 있는 aoe 스냅샷(entities/transforms 배열)을 입력으로 받으므로 신규 쿼리가 없다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Combat/Projectile/BounceRetarget.cs`
- 신규: `Assets/_Project/Tests/EditMode/BounceRetargetTests.cs`

## 구현

```csharp
public static int FindNext(
    float3 hitPos, int excludeIndex,
    NativeArray<float3> positions,
    int tileRange, float tileSize, int2 gridSize, float3 origin)
// 반환: positions 인덱스, 없으면 -1.
// 규칙: excludeIndex(직전 히트 대상의 인덱스) skip, hitPos 셀 기준 Chebyshev
// tileRange 이내(TileAoe.IsInTileRange 재사용), 그중 XZ sq 거리 최근접.
// 동률은 낮은 인덱스(스냅샷 순서 — 결정론). tileRange <= 0 → -1.
```

- **아키텍처-중립 (사용자 확정 2026-07-09)**: 시그니처는 `Entity`/`LocalTransform` 대신 **float3 위치 + int 인덱스**만 받는다. 순수 기하 — 월드/시스템/프레임 무의존, EditMode 에서 `NativeArray<float3>` 만으로 테스트. ECS 글루(unit 2)가 `target` Entity → 인덱스, `aoeTransforms` → positions 매핑을 담당.
- 셀 변환은 `GridMath.WorldToCell`, 반경 판정은 기존 `TileAoe.IsInTileRange` 재사용 (재구현 금지).
- 죽음 판정은 하지 않는다 — positions 스냅샷은 ImpactSystem 의 기존 aoe 풀(살아있는 AttackUnit)과 동일 소스. 같은 프레임 사망 예정 대상에 튕는 것은 TileAoe 와 같은 기존 의미론.

## 완료 기준

- [x] EditMode: 최근접 선택 / exclude 제외 / 반경 밖 -1 / 후보 0개 -1 / 동률 시 낮은 인덱스 / tileRange 0 → -1
- [x] 컴파일 통과, 기존 무회귀

완료 확인: 2026-07-09 — `BounceRetargetTests` 6/6 통과(월드 없는 순수 EditMode), 컴파일 클린, code-review(low) 0건. 시그니처를 Entity→float3/인덱스로 다듬어 아키텍처-중립 확정. 이 문서와 동일 커밋.
