using Spine.Unity;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "PropData", menuName = "Wassup/PropData", order = 20)]
    public class PropData : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;

        [Header("Placement")]
        [Min(1)] public int footprintX = 1;
        [Min(1)] public int footprintY = 1;
        [Tooltip("비주얼 가림 풋프린트(틸트 누운 방향 기준). x=폭, y=depth(+y 셀 수). 1x1=가림 없음, 1x4=큰 나무. 배치 시 이 범위가 플레이 타일 침범하면 거부(occlusionAware). footprint(sim 발자국)와 별개.")]
        public Vector2Int visualFootprint = new Vector2Int(1, 1);
        [Min(0)] public int placementWeight = 10;
        [Min(0)] public int minDistanceCells;
        public Vector3 visualOffset;
        [Min(0.01f)] public float visualScale = 1f;
        public BoardDecorAnchorType[] preferredAnchorTypes;
        [Min(0)] public int preferredRegionSizeMin;
        [Min(0)] public int clusterRadius = 1;
        [Min(1)] public int clusterCount = 1;
        [Range(0f, 1f)] public float clusterProbability;
        [Min(0f)] public float rotationJitterDegrees;
        [Range(0f, 0.8f)] public float scaleJitter;
        public Vector2Int pathProximityRange = new Vector2Int(0, 255);
        public Vector2Int borderProximityRange = new Vector2Int(0, 255);

        [Header("Adjacency (same-category spread)")]
        [Tooltip("같은 category 끼리 인접 회피용 그룹. 비우면 회피 없음. 예: flower 변종들을 모두 \"flower\".")]
        public string category;
        [Min(0)]
        [Tooltip("같은 category 프랍과의 최소 Chebyshev 간격(셀). 0=off. 예: 2 → 같은 카테고리끼리 8-이웃 인접 금지.")]
        public int sameCategoryMinDistanceCells;
        [Tooltip("원경(외곽 터레인 링) 배치에서 제외. 멀리서 안 보이는 작은 프랍(꽃 등)에 체크.")]
        public bool excludeFromDistantRing;
        [Tooltip("원경(외곽 링) 전용 배치 가중치. <0 = placementWeight 사용. 침엽수림처럼 링 분포를 보드와 따로 잡을 때(나무↑·돌↓).")]
        public float distantRingWeight = -1f;

        [Header("Generated Prefab")]
        public GameObject prefab;

        [Header("Sprite Billboard")]
        public Sprite sprite;
        public Texture2D sourceTexture;
        public Color spriteColor = Color.white;
        public int sortingOrder;

        [Header("Spine Billboard")]
        public SkeletonDataAsset skeletonDataAsset;
        public string spineSkinName;
        public string idleAnimation = "idle";

        [Header("Billboard")]
        public PropBillboardMode billboardMode = PropBillboardMode.FullCamera;
        [Tooltip("Tilted 모드의 월드 X 틸트각(도). 캐릭터 레이어와 독립. 기본 0=틸트 없음.")]
        public float tiltAngle;

        public Vector2Int Footprint => new Vector2Int(Mathf.Max(1, footprintX), Mathf.Max(1, footprintY));
        public bool HasSpriteVisual => sprite != null || sourceTexture != null;
        public bool HasSpineVisual => skeletonDataAsset != null;
    }

    public enum PropBillboardMode
    {
        FullCamera,
        YAxis,
        None,
        Tilted,
    }
}
