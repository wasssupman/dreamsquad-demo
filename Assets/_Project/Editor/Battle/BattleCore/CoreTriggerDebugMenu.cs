#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Trigger;
using Somnia.Battle.BattleCoreUnity;

namespace Somnia.Battle.EditorTools.BattleCore
{
    // battle-core-rebuild unit 7d — **규칙(바인딩) 도구**: 목록 · 강제 발화 · 「왜 안 터졌나」.
    //
    // 왜 있어야 하나: 규칙이 안 터졌을 때 원인이 넷이고(감지자 없음 · 조건 불통과 · 발동 상한 소진 · 떨어짐)
    // 화면에서는 전부 「아무 일도 안 일어났다」로 똑같이 보인다. 도구가 없으면 이 영역에서 「재현이 먼저다」
    // (CLAUDE.md 버그 수정 절차 1)가 집행 불가다.
    //
    // ⚠ 원인 판정은 **코어의 것**(`BindingDiagnosis.Diagnose`)이다 — 이 메뉴는 그 답을 찍기만 한다.
    // ⚠ 강제 발화는 코어 커맨드 `DebugFireBinding`(25)이다 — 카운터·게이트·감지자를 건너뛰고 실행자만 부른다.
    //    **발동 상한은 지킨다**(상한 소진이 원인 하나인데 도구가 그것을 넘으면 원인을 가린다).
    // ⚠ 떨어진 규칙은 등록부 목록에서 빠진다. 그래서 이 판의 `BindingDetached` 사건을 **읽기만 하는 구독**으로
    //    모아 둔다(코어 상태를 고치지 않는다 · 순번 = 트레이스와 같고 나중에 붙어 그 뒤에 선다 · 틱 밖에서 붙인다).
    [InitializeOnLoad]
    public static class CoreTriggerDebugMenu
    {
        private const string Root = "Somnia/Battle/BattleCore/Debug/규칙/";
        private const int DetachLogCap = 256;

        private static BattleMatch _watched;
        private static double _nextProbe;
        private static readonly List<string> DetachLog = new List<string>(DetachLogCap);

        static CoreTriggerDebugMenu()
        {
            EditorApplication.update -= Watch;
            EditorApplication.update += Watch;
        }

        [MenuItem(Root + "규칙 목록 · 왜 안 터졌나")] private static void Dump() => DumpBindings();
        [MenuItem(Root + "강제 발화 — 판 규칙 전부")] private static void FireMatch() => FireAll(SimEntityId.Match);
        [MenuItem(Root + "강제 발화 — 첫 배치 유닛의 규칙 전부")] private static void FireFirstDefender()
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return;
            var units = driver.Match.World.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Kind == UnitKind.Defender && !units[i].Dead && units[i].Bindings.Count > 0)
                {
                    FireAll(units[i].Id);
                    return;
                }
            Debug.LogWarning("[CoreTriggerDebug] 규칙을 든 방어유닛이 없다.");
        }

        [MenuItem(Root + "규칙 목록 · 왜 안 터졌나", true)]
        [MenuItem(Root + "강제 발화 — 판 규칙 전부", true)]
        [MenuItem(Root + "강제 발화 — 첫 배치 유닛의 규칙 전부", true)]
        private static bool Validate() => Application.isPlaying;

        // ── 강제 ─────────────────────────────────────────────────────────────

        /// <summary>규칙 하나를 지금 발동시킨다. 반환 = 코어의 영수증 그대로.</summary>
        public static Receipt Fire(SimEntityId owner, int instanceId)
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return Receipt.Reject(RejectReason.UnknownCommand);
            return driver.Apply(Command.DebugFireBinding(owner, instanceId));
        }

        public static int FireAll(SimEntityId owner)
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return 0;
            var list = ListOf(driver.Match, owner);
            if (list == null) { Debug.LogWarning($"[CoreTriggerDebug] 주인 {owner} 이 판에 없다."); return 0; }
            // 발동이 목록을 바꿀 수 있다(상한 도달 → 떨어짐) — id 를 먼저 떠 둔다.
            var ids = new List<int>(list.Count);
            for (int i = 0; i < list.Count; i++) ids.Add(list[i].InstanceId);
            var sb = new StringBuilder($"[CoreTriggerDebug] 강제 발화 — 주인 {owner} · 규칙 {ids.Count}\n");
            int ok = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                var r = driver.Apply(Command.DebugFireBinding(owner, ids[i]));
                if (r.Accepted) ok++;
                sb.AppendLine($"  #{ids[i]} → {(r.Accepted ? "받아들여짐" : "거절 " + r.Reason)}");
            }
            Debug.Log(sb.ToString());
            return ok;
        }

        // ── 찍기 ─────────────────────────────────────────────────────────────

        public static void DumpBindings()
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return;
            var match = driver.Match;
            var triggers = match.Triggers;
            var sb = new StringBuilder();
            sb.AppendLine($"[CoreTriggerDebug] 틱 {match.Clock.Tick} · 판 규칙 {match.Bindings.MatchBindings.Count}");
            sb.AppendLine("주인 | #id | 이름 | 트리거×페이로드 | 발동/상한 | 카운터/N | 주기 | 수명 | 판정");
            AppendList(sb, "판", match.Bindings.MatchBindings, triggers);
            var units = match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.Bindings.Count == 0) continue;
                AppendList(sb, $"{u.Id}({u.Kind})", u.Bindings, triggers);
            }
            sb.AppendLine($"떨어진 규칙(이 판 · 최근 {DetachLog.Count}):");
            for (int i = 0; i < DetachLog.Count; i++) sb.AppendLine("  " + DetachLog[i]);
            Debug.Log(sb.ToString());
        }

        private static void AppendList(StringBuilder sb, string owner, IReadOnlyList<Binding> list, TriggerDispatcher triggers)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                ref var d = ref b.Def;
                var status = BindingDiagnosis.Diagnose(b, triggers);
                string cap = d.FireCap > 0 ? d.FireCap.ToString() : "∞";
                string counter = d.Period > 0 ? $"{b.Counter}/{d.Period}" : "-";
                string period = d.Trigger == TriggerKind.PeriodicTimer ? $"{b.Elapsed:0.0}/{d.PeriodSeconds:0.0}s" : "-";
                string life = d.Lifetime == BindingLifetime.Timed ? $"{b.Remaining:0.0}s" : d.Lifetime.ToString();
                sb.AppendLine($"{owner} | #{b.InstanceId} | {d.Label} | {d.Trigger}×{b.Effect.Kind} | {b.FireCount}/{cap} | "
                    + $"{counter} | {period} | {life} | {Explain(status, d.Trigger, triggers)}");
            }
        }

        private static string Explain(BindingStatus s, TriggerKind trigger, TriggerDispatcher triggers)
        {
            switch (s)
            {
                case BindingStatus.Firing: return "터지고 있다";
                case BindingStatus.NoDetector:
                    return trigger == TriggerKind.None
                        ? "⑴ 감지자 없음 — 부착 즉시 규칙인데 부착 문이 안 열렸다"
                        : $"⑴ 감지자 없음 — 이 판에 {trigger} 사실이 한 번도 안 올라왔다";
                case BindingStatus.ConditionNotMet:
                    return TriggerDispatcher.IsPolled(trigger)
                        ? "⑵ 조건 불통과 — 주기·경계가 아직이다"
                        : $"⑵ 조건 불통과 — {trigger} 사실 {triggers.SensedCount(trigger)}회, 카운터·게이트·주어 필터가 안 찼다";
                case BindingStatus.FireCapSpent: return "⑶ 발동 상한 소진";
                case BindingStatus.Detached: return "⑷ 떨어졌다(아래 목록의 사유)";
                default: return s.ToString();
            }
        }

        private static IReadOnlyList<Binding> ListOf(BattleMatch match, SimEntityId owner)
        {
            if (owner.IsNone || owner == SimEntityId.Match) return match.Bindings.MatchBindings;
            var u = match.World.Find(owner);
            return u != null ? u.Bindings : null;
        }

        // ── 떨어짐 기록 — 판마다 한 번 구독(읽기 전용) ───────────────────────────

        private static void Watch()
        {
            if (!Application.isPlaying) { _watched = null; return; }
            if (EditorApplication.timeSinceStartup < _nextProbe) return;
            _nextProbe = EditorApplication.timeSinceStartup + 0.25;
            var driver = Object.FindAnyObjectByType<BattleDriver>();
            var match = driver != null ? driver.Match : null;
            if (match == null || ReferenceEquals(match, _watched)) return;
            _watched = match;
            DetachLog.Clear();
            match.Bus.Subscribe(CoreEventKind.BindingDetached, EventOrder.Trace, e =>
            {
                if (DetachLog.Count >= DetachLogCap) DetachLog.RemoveAt(0);
                DetachLog.Add($"틱 {e.Tick} · 주인 {e.A} · #{e.Arg} · 줄 {e.DefIndex} · 사유 {(BindingDetachReason)(int)e.Amount}");
            });
        }
    }
}
#endif
