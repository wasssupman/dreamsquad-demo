using UnityEngine;

namespace Wassup.Data
{
    // season-gimmick-clockout — "집에 가도 되나요?" 기믹 (퇴근 → 사직서 → 메테오).
    // 룰1: 전투 시작 후 배치 유닛이 clockOutSeconds 지나면 배치 타일에 사직서 스폰 + 퇴근(사망).
    // 룰2: 사직서 resignationThreshold 장 모이면 소모 + Walk 타일 meteorCount 곳에 메테오 순차 낙하(적만).
    // ECS 소비는 ClockOutGimmickConfig 로 복사돼 들어간다 (BattleBridge 주입 seam).
    [CreateAssetMenu(fileName = "Gimmick_ClockOut", menuName = "Wassup/Gimmick/ClockOut", order = 42)]
    public sealed class ClockOutGimmickData : GimmickData
    {
        [Header("룰1 — 퇴근 → 사직서")]
        [Tooltip("전투 시작(running) 후 배치 유닛이 퇴근(사망)하기까지 (초)")]
        public float clockOutSeconds = 10f;

        [Header("룰2 — 사직서 → 메테오")]
        [Tooltip("메테오를 발동시키는 사직서 누적 수 (도달 시 그만큼 소모)")]
        public byte resignationThreshold = 5;
        [Tooltip("발동당 메테오 발수 (Walk 타일 임의 위치, 순차 낙하)")]
        public byte meteorCount = 3;
        [Tooltip("메테오 TileAoe 데미지")]
        public float meteorDamage = 40f;
        [Tooltip("메테오 AoE 반경 (Chebyshev 타일)")]
        public int meteorTileRange = 1;
        [Tooltip("SkyFall 텔레그래프(경고) 시간 = flightTime 기준 (초)")]
        public float meteorWarningSec = 1.2f;
        [Tooltip("순차 메테오 간 착탄 시차 (초)")]
        public float meteorStaggerSec = 0.4f;

        [Header("메테오 뷰 (unit 4 cast 시 BattleBridge 가 소비)")]
        [Tooltip("메테오 투사체 데이터(SkyFall×TileAoe). 기존 메테오 ProjectileData 재사용 가능.")]
        public ProjectileData meteorProjectile;
    }
}
