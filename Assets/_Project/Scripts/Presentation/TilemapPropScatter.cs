using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Wassup.Presentation
{
    // Tilemap 모드 전용 배경 장식: 지면이 칠해진 뒤 Deco(=grassTile) 셀 위에
    // props 타일맵으로 나무/바위/풀/꽃을 결정론적으로 흩뿌린다. 뷰 계층 장식 전용 —
    // 게임 로직 판정 아님(GetTile 은 Deco 셀 식별용 읽기). props 내용은 런타임 생성.
    public class TilemapPropScatter : MonoBehaviour
    {
        [SerializeField] private Tilemap groundTilemap;   // 빌드 완료 감지 + Deco 셀 식별
        [SerializeField] private Tilemap propsTilemap;     // 출력 대상(상위 레이어)
        [SerializeField] private TileBase decoGroundTile;  // 이 타일이 깔린 셀 = Deco(장식 가능)
        [SerializeField] private TileBase[] propTiles;     // 가중치는 weights 와 1:1
        [SerializeField] private float[] weights;          // propTiles 와 동일 길이. 합으로 정규화
        [SerializeField, Range(0f, 1f)] private float density = 0.5f;
        [SerializeField] private int seedSalt = 12345;

        private IEnumerator Start()
        {
            // 지면이 칠해질 때까지 대기 (TilemapMapView.Initialize 는 BattleBridge 빌드 시 호출).
            int guard = 0;
            while ((groundTilemap == null || groundTilemap.GetUsedTilesCount() == 0) && guard++ < 600)
                yield return null;
            yield return null; // 한 프레임 더: 페인트 안정화
            Scatter();
        }

        public void Scatter()
        {
            if (propsTilemap == null || groundTilemap == null || decoGroundTile == null) return;
            if (propTiles == null || propTiles.Length == 0) return;

            propsTilemap.ClearAllTiles();

            float total = 0f;
            if (weights != null)
                for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0f, weights[i]);
            bool useWeights = weights != null && weights.Length == propTiles.Length && total > 0f;

            var bounds = groundTilemap.cellBounds;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (groundTilemap.GetTile(cell) != decoGroundTile) continue;
                if (Hash01(x, y, seedSalt) >= density) continue;

                int idx = PickIndex(Hash01(x, y, seedSalt + 7919), useWeights, total);
                propsTilemap.SetTile(cell, propTiles[idx]);
            }
        }

        private int PickIndex(float roll, bool useWeights, float total)
        {
            if (!useWeights)
                return Mathf.Clamp(Mathf.FloorToInt(roll * propTiles.Length), 0, propTiles.Length - 1);

            float t = roll * total;
            for (int i = 0; i < weights.Length; i++)
            {
                t -= Mathf.Max(0f, weights[i]);
                if (t <= 0f) return i;
            }
            return propTiles.Length - 1;
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)(x * 374761393);
                h ^= (uint)(y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h & 0x00ffffff) / 16777215f;
            }
        }
    }
}
