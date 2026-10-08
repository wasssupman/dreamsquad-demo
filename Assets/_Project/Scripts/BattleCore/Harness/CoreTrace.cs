using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 1 — 새 코어의 관측 기록.
    //
    // **포맷은 옛 `LTV0` 계열과 같다**(magic `LTV0`, 줄 단위 텍스트, `f` 는 1e-3 격자
    // 정수로 저장). 같은 포맷을 쓰는 이유는 사람이 같은 눈으로 두 계보의 골든을 diff 할
    // 수 있어야 하기 때문이다.
    //
    // 그런데 **채널 enum 은 새로 시작한다**(0 부터). 옛 22 뒤에 이어 붙이지 않는 이유:
    // 옛 채널은 옛 전투의 사건 이름표라, 새 코어의 사건을 그 번호 공간에 넣으면 한 파일이
    // 두 어휘를 갖게 된다. 그래서 파일 계열도 폴더로 나눈다(`Tests/GoldenCore/`).
    //
    // 그 대신 **파서가 계열을 확인한다** — 헤더의 `channels=core` 가 그것이다.
    // 옛 리더는 모르는 키를 무시하므로 포맷 호환은 유지되고, 새 리더는 옛 파일을 거절한다.
    // (구분자가 없으면 두 계열이 육안으로 같아 보이고, 그때 골든 하나가 조용히 엉뚱한
    //  채널 이름으로 읽힌다.)
    //
    // ⚠ **append-only.** 기존 번호를 재사용하면 이미 구운 골든이 다른 사건으로 읽힌다.
    public enum CoreTraceChannel : byte
    {
        MatchStarted = 0,
        UnitSpawned = 1,
        UnitDestroyed = 2,
        MatchEnded = 3,
        // ── unit 2 (맵·이동) ──
        GoalReached = 4,
        Detected = 5,
        AggroAcquired = 6,
        Blinked = 7,
        // ── unit 3 (전투 판정) ──
        AttackResolved = 8,
        ProjectileSpawned = 9,
        ProjectileDespawned = 10,
        ProjectileHit = 11,
        DamageApplied = 12,
        HealApplied = 13,
        ShieldBroken = 14,
        UnitSlain = 15,
        Knockup = 16,
        LeapAscend = 17,
        LeapDescend = 18,
        // ── unit 4 (매치 담당자) ──
        WaveQueued = 19,
        WaveStarted = 20,
        BonusOffered = 21,
        BonusPulled = 22,
        CostChanged = 23,
        Placed = 24,
        Retired = 25,
        PlacementRejected = 26,
        DefenderActivated = 27,
        HeartChanged = 28,
        HeartCollapsed = 29,
        GimmickAssigned = 30,
        PlacementPhaseChanged = 31,
        // ── unit 6a (효과 슬롯) ──
        ModifierApplied = 32,
        ModifierRevoked = 33,
        StackChanged = 34,
        StackThreshold = 35,
        CcApplied = 36,
        CcCleared = 37,
        DotApplied = 38,
        ShieldGranted = 39,
        DotCleared = 40,
        // ── unit 6a2 (탄 부여) ──
        ImbueChanged = 41,
        // ── unit 6b (판 위에 깔리는 것) ──
        HazardSpawned = 42,
        HazardDestroyed = 43,
        FieldSpawned = 44,
        FieldDespawned = 45,
        // ── unit 6b2 (기믹 셈판) ──
        PickupSpawned = 46,
        PickupTaken = 47,
        ResignationDropped = 48,
        ResignationThreshold = 49,
        PickupExpired = 50,
        ResignationConsumed = 51,
        // unit 6c 후속 — 상태의 끝. `i` = 사유 enum(`AggroReleaseReason` · `LastRunEndReason`).
        AggroReleased = 52,
        LastRunEnded = 53,
        // unit 7a — 규칙. `a` = 소유자, `b` = 대상, `i` = InstanceId, `f` = payload(발동·부착) / 사유(떨어짐).
        TriggerFired = 54,
        BindingAttached = 55,
        BindingDetached = 56,
        // unit 7b — 카드. `a` = 숙주(시전은 판), `i` = 손패 항목 번호, `f` = 부착 묶음 핸들.
        CardAttached = 57,
        CardDetached = 58,
        CardCast = 59,
        // unit 7d — 시즌 기믹. `a` = 주인, `b` = 만든 개체, `i` = `GimmickKind`, `f` = 종류별 값.
        GimmickTriggered = 60,
        // unit 8a2 — 방어유닛 행동 상태 전이(옛 채널 22 `DefenderAiState` 의 후계). `a` = 유닛, `i` = 이후, `f` = 이전.
        // ⚠ 골든 하네스는 구독하지 않는다(`GimmickTriggered` 와 같은 형) — 진단 채널이라 코퍼스를 부풀리지 않는다.
        DefenderAiChanged = 61,
        // ⚠ `SkillVisual` 은 채널이 없다 — 뷰 전용 연출 신호라 규칙을 증언하지 않는다(위 `TryChannel` 주석).
        // ⚠ `ScoreChanged` 는 **채널이 없다.** 처치 사건과 1:1 이라 새 정보가 0 이고
        // (`UnitSlain` + 진영으로 정확히 재구성된다) 총점은 아래 `finalScore` 가 증언한다.
        // 「전부 기록」을 강제하지 않는 이유가 이것이다 — 같은 사실의 두 번째 기록은
        // 골든을 부풀리기만 하고, 부푼 골든은 아무도 diff 하지 않는다.
    }

    public struct CoreTraceEvent
    {
        public int tick;
        public CoreTraceChannel channel;
        public int a;      // 주체 SimEntityId (-1 = 없음)
        public int b;      // 대상 SimEntityId (-1 = 없음)
        public int i;      // 채널별 정수(UnitKind · MatchEndReason 등)
        public float f;    // 채널별 실수(체력·경과 시간 등)

        public bool SameAs(in CoreTraceEvent o)
            => tick == o.tick && channel == o.channel && a == o.a && b == o.b && i == o.i
               && Quantize(f) == Quantize(o.f);

        // 저장 해상도 = 비교 해상도. 옛 포맷과 **같은 함수**를 쓴다 — 여기서 갈리면
        // 「파일로는 같은데 메모리로는 다르다」가 생긴다.
        public static int Quantize(float v) => (int)System.Math.Round(v * 1000.0);   // 1e-3 격자 — 비교와 직렬화가 같은 해상도
    }

    public sealed class CoreTrace
    {
        public const string Magic = "LTV0";
        public const string Series = "core";

        public string scenario = "";
        public string configHash = "";
        public int matchSeed;
        public float stepDt;
        public int tickCount;

        public readonly List<CoreTraceEvent> events = new List<CoreTraceEvent>();

        // 최종 결산 — 대조에서 **exact** 로 보는 정수들.
        public int finalKills;
        public int finalScore;
        public int finalLeaks;
        public ulong finalStateHash;

        /// <summary>
        /// 코어 이벤트 한 건을 기록한다. 버스 배달 중에 불린다.
        /// `in` 을 쓰지 않는 것은 `Action&lt;CoreEvent&gt;` 로 구독하기 위해서다 —
        /// `CoreEvent` 는 값 타입이라 복사 비용이 구독 한 겹보다 싸다.
        /// </summary>
        public void Record(CoreEvent e)
        {
            if (!TryChannel(e.Kind, out var channel)) return;
            events.Add(new CoreTraceEvent
            {
                tick = e.Tick,
                channel = channel,
                a = e.A.Value,
                b = e.B.Value,
                i = e.Arg,
                f = e.Amount,
                // `DefIndex` 는 싣지 않는다 — 뷰 전용 필드라 미기록(unified-effect-layer 계약 7). 채널 여섯 칸이 포맷이고,
                // 탄 사건의 정의 줄(unit 4)도 규칙을 증언하지 않는다 — 골든 무변.
            });
        }

        // 기록하지 않는 종류가 생기면 여기서 걸러진다. 「전부 기록」을 강제하지 않는 이유:
        // 뷰 전용 사건(연출 신호)은 규칙을 증언하지 않아 골든을 부풀리기만 한다.
        private static bool TryChannel(CoreEventKind kind, out CoreTraceChannel channel)
        {
            switch (kind)
            {
                case CoreEventKind.MatchStarted: channel = CoreTraceChannel.MatchStarted; return true;
                case CoreEventKind.UnitSpawned: channel = CoreTraceChannel.UnitSpawned; return true;
                case CoreEventKind.UnitDestroyed: channel = CoreTraceChannel.UnitDestroyed; return true;
                case CoreEventKind.MatchEnded: channel = CoreTraceChannel.MatchEnded; return true;
                case CoreEventKind.GoalReached: channel = CoreTraceChannel.GoalReached; return true;
                case CoreEventKind.Detected: channel = CoreTraceChannel.Detected; return true;
                case CoreEventKind.AggroAcquired: channel = CoreTraceChannel.AggroAcquired; return true;
                case CoreEventKind.Blinked: channel = CoreTraceChannel.Blinked; return true;
                case CoreEventKind.AttackResolved: channel = CoreTraceChannel.AttackResolved; return true;
                case CoreEventKind.ProjectileSpawned: channel = CoreTraceChannel.ProjectileSpawned; return true;
                case CoreEventKind.ProjectileDespawned: channel = CoreTraceChannel.ProjectileDespawned; return true;
                case CoreEventKind.ProjectileHit: channel = CoreTraceChannel.ProjectileHit; return true;
                case CoreEventKind.DamageApplied: channel = CoreTraceChannel.DamageApplied; return true;
                case CoreEventKind.HealApplied: channel = CoreTraceChannel.HealApplied; return true;
                case CoreEventKind.ShieldBroken: channel = CoreTraceChannel.ShieldBroken; return true;
                case CoreEventKind.UnitSlain: channel = CoreTraceChannel.UnitSlain; return true;
                case CoreEventKind.Knockup: channel = CoreTraceChannel.Knockup; return true;
                case CoreEventKind.LeapAscend: channel = CoreTraceChannel.LeapAscend; return true;
                case CoreEventKind.LeapDescend: channel = CoreTraceChannel.LeapDescend; return true;
                // ── unit 4 ──
                case CoreEventKind.WaveQueued: channel = CoreTraceChannel.WaveQueued; return true;
                case CoreEventKind.WaveStarted: channel = CoreTraceChannel.WaveStarted; return true;
                case CoreEventKind.BonusOffered: channel = CoreTraceChannel.BonusOffered; return true;
                case CoreEventKind.BonusPulled: channel = CoreTraceChannel.BonusPulled; return true;
                case CoreEventKind.CostChanged: channel = CoreTraceChannel.CostChanged; return true;
                case CoreEventKind.Placed: channel = CoreTraceChannel.Placed; return true;
                case CoreEventKind.Retired: channel = CoreTraceChannel.Retired; return true;
                case CoreEventKind.PlacementRejected: channel = CoreTraceChannel.PlacementRejected; return true;
                case CoreEventKind.DefenderActivated: channel = CoreTraceChannel.DefenderActivated; return true;
                case CoreEventKind.HeartChanged: channel = CoreTraceChannel.HeartChanged; return true;
                case CoreEventKind.HeartCollapsed: channel = CoreTraceChannel.HeartCollapsed; return true;
                case CoreEventKind.GimmickAssigned: channel = CoreTraceChannel.GimmickAssigned; return true;
                case CoreEventKind.PlacementPhaseChanged: channel = CoreTraceChannel.PlacementPhaseChanged; return true;
                // ── unit 6a ──
                case CoreEventKind.ModifierApplied: channel = CoreTraceChannel.ModifierApplied; return true;
                case CoreEventKind.ModifierRevoked: channel = CoreTraceChannel.ModifierRevoked; return true;
                case CoreEventKind.StackChanged: channel = CoreTraceChannel.StackChanged; return true;
                case CoreEventKind.StackThreshold: channel = CoreTraceChannel.StackThreshold; return true;
                case CoreEventKind.CcApplied: channel = CoreTraceChannel.CcApplied; return true;
                case CoreEventKind.CcCleared: channel = CoreTraceChannel.CcCleared; return true;
                case CoreEventKind.DotApplied: channel = CoreTraceChannel.DotApplied; return true;
                case CoreEventKind.ShieldGranted: channel = CoreTraceChannel.ShieldGranted; return true;
                case CoreEventKind.DotCleared: channel = CoreTraceChannel.DotCleared; return true;
                // ── unit 6a2 ──
                case CoreEventKind.ImbueChanged: channel = CoreTraceChannel.ImbueChanged; return true;
                // ── unit 6b ──
                case CoreEventKind.HazardSpawned: channel = CoreTraceChannel.HazardSpawned; return true;
                case CoreEventKind.HazardDestroyed: channel = CoreTraceChannel.HazardDestroyed; return true;
                case CoreEventKind.FieldSpawned: channel = CoreTraceChannel.FieldSpawned; return true;
                case CoreEventKind.FieldDespawned: channel = CoreTraceChannel.FieldDespawned; return true;
                // ── unit 6b2 ──
                case CoreEventKind.PickupSpawned: channel = CoreTraceChannel.PickupSpawned; return true;
                case CoreEventKind.PickupTaken: channel = CoreTraceChannel.PickupTaken; return true;
                case CoreEventKind.ResignationDropped: channel = CoreTraceChannel.ResignationDropped; return true;
                case CoreEventKind.ResignationThreshold: channel = CoreTraceChannel.ResignationThreshold; return true;
                case CoreEventKind.PickupExpired: channel = CoreTraceChannel.PickupExpired; return true;
                case CoreEventKind.ResignationConsumed: channel = CoreTraceChannel.ResignationConsumed; return true;
                case CoreEventKind.AggroReleased: channel = CoreTraceChannel.AggroReleased; return true;
                case CoreEventKind.LastRunEnded: channel = CoreTraceChannel.LastRunEnded; return true;
                case CoreEventKind.TriggerFired: channel = CoreTraceChannel.TriggerFired; return true;
                case CoreEventKind.BindingAttached: channel = CoreTraceChannel.BindingAttached; return true;
                case CoreEventKind.BindingDetached: channel = CoreTraceChannel.BindingDetached; return true;
                case CoreEventKind.CardAttached: channel = CoreTraceChannel.CardAttached; return true;
                case CoreEventKind.CardDetached: channel = CoreTraceChannel.CardDetached; return true;
                case CoreEventKind.CardCast: channel = CoreTraceChannel.CardCast; return true;
                case CoreEventKind.GimmickTriggered: channel = CoreTraceChannel.GimmickTriggered; return true;
                case CoreEventKind.DefenderAiChanged: channel = CoreTraceChannel.DefenderAiChanged; return true;
                default: channel = default; return false;
            }
        }

        public string Serialize()
        {
            var sb = new StringBuilder(1024 + events.Count * 24);
            var inv = CultureInfo.InvariantCulture;
            sb.Append(Magic).Append('\n');
            sb.Append("channels=").Append(Series).Append('\n');
            sb.Append("scenario=").Append(scenario).Append('\n');
            sb.Append("configHash=").Append(configHash).Append('\n');
            sb.Append("matchSeed=").Append(matchSeed.ToString(inv)).Append('\n');
            // "R" 은 런타임마다 자릿수가 다르다(.NET 9 = 최단 왕복 8자리, Mono = 9자리) —
            // 같은 float 이 다른 문자열로 저장돼 골든 diff 에 헤더 잡음이 낀다. G9 는 둘 다 같다.
            sb.Append("stepDt=").Append(stepDt.ToString("G9", inv)).Append('\n');
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
                  .Append(CoreTraceEvent.Quantize(e.f).ToString(inv)).Append('\n');
            }
            sb.Append("finalKills=").Append(finalKills.ToString(inv)).Append('\n');
            sb.Append("finalScore=").Append(finalScore.ToString(inv)).Append('\n');
            sb.Append("finalLeaks=").Append(finalLeaks.ToString(inv)).Append('\n');
            sb.Append("finalStateHash=").Append(finalStateHash.ToString("X16", inv)).Append('\n');
            return sb.ToString();
        }

        public static CoreTrace Deserialize(string text)
        {
            var inv = CultureInfo.InvariantCulture;
            var t = new CoreTrace();
            var lines = text.Split('\n');
            if (lines.Length == 0 || lines[0] != Magic)
                throw new FormatException($"trace magic mismatch: '{(lines.Length > 0 ? lines[0] : "")}'");

            bool seriesSeen = false;
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
                        case "channels":
                            if (v != Series)
                                throw new FormatException($"trace channel series '{v}' — 코어 골든이 아니다(옛 계열은 CoreTrace 로 읽지 않는다)");
                            seriesSeen = true;
                            break;
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
                t.events.Add(new CoreTraceEvent
                {
                    tick = int.Parse(p[0], inv),
                    channel = (CoreTraceChannel)int.Parse(p[1], inv),
                    a = int.Parse(p[2], inv),
                    b = int.Parse(p[3], inv),
                    i = int.Parse(p[4], inv),
                    f = int.Parse(p[5], inv) / 1000f,
                });
            }
            if (!seriesSeen)
                throw new FormatException("trace 에 'channels=core' 가 없다 — 옛 계열(LTV0 — 채널 어휘가 다르다)이다");
            if (declared != t.events.Count)
                throw new FormatException($"trace declares {declared} events but carries {t.events.Count}");
            return t;
        }

        /// <summary>대조. 첫 불일치의 사람이 읽을 설명을 돌려준다(null = 일치).</summary>
        public string DiffAgainst(CoreTrace other)
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
