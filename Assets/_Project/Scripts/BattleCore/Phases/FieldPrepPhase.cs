using Unity.Mathematics;
using Wassup.Skills;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Move;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 2 — **장(場)을 먼저 세운다.** 이동이 읽을 것을 이 단계가 굽는다.
    //
    // 순서가 계약이다(옛 전투의 `[UpdateBefore]` 사슬을 호출 순서로 명시한 것):
    //   ① 장애물 재수집 → 시그니처 → 바뀐 틱에만 흐름장 부분 재빌드 · 벽 캐시 무효화
    //   ② 어그로 상태(만료 · 가디언 사망 해제 · 수용량 재계산)
    //   ③ 공용 사냥판(무제한 감지용)
    //   ④ 순찰 스텝
    //   ⑤ 존 장판(unit 6b) — 수명 → 멤버십 → 부여. 옛 `HazardLifetimeSystem`(1) ·
    //      `ZoneApplySystem`(5) 의 자리 = **이동 앞, 지속 피해 틱 앞**. 그래서 장판이 건 지속
    //      피해는 같은 틱에 첫 지급이 난다(F7 「진입 즉시 1회」가 옛 16번과 같은 틱이다).
    //   ⑥ 아군 버프 장 재발행(unit 6b) — 옛 `AllyBuffFieldSystem`(3) 의 자리(이동 앞).
    //   ⑦ 지속 피해 틱(unit 6a) — **이동 앞**이다(옛 `DotApplySystem` 의 자리와 같다).
    //      여기서 인박스에 넣으면 같은 틱의 피해 단계(`CombatPhase`)가 소비한다.
    //
    // ⚠ **장애물이 바뀌면 어그로가 풀린다**(M10 의 「리무버 둘」 중 둘째). 그 경로가 없으면
    // 길이 막힌 뒤에도 적이 옛 추격판을 하강해 **못 가는 곳으로 영원히 밀린다**.
    // 단 **도발된 적은 필드만 떼고 어그로 표시는 남긴다** — 도발은 1회성이라 재획득 경로가
    // 없어 통째로 풀면 도발이 그 자리에서 사라진다.
    public sealed class FieldPrepPhase : ITickPhase, ISeamHost
    {
        public string Name => "FieldPrep";

        /// <summary>unit 7a — 이 단계가 여는 seam(끝자리).</summary>
        public void AppendSeams(System.Collections.Generic.List<Seam> into) => into.Add(Seam.Periodic);

        private readonly MapRuntime _map;
        private readonly ChaseFieldPool _chasePool;
        private readonly PatrolScratch _patrol;

        private readonly System.Collections.Generic.List<Effects.DotSlot> _dotGone =
            new System.Collections.Generic.List<Effects.DotSlot>(4);

        private int2[] _defenderCells = new int2[16];
        private int2[] _enemyCells = new int2[32];
        private float3[] _enemyPositions = new float3[32];
        private float[] _enemyRadii = new float[32];
        private readonly byte[] _fullMask;

        public FieldPrepPhase(MapRuntime map, ChaseFieldPool chasePool)
        {
            _map = map;
            _chasePool = chasePool;
            _patrol = new PatrolScratch(map.Snapshot.CellCount);
            _fullMask = new byte[math.max(1, map.Snapshot.CellCount)];
        }

        /// <summary>
        /// **저작 거점(본능 · 적 마음)을 세운다.** 판 경계에 한 번.
        ///
        /// 왜 이 단계가 세우나: 본능은 **장(場)의 가구**다 — 이동이 읽는 목적지
        /// (`StepStructureDestination`)와 장애물·점유와 같은 층이고, 그 장을 굽는 자리가
        /// 여기다. 마음 타워만 `HeartMeter` 가 세우는 것은 그 체력이 담당자에게 있기
        /// 때문이고(X29), 가구라서가 아니다.
        ///
        /// 안 세우는 둘:
        ///   · **방어 마음**(`DefenderCore`) — 정본은 `Goals` 이고 세우는 자는 `HeartMeter` 다.
        ///     저작 검증을 뚫고 왔어도 여기서 안 세운다(골이 두 벌이 되는 것을 막는다).
        ///   · **체력 0** — 세우자마자 무너지는 건물은 판에 세우지 않는다.
        /// </summary>
        public void Begin(BattleWorld world, MatchDefinition def, int tick)
        {
            if (_map == null) return;
            var spots = _map.Snapshot.Structures;
            for (int i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                var faction = (Faction)spot.Faction;
                if (faction == Faction.DefenderCore) continue;

                int di = spot.DefIndex;
                if (di < 0 || di >= def.Structures.Length) continue;
                ref var sd = ref def.Structures[di];
                if (sd.Health <= 0f) continue;

                var u = world.SpawnStructure(faction, di, spot.Cell,
                                             _map.CenterOf(spot.Cell),
                                             spot.Footprint <= 0 ? StructureSize.Instinct : spot.Footprint,
                                             sd.Health, healthExternal: false, tick: tick);
                // 공격 저작이 없으면 `AttackState` 자체를 안 붙인다 — 공격 루프가 「팔이
                // 있는데 휘두를 것이 없는」 개체를 매 틱 돌지 않게 하는 것이 그 값이다.
                if (sd.HasAttack) u.Attack = CombatPhase.BuildAttackState(in sd, def, world.Parts);
            }
        }

        public void Run(TickContext ctx)
        {
            if (_map != null && _map.Snapshot.CellCount > 0)
            {
                RebuildObstacles(ctx);
                StepAggro(ctx);
                RebuildHuntField(ctx);
                StepPatrol(ctx);
                StepZones(ctx);
                StepAllyFields(ctx);
                StepDot(ctx);
            }

            // unit 6b2 — **`[Periodic]` seam 의 호출부.** 핸들러가 0 이어도 매 틱 돈다 — 그것이
            // 이 unit 의 산출이다(unit 3 이 seam 넷을 그렇게 뚫었다). enum 값만 더하고 여기를 안
            // 만들면 unit 7 이 「자리가 있는 줄 알고」 등록했다가 아무 일도 안 일어난다.
            // 맵이 없는 판에서도 부른다 — seam 은 장 준비의 일부가 아니라 **그 뒤의 자리**다.
            ctx.Seams?.Run(Seam.Periodic, ctx);
        }

        // ── ⑤ 존 장판 ────────────────────────────────────────────────────────
        //
        // ⚠ **멤버십은 스냅샷이 아니다.** 매 틱 다시 판정하므로 들어온 적도 걸리고 나간 적은
        // 풀린다 — 「풀린다」의 시간은 슬롯을 지우는 것이 아니라 `RestDuration` 이다(F17).
        // ⚠ **겹친 장판은 한 번만, 가장 강한 값으로 쓴다**(F23). 장판마다 쓰면 승자가 순회
        // 순서가 된다 — 새 코어에서 순서는 결정론적이지만, 그러면 「나중에 깐 약한 장판이
        // 먼저 깐 강한 장판을 덮는다」가 규칙이 된다. 규칙은 순서가 아니라 세기여야 한다.
        private void StepZones(TickContext ctx)
        {
            var world = ctx.World;
            var zones = world.Hazards;
            if (zones.Count == 0) return;

            // 수명 — 0 이하가 되는 틱에 사라지고 **그 틱에는 효과를 안 건다**(옛 ECB 즉시 재생).
            _zoneGone.Clear();
            for (int z = 0; z < zones.Count; z++)
            {
                zones[z].Remaining -= ctx.Dt;
                if (zones[z].Remaining <= 0f) _zoneGone.Add(zones[z].Id);
            }
            for (int i = 0; i < _zoneGone.Count; i++) world.DestroyHazard(_zoneGone[i], ctx.Tick);
            if (zones.Count == 0) return;

            var hazards = ctx.Def.Hazards;
            float inv = _map.TileSize > 1e-6f ? 1f / _map.TileSize : 1f;
            var units = world.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.Deploying) continue;
                // 거점은 전면 면역(F3) — 지속 피해 관문은 이 술어를 안 지나므로 여기서 거른다.
                if (!EffectEligibility.AcceptsModifier(u)) continue;
                byte theirs = u.Move != null ? u.Move.TraversalLayers : (byte)0;

                ZoneFold fold = default;
                fold.Reset();
                for (int z = 0; z < zones.Count; z++)
                {
                    var h = zones[z];
                    if (h.RadiusTiles < 0) continue;                       // F18 — 효과 없는 존
                    if (h.DefIndex < 0 || h.DefIndex >= hazards.Length) continue;
                    ref var hd = ref hazards[h.DefIndex];
                    if (hd.EffectCount == 0) continue;
                    if (!LayerBits.CanTarget(h.TargetLayers, theirs)) continue;   // F15 — 0 = 필터 없음
                    // 제약 13 — 존은 「자리에 떨어지는 것」이다. 원점 항은 칸 반폭이고 깐 자의 몸은 안 붙는다.
                    if (!Wassup.Skills.SkillMath.ReachFromCell(
                            (u.Position.x - h.Center.x) * inv, (u.Position.z - h.Center.z) * inv,
                            h.RadiusTiles, u.HitRadius)) continue;

                    for (int e = 0; e < hd.EffectCount; e++)
                    {
                        ref var eff = ref hd.Effects[e];
                        // F34 — 진영은 **저작 축**이다. 하드 게이트가 아니다.
                        if (((int)u.Faction & eff.TargetFactions) == 0) continue;
                        fold.Add(in eff, h.Id, h.DotDamage);
                    }
                }
                if (fold.Any) ApplyZoneFold(ctx, u, ref fold);
            }
        }

        private static void ApplyZoneFold(TickContext ctx, Unit u, ref ZoneFold f)
        {
            // 감속 — 군중 제어가 아니라 **이동속도 모디파이어**(6a 구현 9). 출처를 비워 한 슬롯을
            // 나눠 쓴다(옛 `source = Entity.Null`) — 장판 id 를 넣으면 겹칠 때 곱으로 쌓인다.
            if (f.HasSlow)
                Effects.EffectApply.Stat(ctx, SimEntityId.None, null, u,
                                         Effects.StatKind.MoveSpeedMul, Effects.CombineOp.Multiplicative,
                                         f.SlowMagnitude, f.SlowRest, new Effects.SlotTag(Effects.SlotKind.Zone),
                                         0f, Effects.ModifierOrigin.Zone);

            // 지속 피해 — 출처는 **언제나 장판**(F16). 원소마다 한 슬롯.
            for (int el = 0; el < ZoneFold.ElementCount; el++)
            {
                if (!f.DotHas[el]) continue;
                Effects.EffectApply.Dot(ctx, f.DotSource[el], u, Effects.DotOrigin.Zone,
                                        (Effects.DotElement)el, f.DotScalar[el], f.DotInterval[el], f.DotRest[el]);
            }

            // 행동 불능 — 문은 `RequestCc` 하나다(거점·보스 면역이 거기 있다). 같은 틱의 피해
            // 단계 후처리가 슬롯에 옮긴다.
            if (f.StunRest > 0f)
                ctx.World.RequestCc(CcRequest.Of(u.Id, CcRequestKind.Stun, f.StunRest, f.StunSource));
            if (f.SleepRest > 0f)
                ctx.World.RequestCc(CcRequest.Of(u.Id, CcRequestKind.Sleep, f.SleepRest, f.SleepSource));
        }

        // 한 대상이 이번 틱에 겹쳐 선 장판들의 **접힌 결과**. 스택 위 값이라 할당이 없다.
        private struct ZoneFold
        {
            public const int ElementCount = 5;   // `DotElement` None..Poison

            public bool Any;
            public bool HasSlow;
            public float SlowMagnitude, SlowRest;
            public bool[] DotHas;
            public float[] DotScalar, DotInterval, DotRest;
            public SimEntityId[] DotSource;
            public float StunRest, SleepRest;
            public SimEntityId StunSource, SleepSource;

            public void Reset()
            {
                Any = false;
                HasSlow = false;
                SlowMagnitude = 1f;
                SlowRest = 0f;
                StunRest = SleepRest = 0f;
                StunSource = SleepSource = SimEntityId.None;
                if (DotHas == null)
                {
                    DotHas = s_dotHas; DotScalar = s_dotScalar; DotInterval = s_dotInterval;
                    DotRest = s_dotRest; DotSource = s_dotSource;
                }
                for (int i = 0; i < ElementCount; i++)
                {
                    DotHas[i] = false;
                    DotScalar[i] = DotInterval[i] = DotRest[i] = 0f;
                    DotSource[i] = SimEntityId.None;
                }
            }

            public void Add(in HazardEffectDef eff, SimEntityId zone, float dotDamage)
            {
                switch ((HazardEffectKind)eff.Kind)
                {
                    case HazardEffectKind.Slow:
                        SlowMagnitude = HasSlow
                            ? Effects.FieldFold.Strongest(Effects.CombineOp.Multiplicative, SlowMagnitude, eff.Magnitude)
                            : eff.Magnitude;
                        SlowRest = math.max(SlowRest, eff.RestDuration);
                        HasSlow = true;
                        Any = true;
                        break;
                    case HazardEffectKind.DoT:
                    {
                        int el = eff.Element;
                        if (el < 0 || el >= ElementCount) break;
                        // 같은 원소끼리는 **센 쪽의 요율·주기**가 이긴다. 남은 여유는 긴 쪽.
                        // U10 — DoT 의 크기는 장판 개체가 든다(까는 효과 줄의 피해). 장판 줄의 크기 칸은 읽지 않는다.
                        if (!DotHas[el] || dotDamage > DotScalar[el])
                        {
                            DotScalar[el] = dotDamage;
                            DotInterval[el] = eff.TickInterval;
                            DotSource[el] = zone;
                        }
                        DotRest[el] = math.max(DotRest[el], eff.RestDuration);
                        DotHas[el] = true;
                        Any = true;
                        break;
                    }
                    case HazardEffectKind.Stun:
                        if (eff.RestDuration > StunRest) { StunRest = eff.RestDuration; StunSource = zone; }
                        Any = true;
                        break;
                    case HazardEffectKind.Sleep:
                        if (eff.RestDuration > SleepRest) { SleepRest = eff.RestDuration; SleepSource = zone; }
                        Any = true;
                        break;
                    // `Impulse` — **방향이 없다**(옛 저작도 벡터 0). 방향을 모르는 대상은 밀지 않는다(C8).
                    // 「이식 제외」 표에 있다.
                }
            }

            // 단일 스레드라 한 벌이면 된다(계약 6 — 틱 중 할당 0).
            private static readonly bool[] s_dotHas = new bool[ElementCount];
            private static readonly float[] s_dotScalar = new float[ElementCount];
            private static readonly float[] s_dotInterval = new float[ElementCount];
            private static readonly float[] s_dotRest = new float[ElementCount];
            private static readonly SimEntityId[] s_dotSource = new SimEntityId[ElementCount];
        }

        // ── ⑥ 아군 버프 장 ────────────────────────────────────────────────────
        //
        // 안에 선 **배치 완료** 방어유닛에게 매 틱 짧은 모디파이어를 재발행한다. 이탈·만료·사망이
        // 전부 «재발행이 멈춘다»로 처리된다(회수 원시연산 불요). 겹치면 스탯마다 가장 강한 값(F23).
        // ⚠ 제약 13 — 아군 **장판**은 「자리에 떨어지는 것」(칸 반폭)이다. 드림캐쳐 **오라**는
        // 몸형이고(숙주 `HitRadius`) 그 생산자는 unit 7 이다 — 둘을 한 자로 재지 말 것.
        private void StepAllyFields(TickContext ctx)
        {
            var fields = ctx.World.Fields;
            bool any = false;
            for (int f = 0; f < fields.Count; f++) if (fields[f].Kind == FieldKind.AllyBuff) { any = true; break; }
            if (!any) return;

            float inv = _map.TileSize > 1e-6f ? 1f / _map.TileSize : 1f;
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Defender || u.Dead || u.Deploying) continue;

                for (int s = 0; s < _allyBest.Length; s++) _allyBest[s] = 0f;
                for (int f = 0; f < fields.Count; f++)
                {
                    var fc = fields[f];
                    if (fc.Kind != FieldKind.AllyBuff) continue;
                    if (!Wassup.Skills.SkillMath.ReachFromCell(
                            (u.Position.x - fc.Center.x) * inv, (u.Position.z - fc.Center.z) * inv,
                            fc.Range, u.HitRadius)) continue;
                    int s = (int)fc.Stat;
                    if (s < 0 || s >= _allyBest.Length) continue;
                    // 올리는 장이다 — 배율이 클수록 세다(옛 `max`). 버킷 분류는 아래 한 곳.
                    if (fc.Magnitude > _allyBest[s])
                    {
                        _allyBest[s] = fc.Magnitude;
                        _allyRefresh[s] = fc.RefreshSeconds > 0f ? fc.RefreshSeconds
                                                                 : Effects.FieldRefresh.Seconds(ctx.Dt);
                    }
                }
                for (int s = 0; s < _allyBest.Length; s++)
                {
                    if (_allyBest[s] <= 0f) continue;
                    Effects.ModifierAuthoring.FromMultiplier(_allyBest[s], out var op, out float mag);
                    // 출처 = 자기(옛과 같다) · 칸 = 아군 장 — 겹친 장판이 한 슬롯을 나눠 쓴다.
                    Effects.EffectApply.Stat(ctx, u.Id, u, u, (Effects.StatKind)s, op, mag, _allyRefresh[s],
                                             new Effects.SlotTag(Effects.SlotKind.AllyField), 0f,
                                             Effects.ModifierOrigin.Skill);
                }
            }
        }

        private readonly System.Collections.Generic.List<SimEntityId> _zoneGone =
            new System.Collections.Generic.List<SimEntityId>(4);
        private readonly float[] _allyBest = new float[7];
        private readonly float[] _allyRefresh = new float[7];

        // ── ⑦ 지속 피해 ──────────────────────────────────────────────────────
        //
        // ⚠ **지급은 앞에서부터, 제거는 뒤에서부터**(F8). 역순으로 지급하면 여러 도트가
        // 걸린 대상의 피해 숫자 표시 순서가 조용히 뒤집힌다.
        // ⚠ 지급 한 번 = 인박스 한 건 = 화면의 숫자 하나다. 청크를 합치면 「초당 5씩
        // 네 번」이 「20 한 번」으로 보인다.
        // ⚠ **출처가 없다**(`SimEntityId.None`) — 지속 피해로 죽은 것은 미귀속이라
        // 처치 보상이 안 난다(옛 전투와 같다. 의도).
        private void StepDot(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || !u.Dot.Any) continue;   // 시체는 안 탄다

                for (int k = 0; k < u.Dot.Count; k++)
                {
                    u.Dot.Step(k, ctx.Dt, out int ticks, out float perTick, out float continuous);
                    if (continuous > 0f)
                        u.Inbox.Damage.Add(new DamageEntry { Amount = continuous, Source = SimEntityId.None });
                    for (int t = 0; t < ticks; t++)
                        u.Inbox.Damage.Add(new DamageEntry { Amount = perTick, Source = SimEntityId.None });
                }

                // 계약 7 — **사라진 슬롯마다 사건 하나.** 뷰가 「언제 끄나」를 폴링으로
                // 되묻지 않게 하는 것이 그 값이다. 제거는 뒤에서부터라(F8) 모인 순서가
                // 역순이므로, **발행은 삽입 순서 오름차순**으로 되돌린다.
                _dotGone.Clear();
                if (u.Dot.RemoveExpired(_dotGone) == 0) continue;
                for (int k = _dotGone.Count - 1; k >= 0; k--)
                    ctx.Bus.Publish(CoreEvent.DotCleared(ctx.Tick, u,
                                                         _dotGone[k].Origin, _dotGone[k].Element));
            }
        }

        // ── ① 장애물 ──────────────────────────────────────────────────────────
        private void RebuildObstacles(TickContext ctx)
        {
            var obstacles = _map.Obstacles;
            obstacles.BeginRebuild();

            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead) continue;
                if (u.Kind == UnitKind.Defender && u.Footprint != null)
                {
                    obstacles.BlockRect(u.Footprint.Anchor, u.Footprint.Width, u.Footprint.Height);
                    continue;
                }
                // 길막 장판 — 「막으면 돌아간다」의 다른 소스. 거점은 통행을 안 막는다(점유만).
                // unit 6b — **저작 모양만큼** 막는다(3×3 바위는 아홉 칸). 정의 줄이 없는 개체
                // (정의표 밖에서 선 것)는 자기 칸 하나다 — unit 3 의 종전 동작.
                if (u.Kind == UnitKind.BlockingHazard)
                {
                    var c = _map.CellOf(u.Position);
                    int span = u.DefIndex >= 0 && u.DefIndex < ctx.Def.BlockingHazards.Length
                        ? ctx.Def.BlockingHazards[u.DefIndex].SpanRadius : 0;
                    if (span <= 0) obstacles.Block(c);
                    else obstacles.BlockRect(new int2(c.x - span, c.y - span), span * 2 + 1, span * 2 + 1);
                }
            }

            if (!obstacles.EndRebuild()) return;   // 안 바뀌었으면 다시 굽지 않는다

            _map.Flow.Rebuild(obstacles);
            _map.Nav.Invalidate(obstacles.Signature);

            // 낡은 추격판 무효화 = 어그로 해제(M10). 도발은 표시만 남긴다.
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var aggro = u.Aggro;
                if (aggro == null || aggro.Target.IsNone) continue;
                if (!aggro.Taunted) { Release(ctx, u, AggroReleaseReason.Rebuilt); continue; }
                if (aggro.Chase != null)
                {
                    _chasePool.Return(aggro.Chase);
                    aggro.Chase = null;
                }
            }
        }

        // ── ② 어그로 상태 ─────────────────────────────────────────────────────
        private void StepAggro(TickContext ctx)
        {
            var units = ctx.World.Units;

            // 만료·가디언 사망 해제. 시한(>0)만 감소한다 — 0 은 무기한 센티널이다.
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var aggro = u.Aggro;
                if (aggro == null || aggro.Target.IsNone) continue;

                if (aggro.Remaining > 0f)
                {
                    aggro.Remaining -= ctx.Dt;
                    if (aggro.Remaining <= 0f) { Release(ctx, u, AggroReleaseReason.Expired); continue; }
                }

                var guardian = ctx.World.Find(aggro.Target);
                if (guardian == null || guardian.Dead || guardian.Health <= 0f)
                    Release(ctx, u, AggroReleaseReason.GuardianGone);
            }

            // 수용량 재계산은 **full recompute** 다 — 증감으로 유지하면 drift 가 쌓인다.
            for (int i = 0; i < units.Count; i++)
                if (units[i].Aggro != null) units[i].Aggro.Held = 0;

            for (int i = 0; i < units.Count; i++)
            {
                var aggro = units[i].Aggro;
                if (aggro == null || aggro.Target.IsNone || units[i].Dead) continue;
                var guardian = ctx.World.Find(aggro.Target);
                if (guardian?.Aggro != null) guardian.Aggro.Held++;
            }
        }

        // **어그로 해제의 유일한 자리.** 해제 경로 셋(시한 · 가디언 부재 · 추격판 무효화)이 전부 여기를
        // 부르고, 여기가 풀림 사건을 낸다(계약 7). 경로마다 사건을 따로 내면 언젠가 한쪽이 빠지고,
        // 그 경로로 풀린 적의 표식은 판이 끝날 때까지 떠 있는다.
        // 사건은 **지우기 전에** 만든다 — 가디언 id 가 `None` 으로 덮이기 전 값이 필요하다.
        private void Release(TickContext ctx, Unit enemy, AggroReleaseReason reason)
        {
            var aggro = enemy.Aggro;
            var ev = CoreEvent.AggroReleased(ctx.Tick, enemy, aggro.Target, reason);
            aggro.Target = SimEntityId.None;
            aggro.Remaining = 0f;
            aggro.Taunted = false;
            if (aggro.Chase != null) { _chasePool.Return(aggro.Chase); aggro.Chase = null; }
            ctx.Bus.Publish(ev);
        }

        // ── ③ 공용 사냥판 ─────────────────────────────────────────────────────
        //
        // 헌터(무제한 감지)가 하나도 없으면 굽지 않는다 — 소비자가 없는 필드다.
        // 반경 = 동시에 살아 있는 헌터 **사거리의 min fold**(M7).
        private void RebuildHuntField(TickContext ctx)
        {
            var units = ctx.World.Units;
            int range = int.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.Detection == null || !u.Detection.Unlimited) continue;
                float atk = u.DefIndex >= 0 && u.DefIndex < ctx.Def.Enemies.Length
                    ? ctx.Def.Enemies[u.DefIndex].AttackRange : 1f;
                range = math.min(range, GridMath.RangeToTiles(atk));
            }
            if (range == int.MaxValue) { _map.Hunt.Clear(); return; }
            range = math.max(1, range);

            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || u.Deploying) continue;
                if (((int)u.Faction & (int)Faction.DefenderUnit) == 0) continue;
                if (count >= _defenderCells.Length) Grow(ref _defenderCells);
                _defenderCells[count++] = _map.CellOf(u.Position);
            }

            _map.Hunt.Rebuild(_map.Snapshot.CellLayers, _map.HuntLayers, _map.Obstacles,
                              _defenderCells, count, range);
        }

        // ── ④ 순찰 ────────────────────────────────────────────────────────────
        //
        // 순찰 스텝은 이동 **앞**에서 굽는다(옛 `PatrolFieldSystem` 이 Movement 앞이었던 것과 같다).
        // 매 틱 굽는 이유: 목적지가 움직이는 적이라 필드가 매 틱 무효가 된다 — 그래서 유닛당
        // 격자 버퍼를 들지 않고 **방향 하나로 접는다.**
        private void StepPatrol(TickContext ctx)
        {
            var units = ctx.World.Units;

            int patrolCount = 0;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Patrol != null && !units[i].Dead) patrolCount++;
            if (patrolCount == 0) return;

            int enemyCount = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Dead || !u.IsTargetable()) continue;
                if (((int)u.Faction & (int)Faction.EnemyUnit) == 0) continue;
                if (enemyCount >= _enemyCells.Length)
                {
                    Grow(ref _enemyCells);
                    Grow(ref _enemyPositions);
                    Grow(ref _enemyRadii);
                }
                _enemyCells[enemyCount] = _map.CellOf(u.Position);
                _enemyPositions[enemyCount] = u.Position;
                // ⚠ **대상 몸을 함께 넘긴다.** 안 넘기면 순찰 이동만 다른 답을 받는다 —
                // 보스가 사거리 안인데 이동은 밖으로 읽어 **이미 쏠 수 있는데 계속 다가간다.**
                _enemyRadii[enemyCount] = u.HitRadius;
                enemyCount++;
            }

            var gridSize = _map.GridSize;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Patrol == null || u.Move == null || u.Dead) continue;

                // 소환사가 죽으면 소환물도 사라진다 — 이동을 멈추는 것이 아니라 소멸이다.
                //
                // ⚠ **여기서 지우지 않는다.** 표시만 하고 소멸은 `CombatPhase` 의 사망 단계가
                // `BattleWorld.Destroy` 로 한다(unit 3 사망 2단계). 두 번째 제거 경로를 만들면
                // 계약 7(「모든 소멸은 소멸 이벤트를 낸다」)이 경로마다 따로 지켜져야 하고,
                // 그러면 언젠가 한쪽이 조용히 빠진다 — 실제로 초판이 `Dead` 만 세우고
                // `UnitDestroyed` 를 안 내서 뷰가 그 순찰병을 영원히 들고 있었다.
                // `DeathTick` 을 함께 찍는 것도 계약이다: 안 찍으면 표시 틱과 소멸 틱이 같아져
                // 시체가 자기 자리를 읽을 창(시체 폭발·사직서 드랍)이 이 경로에만 없어진다.
                if (!u.Patrol.SummonedBy.IsNone && ctx.World.Find(u.Patrol.SummonedBy) == null)
                {
                    u.Dead = true;
                    u.DeathTick = ctx.Tick;
                    u.Move.PatrolStep = float2.zero;
                    continue;
                }

                byte layers = u.Move.TraversalLayers;
                MovementCellTrim.FillWalkMask(_map.Snapshot.CellLayers, gridSize,
                                              layers == 0 ? TraversalSlots.DefaultMask : layers,
                                              _map.Obstacles.HasObstacles, _map.Obstacles.Blocked, _fullMask);
                PatrolAreaMath.FillAreaMask(_fullMask, gridSize, u.Patrol.Anchor, u.Patrol.Radius,
                                            _patrol.AreaMask);

                float range = u.DefIndex >= 0 && u.DefIndex < ctx.Def.Units.Length
                    ? ctx.Def.Units[u.DefIndex].AttackRange : 1f;

                u.Move.PatrolStep = PatrolAreaMath.StepDir(
                    _patrol.AreaMask, _fullMask, gridSize,
                    u.Patrol.Anchor, u.Patrol.Home, u.Patrol.Radius,
                    _map.CellOf(u.Position), u.Position, u.HitRadius,
                    GridMath.RangeToTiles(range), _map.TileSize,
                    _enemyCells, _enemyPositions, _enemyRadii, enemyCount, _patrol);
            }
        }

        private static void Grow<T>(ref T[] buffer)
        {
            var next = new T[buffer.Length * 2];
            System.Array.Copy(buffer, next, buffer.Length);
            buffer = next;
        }
    }
}
