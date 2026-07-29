using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Presentation
{
    public static class BoardSortOrder
    {
        public const int CharacterOffset = 1;
        // 투사체 VFX: 유닛 order(Compute 최대 ≈ 보드행×10+열, 수백대) 보다 확실히 위,
        // 데미지 숫자(32000)·UI 아래. 근접 시 적 스프라이트에 가려지지 않게.
        public const int ProjectileOffset = 1000;
        // tilted-billboard unit 3 — 블롭 그림자: 바닥 타일맵(ground −20 / overlay −10) 위, 캐릭터(양수) 아래.
        public const int ShadowOrder = -5;
        // unit-health-display unit 2 — 적 피격 마이크로바: 캐릭터·투사체(1000) 위, 데미지 숫자(32000) 아래.
        // bg = 이 값, fill = +1.
        public const int HitBarOrder = 16000;
        // unit-health-display unit 3 — 방어유닛 타일 테두리 게이지: 바닥 데칼(그림자 −5 위, 캐릭터 양수 아래).
        public const int TileGaugeOrder = -4;
        // placement-drag-preview-polish — 드래그 프리뷰 실루엣: 배치 중 배경 프랍/유닛/투사체 위로.
        // 프랍(prop.sortingOrder + Compute)·유닛(Compute+1)·투사체(+1000) 위, UI(Canvas) 아래.
        public const int DragPreviewOrder = 20000;
        // placement-cell-snap unit 4 — 배치 확정 팝: 상승한 overlay 하이라이트(10002) 위, 드래그 프리뷰(20000) 아래.
        public const int PlacementCommitPopOrder = 12000;
        // placement-cell-snap unit 7 rev — 끈적 액체 하이라이트: 바닥 타일 하이라이트 위, 확정 팝(12000) 아래.
        // 팝은 확정 순간의 이완이라 액체보다 앞에 터져야 한다.
        public const int PlacementLiquidOrder = 11000;

        // defender-directional-volley unit 9 — 방향 지정 화살표. **보드에 그려진 것**이므로
        // "보드 레이어 < 유닛 레이어" 규칙을 따른다(스폰 예고 라인과 같은 판단):
        // 범위 타일(−12) 바로 위, overlay(−10)·그림자(−5)·유닛(양수) 아래 = 유닛이 화살표를 가린다.
        // 이전 값 11500 은 유닛 위로 떠서 조준 중 유닛을 덮었다(사용자 지적 2026-07-28, unit 5).
        public const int AimArrowOrder = -11;

        // spawn-point-alert unit 1(rev) — 스폰 예고 라인은 **바닥에 그려진 것**이다.
        // "보드 레이어 < 유닛 레이어" 규칙(TilemapMapView)에 따라 음수 대역에 둔다:
        // overlay 타일맵(−10) 위 · 블롭 그림자(−5)/타일 게이지(−4)/유닛(양수) 아래.
        // 레이어 4개(광휘/스트릭/코어/링)가 +0~+3 을 쓰므로 −9 부터 −6 까지 정확히 채운다.
        public const int SpawnAlertOrder = -9;

        // beam-ranger-defender unit 1 — 지속 빔. 벤더 프리팹은 order 0~2 로 들어오는데 그대로
        // 두면 **유닛(Compute = 수백대) 뒤에 깔린다**: 바닥(−20/−10) 위라 빈 땅 구간만 보이고
        // 유닛에 가린 구간은 끊겨 보이며, 적이 여럿이면 대부분 적 뒤에 숨어 "빔이 1개만" 처럼
        // 보인다(사용자 제보 2건의 실제 원인). 투사체(+1000) 위 · 피격바(16000) 아래에 둔다.
        // 프리팹 내부의 상대 순서(0/1/2)는 이 값에 **더해서** 보존한다.
        public const int BeamOrder = 15000;

        public static int Compute(int2 gridSize, int cellX, int cellY, int offset = 0)
            => (gridSize.y - cellY) * 10 + cellX + offset;

        public static int ComputeFromWorld(int2 gridSize, Vector3 world, float tileSize, int offset = 0)
        {
            float safeTileSize = Mathf.Max(0.0001f, tileSize);
            int cellX = Mathf.RoundToInt(world.x / safeTileSize);
            int cellY = Mathf.RoundToInt(world.z / safeTileSize);
            return Compute(gridSize, cellX, cellY, offset);
        }
    }
}
