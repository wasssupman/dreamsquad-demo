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

        // unit 9 — 판 위에 서 있는 동안 **스스로 닳는** 체력(초당). 0 = 안 닳음.
        //
        // unit 7 이 걷어낸 「설치물 수명」의 자리를 대신하되 성격이 다르다. 시한은 **두 번째
        // 죽음 경로**를 만들어 폭발을 시계 사건으로 바꿨고, 그래서 남은 시간을 알리는 별도
        // 장치(퓨즈 틴트)가 필요했다. 노후화는 그냥 **피해**라서 죽음도 폭발도 「부서짐」
        // 하나로 나가고, 남은 시간은 이미 있는 체력 바가 그대로 말한다(unit 8).
        //
        // 저작 감각: `maxHp / healthDecayPerSec` = 아무도 안 때렸을 때의 수명(초).
        [Tooltip("초당 스스로 닳는 체력. 0 = 안 닳음. maxHp 를 이 값으로 나누면 무방비 수명(초).")]
        [Min(0f)]
        public float healthDecayPerSec;

        [Header("Destruction VFX")]
        [Tooltip("Optional. If set, BattleBridge spawns this on destruction.")]
        public GameObject destructionVfxPrefab;

        // bomb-barrel-on-place unit 0 — 「부서지면 터진다」. 적이 부숴 체력이 0 이 되는 순간
        // 자기 칸을 중심으로 광역 피해를 낸다(unit 7 이후 이것이 **유일한** 폭발 계기다). 폭발 자체는 신규 로직이 아니라 기존 칸 광역
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

        // unit 8 — 머리 위 체력 바가 뜨는 높이(월드 단위). 「언제 터지나」를 이제 시계가
        // 아니라 **남은 체력**이 말하므로, 그 값이 화면에 보여야 한다.
        //
        // 설치물마다 덩치가 다르다(1칸 배럴 ↔ 3x3 방벽). 그래서 높이는 브리지의 공용
        // 상수가 아니라 **설치물 자신의 저작값**이다 — 골 거점이 `goalOverheadHeight` 를
        // 따로 갖는 것과 같은 이유.
        //
        // 기본 0 = **바 없음**. 기존 길막 설치물(바위 2종)은 이 키가 없어 0 으로 떨어지므로
        // 무회귀다 — `explodeDamage` 와 같은 형태의 옵트인이다.
        [Header("Health Bar")]
        [Tooltip("머리 위 체력 바 높이(월드 단위). 0 이하 = 바 없음.")]
        [Min(0f)]
        public float overheadHeight;
    }
}
