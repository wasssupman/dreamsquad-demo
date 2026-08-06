using Wassup.Sim.Effects;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 공격 산출물 로그 1건. 구 `AttackOutputLogEvent` 이식.
    ///
    /// ⚠ **상태 해시에 실리지 않는다** — 진단·전투로그용이다(`SimWarning`·`HazardRuntime` 과 같은 성격).
    /// 이 채널이 비거나 넘쳐도 A/B 는 갈리지 않아야 한다.
    ///
    /// 구 sim 은 `TryGetSingletonRW` 로 채널 존재를 확인해 enqueue 를 건너뛸 수 있었다(분류 A 게이트).
    /// 신 sim 에서 그 게이트는 **증발한다** — 채널은 항상 존재한다(<see cref="SimChannels"/> 주석).
    /// </summary>
    public struct AttackOutputLogEvent
    {
        public SimEntityId attacker;
        public AttackOutputKind kind;
        public float magnitude;
        /// `kind == ApplyStat` 일 때만 유의미.
        public StatKind stat;
        /// `kind == ApplyStack` 일 때만 유의미.
        public StackKind stackKind;
        public float duration;
        public SimVec3 sourcePos;
        public SimVec3 targetPos;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 부착 카드 발동 신호. 구 `DcTriggerFiredEvent` 이식.
    ///
    /// **발동 = 카운터 소비가 성사된 프레임**이고, payload arm 이나 대상 유무와 **무관하게** 신호한다
    /// (카운트가 소비됐다는 사실이 곧 사건이다). 조건부 카드(궁지폭발·처형타)는 이 신호 없이는
    /// 조건 충족 여부를 영영 알 수 없다.
    ///
    /// 생산자는 `AttackN` 계열 발동 3지점(RESOLVE / 폭탄 발사 훅 / 캐스트 드레인) —
    /// <see cref="DcTriggerSlot"/> 의 "counter 쓰기 소유" 3지점과 **같은 집합**이다.
    /// 소비자는 뷰(18-K): 유닛 머리 위 아이콘 행 펄스.
    ///
    /// 귀속은 **host 단위**다(카드 정밀 귀속은 recall registry 후속 spec).
    /// ⚠ 상태 해시에 실리지 않는다.
    /// </summary>
    public struct DcTriggerFiredEvent
    {
        /// 발동한 슬롯이 부착된 방어유닛.
        public SimEntityId host;
    }
}
