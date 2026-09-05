using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wassup.Core.Trace
{
    // battle-sim-extraction M0 unit 4 — 구 sim 의 관측 기록 포맷 v0.
    //
    // 이것은 **M1 신 sim 과의 A/B 기준선**이다. 목적이 「구 sim 이 무엇을 했는지」를
    // 다시 만들 수 없는 형태로 박제하는 것이므로, 두 가지를 의도적으로 강제한다:
    //
    //  ① **엔티티 참조를 싣지 않는다.** 축은 `SimEntityId`(unit 1) 하나다. 오브젝트
    //     참조를 실으면 「지금 이 프로세스에서만 의미 있는」 기록이 되어 네트워크에도
    //     파일에도 못 태운다. 그 사실을 나중이 아니라 **첫날** 알아야 한다.
    //  ② **직렬화 왕복을 통과한 것만 저장한다.** 쓰기 → 읽기 → 다시 쓰기가 바이트로
    //     같지 않으면 그 기록은 골든이 될 자격이 없다(비교가 포맷 잡음을 잡게 된다).
    //
    // 포맷은 줄 단위 텍스트다. 바이너리로 안 하는 이유: 골든이 갈렸을 때 사람이 diff 로
    // **어디서** 갈렸는지 바로 봐야 하고, 그게 이 파일의 존재 이유이기 때문이다.
    public enum TraceChannel : byte
    {
        // sim 결과 = 규칙이 정한 사건. 순서와 값이 parity 대상이다.
        EnemyKilled = 1,
        GoalReached = 2,        // 유출
        GoalCollapsed = 3,
        DefenderDeath = 4,
        UnitAttack = 5,         // 공격 성사(공격자당 1회)
        ProjectileSpawn = 6,
        ProjectileHit = 7,
        DamageNumber = 8,       // 실제 적용된 피해
        HealApplied = 9,
        ShieldGranted = 10,
        ShieldBreak = 11,
        Knockup = 12,
        DcTriggerFired = 13,
        CastHazardSpawn = 14,
        HazardRuntime = 15,
        HazardDestroyed = 16,
        MeteorBarrage = 17,
        PatrolSpawn = 18,
        AttackOutputLog = 19,
        // enemy-detection-range unit 5 — 적이 방어유닛을 «발견» 한 순간(hunting 0→1).
        // a = 발견한 적, b = 발견당한 방어유닛. **append-only** — 기존 번호를 재사용하면
        // 옛 골든이 다른 사건으로 읽힌다.
        Detection = 20,
    }

    // 채널 무관 고정 폭 레코드. 채널마다 다른 구조체를 두지 않는 이유: 스키마가 채널 수만큼
    // 늘어나면 upcaster(M2)가 그만큼 늘고, 정작 parity 가 보는 것은 «누가·언제·얼마나» 뿐이다.
    public struct TraceEvent
    {
        public int tick;
        public TraceChannel channel;
        public int a;      // 주체 SimEntityId (-1 = 없음)
        public int b;      // 대상 SimEntityId (-1 = 없음)
        public int i;      // 채널별 정수(킬 점수·kind·index 등)
        public float f;    // 채널별 실수(피해량·지속시간 등)

        public bool SameAs(in TraceEvent o)
            => tick == o.tick && channel == o.channel && a == o.a && b == o.b && i == o.i
               && Quantize(f) == Quantize(o.f);

        // 연속 물리값은 exact 로 보지 않는다(parity 기준: 연속값은 epsilon).
        // 1e-3 격자로 접어 비교와 직렬화가 **같은 해상도**를 쓰게 한다 — 다르면
        // 「파일로는 같은데 메모리로는 다르다」가 생긴다.
        public static int Quantize(float v) => (int)Math.Round(v * 1000.0);
    }

    public sealed class LegacyTraceV0
    {
        public const string Magic = "LTV0";

        public string configHash = "";
        public int matchSeed;
        public float stepDt;
        public int tickCount;
        public string scenario = "";

        public readonly List<TraceEvent> events = new List<TraceEvent>();

        // 최종 결산 — parity 에서 **exact** 로 보는 정수들.
        public int finalKills;
        public int finalScore;
        public int finalLeaks;
        public ulong finalStateHash;

        public string Serialize()
        {
            var sb = new StringBuilder(1024 + events.Count * 24);
            var inv = CultureInfo.InvariantCulture;
            sb.Append(Magic).Append('\n');
            sb.Append("scenario=").Append(scenario).Append('\n');
            sb.Append("configHash=").Append(configHash).Append('\n');
            sb.Append("matchSeed=").Append(matchSeed.ToString(inv)).Append('\n');
            sb.Append("stepDt=").Append(stepDt.ToString("R", inv)).Append('\n');
            sb.Append("tickCount=").Append(tickCount.ToString(inv)).Append('\n');
            sb.Append("events=").Append(events.Count.ToString(inv)).Append('\n');
            for (int n = 0; n < events.Count; n++)
            {
                var e = events[n];
                sb.Append(e.tick.ToString(inv)).Append(' ')
                  .Append(((int)e.channel).ToString(inv)).Append(' ')
                  .Append(e.a.ToString(inv)).Append(' ')
                  .Append(e.b.ToString(inv)).Append(' ')
                  .Append(e.i.ToString(inv)).Append(' ')
                  // 저장 해상도 = 비교 해상도(위 Quantize). 정수로 적어 왕복 손실을 없앤다.
                  .Append(TraceEvent.Quantize(e.f).ToString(inv)).Append('\n');
            }
            sb.Append("finalKills=").Append(finalKills.ToString(inv)).Append('\n');
            sb.Append("finalScore=").Append(finalScore.ToString(inv)).Append('\n');
            sb.Append("finalLeaks=").Append(finalLeaks.ToString(inv)).Append('\n');
            sb.Append("finalStateHash=").Append(finalStateHash.ToString("X16", inv)).Append('\n');
            return sb.ToString();
        }

        public static LegacyTraceV0 Deserialize(string text)
        {
            var inv = CultureInfo.InvariantCulture;
            var t = new LegacyTraceV0();
            var lines = text.Split('\n');
            if (lines.Length == 0 || lines[0] != Magic)
                throw new FormatException($"trace magic mismatch: '{(lines.Length > 0 ? lines[0] : "")}'");

            int declared = 0;
            for (int n = 1; n < lines.Length; n++)
            {
                string line = lines[n];
                if (line.Length == 0) continue;
                int eq = line.IndexOf('=');
                if (eq > 0)
                {
                    string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                    switch (k)
                    {
                        case "scenario": t.scenario = v; break;
                        case "configHash": t.configHash = v; break;
                        case "matchSeed": t.matchSeed = int.Parse(v, inv); break;
                        case "stepDt": t.stepDt = float.Parse(v, NumberStyles.Float, inv); break;
                        case "tickCount": t.tickCount = int.Parse(v, inv); break;
                        case "events": declared = int.Parse(v, inv); break;
                        case "finalKills": t.finalKills = int.Parse(v, inv); break;
                        case "finalScore": t.finalScore = int.Parse(v, inv); break;
                        case "finalLeaks": t.finalLeaks = int.Parse(v, inv); break;
                        case "finalStateHash": t.finalStateHash = ulong.Parse(v, NumberStyles.HexNumber, inv); break;
                    }
                    continue;
                }
                var p = line.Split(' ');
                if (p.Length != 6) throw new FormatException($"trace event row has {p.Length} fields: '{line}'");
                t.events.Add(new TraceEvent
                {
                    tick = int.Parse(p[0], inv),
                    channel = (TraceChannel)int.Parse(p[1], inv),
                    a = int.Parse(p[2], inv),
                    b = int.Parse(p[3], inv),
                    i = int.Parse(p[4], inv),
                    f = int.Parse(p[5], inv) / 1000f,
                });
            }
            if (declared != t.events.Count)
                throw new FormatException($"trace declares {declared} events but carries {t.events.Count}");
            return t;
        }

        // parity 판정. 첫 불일치의 사람이 읽을 설명을 돌려준다(null = 일치).
        // exact 로 보는 것: 이벤트 시퀀스·킬/유출/점수(int)·최종 상태 해시.
        // epsilon 으로 보는 것: 이벤트의 연속 물리값(위 Quantize 해상도).
        public string DiffAgainst(LegacyTraceV0 other)
        {
            if (other == null) return "상대 trace 가 없다";
            if (configHash != other.configHash)
                return $"configHash 가 다르다 ({configHash} vs {other.configHash}) — 코드 회귀가 아니라 **조건 드리프트**다";
            if (tickCount != other.tickCount) return $"tickCount {tickCount} vs {other.tickCount}";
            int n = Math.Min(events.Count, other.events.Count);
            for (int k = 0; k < n; k++)
                if (!events[k].SameAs(other.events[k]))
                    return $"이벤트 #{k} 불일치 — golden(t{events[k].tick} {events[k].channel} a{events[k].a} b{events[k].b} i{events[k].i} f{events[k].f:F3})"
                         + $" vs run(t{other.events[k].tick} {other.events[k].channel} a{other.events[k].a} b{other.events[k].b} i{other.events[k].i} f{other.events[k].f:F3})";
            if (events.Count != other.events.Count)
                return $"이벤트 수 {events.Count} vs {other.events.Count} (앞 {n}개는 동일 — 뒤에서 갈렸다)";
            if (finalKills != other.finalKills) return $"finalKills {finalKills} vs {other.finalKills}";
            if (finalScore != other.finalScore) return $"finalScore {finalScore} vs {other.finalScore}";
            if (finalLeaks != other.finalLeaks) return $"finalLeaks {finalLeaks} vs {other.finalLeaks}";
            if (finalStateHash != other.finalStateHash)
                return $"finalStateHash {finalStateHash:X16} vs {other.finalStateHash:X16}";
            return null;
        }
    }
}
