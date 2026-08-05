using Wassup.Sim.Combat;

namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 이번 프레임 회복 펄스 1건. 구 `IncomingHeal` 이식.
    ///
    /// ⚠ **매 프레임 비워야 하는 펄스 채널이다**(버퍼로 표현된 채널). `RegenPerSec` 은
    /// 여기로 오지 않는다 — `DamageApplicationSystem` 이 `ModifierStats` 에서 직접 읽어
    /// 프레임마다 더한다. 그 비대칭이 연출 규칙을 낳는다(펄스만 VFX, 재생은 무연출).
    /// </summary>
    public struct IncomingHeal
    {
        public float amount;
    }

    /// <summary>
    /// 이 적이 죽을 때 주는 각성 재화. 구 `AwakeningReward` 이식.
    /// ⚠ 값이 **이벤트에 실려 나간다** — 드레인 시점엔 엔티티가 이미 없다.
    /// </summary>
    public struct AwakeningReward
    {
        public int value;
    }

    /// <summary>
    /// 이 적을 잡을 때 주는 점수. 구 `KillScore` 이식. <see cref="AwakeningReward"/> 와 같은 이유로
    /// 값이 이벤트에 실린다.
    ///
    /// ⚠ **유출된 적은 아무것도 남기지 않는다** — 목표 도달 제거는 HP&lt;=0 분기에 오지 않는다.
    /// 그 비대칭이 사양이다(유출은 두 축을 동시에 깎는다).
    /// </summary>
    public struct KillScore
    {
        public int value;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — "N회 피격" 카운터. 구 `DamagedCounter` 이식.
    ///
    /// **`DcTriggerSlot` 에 합치지 않은 것이 계약이다.** 카운터가 써지는 곳이
    /// `DamageApplicationSystem`(Units 맥락)이고, 컴포넌트 쓰기는 소유 맥락 안에 머물러야 한다.
    /// 버퍼인 이유도 `DcTriggerSlot` 과 같다 — 같은 카드 두 장이 독립 카운터를 갖는다.
    ///
    /// ⚠ 게이트에 `subject` 필드가 없다 — 배선이 `OnDamagedN × Self` 뿐이라 Self 고정이다.
    /// 판정 hp 는 **이 피격을 적용한 뒤**(newHp) 값이다: "그 이하로 만든 그 피격부터" 센다.
    /// </summary>
    public struct DamagedCounter
    {
        public int instanceId;
        /// OnDamagedN: N 번째 피격 **프레임**마다 발동(프레임당 피격 = 1).
        public ushort period;
        /// owned write: <see cref="DamageApplicationSystem"/> 단독.
        public ushort counter;

        public DcPayloadKind payload;
        /// SelfTileAoe: flat AoE 데미지.
        public float magnitude;
        /// SelfTileAoe: Chebyshev 반경.
        public int tileRange;
        /// SelfTileAoe: AoE 뷰 데이터 index (-1 = 없음).
        public int aoeDataIndex;

        public DcGateKind gate;
        public float gateValue;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 킬 귀속 규칙을 순수 fold 로 고정. 구 `KillAttribution` 이식.
    ///
    /// 규칙: 한 프레임 `IncomingDamage` 중 **`source` 非Null 이면서 `amount` 최대**인 엔트리의
    /// source 가 killer 다.
    ///
    /// 두 가지가 결정론의 근거다:
    /// <list type="bullet">
    /// <item><b>동점은 먼저 접힌 쪽이 이긴다</b> — strict `&gt;` 다. 그래서 결과가
    ///       **버퍼 적재 순서**에 걸린다(신 sim 의 순회가 생성 순서인 이유 중 하나).</item>
    /// <item><b>source 없는 피해는 후보가 아니다</b> — DoT·배치·환경은 미귀속이고,
    ///       전부 미귀속이면 killer 가 없어 OnKill 이 발동하지 않는다(의도).</item>
    /// </list>
    /// </summary>
    public static class KillAttribution
    {
        public static void Consider(float amount, SimEntityId source, ref SimEntityId bestSource, ref float bestAmount)
        {
            if (!source.IsNull && amount > bestAmount)
            {
                bestAmount = amount;
                bestSource = source;
            }
        }
    }
}
