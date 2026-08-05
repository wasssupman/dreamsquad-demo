// battle-sim-extraction unit 15-B — 배치 거절 사유는 **배치 규칙의 산출물**이라 규칙과 함께
// sim 쪽에 산다. `Wassup.Bridge` 에 두면 sim 모듈이 Bridge 네임스페이스를 알아야 해서
// 의존 방향(sim 은 Bridge 를 모른다)이 뒤집힌다 — 그 방향은 CLAUDE.md 제약 1 의 후계다.
namespace Wassup.Sim.Match
{
    public enum PlacementRejectReason
    {
        None,
        NotRunningOrPlacementClosed,
        MissingMap,
        OutOfBounds,
        NotBuildable,
        Occupied,
        InvalidUnit,
        NotInPickedPool,
        InsufficientCost,
        // defender-relocation unit 0 — 재배치 전용 사유 (뒤에 추가: 기존 직렬화 값 보존)
        NoDefenderAtSource,
        SourceBusy,
        SameCell,
        // battle-sim-extraction unit 15 — 배치 쿨타임. 그 전까지는 **UI 게이트뿐이라 커맨드로
        // 직접 배치하면 쿨타임이 무시됐다**(뷰를 우회하는 경로가 규칙을 통과했다). 이제 배치
        // 판정 자체가 본다. 값은 맨 뒤에 붙인다 — 기존 직렬화 값 보존.
        OnCooldown
    }
}
