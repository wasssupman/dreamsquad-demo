using UnityEngine;

namespace Wassup.Data.BattleView
{
    // battle-core-rebuild unit 5a — 바닥에 떨어지는 것들의 뷰. 옛 브리지의 `pickup*` 5 ·
    // `resignationView*` 2 의 새 주인.
    //
    // 소비자(픽업·사직서 풀)는 **조각 C(unit 6)** 에서 선다 — 그 사건이 코어에 아직 없다.
    // 값만 먼저 이사시키는 이유는 91행 귀속을 이 unit 에서 닫기 때문이다(완료 기준 4).
    [CreateAssetMenu(menuName = "Wassup/BattleView/Pickup View Config", fileName = "PickupViewConfig")]
    public sealed class PickupViewConfig : ScriptableObject
    {
        [Header("픽업")]
        [SerializeField] private GameObject pickupPrefab;
        [Tooltip("픽업이 뜨는 높이(월드).")]
        [SerializeField] private float pickupHeight = 0.3f;
        [SerializeField, Min(0.01f)] private float pickupModelScale = 1f;
        [Tooltip("모델 자체의 밑동 보정(월드).")]
        [SerializeField] private float pickupModelBaseY = 0f;
        [Tooltip("비우면 프리팹 머티리얼 그대로.")]
        [SerializeField] private Material pickupOverrideMaterial;

        [Header("사직서")]
        [SerializeField] private GameObject resignationPrefab;
        [SerializeField] private float resignationHeight = 0.2f;

        public GameObject PickupPrefab => pickupPrefab;
        public float PickupHeight => pickupHeight;
        public float PickupModelScale => pickupModelScale;
        public float PickupModelBaseY => pickupModelBaseY;
        public Material PickupOverrideMaterial => pickupOverrideMaterial;
        public GameObject ResignationPrefab => resignationPrefab;
        public float ResignationHeight => resignationHeight;
    }
}
