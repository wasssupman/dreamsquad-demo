using System.Collections.Generic;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.BattleView;
using Wassup.Presentation;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5a — 거점 프랍. 옛 `BattleBridge.SpawnStructureViews` 의 후계다.
    //
    // ⚠ **유닛 뷰 풀이 아니다.** 거점 프랍은 **맵 수명**이라 배치 페이즈부터 서 있고, 판이
    // 시작돼 거점 개체가 생기기 **전에** 보인다. 옛 전투도 같은 판단이었다 — 프랍은 맵 빌드가
    // 소유하고 엔티티는 판이 소유한다. 그래서 여기에는 개체 id 도 사건 구독도 없다.
    //
    // 체력 게이지가 여기 없는 것도 그 때문이다: 프랍이 서 있는 동안 체력은 아직 없다.
    [DisallowMultipleComponent]
    public sealed class CoreStructurePropLayer : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private BlobShadowConfig _blobShadow;

        private readonly List<GameObject> _props = new List<GameObject>();

        /// <summary>세워진 프랍 수. 「저작 = 화면」 대조의 오른쪽 항이다.</summary>
        public int PropCount => _props.Count;

        private void Start() => Rebuild();

        private void OnDestroy() => Clear();

        /// <summary>멱등 — 재빌드마다 정확히 한 벌.</summary>
        public void Rebuild()
        {
            Clear();
            if (_driver == null) return;

            var entries = _driver.StageStructures;
            float tileToWorld = _driver.TileSize;

            for (int i = 0; i < entries.Count; i++)
            {
                var s = entries[i];
                if (s.data == null || s.data.viewPrefab == null) continue;

                // 방어 마음은 골(`Goals`)이 정본이라 거점 프랍을 세우지 않는다 — 세우면 골이
                // 두 벌이 된다. 코어의 `FieldPrepPhase` 가 같은 필터를 쓴다.
                var faction = StructurePlacements.DeriveFaction(s.side, s.data.kind);
                if (faction == Wassup.Skills.Faction.DefenderCore) continue;

                var simCenter = CellCenter(s.cell, tileToWorld);
                var prop = Instantiate(s.data.viewPrefab,
                    (Vector3)Wassup.Core.BoardSpace.ToView(simCenter), Quaternion.identity, transform);
                prop.transform.localScale *= s.data.viewScale;
                prop.name = $"Structure_{s.data.displayName}_{s.cell.x}_{s.cell.y}";
                _props.Add(prop);

                // 거점도 **자기 몸을 그림자로 말한다**(그림자 = 상시 몸). 지름 = 2 × 판정 반경이고
                // 그 반경은 코어가 쓰는 것과 **같은 함수**에서 온다 — 같은 수를 다른 모양으로
                // 적으면 둘이 형제로 보이지 않는다.
                // live:false — 거점은 안 움직이고, 붕괴 주저앉음에서 부모와 함께 줄어드는 편이 맞다.
                if (_blobShadow != null && _blobShadow.Sprite != null)
                    BlobShadow.Attach(prop.transform, _blobShadow.Sprite,
                        2f * StructurePlacements.BodyRadiusOf(faction) * tileToWorld,
                        _blobShadow.Color, _blobShadow.Lift, BoardSortOrder.ShadowOrder);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _props.Count; i++)
                if (_props[i] != null) Destroy(_props[i]);
            _props.Clear();
        }

        // 셀 → 셀 중심 sim 좌표. 정수배가 셀 중심이라는 격자 규약 그대로다.
        private static Unity.Mathematics.float3 CellCenter(Vector2Int cell, float tileSize)
            => new Unity.Mathematics.float3(cell.x * tileSize, 0f, cell.y * tileSize);
    }
}
