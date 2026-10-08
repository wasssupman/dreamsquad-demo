using Unity.Mathematics;

namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 3 — 「이 대상에게 이 군중 제어를 걸어라」.
    //
    // **요청이지 사실이 아니다**(`AggroRequest` 와 같은 규율). 슬롯 병합·면역·지속 감쇠는
    // 효과 레이어(unit 6)가 갖고, 이 unit 은 **누가 무엇을 얼마나** 를 정해 줄에 넣는 데까지다.
    //
    // ⚠ **이 줄이 C9 의 이행 장치다.** 「내가 때린 피해가 내가 건 잠을 깨우지 않는다」는
    // 옛 전투에서 **시스템 순서의 우연**이었다(피해가 프레임 N, 수면 적용이 N+1). 한 틱 안에서
    // 도는 새 코어에는 그 우연이 없으므로 가드를 **명시로** 세운다:
    //   ① CC 부여는 피해 판정 **뒤** 단계다(`CombatPhase` 의 후처리).
    //   ② 기상(`WakeRequest`)은 **그 틱에 새로 부여된 슬롯을 대상에서 뺀다** —
    //      `CcRequests` 에 이번 틱 수면이 들어 있으면 같은 대상의 기상 요청을 버린다.
    // 둘 중 하나만으로는 부족하다: ①만 두면 같은 틱의 피해가 같은 틱의 수면을 깨우고,
    // ②만 두면 순서가 뒤집혔을 때 조용히 무효가 된다.
    public enum CcRequestKind : byte
    {
        None = 0,
        Stun = 1,
        Sleep = 2,
        Slow = 3,
        /// <summary>넉백 — 외력이라 행동 잠금 축이 아니다.</summary>
        Impulse = 4,
    }

    public struct CcRequest
    {
        public SimEntityId Target;
        public CcRequestKind Kind;
        public float Seconds;
        /// <summary>`Impulse` 전용 — 초당 속도(거리 ÷ 지속). 방향은 **적이 가던 방향의 반대**다.</summary>
        public float3 Vector;
        /// <summary>건 쪽. 트레이스·귀속용이고 병합 키는 아니다.</summary>
        public SimEntityId Source;

        public static CcRequest Of(SimEntityId target, CcRequestKind kind, float seconds, SimEntityId source)
            => new CcRequest { Target = target, Kind = kind, Seconds = seconds, Source = source };

        /// <summary>
        /// 넉백. **방향을 모르는 대상은 밀리지 않는다**(C8) — 0 방향으로 밀면 원점으로 빨려든다.
        /// 그 판정은 호출부가 한다(마지막 이동 방향이 있어야 이 요청 자체를 만들 수 있다).
        /// </summary>
        public static CcRequest Push(SimEntityId target, float3 velocity, float seconds, SimEntityId source)
            => new CcRequest
            {
                Target = target,
                Kind = CcRequestKind.Impulse,
                Seconds = seconds,
                Vector = velocity,
                Source = source,
            };
    }

    /// <summary>
    /// 피격으로 잠을 깨우는 요청. **기절은 안 깬다** — 깨우는 것은 수면뿐이다.
    /// 옛 전투의 `CcClearRequestsSingleton`(Units→Effects) 과 같은 축이다.
    /// </summary>
    public struct WakeRequest
    {
        public SimEntityId Target;
    }
}
