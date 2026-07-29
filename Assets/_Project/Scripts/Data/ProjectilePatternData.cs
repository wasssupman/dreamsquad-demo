using UnityEngine;

namespace Wassup.Data
{
    [System.Serializable]
    public struct ProjectileShotStep
    {
        [Tooltip("minAngleDeg~maxAngleDeg 안의 위치. 0=min, 0.5=center, 1=max.")]
        [Range(0f, 1f)]
        public float directionT;

        [Tooltip("직전 탄 이후 발사 간격(초). 첫 탄의 값은 무시되고 trigger 프레임에 즉시 발사.")]
        [Min(0f)]
        public float intervalAfterPreviousSec;
    }

    // projectile-emission-pattern unit 0 — 발사(emission) 명세. 한 발의 명세는
    // ProjectileData(barrel)가 이미 갖고 있고, 이 SO 는 그 위에 "누구를 겨냥해 ·
    // 어떤 순서·방향·간격으로" 를 얹는다. 두 SO 가 곱해져 조합 공간을 만든다.
    //
    // 계약 3 — 탄의 성질은 barrel 이 소유하고 여기서 복제하지 않는다:
    // impactTileRange · splashRadius · arcHeight · pierceCount · dropHeight ·
    // 베지어 스윙 파라미터는 전부 ProjectileData 에 있다. 같은 값이 두 곳에
    // 생기면 어느 쪽이 이기는지 매번 판단해야 한다.
    //
    // 계약 4 — 반복 주기는 트리거가 소유한다. shots 는 "한 번의 발사 안의
    // 연발" 이다(PeriodicTimer(0.5s) × 패턴(1발) 이 0.5초 간격 사격이고,
    // 패턴이 스스로 반복하지 않는다).
    [CreateAssetMenu(fileName = "Pattern", menuName = "Wassup/ProjectilePattern", order = 14)]
    public class ProjectilePatternData : ScriptableObject
    {
        public static int MaxShotCount
            => default(Unity.Collections.FixedList128Bytes<PatternShotSpec>).Capacity;

        public string id;

        [Header("Barrel (탄 1발 명세 — 궤적·효과·비주얼 전부 여기)")]
        public ProjectileData barrel;

        [Tooltip("이 패턴이 쏘는 탄의 데미지. 시전자 damageMul 은 적용하지 않는다(카드/스킬 magnitude 컨벤션).")]
        [Min(0f)]
        public float damage = 10f;

        [Header("Selection")]
        [Tooltip("맵 전체 후보 중 누구를 겨냥하나. RoundRobin=순회, DeterministicShuffle=결정론 랜덤.")]
        public PatternSelectionRule selection = PatternSelectionRule.RoundRobin;

        [Header("Shot Sequence (한 번의 trigger 안의 개별 탄)")]
        [Tooltip("base direction 기준 최소/최대 회전각. directionT 로 이 범위를 보간한다.")]
        public float minAngleDeg;
        public float maxAngleDeg;
        [Tooltip("트리거당 탄 목록. 순서가 발사 순서이며 최대 15발.")]
        public ProjectileShotStep[] shots =
        {
            new ProjectileShotStep { directionT = 0.5f, intervalAfterPreviousSec = 0f },
        };
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
        public bool TryToSpec(int barrelDataIndex, out PatternSpec spec)
        {
            spec = default;
            var runtimeShots = default(Unity.Collections.FixedList128Bytes<PatternShotSpec>);
            if (shots == null || shots.Length == 0 || shots.Length > MaxShotCount
                || minAngleDeg > maxAngleDeg)
            {
                return false;
            }

            for (int i = 0; i < shots.Length; i++)
            {
                runtimeShots.Add(new PatternShotSpec
                {
                    directionT = Mathf.Clamp01(shots[i].directionT),
                    intervalAfterPreviousSec = Mathf.Max(0f, shots[i].intervalAfterPreviousSec),
                });
            }

            spec = new PatternSpec
            {
                barrelDataIndex = barrelDataIndex,
                damage = damage,
                selection = selection,
                minAngleDeg = minAngleDeg,
                maxAngleDeg = maxAngleDeg,
                shots = runtimeShots,
                reselectPerShot = reselectPerShot,
                telegraphSec = Mathf.Max(0f, telegraphSec),
            };
            return true;
        }
    }
}
