using UnityEngine;

namespace Wassup.Battle.Effects
{
    [CreateAssetMenu(menuName = "Wassup/Hazards/Blocking Hazard SO", fileName = "Hazard_Blocking_New")]
    public class BlockingHazardSO : ScriptableObject
    {
        [Header("Visual")]
        [Tooltip("Spawned by BattleBridge as the visual representation.")]
        public GameObject visualPrefab;

        [Tooltip("Optional particle prefab spawned when the hazard visual is bound.")]
        public GameObject spawnVfxPrefab;

        [Header("Shape")]
        [Tooltip("Cell shape sampled at spawn. Reuses HazardShapeSampler.")]
        public HazardShape shape = HazardShape.Square3x3;

        [Header("Combat")]
        [Min(1f)]
        public float maxHp = 100f;

        // bomb-barrel-on-place unit 1 — 아무도 안 때려도 사라지는 시한. 0 이하 = 무한
        // (기존 길막 설치물 전부가 여기 해당하므로 무회귀).
        //
        // 만료는 **파괴와 같은 문**으로 나간다(`DeadTag`) — 그래야 unit 0 의 폭발이
        // 「적이 부쉈다」와 「시간이 다했다」 둘 다를 규칙 하나로 덮는다.
        [Tooltip("설치물 수명(초). 0 이하 = 무한. 만료는 파괴와 같은 경로로 나간다(폭발 포함).")]
        [Min(0f)]
        public float lifetime;

        [Header("Destruction VFX")]
        [Tooltip("Optional. If set, BattleBridge spawns this on destruction.")]
        public GameObject destructionVfxPrefab;

        // bomb-barrel-on-place unit 0 — 「부서지면 터진다」. 죽는 순간(체력 0 또는 수명 만료)
        // 자기 칸을 중심으로 광역 피해를 낸다. 폭발 자체는 신규 로직이 아니라 기존 칸 광역
        // 착탄(TileAoe)이라 여기 값은 그 요청의 파라미터다.
        //
        // damage 0 = 폭발 없음. 기존 길막 설치물 에셋은 전부 0 으로 역직렬화되므로 무회귀.
        [Header("Explode On Death (bomb-barrel-on-place unit 0)")]
        [Tooltip("죽을 때 낼 광역 피해. 0 이면 폭발하지 않는다(기존 길막 설치물 기본값).")]
        [Min(0f)]
        public float explodeDamage;

        [Tooltip("폭발 반경(Chebyshev 타일). 0 = 자기 칸만.")]
        [Min(0)]
        public int explodeTileRange = 1;

        [Tooltip("가까운 순 최대 타격 수. 0 = 무제한.")]
        [Min(0)]
        public int explodeTargetCap;

        // 폭발을 «해결» 하는 즉발 탄. 연출은 destructionVfxPrefab 이 이미 내므로 비주얼이
        // 없어도 되지만, 탄이 아예 없으면 index 가 0 으로 떨어져 **엉뚱한 탄의 비주얼이 한
        // 프레임 번쩍인다**(조용한 오작동). explodeDamage > 0 이면 반드시 배선한다.
        [Tooltip("폭발 해결용 즉발 탄. explodeDamage > 0 이면 필수.")]
        public Wassup.Data.ProjectileData explodeProjectile;

        // bomb-barrel-on-place unit 6 — 수명이 다해 갈수록 물드는 색. 「언제 터지나」를
        // 숫자나 게이지가 아니라 **물건 자체의 색**으로 말한다(이 프로젝트의 게이지 금지 규율).
        //
        // ⚠ 틴트는 **인스턴스별 오버라이드**여야 한다. 벤더 메시는 프랍 500여 개가 공유하는
        // 머티리얼 하나를 쓰므로, 머티리얼을 직접 물들이면 맵의 모든 프랍이 같이 빨개진다.
        // 적용은 `BlockingHazardPresenter` 가 MaterialPropertyBlock 으로 한다.
        [Header("Fuse Tint (수명 경과 색)")]
        [Tooltip("수명이 다해 갈수록 이 색으로 물든다. 흰색(기본) = 물들지 않음.")]
        public Color fuseTintColor = Color.white;

        [Tooltip("물드는 곡선. 1 = 선형, 값이 클수록 막판에 몰아서 빨개진다.")]
        [Min(0.1f)]
        public float fuseTintExponent = 2.5f;
    }
}
