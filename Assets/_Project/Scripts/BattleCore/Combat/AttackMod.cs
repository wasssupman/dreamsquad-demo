using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Trigger;

namespace Somnia.Battle.BattleCore.Combat
{
    // battle-core-rebuild unit 7a — **공격 수식자** 5축(rev 3 §1 「어휘 밖 = 5」).
    //
    // 판별 기준은 「이번 공격의 **출력 조립**에 참여하나」다. 이들은 사건을 안 내고 자기를 부른 공격 자체를
    // 바꾼다 — 그래서 바인딩이 아니다(스킬 seam 은 정의상 공격 해결 **뒤**라 늦다). 「하나」라고 적지 않는다:
    //   ① 강공(`HeavyStrike`)   — N 번째 공격의 피해 × 배율(자기참조 — 카운터가 그 공격을 센다)
    //   ② 튕김 부여(`ProjectileBounce`) — 탄이 맞고 N 번 더 튄다(수 합 · 반경 최대 · 감쇠 곱)
    //   ③ 최전방 배율(`FrontmostTarget`) — 최전방을 문 공격의 주 대상 × 배율(START 에 스냅샷)
    //   ④ 수면 배율(`DamageVsSleeping`)  — 잠든 대상에게만 × 배율(피해자별 판정)
    //   ⑤ 충전 소비             — 부여는 스킬(`GrantSelfCharge`)이고 **소비가 여기**다(경계가 여기 있다)
    // 실행 자리는 둘: 근접·즉시는 `CombatPhase` 공격 조립, 탄은 발사 스냅샷(피해가 발사 때 정해진다).
    //
    // ⚠ 탄 피해에는 공격력 배율이 붙지만 **바늘 캐리어**(스킬이 쏘는 대상 탄)는 flat 이다(C5) — 그쪽은
    // 이 조립을 안 지난다(`IntentApplier.SpawnProjectile`). 배율을 붙이면 강화 스택이 그대로 곱해진다.
    public enum AttackModKind : byte
    {
        // 튕김 · 최전방 · 수면 = 저작 효과 종류(`EffectKind` 상시 수식자 3 — skill-data-table unit 8)와 이름이 같다(핀 — `CoreTriggerEnumPinTests`).
        None = 0,
        ProjectileBounce = 1,
        FrontmostTarget = 2,
        DamageVsSleeping = 3,
        /// <summary>저작상 payload(`AttackN × HeavyStrike`)에서 온다 — 여기로 접는다(어휘 밖).</summary>
        HeavyStrike = 4,
    }

    public struct AttackModDef
    {
        public AttackModKind Kind;
        /// <summary>튕김 수.</summary>
        public int Count;
        /// <summary>튕김 재조준 반경(칸).</summary>
        public int TileRange;
        /// <summary>튕김 감쇠 · 최전방 배율 · 수면 배율 · 강공 배율.</summary>
        public float DamageMul;
        /// <summary>강공 — N 번째 공격.</summary>
        public int Period;
        /// <summary>강공의 게이트(처형타와 같은 어휘 — `AttackN × EventTarget` 만 열린다).</summary>
        public GateKind Gate;
        public float GateValue;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv, string key)
            => MatchDefinition.Put(sb, key,
                ((int)Kind).ToString(inv) + "," + Count.ToString(inv) + "," + TileRange.ToString(inv) + ","
                + DamageMul.ToString("R", inv) + "," + Period.ToString(inv) + "," + ((int)Gate).ToString(inv) + ","
                + GateValue.ToString("R", inv));
    }

    /// <summary>붙어 있는 수식자 하나. 카드가 붙인 것은 그 규칙의 `InstanceId` 로 뗀다(7b).</summary>
    public sealed class AttackModState
    {
        public AttackModDef Def;
        public int Counter;
        /// <summary>붙인 규칙(카드 — 7b). 0 = 유닛 저작.</summary>
        public int OwnerInstanceId;
    }

    public static class AttackMod
    {
        /// <summary>최전방 공격인가 — 최전방 수식자가 하나라도 있으면(옛 `wantFrontmost`).</summary>
        public static bool WantsFrontmost(List<AttackModState> mods)
        {
            for (int i = 0; i < mods.Count; i++) if (mods[i].Def.Kind == AttackModKind.FrontmostTarget) return true;
            return false;
        }

        /// <summary>최전방 배율 — 곱 중첩.</summary>
        public static float FrontmostMul(List<AttackModState> mods)
        {
            float m = 1f;
            for (int i = 0; i < mods.Count; i++)
                if (mods[i].Def.Kind == AttackModKind.FrontmostTarget) m *= mods[i].Def.DamageMul;
            return m;
        }

        /// <summary>수면 배율 — 곱 중첩. **피해자가 잘 때만** 곱한다(판정은 호출부가 피해자별로).</summary>
        public static float SleepMul(List<AttackModState> mods)
        {
            float m = 1f;
            for (int i = 0; i < mods.Count; i++)
                if (mods[i].Def.Kind == AttackModKind.DamageVsSleeping) m *= mods[i].Def.DamageMul;
            return m;
        }

        /// <summary>그 피해자에게 붙는 수면 배율(잠들지 않았으면 1). 기절·출혈은 안 본다 — 넓히면 다른 카드와 구분이 사라진다.</summary>
        public static float SleepMulFor(List<AttackModState> mods, Unit victim)
            => victim != null && victim.Cc.IsActive(Effects.CcSlotKind.Sleep) ? SleepMul(mods) : 1f;

        /// <summary>튕김 부여 — 수는 합, 반경은 최대, 감쇠는 곱(옛 집계 그대로).</summary>
        public static void Bounce(List<AttackModState> mods, out int count, out int tileRange, out float mul)
        {
            count = 0; tileRange = 0; mul = 1f;
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i].Def.Kind != AttackModKind.ProjectileBounce) continue;
                count += mods[i].Def.Count;
                tileRange = math.max(tileRange, mods[i].Def.TileRange);
                mul *= mods[i].Def.DamageMul;
            }
        }

        /// <summary>
        /// 강공 — **이번 공격**이 N 번째인가(대표 대상이 있을 때만 센다 — 빗나간 공격은 0). 게이트 실패는 카운터를
        /// 안 올린다(카운트 게이트). 반환 = 이번 공격의 배율(여러 장은 곱). 카운터를 **이 함수가 소유한다**.
        /// </summary>
        public static float HeavyStrike(List<AttackModState> mods, Unit target)
        {
            if (target == null) return 1f;
            float mul = 1f;
            for (int i = 0; i < mods.Count; i++)
            {
                var m = mods[i];
                if (m.Def.Kind != AttackModKind.HeavyStrike) continue;
                if (!SkillRouting.GatePass(m.Def.Gate, m.Def.GateValue, target.Health, target.MaxHealth)) continue;
                if (!TriggerCounters.Tick(ref m.Counter, m.Def.Period)) continue;
                mul *= m.Def.DamageMul > 0f ? m.Def.DamageMul : 1f;
            }
            return mul;
        }

        /// <summary>
        /// 충전 소비 — START 에서 쿨다운을 0 으로 만들어 **다음 공격을 즉시 한 번 더** 쏜다(각 발이 온전한 공격).
        /// ⚠ 한 번 쓰면 **전부** 사라진다 — 옛 전투는 충전을 컴포넌트로 들고 소비 때 통째로 뗐다(`RemoveComponent`).
        /// 반환 = 소비했나.
        /// </summary>
        public static bool ConsumeCharge(Unit u)
        {
            var pg = u.Progressive;
            if (pg == null || pg.Charge <= 0) return false;
            pg.Charge = 0;
            return true;
        }
    }
}
