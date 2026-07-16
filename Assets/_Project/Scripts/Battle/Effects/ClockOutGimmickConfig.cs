using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // season-gimmick-clockout — "집에 가도 되나요?" 기믹 config 싱글턴.
    // ClockOutGimmickData(SO) 수치의 blittable 사본 — Burst 시스템이 SO 를 직접 만지지 않는다.
    // 존재 = 이 기믹 활성 → ClockOutSystem / ResignationThresholdSystem 이 RequireForUpdate 로 self-gate.
    // 생성/파괴: BattleBridge.CreateGimmickConfigIfActive / DestroyEcsInfrastructureEntities.
    // 메테오 ProjectileData(managed)는 여기 없다 — BattleBridge 가 cast 시 직접 읽는다(unit 4).
    public struct ClockOutGimmickConfig : IComponentData
    {
        public float clockOutSeconds;      // 전투 시작(running) 후 퇴근(사망)까지
        public byte  resignationThreshold; // 메테오를 발동시키는 사직서 누적 수 (도달 시 소모)
        public byte  meteorCount;          // 발동당 메테오 발수 (Walk 타일 임의 위치)
        public float meteorDamage;         // 메테오 TileAoe 데미지
        public int   meteorTileRange;      // 메테오 AoE 반경 (Chebyshev 타일)
        public float meteorWarningSec;     // SkyFall 텔레그래프(flightTime) 기준
        public float meteorStaggerSec;     // 순차 메테오 간 착탄 시차
    }
}
