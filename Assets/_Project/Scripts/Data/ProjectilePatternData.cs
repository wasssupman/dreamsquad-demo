using UnityEngine;

namespace Wassup.Data
{
    // projectile-emission-pattern unit 0 — 발사(emission) 명세. 한 발의 명세는
    // ProjectileData(barrel)가 이미 갖고 있고, 이 SO 는 그 위에 "누구를 겨냥해 ·
    // 몇 발 · 어떤 간격으로" 를 얹는다. 두 SO 가 곱해져 조합 공간을 만든다.
    //
    // 계약 3 — 탄의 성질은 barrel 이 소유하고 여기서 복제하지 않는다:
    // impactTileRange · splashRadius · arcHeight · pierceCount · dropHeight ·
    // 베지어 스윙 파라미터는 전부 ProjectileData 에 있다. 같은 값이 두 곳에
    // 생기면 어느 쪽이 이기는지 매번 판단해야 한다.
    //
    // 계약 4 — 반복 주기는 트리거가 소유한다. shotCount/shotIntervalSec 는
    // "한 번의 발사 안의 연발" 이다(PeriodicTimer(0.5s) × 패턴(1발) 이 0.5초
    // 간격 사격이고, 패턴이 스스로 반복하지 않는다).
    [CreateAssetMenu(fileName = "Pattern", menuName = "Wassup/ProjectilePattern", order = 14)]
    public class ProjectilePatternData : ScriptableObject
    {
        public string id;

        [Header("Barrel (탄 1발 명세 — 궤적·효과·비주얼 전부 여기)")]
        public ProjectileData barrel;

        [Tooltip("이 패턴이 쏘는 탄의 데미지. 시전자 damageMul 은 적용하지 않는다(카드/스킬 magnitude 컨벤션).")]
        [Min(0f)]
        public float damage = 10f;

        [Header("Selection")]
        [Tooltip("맵 전체 후보 중 누구를 겨냥하나. RoundRobin=순회, DeterministicShuffle=결정론 랜덤.")]
        public PatternSelectionRule selection = PatternSelectionRule.RoundRobin;

        [Header("Schedule (한 번의 발사 안의 연발)")]
        [Min(1)]
        public int shotCount = 1;
        [Tooltip("연발 사이 간격(초). 0 = 같은 프레임에 전부.")]
        [Min(0f)]
        public float shotIntervalSec = 0f;
        [Tooltip("true = 발마다 타겟 재추첨(산개) · false = 첫 타겟에 집중.")]
        public bool reselectPerShot = false;

        [Header("Telegraph")]
        [Tooltip("착탄 지연 초(셀을 겨누는 궤적 전용). SkyFall = 낙하 예고, GrenadeToCell = 굴러가는 시간. " +
                 "0 이면 즉시 착탄 — SkyFall 패턴에서 0 은 예고 없는 폭격이라 bake 가 경고한다. " +
                 "BallisticArcToPoint 는 드레인이 거리/속도로 재산출하므로 이 값을 쓰지 않는다.")]
        [Min(0f)]
        public float telegraphSec = 0f;

        // 정의 계층 → unmanaged 미러. barrelDataIndex 는 아키텍처가 해석하는
        // 핸들이므로 호출자(bake seam)가 넘긴다 — 이 SO 는 레지스트리를 모른다.
        public PatternSpec ToSpec(int barrelDataIndex) => new PatternSpec
        {
            barrelDataIndex = barrelDataIndex,
            damage = damage,
            selection = selection,
            shotCount = Mathf.Max(1, shotCount),
            shotIntervalSec = Mathf.Max(0f, shotIntervalSec),
            reselectPerShot = reselectPerShot,
            telegraphSec = Mathf.Max(0f, telegraphSec),
        };
    }
}
