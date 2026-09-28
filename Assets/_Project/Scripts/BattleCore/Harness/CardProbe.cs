using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Mathematics;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;

namespace Wassup.BattleCore
{
    /// <summary>프로브 한 장의 결과. ○ = 구워졌고 · 발동했고 · 기대한 신호가 전부 판에 남았다.</summary>
    public struct CardProbeResult
    {
        public int Row;
        public string CardId;
        public CardKind Kind;
        /// <summary>정의표에 실행할 것이 있나(규칙 줄마다 실행자 · 공격 수식자 · 인수인계 선언 · 액티브 규칙).</summary>
        public bool Baked;
        /// <summary>그 카드의 규칙이 실제로 발동했나(`TriggerFired`). 수식자·인수인계는 「달렸다 / 퇴근이 받아들여졌다」.</summary>
        public bool Fired;
        /// <summary>관측된 기대(의도 종류 이름 · `AttackMod:종류` · `RecallToFront`).</summary>
        public string[] Witnessed;
        /// <summary>관측 안 된 기대. 하나라도 있으면 ×.</summary>
        public string[] Missing;
        /// <summary>규칙이 있으면 그 판정(`BindingDiagnosis`) — 발동하지 않은 첫 규칙의 원인, 전부 발동했으면 `Firing`.</summary>
        public BindingStatus Diagnosis;
        public bool HasDiagnosis;
        /// <summary>프로브가 세운 숙주(정의표 id). 액티브는 조준 칸 옆 아군.</summary>
        public string Host;
        /// <summary>× 의 단서 — 거절 사유 · 발동 판이 코어에게서 들은 첫 경고.</summary>
        public string Note;

        public bool Ok => Baked && Fired && Missing != null && Missing.Length == 0
                          && Witnessed != null && Witnessed.Length > 0;

        public override string ToString()
            => $"{CardId}: {(Ok ? "○" : "×")} 구움={(Baked ? "○" : "×")} 발동={(Fired ? "○" : "×")} "
               + $"관측=[{string.Join(",", Witnessed ?? System.Array.Empty<string>())}] "
               + $"미관측=[{string.Join(",", Missing ?? System.Array.Empty<string>())}] "
               + $"진단={(HasDiagnosis ? Diagnosis.ToString() : "-")} 숙주={Host ?? "-"}"
               + (string.IsNullOrEmpty(Note) ? "" : " · " + Note);
    }

    /// <summary>
    /// 테스트 전용 봉합점. `MuteIntents` = 카드의 실행자가 낸 의도를 **기록은 하되 적용하지 않는다** —
    /// 「구워졌고 발동했는데 아무 일도 없다」를 이 장치가 실제로 ×로 잡는다는 반증용(7e 완료 기준 ① 반증).
    /// </summary>
    public struct CardProbeOptions
    {
        public bool MuteIntents;
    }

    // battle-core-rebuild unit 7e — **카드 한 장을 판 하나에 세워 「걸렸나」를 증언한다.** 판정·상태를 갖지 않는 도구다.
    //
    // 하는 일(한 장당):
    //   ① 정의표를 **복사해** 프로브 판을 만든다 — 빈 판 · 웨이브 0 · 거점 0 · 효과 타일 0 · 판 규칙(드림스톤) 0.
    //      원본은 건드리지 않는다(③ 자가진단이 살아 있는 판의 정의표를 빌려도 사용자 판이 무변인 이유).
    //   ② 그 카드의 규칙 줄마다 실행자를 **기록기로 감싼다**(`SkillId` 그대로 — 라우팅·진단·형 카탈로그가 같은 답을 받는다).
    //      기대 의도는 카드마다 적지 않는다 — 실행자가 실제로 낸 의도가 곧 기대다.
    //   ③ 숙주 1 · 표적 적 3 을 **그 규칙의 형 안에** 세우고(`RangeCatalog` → `SkillMath.TryOriginRadius` →
    //      `SkillMath.ReachWithOrigin` — 제약 13 정본 진입점만), 붙이고(22) 또는 시전하고(23), 강제 발동(25)한다.
    //   ④ 같은 판을 카드 없이 한 번 더 돌려 **대조**하고 `EffectWitness` 로 판정한다.
    //
    // 강제 발동은 규칙을 우회하지 않는다(7d): 상한은 지키고, 「왜 안 터졌나」는 ×가 아니라 진단(`BindingDiagnosis`)으로 찍힌다.
    // ⚠ 예외 하나 — `AttackN` 은 25 가 대상 없는 사건을 만들어(공격 사건의 대상은 감지자만 안다) 대상형 실행자가 할 일이 없다.
    //    그래서 그 종류만 **감지자(`RaiseAttack`)와 같은 모양의 사건**을 공격 seam 에 건다. 카운터·게이트를 건너뛰는 것은 25 와 같다.
    //
    // 결정론: 시드 = 정의표 시드, 틱 고정, 후보 순회 = 정의표 순. 같은 입력이면 같은 표다.
    public static class CardProbe
    {
        // ── 구조 상수(밸런스 값이 아니다) ─────────────────────────────────────
        // 판: 가장 큰 발자국(2×3)과 표적 고리(1칸)가 들어가고 골까지 여유가 있는 최소 크기.
        private const int BoardWidth = 17;
        private const int BoardHeight = 9;
        private static readonly int2 HostCell = new int2(8, 4);
        // 표적 수: 「여럿을 겨누는」 실행자(재울 수 · 대상 수)가 0 이 아닌 답을 내는 최소 + 한 칸 방향이 셋.
        private const int TargetCount = 3;
        private static readonly int2[] RingDirections = { new int2(1, 0), new int2(-1, 0), new int2(0, 1) };
        // 관측 창: 최소 2틱(요청 소비 한 번 + 사건 배달 한 번) · 최대 5초(가장 긴 첫 발 간격보다 넉넉한 상한).
        private const int MinWindowTicks = 2;
        private const int MaxWindowTicks = 300;
        // 실드 전제(파열 규칙): 부여는 다음 틱 드레인이라 두 틱 기다린다.
        private const int ShieldSettleTicks = 2;
        // 포탈 둘째 칸: 입구와 겹치지 않는(같은 칸은 거절이다) 판 안의 칸.
        private static readonly int2 SecondCellOffset = new int2(3, 0);

        // ── 진입 ──────────────────────────────────────────────────────────────

        /// <summary>정의표의 카드 전부. 순서 = 카드 줄 순서.</summary>
        public static CardProbeResult[] RunAll(MatchDefinition source, CardProbeOptions options = default)
        {
            var results = new CardProbeResult[source.Cards.Length];
            for (int i = 0; i < results.Length; i++) results[i] = Run(source, i, options);
            return results;
        }

        public static CardProbeResult Run(MatchDefinition source, int cardRow, CardProbeOptions options = default)
        {
            var result = new CardProbeResult
            {
                Row = cardRow,
                Witnessed = System.Array.Empty<string>(),
                Missing = System.Array.Empty<string>(),
            };
            if (source == null || cardRow < 0 || cardRow >= source.Cards.Length)
            {
                result.CardId = "?";
                result.Missing = new[] { "카드 줄 없음" };
                return result;
            }
            ref var card = ref source.Cards[cardRow];
            result.CardId = card.Id;
            result.Kind = card.Kind;
            result.Baked = IsBaked(source, cardRow, out string bakeNote);
            if (!result.Baked)
            {
                result.Missing = new[] { "굽기" };
                result.Note = bakeNote;
                return result;
            }

            var log = new IntentLog { Mute = options.MuteIntents };
            var probe = ProbeDefinition(source, cardRow, log);
            int enemyDef = ChooseEnemy(probe);
            if (enemyDef < 0)
            {
                result.Missing = new[] { "표적 적 정의 없음" };
                return result;
            }

            if (card.Kind == CardKind.Active) return RunActive(probe, cardRow, enemyDef, log, result);

            bool hasRules = (card.Bindings?.Length ?? 0) + (card.SquadBindings?.Length ?? 0)
                            + (card.AttackMods?.Length ?? 0) > 0;
            var hosts = HostCandidates(probe, card.TargetsEnemies);
            CardProbeResult? firstAttached = null;
            string firstReject = null;
            for (int h = 0; h < hosts.Count; h++)
            {
                var r = result;
                bool attached = true;
                if (hasRules) attached = AttemptAttach(probe, cardRow, hosts[h], enemyDef, log, ref r, ref firstReject);
                if (attached && card.DeclaresRetireRecall)
                    attached = AttemptRecall(probe, cardRow, hosts[h], enemyDef, ref r, ref firstReject, merge: hasRules);
                if (!attached) continue;
                if (r.Ok) return r;
                // × 로 보고할 숙주: 규칙이 **발동한** 첫 숙주(없으면 붙은 첫 숙주) — 「붙었지만 대상이 없어 안 돈」 숙주보다 단서가 많다.
                if (firstAttached == null || (!firstAttached.Value.Fired && r.Fired)) firstAttached = r;
            }
            if (firstAttached != null) return firstAttached.Value;
            result.Missing = new[] { "붙는 숙주 없음" };
            result.Note = firstReject;
            return result;
        }

        // ── 굽기 ──────────────────────────────────────────────────────────────

        private static bool IsBaked(MatchDefinition def, int row, out string note)
        {
            note = null;
            ref var card = ref def.Cards[row];
            if (card.Kind == CardKind.Active)
            {
                int a = card.ActiveBinding;
                if (a < 0 || a >= def.Bindings.Length || def.Bindings[a].Origin != BindingOrigin.Card)
                { note = "액티브 규칙 줄이 없다"; return false; }
                if (!def.Bindings[a].HasEffect) { note = "액티브 규칙에 실행자가 없다"; return false; }
                return true;
            }
            int n = 0;
            foreach (var r in Rows(card))
            {
                if (r < 0 || r >= def.Bindings.Length) { note = $"규칙 줄 {r} 이 표 밖이다"; return false; }
                if (!def.Bindings[r].HasEffect) { note = $"규칙 줄 {r}({def.Bindings[r].Label})에 실행자가 없다"; return false; }
                n++;
            }
            n += card.AttackMods?.Length ?? 0;
            if (card.DeclaresRetireRecall) n++;
            if (n == 0) note = "싣는 것이 없다";
            return n > 0;
        }

        private static IEnumerable<int> Rows(CardDef card)
        {
            if (card.Bindings != null) foreach (var r in card.Bindings) yield return r;
            if (card.SquadBindings != null) foreach (var r in card.SquadBindings) yield return r;
            if (card.Kind == CardKind.Active && card.ActiveBinding >= 0) yield return card.ActiveBinding;
        }

        private static bool IsSquadRow(in CardDef card, int row)
        {
            if (card.SquadBindings == null) return false;
            for (int i = 0; i < card.SquadBindings.Length; i++) if (card.SquadBindings[i] == row) return true;
            return false;
        }

        // ── 프로브 정의표 ─────────────────────────────────────────────────────

        private static MatchDefinition ProbeDefinition(MatchDefinition source, int cardRow, IntentLog log)
        {
            // 공개 칸을 **전부** 옮긴다 — 칸이 늘어도 이 복사가 낡지 않게(값 칸은 복사, 배열은 공유 — 코어는 정의표를 안 고친다).
            var def = new MatchDefinition();
            foreach (var f in typeof(MatchDefinition).GetFields(System.Reflection.BindingFlags.Public
                                                                | System.Reflection.BindingFlags.Instance))
                if (!f.IsInitOnly) f.SetValue(def, f.GetValue(source));

            var map = new MapSnapshot
            {
                Width = BoardWidth,
                Height = BoardHeight,
                TileSize = 1f,
                Goals = new[] { new int2(BoardWidth - 1, HostCell.y) },
                Spawns = new[] { new int2(0, HostCell.y) },
            };
            map.Normalize();
            def.Map = map;
            def.Structures = System.Array.Empty<StructureDef>();
            def.EffectTileCount = 0;
            def.WaveDeck = Wave.WaveDeckDef.Empty();
            def.WavePlan = default;
            def.Bonus = Wave.BonusWaveDef.None();
            def.Heart = default;
            def.Gimmicks = System.Array.Empty<GimmickDef>();
            def.MatchBindings = System.Array.Empty<int>();   // 드림스톤은 카드가 아니다 — 대조를 흐린다
            def.CostRateMultiplier = 1f;

            // 모드: 배치 창 없음(곧장 전투) · 기믹 없음 · 퇴근 허용 · 손패 = 덱 전부 · 각성 = 전 카드를 한 번씩 치를 값.
            float awakening = 1f;
            for (int i = 0; i < source.Cards.Length; i++) awakening += math.max(0, source.Cards[i].Cost);
            var mode = ModeDef.Default();
            mode.ModeId = "card_probe";
            mode.GimmickEnabled = false;
            mode.PlacementInputEnabled = false;
            mode.PlacementSeconds = 0f;
            mode.RetireEnabled = true;
            mode.BoardCap = 0;
            mode.HandSize = math.max(1, source.Cards.Length);
            mode.AttachCap = math.max(1, source.Cards.Length);
            mode.Awakening = new AwakeningDef { Start = awakening, Max = awakening };
            mode.Cost = source.Mode.Cost;
            def.Mode = mode;

            // 그 카드의 규칙 줄만 기록기로 감싼다(표는 복사본이다).
            var rows = (BindingDef[])source.Bindings.Clone();
            foreach (var r in Rows(source.Cards[cardRow]))
                if (r >= 0 && r < rows.Length && rows[r].Skill != null && !(rows[r].Skill is RecordingSkill))
                    rows[r].Skill = new RecordingSkill(rows[r].Skill, log);
            def.Bindings = rows;
            def.ConfigHash = "";
            return def;
        }

        // 표적 적: 땅으로 걷고 · 분열하지 않고 · 스스로 규칙을 들지 않는 첫 줄(잡음이 없는 과녁). 없으면 첫 줄.
        private static int ChooseEnemy(MatchDefinition def)
        {
            for (int i = 0; i < def.Enemies.Length; i++)
            {
                ref var e = ref def.Enemies[i];
                if (e.Health <= 0f) continue;
                if ((e.TraversalLayers & LayerBits.Path) == 0 || (e.TraversalLayers & LayerBits.Air) != 0) continue;
                if (e.SplitCount > 0 || (e.Bindings?.Length ?? 0) > 0) continue;
                return i;
            }
            return def.Enemies.Length > 0 ? 0 : -1;
        }

        // 숙주 후보: 적 표식이면 표적 적 자신(-1), 아니면 로스터 순(비면 정의표 전체).
        private static List<int> HostCandidates(MatchDefinition def, bool enemyHost)
        {
            var list = new List<int>();
            if (enemyHost) { list.Add(-1); return list; }
            if (def.Roster != null && def.Roster.Length > 0) list.AddRange(def.Roster);
            else for (int i = 0; i < def.Units.Length; i++) list.Add(i);
            return list;
        }

        // ── 한 판 ─────────────────────────────────────────────────────────────

        private sealed class Board
        {
            public BattleMatch M;
            public Unit Host;
            public readonly List<Unit> Targets = new List<Unit>(TargetCount);
            public readonly WitnessFrame Events = new WitnessFrame();
            public readonly List<WitnessFrame> Frames = new List<WitnessFrame>(16);
            public readonly List<CoreEvent> Captured = new List<CoreEvent>(16);
            public readonly List<string> Reports = new List<string>(4);

            /// <summary>배달된 사건을 접고 이 시점의 관측을 하나 남긴다.</summary>
            public void Step(bool record)
            {
                var outbox = M.Events;
                for (int i = 0; i < outbox.Count; i++)
                {
                    var e = outbox[i];
                    EffectWitness.Fold(in e, Events);
                    if (e.Kind == CoreEventKind.TriggerFired || e.Kind == CoreEventKind.BindingAttached) Captured.Add(e);
                }
                M.ClearEvents();
                if (!record) return;
                var f = Events.Clone();
                EffectWitness.Observe(M, f);
                Frames.Add(f);
            }
        }

        private static Board NewBoard(MatchDefinition def)
        {
            var b = new Board { M = new BattleMatch(def) };
            b.M.Report = msg => { if (!msg.StartsWith("[WaveScheduler]")) b.Reports.Add(msg); };
            b.M.Begin();
            b.Step(record: false);
            return b;
        }

        private static Unit Last(BattleMatch m)
        {
            var u = m.World.Units;
            return u.Count > 0 ? u[u.Count - 1] : null;
        }

        /// <summary>숙주와 표적을 세운다. `hostDef` -1 = 표적 적 하나가 숙주(적 표식).</summary>
        private static void Populate(Board b, int hostDef, int enemyDef, int ring, int2 aroundCell, bool hostAtAround)
        {
            var m = b.M;
            if (hostDef >= 0)
            {
                m.Apply(Command.DebugSpawnDefender(hostDef, hostAtAround ? aroundCell : HostCell));
                b.Host = Last(m);
            }
            int2 center = hostDef >= 0 && hostAtAround ? m.Map.CellOf(b.Host.Position) : aroundCell;
            for (int k = 0; k < TargetCount; k++)
            {
                var c = math.clamp(center + RingDirections[k] * ring, int2.zero,
                                   new int2(BoardWidth - 1, BoardHeight - 1));
                m.Apply(Command.DebugSpawnEnemy(enemyDef, c));
                b.Targets.Add(Last(m));
            }
            if (hostDef < 0) b.Host = b.Targets[0];
            b.Step(record: false);
        }

        // 형 안의 고리: 모든 원형 규칙이 세 방향 표적 전부에 닿는 가장 먼 고리(1칸 → 같은 칸). 판정은 정본 진입점만.
        private static int ChooseRing(MatchDefinition def, int cardRow, float3 hostPos, float hostBody,
                                      float enemyBody, MapRuntime map)
        {
            var hostCell = map.CellOf(hostPos);
            for (int ring = 1; ring >= 0; ring--)
            {
                bool all = true;
                foreach (var r in Rows(def.Cards[cardRow]))
                {
                    if (r < 0 || r >= def.Bindings.Length) continue;
                    ref var d = ref def.Bindings[r];
                    var spec = RangeCatalog.Resolve(d.Trigger, d.Payload, d.TileRange);
                    if (spec.Shape != RangeShape.Circle) continue;
                    if (!SkillMath.TryOriginRadius(spec.Metric, hostBody, out float originR)) continue;
                    for (int k = 0; k < TargetCount && all; k++)
                    {
                        var p = map.CenterOf(hostCell + RingDirections[k] * ring);
                        float inv = 1f / math.max(1e-6f, map.TileSize);
                        all = SkillMath.ReachWithOrigin((p.x - hostPos.x) * inv, (p.z - hostPos.z) * inv,
                                                        spec.RadiusTiles, originR, enemyBody);
                    }
                    if (!all) break;
                }
                if (all) return ring;
            }
            return 0;
        }

        // ── 부착 카드 ─────────────────────────────────────────────────────────

        private static bool AttemptAttach(MatchDefinition def, int cardRow, int hostDef, int enemyDef, IntentLog log,
                                          ref CardProbeResult result, ref string firstReject)
        {
            ref var card = ref def.Cards[cardRow];
            bool shieldFirst = false;
            foreach (var r in Rows(card))
                if (r >= 0 && r < def.Bindings.Length && def.Bindings[r].Trigger == TriggerKind.OnShieldBreak) shieldFirst = true;

            // 고리는 숙주의 몸과 자리로 정한다 — 숙주를 먼저 세워 본다(같은 판을 다시 세우므로 결정론은 그대로다).
            int ring = 1;
            {
                var scout = NewBoard(def);
                if (hostDef >= 0)
                {
                    scout.M.Apply(Command.DebugSpawnDefender(hostDef, HostCell));
                    var h = Last(scout.M);
                    ring = ChooseRing(def, cardRow, h.Position, h.HitRadius, def.Enemies[enemyDef].BodyRadius, scout.M.Map);
                }
                else
                {
                    var hb = def.Enemies[enemyDef].BodyRadius;
                    ring = ChooseRing(def, cardRow, scout.M.Map.CenterOf(HostCell), hb, hb, scout.M.Map);
                }
            }

            log.Reset();
            var fired = NewBoard(def);
            Populate(fired, hostDef, enemyDef, ring, HostCell, hostAtAround: true);
            if (shieldFirst) GrantShieldAndSettle(fired);

            var receipt = fired.M.Apply(Command.DebugAttachCard(cardRow, fired.Host.Id));
            if (!receipt.Accepted)
            {
                if (firstReject == null) firstReject = $"부착 거절 {receipt.Reason}";
                return false;
            }
            result.Host = hostDef >= 0 ? def.Units[hostDef].Id : def.Enemies[enemyDef].Id;

            // 이 카드가 붙인 규칙(부착 사건 — 줄 번호로 가린다).
            var instances = new List<int>(4);
            var squadInstances = new HashSet<int>();
            var outbox = fired.M.Events;
            var cardRows = new HashSet<int>(Rows(card));
            for (int i = 0; i < outbox.Count; i++)
            {
                var e = outbox[i];
                if (e.Kind != CoreEventKind.BindingAttached || !cardRows.Contains(e.DefIndex)) continue;
                instances.Add(e.Arg);
                if (IsSquadRow(in card, e.DefIndex)) squadInstances.Add(e.Arg);
            }
            instances.Sort();

            // 공격 수식자 — 이 숙주에서 실제로 붙는 것(판정 = 커밋과 같은 함수).
            var planMods = new List<Combat.AttackModDef>(2);
            CardBindings.Plan(def, cardRow, fired.Host, null, null, planMods);
            fired.Step(record: false);

            // 강제 발동. 부착 즉시 규칙(`None`)은 부착이 이미 발화했고, Squad 줄은 부착이 판 위 대상에 이미 폈다
            // (25 로 다시 쏘면 주어 필터를 지나지 않는다 — 규칙 우회).
            for (int i = 0; i < instances.Count; i++)
            {
                if (squadInstances.Contains(instances[i])) continue;
                var b = Find(fired.Host.Bindings, instances[i]);
                if (b == null || b.Def.Trigger == TriggerKind.None) continue;
                if (b.Def.Trigger == TriggerKind.AttackN) RaiseAttack(fired, b);
                else fired.M.Apply(Command.DebugFireBinding(b.Owner, b.InstanceId));
            }

            var control = NewBoard(def);
            Populate(control, hostDef, enemyDef, ring, HostCell, hostAtAround: true);
            if (shieldFirst) GrantShieldAndSettle(control);

            Observe(fired, control, log, def);

            // 판정.
            var expected = Expected(log);
            for (int i = 0; i < planMods.Count; i++)
                expected.Add(new Expect("AttackMod:" + planMods[i].Kind, WitnessSignal.AttackMod, fired.Host.Id.Value));
            Judge(expected, fired, control, ref result);

            bool anyFire = false;
            foreach (var e in fired.Captured)
                if (e.Kind == CoreEventKind.TriggerFired && instances.Contains(e.Arg)) anyFire = true;
            result.Fired = anyFire || (instances.Count == 0 && planMods.Count > 0);

            if (instances.Count > 0)
            {
                result.HasDiagnosis = true;
                result.Diagnosis = BindingStatus.Firing;
                for (int i = 0; i < instances.Count; i++)
                {
                    var b = Find(fired.Host.Bindings, instances[i]);
                    var s = b != null ? BindingDiagnosis.Diagnose(b, fired.M.Triggers) : BindingStatus.Detached;
                    // 발동 상한을 다 써서 떨어진 규칙(부착 즉시 1회)은 **터진 것**이다 — 발동 사건이 증언한다.
                    if (b == null && Fired(fired, instances[i])) s = BindingStatus.Firing;
                    if (s != BindingStatus.Firing && s != BindingStatus.FireCapSpent) { result.Diagnosis = s; break; }
                }
            }
            if (fired.Reports.Count > 0) result.Note = fired.Reports[0];
            return true;
        }

        private static bool Fired(Board b, int instance)
        {
            foreach (var e in b.Captured)
                if (e.Kind == CoreEventKind.TriggerFired && e.Arg == instance) return true;
            return false;
        }

        private static Binding Find(List<Binding> list, int instance)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].InstanceId == instance) return list[i];
            return null;
        }

        // 공격 사건 — `TriggerDispatcher.RaiseAttack` 과 같은 모양(대표 대상 = 첫 표적).
        private static void RaiseAttack(Board b, Binding binding)
        {
            var host = b.Host;
            Unit target = null;
            for (int i = 0; i < b.Targets.Count; i++)
                if (b.Targets[i] != host && !b.Targets[i].Dead) { target = b.Targets[i]; break; }
            var e = TriggerDispatcher.SubjectOf(host, Seam.Attack, TriggerKind.AttackN);
            if (target != null)
            {
                e.Target = target.Id;
                e.TargetHp = target.Health;
                e.TargetMaxHp = target.MaxHealth;
                e.HasSite = true;
                e.Site = target.Position;
                e.SiteBody = target.HitRadius;
                e.Direction = math.normalizesafe((target.Position - host.Position).xz);
            }
            e.TargetLayers = host.Attack != null ? host.Attack.TargetLayers : (byte)0;
            b.M.Triggers.RaiseFor(binding, in e);
        }

        // 실드 전제 — 쓰기 표면 하나(`IntentApplier`)로 건다. 양은 숙주 최대 체력(파열 전에 한 방에 안 깨지는 값).
        private static void GrantShieldAndSettle(Board b)
        {
            var id = CoreSkillContext.ToSkill(b.Host.Id);
            b.M.Intents.Apply(new SimIntent
            {
                Kind = SimIntentKind.GrantShield, Target = id, Source = id, Amount = math.max(1f, b.Host.MaxHealth),
            });
            for (int t = 0; t < ShieldSettleTicks; t++) { b.M.Tick(); b.Step(record: false); }
        }

        // ── 인수인계 ──────────────────────────────────────────────────────────

        private static bool AttemptRecall(MatchDefinition def, int cardRow, int hostDef, int enemyDef,
                                          ref CardProbeResult result, ref string firstReject, bool merge)
        {
            if (hostDef < 0) return false;
            var fired = NewBoard(def);
            Populate(fired, hostDef, enemyDef, 1, HostCell, hostAtAround: true);
            // 동반 카드: 이 숙주에 붙는 다른 부착 카드 중 정의표 순 첫 장(인수인계 선언 · 적 표식 제외).
            int companion = -1;
            for (int c = 0; c < def.Cards.Length && companion < 0; c++)
            {
                if (c == cardRow) continue;
                ref var cd = ref def.Cards[c];
                if (cd.Kind != CardKind.Attach || cd.TargetsEnemies || cd.DeclaresRetireRecall) continue;
                if (fired.M.Hand.WouldAttach(c, fired.Host.Id) == RejectReason.None) companion = c;
            }
            if (companion < 0) { if (firstReject == null) firstReject = "동반 카드가 붙는 숙주가 아니다"; return false; }

            var a = fired.M.Apply(Command.AttachCard(EntryOf(fired.M, companion), fired.Host.Id));
            var d = fired.M.Apply(Command.AttachCard(EntryOf(fired.M, cardRow), fired.Host.Id));
            if (!a.Accepted || !d.Accepted)
            {
                if (firstReject == null) firstReject = $"부착 거절 {(a.Accepted ? d.Reason : a.Reason)}";
                return false;
            }
            var retire = fired.M.Apply(Command.Retire(fired.Host.Id));

            var control = NewBoard(def);
            Populate(control, hostDef, enemyDef, 1, HostCell, hostAtAround: true);
            control.M.Apply(Command.AttachCard(EntryOf(control.M, companion), control.Host.Id));
            control.M.Apply(Command.Retire(control.Host.Id));

            fired.Step(record: true);
            control.Step(record: true);

            var expected = new List<Expect> { new Expect("RecallToFront", WitnessSignal.HandFront, companion) };
            var r = result;
            r.Host = def.Units[hostDef].Id;
            if (merge)
            {
                var more = new CardProbeResult { Witnessed = System.Array.Empty<string>(), Missing = System.Array.Empty<string>() };
                Judge(expected, fired, control, ref more);
                r.Witnessed = Concat(r.Witnessed, more.Witnessed);
                r.Missing = Concat(r.Missing, more.Missing);
                r.Fired = r.Fired && retire.Accepted;
            }
            else
            {
                Judge(expected, fired, control, ref r);
                r.Fired = retire.Accepted;
            }
            if (!retire.Accepted) r.Note = $"퇴근 거절 {retire.Reason}";
            result = r;
            return true;
        }

        private static int EntryOf(BattleMatch m, int cardIndex)
        {
            var hand = new List<HandDeck.Entry>(m.Definition.Cards.Length);
            m.Hand.Hand(hand);
            for (int i = 0; i < hand.Count; i++) if (hand[i].CardIndex == cardIndex) return hand[i].EntryId;
            return -1;
        }

        // ── 액티브 ────────────────────────────────────────────────────────────

        private static CardProbeResult RunActive(MatchDefinition def, int cardRow, int enemyDef, IntentLog log,
                                                 CardProbeResult result)
        {
            ref var card = ref def.Cards[cardRow];
            var hosts = HostCandidates(def, enemyHost: false);
            CardProbeResult? first = null;
            for (int h = 0; h < hosts.Count; h++)
            {
                var r = result;
                log.Reset();
                // 조준 칸 = 아군 숙주의 칸(아군 장 · 스탯 버스트가 받을 자) · 표적 적은 그 칸의 고리 1.
                var fired = NewBoard(def);
                Populate(fired, hosts[h], enemyDef, 1, HostCell, hostAtAround: true);
                var cellA = fired.M.Map.CellOf(fired.Host.Position);
                var cellB = math.clamp(cellA + SecondCellOffset, int2.zero, new int2(BoardWidth - 1, BoardHeight - 1));
                var receipt = fired.M.Apply(Command.DebugCastCard(cardRow, cellA, cellB, card.NeedsTwoCells));
                r.Host = def.Units[hosts[h]].Id;
                if (!receipt.Accepted)
                {
                    r.Missing = new[] { "시전 거절" };
                    r.Note = $"시전 거절 {receipt.Reason}";
                    if (first == null) first = r;
                    continue;
                }

                var control = NewBoard(def);
                Populate(control, hosts[h], enemyDef, 1, HostCell, hostAtAround: true);
                Observe(fired, control, log, def);

                Judge(Expected(log), fired, control, ref r);
                bool fire = false;
                foreach (var e in fired.Captured)
                    if (e.Kind == CoreEventKind.TriggerFired && e.DefIndex == card.ActiveBinding) fire = true;
                r.Fired = fire;
                if (fired.Reports.Count > 0) r.Note = fired.Reports[0];
                if (r.Ok) return r;
                if (first == null || (!first.Value.Fired && r.Fired)) first = r;
            }
            if (first != null) return first.Value;
            result.Missing = new[] { "아군 숙주 없음" };
            return result;
        }

        // ── 관측 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 두 판을 같은 틱만큼 굴리며 관측을 쌓는다. 창의 길이 = 기록된 의도마다 「낸 시점 + 드러나기까지」의 최댓값
        /// (`EffectWitness.LatencyTicks`) — 대상형 공격 규칙은 첫 틱에 발동하므로 창은 굴리면서 늘어난다.
        /// </summary>
        private static int Observe(Board fired, Board control, IntentLog log, MatchDefinition def)
        {
            fired.Step(record: true);
            control.Step(record: true);
            int needed = MinWindowTicks;
            int seen = 0;
            needed = Extend(needed, log, ref seen, 0, def);
            int t = 0;
            while (t < needed && t < MaxWindowTicks)
            {
                t++;
                fired.M.Tick();
                fired.Step(record: true);
                control.M.Tick();
                control.Step(record: true);
                needed = Extend(needed, log, ref seen, t, def);
            }
            return t;
        }

        private static int Extend(int needed, IntentLog log, ref int seen, int t, MatchDefinition def)
        {
            for (; seen < log.Intents.Count; seen++)
            {
                var i = log.Intents[seen];
                needed = math.max(needed, t + EffectWitness.LatencyTicks(in i, def));
            }
            return needed;
        }

        private readonly struct Expect
        {
            public readonly string Name;
            public readonly WitnessSignal Signal;
            public readonly int Subject;

            public Expect(string name, WitnessSignal signal, int subject)
            {
                Name = name; Signal = signal; Subject = subject;
            }
        }

        private static List<Expect> Expected(IntentLog log)
        {
            var list = new List<Expect>(log.Intents.Count + log.Meta.Count);
            for (int i = 0; i < log.Intents.Count; i++)
            {
                var intent = log.Intents[i];
                var s = EffectWitness.SignalOf(intent.Kind);
                if (s == WitnessSignal.None) continue;
                list.Add(new Expect(intent.Kind.ToString(), s, EffectWitness.SubjectOf(in intent)));
            }
            for (int i = 0; i < log.Meta.Count; i++)
            {
                var s = EffectWitness.SignalOf(log.Meta[i].Kind);
                if (s == WitnessSignal.None) continue;
                list.Add(new Expect(log.Meta[i].Kind.ToString(), s, 0));
            }
            return list;
        }

        // 이름마다: 그 이름의 기대 중 하나라도 어느 시점에 대조보다 더 났으면 관측.
        private static void Judge(List<Expect> expected, Board fired, Board control, ref CardProbeResult result)
        {
            var names = new List<string>();
            var witnessed = new List<string>();
            for (int i = 0; i < expected.Count; i++) if (!names.Contains(expected[i].Name)) names.Add(expected[i].Name);
            for (int n = 0; n < names.Count; n++)
            {
                bool ok = false;
                for (int i = 0; i < expected.Count && !ok; i++)
                {
                    if (expected[i].Name != names[n]) continue;
                    int frames = math.min(fired.Frames.Count, control.Frames.Count);
                    for (int t = 0; t < frames && !ok; t++)
                        ok = EffectWitness.Seen(expected[i].Signal, expected[i].Subject, control.Frames[t], fired.Frames[t]);
                }
                if (ok) witnessed.Add(names[n]);
            }
            var missing = new List<string>();
            for (int n = 0; n < names.Count; n++) if (!witnessed.Contains(names[n])) missing.Add(names[n]);
            if (names.Count == 0) missing.Add("의도 0");
            result.Witnessed = witnessed.ToArray();
            result.Missing = missing.ToArray();
        }

        private static string[] Concat(string[] a, string[] b)
        {
            var r = new string[(a?.Length ?? 0) + (b?.Length ?? 0)];
            a?.CopyTo(r, 0);
            b?.CopyTo(r, a?.Length ?? 0);
            return r;
        }

        // ── 기록기 ────────────────────────────────────────────────────────────

        private sealed class IntentLog
        {
            public readonly List<SimIntent> Intents = new List<SimIntent>(8);
            public readonly List<MetaIntent> Meta = new List<MetaIntent>(2);
            public bool Mute;

            public void Reset() { Intents.Clear(); Meta.Clear(); }
        }

        // 실행자를 감싼다 — 질의는 그대로 넘기고 `Emit` 만 적어 둔 뒤 넘긴다(`Mute` 면 적기만). 무상태 concrete 를 안 고친다.
        private sealed class RecordingSkill : ISkill
        {
            private readonly ISkill _inner;
            private readonly IntentLog _log;

            public RecordingSkill(ISkill inner, IntentLog log) { _inner = inner; _log = log; }

            public int SkillId => _inner.SkillId;

            public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
                => _inner.Execute(caster, in target, in p, new RecordingContext(ctx, _log));
        }

        private sealed class RecordingContext : ISkillContext
        {
            private readonly ISkillContext _c;
            private readonly IntentLog _log;

            public RecordingContext(ISkillContext inner, IntentLog log) { _c = inner; _log = log; }

            public float3 Position(SkillEntityId id) => _c.Position(id);
            public int2 CellOf(SkillEntityId id) => _c.CellOf(id);
            public int2 CellOfPosition(float3 world) => _c.CellOfPosition(world);
            public float3 CellCenter(int2 cell) => _c.CellCenter(cell);
            public float TileSize => _c.TileSize;
            public bool TryFacing(SkillEntityId id, out float2 dirXZ) => _c.TryFacing(id, out dirXZ);
            public Faction FactionOf(SkillEntityId id) => _c.FactionOf(id);
            public float Health(SkillEntityId id) => _c.Health(id);
            public float MaxHealth(SkillEntityId id) => _c.MaxHealth(id);
            public float Stat(SkillEntityId id, UnitStat stat) => _c.Stat(id, stat);
            public bool Has(SkillEntityId id, UnitPredicate pred) => _c.Has(id, pred);
            public byte TraversalLayers(SkillEntityId id) => _c.TraversalLayers(id);
            public float ShieldValueFrom(SkillEntityId target, SkillEntityId source) => _c.ShieldValueFrom(target, source);

            public int Opponents(CasterRef caster, float3 center, int tileRange, CandidateFilter filter,
                                 RangeMetric metric, SkillEntityId[] into)
                => _c.Opponents(caster, center, tileRange, filter, metric, into);

            public int Allies(CasterRef caster, float3 center, int tileRange, CandidateFilter filter,
                              RangeMetric metric, SkillEntityId[] into)
                => _c.Allies(caster, center, tileRange, filter, metric, into);

            public bool TryDensestOpponentCluster(CasterRef caster, int densityRadius, out int2 cell, out int count)
                => _c.TryDensestOpponentCluster(caster, densityRadius, out cell, out count);

            public bool TryLandingCellNear(int2 desired, int maxRing, out int2 cell)
                => _c.TryLandingCellNear(desired, maxRing, out cell);

            public PatternAimNeed AimNeedOfPattern(SkillEntityId host, int patternIndex)
                => _c.AimNeedOfPattern(host, patternIndex);

            public void Emit(in SimIntent intent)
            {
                _log.Intents.Add(intent);
                if (!_log.Mute) _c.Emit(in intent);
            }

            public void Emit(in MetaIntent intent)
            {
                _log.Meta.Add(intent);
                if (!_log.Mute) _c.Emit(in intent);
            }
        }

        // ── 저작 스냅샷(7e ②) ─────────────────────────────────────────────────

        /// <summary>
        /// 카드 한 장의 canonical 텍스트 — 카드 줄 + 그 카드가 가리키는 규칙 줄 + 규칙이 가리키는 탄·발사 명세·장판 줄.
        /// 정의표 해시(`ComputeConfigHash`)와 **같은 canonicalize** 를 쓴다(두 벌이면 스냅샷과 해시가 다른 것을 본다).
        /// </summary>
        public static string CanonicalCardText(MatchDefinition def, int cardRow)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(512);
            ref var card = ref def.Cards[cardRow];
            sb.Append("[card ").Append(cardRow.ToString(inv)).Append(' ').Append(card.Id).Append("]\n");
            card.Canonicalize(sb, inv);
            foreach (var r in Rows(card)) AppendRule(sb, def, r, inv);
            return sb.ToString();
        }

        /// <summary>정의표의 카드 전부를 카드 순으로 이은 스냅샷 본문.</summary>
        public static string CanonicalDeckText(MatchDefinition def)
        {
            var sb = new StringBuilder(def.Cards.Length * 512);
            for (int i = 0; i < def.Cards.Length; i++) sb.Append(CanonicalCardText(def, i));
            return sb.ToString();
        }

        /// <summary>
        /// unified-effect-layer unit 5 — 유닛·적 줄 하나가 든 **공격 수식자 + 규칙 줄**의 canonical 텍스트(카드 스냅샷과 같은
        /// canonicalize). 규칙도 수식자도 없으면 빈 문자열. 머리줄(어느 에셋인가)은 호출부가 쓴다 — 코어는 에셋을 모른다.
        /// </summary>
        public static string CanonicalHostRulesText(MatchDefinition def, bool enemy, int row)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(256);
            var attack = enemy ? def.Enemies[row].Attack : def.Units[row].Attack;
            var rules = enemy ? def.Enemies[row].Bindings : def.Units[row].Bindings;
            int mods = attack.Mods != null ? attack.Mods.Length : 0;
            for (int i = 0; i < mods; i++) attack.Mods[i].Canonicalize(sb, inv, "attackMod" + i.ToString(inv));
            if (rules != null) foreach (var r in rules) AppendRule(sb, def, r, inv);
            return sb.ToString();
        }

        // 규칙 줄 하나 + 그 줄이 가리키는 탄·발사 명세·장판 줄(표에 있을 때만).
        private static void AppendRule(StringBuilder sb, MatchDefinition def, int r, CultureInfo inv)
        {
            if (r < 0 || r >= def.Bindings.Length) { sb.Append("[rule ").Append(r.ToString(inv)).Append("] 표 밖\n"); return; }
            ref var d = ref def.Bindings[r];
            sb.Append("[rule ").Append(r.ToString(inv)).Append("] ").Append(d.Label).Append('\n');
            d.Canonicalize(sb, inv);
            if (d.DataIndex >= 0 && d.DataIndex < def.Projectiles.Length)
            {
                sb.Append("[projectile ").Append(d.DataIndex.ToString(inv)).Append("]\n");
                def.Projectiles[d.DataIndex].Canonicalize(sb, inv);
            }
            if (d.PatternDefIndex >= 0 && d.PatternDefIndex < def.Patterns.Length)
            {
                sb.Append("[pattern ").Append(d.PatternDefIndex.ToString(inv)).Append("]\n");
                def.Patterns[d.PatternDefIndex].Canonicalize(sb, inv);
            }
            if (d.HazardDefIndex >= 0 && d.HazardDefIndex < def.Hazards.Length)
            {
                sb.Append("[hazard ").Append(d.HazardDefIndex.ToString(inv)).Append("]\n");
                def.Hazards[d.HazardDefIndex].Canonicalize(sb, inv);
            }
        }

        /// <summary>콘솔·테스트 메시지용 표(카드 · 구움 · 발동 · 관측 · 진단).</summary>
        public static string FormatTable(IReadOnlyList<CardProbeResult> results)
        {
            var sb = new StringBuilder();
            int ok = 0;
            for (int i = 0; i < results.Count; i++) if (results[i].Ok) ok++;
            sb.Append("카드 ").Append(results.Count).Append("장 · ○ ").Append(ok).Append(" · × ").Append(results.Count - ok).Append('\n');
            sb.Append("줄 | 카드 | 판정 | 구움 | 발동 | 관측 | 미관측 | 진단 | 숙주 | 단서\n");
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                sb.Append(r.Row).Append(" | ").Append(r.CardId).Append(" | ").Append(r.Ok ? "○" : "×")
                  .Append(" | ").Append(r.Baked ? "○" : "×").Append(" | ").Append(r.Fired ? "○" : "×")
                  .Append(" | ").Append(string.Join(",", r.Witnessed ?? System.Array.Empty<string>()))
                  .Append(" | ").Append(string.Join(",", r.Missing ?? System.Array.Empty<string>()))
                  .Append(" | ").Append(r.HasDiagnosis ? r.Diagnosis.ToString() : "-")
                  .Append(" | ").Append(r.Host ?? "-")
                  .Append(" | ").Append(r.Note ?? "").Append('\n');
            }
            return sb.ToString();
        }
    }
}
