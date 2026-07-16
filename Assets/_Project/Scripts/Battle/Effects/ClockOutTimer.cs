using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // season-gimmick-clockout unit 2 — 배치 defender 퇴근 카운트다운 (Effects 소유).
    // ClockOutSystem 이 running 중 활성 defender 에 lazy-attach(FatigueAccrual 전례) — 스폰 경로
    // 무수정. elapsed 가 clockOutSeconds 를 넘으면 배치 타일에 사직서 스폰 + 치명 피해로 퇴근.
    public struct ClockOutTimer : IComponentData
    {
        public float elapsed;
    }
}
