using System.Collections.Generic;
using Wassup.Skills;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7e — **「걸렸나」를 무엇으로 보나**의 표. 판정도 상태도 갖지 않는 도구다.
    //
    // 카드의 페이로드가 무엇이든 세상을 바꾸는 표면은 `IntentApplier` 하나(S20)라, 결국 의도 종류 몇 개로 접힌다.
    // 그래서 기대값을 **카드마다 적지 않는다** — 카드가 늘어도 이 표는 안 는다. 카드가 무엇을 기대하는지는 그 카드의
    // 실행자(concrete)가 실제로 낸 의도가 말하고(`CardProbe` 가 기록한다), 이 표는 「그 종류의 의도가 적용됐다면
    // 판 위에 무엇이 남는가」만 안다.
    //
    // 판정은 **대조**다: 같은 시드 · 같은 배치의 판을 둘 세우고 한쪽에서만 카드를 쓴다. 평타·이동이 만든 사건은
    // 두 판에 똑같이 나므로, 「쓴 판에서 그 신호가 더 많다」가 곧 「그 카드가 걸었다」다(결정론이 이 비교를 성립시킨다).
    //
    // ⚠ 한계(7e 목적 절): 이 표는 **존재**를 본다. 세기가 게임적으로 맞는지 · 그림이 보이는지는 못 본다.

    /// <summary>판 위에 남는 관측 신호. 의도 종류가 이것으로 접힌다.</summary>
    public enum WitnessSignal : byte
    {
        /// <summary>관측 대상이 아니다(연출 · 소비자 없는 의도).</summary>
        None = 0,
        Damage,
        Heal,
        Modifier,
        Stack,
        Cc,
        CcCleared,
        Dot,
        Shield,
        Taunt,
        Blink,
        Projectile,
        Hazard,
        Field,
        Leap,
        AttackDelay,
        Charge,
        Lethal,
        Cocoon,
        RewardMul,
        Cost,
        /// <summary>액티브 재사용 대기 합(틱). **줄어드는 쪽**이 관측이다.</summary>
        Cooldown,
        /// <summary>공격 수식자(튕김 · 최전방 · 수면 배율 · 강공). 발동이 아니라 **달려 있음**이 관측이다.</summary>
        AttackMod,
        /// <summary>손패 맨 앞 카드(주어 = 카드 줄). 인수인계의 관측.</summary>
        HandFront,
        /// <summary>살아서 판 위에 있다. 피해 관측의 짝(쓴 판에서 **없어졌다** = 걸린 것).</summary>
        Alive,
        /// <summary>스킬이 실패를 보고했다 — 판에 아무것도 남지 않으므로 **언제나 미관측**이다.</summary>
        Report,
    }

    /// <summary>
    /// 한 시점의 관측 — (신호, 주어) → 개수. 사건 신호는 누적, 상태 신호는 그 시점 값이다.
    /// 주어는 개체 id 값(판 신호는 0, 손패 맨 앞은 카드 줄).
    /// </summary>
    public sealed class WitnessFrame
    {
        private readonly Dictionary<long, int> _counts = new Dictionary<long, int>(64);
        private readonly int[] _totals = new int[32];

        private static long Key(WitnessSignal s, int subject) => ((long)(byte)s << 32) | (uint)subject;

        public void Add(WitnessSignal s, int subject, int n = 1)
        {
            if (s == WitnessSignal.None || n == 0) return;
            long k = Key(s, subject);
            _counts.TryGetValue(k, out int v);
            _counts[k] = v + n;
            _totals[(int)s] += n;
        }

        /// <summary>그 주어의 개수. `subject` 가 <see cref="AnySubject"/> 면 그 신호의 전부.</summary>
        public int Count(WitnessSignal s, int subject)
        {
            if (subject == AnySubject) return _totals[(int)s];
            return _counts.TryGetValue(Key(s, subject), out int v) ? v : 0;
        }

        public WitnessFrame Clone()
        {
            var f = new WitnessFrame();
            foreach (var kv in _counts) f._counts[kv.Key] = kv.Value;
            System.Array.Copy(_totals, f._totals, _totals.Length);
            return f;
        }

        /// <summary>「누구든」. 개체 id 는 음수가 아니고(-1 = 없음) 판은 0 이라 겹치지 않는다.</summary>
        public const int AnySubject = int.MinValue;
    }

    public static class EffectWitness
    {
        // ── 의도 → 신호 ────────────────────────────────────────────────────────

        public static WitnessSignal SignalOf(SimIntentKind k)
        {
            switch (k)
            {
                case SimIntentKind.DealDamage: return WitnessSignal.Damage;
                case SimIntentKind.Heal: return WitnessSignal.Heal;
                case SimIntentKind.ApplyStatModifier: return WitnessSignal.Modifier;
                case SimIntentKind.ApplyStack: return WitnessSignal.Stack;
                case SimIntentKind.ApplyCc: return WitnessSignal.Cc;
                case SimIntentKind.ApplyDot: return WitnessSignal.Dot;
                case SimIntentKind.ClearCc: return WitnessSignal.CcCleared;
                case SimIntentKind.GrantShield: return WitnessSignal.Shield;
                case SimIntentKind.Taunt: return WitnessSignal.Taunt;
                case SimIntentKind.Blink: return WitnessSignal.Blink;
                case SimIntentKind.SpawnProjectile:
                case SimIntentKind.EmitPattern:
                case SimIntentKind.SpawnOrbitProjectile: return WitnessSignal.Projectile;
                case SimIntentKind.SpawnZoneCarrier: return WitnessSignal.Hazard;
                case SimIntentKind.SpawnFieldCarrier: return WitnessSignal.Field;
                case SimIntentKind.BeginUltimateLeap: return WitnessSignal.Leap;
                case SimIntentKind.DelaySelfAttack: return WitnessSignal.AttackDelay;
                case SimIntentKind.GrantCharge: return WitnessSignal.Charge;
                case SimIntentKind.StartLethalTimer: return WitnessSignal.Lethal;
                case SimIntentKind.BeginDreamCocoon: return WitnessSignal.Cocoon;
                case SimIntentKind.ScaleKillReward: return WitnessSignal.RewardMul;
                case SimIntentKind.Report: return WitnessSignal.Report;
                // 연출은 상태를 안 바꾼다(`SkillIntent` 헤더). 위협 귀속은 소비자가 없어 이식하지 않았다(7d) — 적용 표면이
                // 이미 말하고 버린다. 둘 다 「걸렸나」를 물을 대상이 아니다.
                case SimIntentKind.PlayVisual:
                case SimIntentKind.CreditThreat:
                default:
                    return WitnessSignal.None;
            }
        }

        public static WitnessSignal SignalOf(MetaIntentKind k)
        {
            switch (k)
            {
                case MetaIntentKind.GainCost: return WitnessSignal.Cost;
                case MetaIntentKind.ReduceSkillCooldown: return WitnessSignal.Cooldown;
                default: return WitnessSignal.None;
            }
        }

        /// <summary>
        /// 관측의 주어. 개체에 걸리는 신호는 그 대상(없으면 누구든), 판 위에 새로 생기는 것(탄 · 장판 · 장)은 누구든 —
        /// 새 개체의 id 는 쓴 판에만 있어서 대조 판과 맞출 수 없다.
        /// </summary>
        public static int SubjectOf(in SimIntent i)
        {
            switch (SignalOf(i.Kind))
            {
                case WitnessSignal.Projectile:
                case WitnessSignal.Hazard:
                case WitnessSignal.Field:
                case WitnessSignal.Report:
                    return WitnessFrame.AnySubject;
                default:
                    return i.Target.IsValid ? i.Target.Value : WitnessFrame.AnySubject;
            }
        }

        /// <summary>
        /// 의도가 적용된 뒤 관측 신호가 판에 드러나기까지의 **최대 틱**(구조 상수 — 밸런스 값이 아니다).
        /// 0 = 적용 콜스택 안에서 이미 보인다. 1 = 다음 틱의 단계가 요청을 소비한다(군중 제어 요청 · 탄 요청 · 어그로 요청).
        /// 2 = 한 번 더 미뤄진다(실드는 **다음 틱** 드레인이 의도다 — `SimIntentKind.GrantShield` 주석).
        /// 발사 명세는 첫 발의 저작 간격만큼 더 기다린다.
        /// </summary>
        public static int LatencyTicks(in SimIntent i, MatchDefinition def)
        {
            switch (SignalOf(i.Kind))
            {
                case WitnessSignal.Shield:
                case WitnessSignal.Damage:
                case WitnessSignal.Heal:
                    return 2;
                case WitnessSignal.Cc:
                case WitnessSignal.Taunt:
                case WitnessSignal.Blink:
                    return 2;
                case WitnessSignal.Projectile:
                    if (i.Kind == SimIntentKind.EmitPattern && def != null
                        && i.PatternIndex >= 0 && i.PatternIndex < def.Patterns.Length)
                    {
                        ref var pd = ref def.Patterns[i.PatternIndex];
                        float first = pd.Shots != null && pd.Shots.Length > 0 ? pd.Shots[0].IntervalAfterPreviousSec : 0f;
                        return MatchClock.TicksOf(first, BattleMatch.Dt) + 2;
                    }
                    return 2;
                default:
                    return 1;
            }
        }

        // ── 판 → 관측 ──────────────────────────────────────────────────────────

        /// <summary>사건 하나를 누적 관측에 접는다. 사건은 값 스냅샷이라 드레인 뒤에도 뜻이 같다(계약 4).</summary>
        public static void Fold(in CoreEvent e, WitnessFrame into)
        {
            switch (e.Kind)
            {
                case CoreEventKind.DamageApplied: into.Add(WitnessSignal.Damage, e.B.Value); break;
                case CoreEventKind.HealApplied: into.Add(WitnessSignal.Heal, e.A.Value); break;
                case CoreEventKind.ModifierApplied: into.Add(WitnessSignal.Modifier, e.B.Value); break;
                case CoreEventKind.StackChanged: into.Add(WitnessSignal.Stack, e.B.Value); break;
                case CoreEventKind.CcApplied: into.Add(WitnessSignal.Cc, e.B.Value); break;
                case CoreEventKind.CcCleared: into.Add(WitnessSignal.CcCleared, e.A.Value); break;
                case CoreEventKind.DotApplied: into.Add(WitnessSignal.Dot, e.B.Value); break;
                case CoreEventKind.ShieldGranted: into.Add(WitnessSignal.Shield, e.B.Value); break;
                case CoreEventKind.AggroAcquired:
                    if (e.Arg == 1) into.Add(WitnessSignal.Taunt, e.A.Value);   // 도발(1) — 히트 어그로(0)는 평타의 것
                    break;
                case CoreEventKind.Blinked: into.Add(WitnessSignal.Blink, e.A.Value); break;
                case CoreEventKind.ProjectileSpawned: into.Add(WitnessSignal.Projectile, 0); break;
                case CoreEventKind.HazardSpawned: into.Add(WitnessSignal.Hazard, 0); break;
                case CoreEventKind.FieldSpawned: into.Add(WitnessSignal.Field, 0); break;
                case CoreEventKind.LeapAscend: into.Add(WitnessSignal.Leap, e.A.Value); break;
                case CoreEventKind.CostChanged:
                    if (e.Arg > 0) into.Add(WitnessSignal.Cost, 0, e.Arg);
                    break;
            }
        }

        /// <summary>
        /// 그 시점의 **상태** 신호를 더한다(읽기만). 진행형 상태(충전 · 치명 · 고치 · 표식 배율 · 공격 대기)와
        /// 달려 있는 공격 수식자 · 손패 맨 앞 · 재사용 대기 합 · 생존.
        /// </summary>
        public static void Observe(BattleMatch m, WitnessFrame into)
        {
            var units = m.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                int id = u.Id.Value;
                if (!u.Dead) into.Add(WitnessSignal.Alive, id);
                var pg = u.Progressive;
                if (pg != null)
                {
                    if (pg.Charge > 0) into.Add(WitnessSignal.Charge, id, pg.Charge);
                    if (pg.LethalActive) into.Add(WitnessSignal.Lethal, id);
                    if (pg.CocoonActive) into.Add(WitnessSignal.Cocoon, id);
                }
                if (u.AwakeningRewardMul != 1f) into.Add(WitnessSignal.RewardMul, id);
                if (u.Attack != null)
                {
                    if (u.Attack.CooldownRemaining > 0f)
                        into.Add(WitnessSignal.AttackDelay, id, MatchClock.TicksOf(u.Attack.CooldownRemaining, BattleMatch.Dt));
                    if (u.Attack.Mods.Count > 0) into.Add(WitnessSignal.AttackMod, id, u.Attack.Mods.Count);
                }
            }

            var hand = new List<HandDeck.Entry>(8);
            m.Hand.Hand(hand);
            if (hand.Count > 0) into.Add(WitnessSignal.HandFront, hand[0].CardIndex);

            var cards = m.Definition.Cards;
            int left = 0;
            for (int c = 0; c < cards.Length; c++)
            {
                int total = MatchClock.TicksOf(cards[c].CooldownSeconds, BattleMatch.Dt);
                left += (int)System.Math.Round(m.Hand.CooldownNormalized(c) * total);
            }
            if (left > 0) into.Add(WitnessSignal.Cooldown, 0, left);
        }

        // ── 판정 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 그 신호가 **쓴 판에서** 대조 판보다 더 났나(재사용 대기는 덜 남았나). 피해는 「대상이 사라졌다」도 관측이다 —
        /// 한 방에 죽으면 피해 사건 수가 대조 판의 평타보다 적을 수 있다.
        /// </summary>
        public static bool Seen(WitnessSignal s, int subject, WitnessFrame control, WitnessFrame fired)
        {
            switch (s)
            {
                case WitnessSignal.None:
                case WitnessSignal.Report:
                    return false;
                case WitnessSignal.Cooldown:
                    return fired.Count(s, subject) < control.Count(s, subject);
                case WitnessSignal.Damage:
                    return fired.Count(s, subject) > control.Count(s, subject)
                        || (subject != WitnessFrame.AnySubject
                            && fired.Count(WitnessSignal.Alive, subject) < control.Count(WitnessSignal.Alive, subject));
                default:
                    return fired.Count(s, subject) > control.Count(s, subject);
            }
        }
    }
}
