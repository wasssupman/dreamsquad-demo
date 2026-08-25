using Unity.Entities;

namespace Wassup.Battle.Units
{
    // battle-sim-extraction M0 unit 1 — 매치 내 stable ID.
    //
    // `Entity.Index/Version` 은 **할당기의 산물**이다: 값이 재사용되고, 앞선 스폰·파괴
    // 이력에 따라 달라지며, 엔티티가 없는 신 sim(M1)에서는 아예 존재하지 않는다.
    // 그런데 지금 그 번호가 **시뮬 결과를 정한다** — 타겟팅 동률 승자와 발사 패턴의
    // 난수열이 거기서 나온다. 그 상태로 골든(unit 4)을 뜨면 골든이 할당기를 박제해
    // A/B parity 가 성립하지 않는다. 그래서 축을 먼저 갈아끼운다.
    //
    // 계약:
    //   · **매치 안에서 유일하고 재사용되지 않는다.** 스폰 순서대로 0,1,2… 발급.
    //   · 발급은 스폰 지점에서만. 사후에 붙이거나 고쳐 쓰지 않는다.
    //   · 매치 경계에서 0 으로 리셋한다(`EnsureQueriesAndQueues`).
    //
    // 부착 범위(unit 1): 타겟 후보가 될 수 있는 것 전부 — 즉 `FactionTag + Health +
    // LocalTransform` 아키타입(적/방어유닛/소환 순찰병/골 타워/저작 거점/길막 해저드)
    // 과 투사체. 부착하지 **않는** 것: 요청 캐리어·픽업·사직서·장판 캐리어·싱글턴.
    // 전부 타겟 후보도 난수 씨앗도 아니고 뷰 키도 아니라 지금 읽을 곳이 없다
    // (M1 이 이벤트·스냅샷 키로 ID 를 쓰기 시작하면 그때 확장한다 — 그 시점엔
    // 카운터가 Bridge 필드에서 싱글턴으로 승격돼야 ECS 쪽에서도 발급할 수 있다).
    public struct SimEntityId : IComponentData
    {
        // 미발급. 동률 비교에서 **맨 뒤**로 밀린다 — 0 을 폴백으로 쓰면 미발급끼리가
        // 아니라 «0번 유닛» 과 충돌해 조용히 순위를 훔친다.
        public const int Unassigned = int.MaxValue;

        public int value;
    }
}
