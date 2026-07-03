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
