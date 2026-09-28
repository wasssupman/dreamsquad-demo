using Unity.Mathematics;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Map;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1·4 — phase 0. 커맨드의 자리.
    //
    // **`Execute` 는 `BattleMatch.Apply` 가 곧바로 부른다**(틱을 기다리지 않는다) —
    // 그것이 「동기 + receipt」의 뜻이다. 이 클래스가 파이프라인의 0번에도 서 있는 것은
    // 그 자리가 **Immediate seam**(커맨드가 만든 사건의 same-frame 하류)이기 때문이고,
    // 그 드레인은 트리거 레이어가 생기는 unit 7 에서 `Run` 안으로 들어온다.
    //
    // unit 4 — **여기에 판정이 없다.** 배치·퇴근·당김·카드는 전부 담당자에게 넘긴다.
    // 이 클래스가 하는 일은 「어느 담당자에게 가나」뿐이고, 그것이 계약 12 의 이행이다 —
    // 커맨드마다 판정을 조금씩 여기 두면 이 파일이 새 브리지가 된다.
    // 남아 있는 판정은 **디버그 커맨드**뿐이고 그쪽은 정의상 판정을 갖지 않는다(시나리오가 곧 의도다).
    public sealed class CommandPhase : ITickPhase, ISeamHost
    {
        public string Name => "Command";

        private readonly BattleWorld _world;
        private readonly MatchClock _clock;
        private readonly MatchDefinition _def;
        private readonly MapRuntime _map;
        private readonly PlacementService _placement;
        private readonly CostLedger _cost;
        private readonly WaveScheduler _waves;
        private readonly HandDeck _hand;
        private readonly GimmickHost _gimmick;

        public CommandPhase(BattleWorld world, MatchClock clock, MatchDefinition def, MapRuntime map,
                            PlacementService placement, CostLedger cost, WaveScheduler waves, HandDeck hand,
                            GimmickHost gimmick)
        {
            _gimmick = gimmick;
            _world = world;
            _clock = clock;
            _def = def;
            _map = map;
            _placement = placement;
            _cost = cost;
            _waves = waves;
            _hand = hand;
        }

        /// <summary>틱 안 seam 순서표에서 이 단계는 **맨 앞**(`Immediate`)이다 — 실제 드레인은 커맨드 콜스택이다.</summary>
        public void AppendSeams(System.Collections.Generic.List<Seam> into) => into.Add(Seam.Immediate);

        public void Run(TickContext ctx)
        {
            // unit 7a — 틱 시작. 디스패처의 「이번 틱에 어디까지 돌았나」가 여기서 처음으로 돌아간다 —
            // 잔여 규칙(후속 seam 이면 같은 틱 · 지난 seam 이면 다음 틱)의 기준점이다.
            ctx.Triggers?.BeginTick(ctx.Tick);
        }

        public Receipt Execute(in Command cmd, int tick)
        {
            // 종료 뒤에는 전부 거절한다 — 판이 끝난 뒤의 입력이 상태를 움직이면
            // 결과 화면이 판 뒤에 바뀐다(계약 5).
            if (_clock.Ended) return Receipt.Reject(RejectReason.MatchEnded);

            var receipt = Dispatch(in cmd, tick);
            // unit 7a — **Immediate seam 의 유일한 호출부.** 커맨드를 적용한 **이 콜스택 안**에서 드레인한다 —
            // 큐에 넣고 틱을 기다리면 소모(차감·쿨다운) 뒤에 실행이 도착한다(퇴근 운석 · 부착 즉시 · 액티브).
            if (_ctx != null)
            {
                // 커맨드는 틱 사이에 온다 — 이 드레인이 내는 사건의 틱 = 커맨드의 틱(다음 틱 번호)이다.
                _ctx.Tick = tick;
                _ctx.Seams?.Run(Seam.Immediate, _ctx);
            }
            return receipt;
        }

        private Receipt Dispatch(in Command cmd, int tick)
        {
            switch (cmd.Kind)
            {
                case CommandKind.PlaceDefender:
                    return _placement.TryPlace(cmd.DefIndex, cmd.Cell, cmd.Facing, tick);
                case CommandKind.LandDefender:
                    return _placement.Land(cmd.Target);
                case CommandKind.Retire:
                    return _placement.Retire(cmd.Target, tick);
                case CommandKind.FinishPlacement:
                    return _clock.FinishPlacement()
                        ? Receipt.Ok
                        : Receipt.Reject(RejectReason.NotRunningOrPlacementClosed);
                case CommandKind.PullWave:
                    return _waves.TryPull(tick);
                case CommandKind.PullBonus:
                    return _waves.TryPullBonus(tick);
                case CommandKind.AttachCard:
                    return _hand.TryAttach(cmd.CardIndex, cmd.Target, tick);
                case CommandKind.CastActive:
                    return _hand.TryCast(cmd.CardIndex, cmd.Cell, cmd.CellB, cmd.HasCellB, tick);
                case CommandKind.DebugAttachCard:
                    return _hand.DebugAttach(cmd.CardIndex, cmd.Target, tick);
                case CommandKind.DebugCastCard:
                    return _hand.DebugCast(cmd.CardIndex, cmd.Cell, cmd.CellB, cmd.HasCellB, tick);
                case CommandKind.Submit:
                    return Submit();

                // ── 디버그 ── 판정을 갖지 않는다(시나리오가 곧 의도다).
                case CommandKind.DebugSpawnEnemy: return DebugSpawn(cmd, tick);
                case CommandKind.DebugDestroy: return DebugDestroy(cmd, tick);
                case CommandKind.DebugSetObstacle: return DebugObstacle(cmd);
                case CommandKind.DebugSpawnDefender: return DebugSpawnDefender(cmd, tick);
                case CommandKind.DebugForceWave:
                    return _waves.ForceNext() ? Receipt.Ok : Receipt.Reject(RejectReason.NoMoreWaves);
                case CommandKind.DebugFireProjectile: return DebugFire(cmd);
                case CommandKind.DebugImbue: return DebugImbue(cmd);
                case CommandKind.DebugSpawnHazard: return DebugHazard(cmd, tick);
                case CommandKind.DebugSpawnBlocker: return DebugBlocker(cmd, tick);
                case CommandKind.DebugSpawnPickup: return DebugPickup(cmd, tick);
                case CommandKind.DebugDropResignation: return DebugResignation(cmd, tick);
                case CommandKind.DebugSetStack: return DebugSetStack(cmd, tick);
                case CommandKind.DebugSummonPatrol: return DebugSummonPatrol(cmd);
                case CommandKind.DebugFireBinding: return DebugFireBinding(cmd);

                default: return Receipt.Reject(RejectReason.UnknownCommand);
            }
        }

        // 디버그 스폰 — **판정을 갖지 않는다**. 배치 마스크·점유·코스트를 전부 건너뛰므로
        // 골든이 「방어유닛이 선 판」을 배치 판정 없이 세울 수 있다.
        // 스폰 배선 자체는 `PlacementService` 와 **같은 함수**를 지난다 — 두 벌이면
        // 「어떤 경로로 태어났나」가 공격·점유 규칙을 바꾼다.
        private Receipt DebugSpawnDefender(in Command cmd, int tick)
        {
            if (cmd.DefIndex < 0 || cmd.DefIndex >= _def.Units.Length)
                return Receipt.Reject(RejectReason.InvalidUnit);

            ref var d = ref _def.Units[cmd.DefIndex];
            int w = math.max(1, d.FootprintWidth);
            int h = math.max(1, d.FootprintHeight);
            _placement.SpawnDefender(cmd.DefIndex, cmd.Cell, w, h, cmd.Facing, tick, deploying: false);
            return Receipt.Ok;
        }

        // unit 7d — tools.md 10(순찰병 수동 스폰). 소환사와 **같은 문**을 지난다. 소환사가 없으니 연쇄 소멸도 없다.
        private Receipt DebugSummonPatrol(in Command cmd)
        {
            if (_ctx == null) return Receipt.Reject(RejectReason.UnknownCommand);
            if (cmd.DefIndex < 0 || cmd.DefIndex >= _def.Units.Length) return Receipt.Reject(RejectReason.InvalidUnit);
            if (_map != null && _map.Snapshot.CellCount > 0 && !_map.Snapshot.InBounds(cmd.Cell))
                return Receipt.Reject(RejectReason.OutOfBounds);
            _ctx.Tick = _clock.Tick;
            float3 at = _map != null && _map.Snapshot.CellCount > 0 ? _map.CenterOf(cmd.Cell) : new float3(cmd.Cell.x, 0f, cmd.Cell.y);
            CombatPhase.SpawnPatrol(_ctx, cmd.DefIndex, cmd.Cell, cmd.Count, SimEntityId.None, at);
            return Receipt.Ok;
        }

        // unit 7d — 규칙 강제 발화(「왜 안 터졌나」 도구). 카운터·게이트·감지자를 건너뛰고 실행자만 — 발동 상한은 지킨다.
        // 사건은 `Immediate` seam 에 줄 서고 이 커맨드의 콜스택(`Execute`)이 곧 드레인한다.
        private Receipt DebugFireBinding(in Command cmd)
        {
            var triggers = _ctx?.Triggers;
            if (triggers == null) return Receipt.Reject(RejectReason.UnknownCommand);
            Unit owner = null;
            System.Collections.Generic.IReadOnlyList<Trigger.Binding> list;
            if (cmd.Target.IsNone || cmd.Target == SimEntityId.Match) list = triggers.Registry.MatchBindings;
            else
            {
                owner = _world.Find(cmd.Target);
                if (owner == null) return Receipt.Reject(RejectReason.NoSuchEntity);
                list = owner.Bindings;
            }
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.InstanceId != cmd.Count) continue;
                var e = owner != null
                    ? Trigger.TriggerDispatcher.SubjectOf(owner, Seam.Immediate, b.Def.Trigger)
                    : new Trigger.TriggerEvent
                    {
                        Seam = Seam.Immediate, Kind = b.Def.Trigger, Subject = SimEntityId.Match,
                        SubjectFaction = Wassup.Skills.Faction.DefenderUnit, Target = SimEntityId.None,
                    };
                if (owner?.Attack != null) e.TargetLayers = owner.Attack.TargetLayers;
                triggers.RaiseFor(b, in e);
                return Receipt.Ok;
            }
            return Receipt.Reject(RejectReason.NoSuchEntity);
        }

        private Receipt Submit()
        {
            if (!_clock.SubmitUnlocked) return Receipt.Reject(RejectReason.SubmitLocked);
            _clock.EndMatch(MatchEndReason.Submitted);
            return Receipt.Ok;
        }

        // 디버그 스폰. 레인을 주면 그 입구 칸에서 나오고 **경로·측면 분산도 그 레인에서** 나온다 —
        // 웨이브 생성기도 같은 배선(`EnemySpawn`)을 쓴다.
        private Receipt DebugSpawn(in Command cmd, int tick)
        {
            if (cmd.DefIndex < 0 || cmd.DefIndex >= _def.Enemies.Length)
                return Receipt.Reject(RejectReason.InvalidUnit);
            if (cmd.Lane >= 0 && _map.Snapshot.Spawns.Length == 0)
                return Receipt.Reject(RejectReason.MissingMap);

            var u = EnemySpawn.At(_ctx, _map, cmd.DefIndex, cmd.Lane, cmd.Cell, -1, tick);
            return u != null ? Receipt.Ok : Receipt.Reject(RejectReason.MissingMap);
        }

        private Receipt DebugDestroy(in Command cmd, int tick)
        {
            if (!_world.IsAlive(cmd.Target)) return Receipt.Reject(RejectReason.NoSuchEntity);
            _map.Occupancy.Release(cmd.Target);
            _world.Destroy(cmd.Target, tick);
            return Receipt.Ok;
        }

        // unit 6a2 — **공격 루프 밖의 발사.** 요청 줄에 넣기만 한다(생산자는 자기가 무엇에
        // 얹히는지 모른다). 접는 것은 관문 하나이고, 그 관문을 이 경로도 똑같이 지난다.
        private Receipt DebugFire(in Command cmd)
        {
            if (cmd.ProjectileDefIndex < 0 || cmd.ProjectileDefIndex >= _def.Projectiles.Length)
                return Receipt.Reject(RejectReason.InvalidUnit);
            var caster = _world.Find(cmd.Target);
            if (caster == null) return Receipt.Reject(RejectReason.NoSuchEntity);

            ref var pd = ref _def.Projectiles[cmd.ProjectileDefIndex];
            var req = ProjectileRequest.Empty;
            req.DefIndex = cmd.ProjectileDefIndex;
            req.Movement = (MovementKind)pd.Movement;
            req.Payload = (PayloadKind)pd.Payload;
            req.Owner = caster.Id;
            req.OwnerFaction = caster.Faction;
            req.TargetMask = caster.Attack != null ? caster.Attack.TargetMask : 0;
            req.TargetLayers = caster.Attack != null ? caster.Attack.TargetLayers : (byte)0;
            req.Origin = caster.Position;
            req.Impact = _map != null ? _map.CenterOf(cmd.Cell)
                                      : new float3(cmd.Cell.x, 0f, cmd.Cell.y);
            req.Direction = math.normalizesafe((req.Impact - caster.Position).xz, new float2(0f, 1f));
            req.Damage = cmd.Magnitude;
            // 「자리에 떨어지는 것」이다 — 시전자가 **지정한 좌표**이지 그 몸이 아니다(제약 13).
            req.OriginBodyRadius = 0f;
            _world.ProjectileRequests.Add(req);
            return Receipt.Ok;
        }

        // unit 6a2 — 부여의 생산자 자리. 진짜 생산자(카드·스킬)는 unit 7 이고, 그때도
        // 이 함수가 아니라 **같은 관문**(`ImbueGate`)을 부른다.
        private Receipt DebugImbue(in Command cmd)
        {
            var caster = _world.Find(cmd.Target);
            if (caster == null) return Receipt.Reject(RejectReason.NoSuchEntity);

            if (!cmd.Flag)
            {
                Effects.ImbueGate.Revoke(_ctx, caster, caster.Id, _revokedImbue);
                return Receipt.Ok;
            }
            return Effects.ImbueGate.Grant(_ctx, caster, caster.Id, cmd.Key, cmd.Magnitude, cmd.Seconds)
                ? Receipt.Ok : Receipt.Reject(RejectReason.Unclassified);
        }

        // 회수 사건의 순서를 순회 순서에 안 맡기려고 지운 슬롯을 받는 자리(6a 규율).
        private readonly System.Collections.Generic.List<Effects.ImbueSlot> _revokedImbue
            = new System.Collections.Generic.List<Effects.ImbueSlot>(4);

        // unit 6b — 판 위에 깔리는 것의 생산자 자리. 진짜 생산자(카드·스킬)는 unit 7 이고,
        // 그때도 이 함수가 아니라 **같은 조립 자리**(`HazardSpawn` · `BlockerSpawn`)를 부른다.
        private Receipt DebugHazard(in Command cmd, int tick)
        {
            if (_map != null && !_map.Snapshot.InBounds(cmd.Cell)) return Receipt.Reject(RejectReason.OutOfBounds);
            var h = HazardSpawn.Spawn(_world, _map, _def, cmd.HazardDefIndex, cmd.Cell,
                                      SimEntityId.None, cmd.HazardFaction, targetLayers: 0, tick: tick,
                                      dotDamage: cmd.Magnitude);
            return h != null ? Receipt.Ok : Receipt.Reject(RejectReason.InvalidUnit);
        }

        private Receipt DebugBlocker(in Command cmd, int tick)
        {
            var u = BlockerSpawn.TrySpawn(_world, _map, _def, cmd.HazardDefIndex, cmd.Cell, tick,
                                          cmd.Magnitude, out var reason);
            if (u != null) return Receipt.Ok;
            switch (reason)
            {
                case BlockerSpawn.Reject.OutOfBounds: return Receipt.Reject(RejectReason.OutOfBounds);
                case BlockerSpawn.Reject.NoDefinition: return Receipt.Reject(RejectReason.InvalidUnit);
                default: return Receipt.Reject(RejectReason.Occupied);
            }
        }

        // unit 6b2 — 기믹 셈판의 생산자 자리. 진짜 생산자(주기 바인딩·사망 seam)는 unit 7 이고,
        // 그때도 이 함수가 아니라 **같은 조립 자리**(`PickupSpawn` · `ResignationDrop`)를 부른다.
        private Receipt DebugPickup(in Command cmd, int tick)
        {
            if (_gimmick == null || !_gimmick.TryActive(GimmickKind.RedBull, out var g))
                return Receipt.Reject(RejectReason.GimmickInactive);
            if (!cmd.Flag && _map != null && !_map.Snapshot.InBounds(cmd.Cell))
                return Receipt.Reject(RejectReason.OutOfBounds);
            var p = cmd.Flag
                ? PickupSpawn.TrySpawnRandom(_ctx, PickupKind.RedBull, in g.RedBull, tick)
                : PickupSpawn.At(_world, _map, PickupKind.RedBull, cmd.Cell, g.RedBull.Lifetime, tick);
            return p != null ? Receipt.Ok : Receipt.Reject(RejectReason.Occupied);
        }

        private Receipt DebugResignation(in Command cmd, int tick)
        {
            if (_gimmick == null || !_gimmick.TryActive(GimmickKind.ClockOut, out _))
                return Receipt.Reject(RejectReason.GimmickInactive);
            if (_map != null && !_map.Snapshot.InBounds(cmd.Cell)) return Receipt.Reject(RejectReason.OutOfBounds);
            var src = _world.Find(cmd.Target);
            var r = ResignationDrop.At(_world, _map, cmd.Cell, cmd.Target,
                                       src != null ? src.Faction : Wassup.Skills.Faction.None, tick);
            return r != null ? Receipt.Ok : Receipt.Reject(RejectReason.OutOfBounds);
        }

        private Receipt DebugSetStack(in Command cmd, int tick)
        {
            var u = _world.Find(cmd.Target);
            if (u == null) return Receipt.Reject(RejectReason.NoSuchEntity);

            if (cmd.Flag)
            {
                if (_gimmick == null || !_gimmick.TryActive(GimmickKind.Onsen, out _))
                    return Receipt.Reject(RejectReason.GimmickInactive);
                u.Stacks.SetHeat(cmd.Count);
                return Receipt.Ok;
            }

            if (cmd.Stack == Effects.StackKind.None) return Receipt.Reject(RejectReason.Unclassified);
            int idx = u.Stacks.IndexOf(u.Id, cmd.Stack);
            int have = idx >= 0 ? u.Stacks.Slots[idx].Count : 0;
            if (cmd.Count > have)
            {
                // 올리는 쪽은 **부여 관문을 지난다** — 상한·지속·거점 면역이 라이브와 같다. 피로는 번아웃
                // 기믹이 가리킨 저작 줄을 쓴다(F31).
                int rule = cmd.Stack == Effects.StackKind.Fatigue
                           && _gimmick != null && _gimmick.TryActive(GimmickKind.Burnout, out var g)
                    ? g.Burnout.FatigueStackRule : -1;
                return Effects.EffectApply.Stack(_ctx, u.Id, u, cmd.Stack, cmd.Count - have, 0,
                                                 cmd.Seconds, rule) > 0
                    ? Receipt.Ok : Receipt.Reject(RejectReason.Unclassified);
            }
            if (idx < 0) return Receipt.Ok;
            // 내리는 쪽은 경계 캐시도 같이 내린다 — 다음에 다시 올라가면 **올라가는 길**이라 발화한다.
            var slot = u.Stacks.Slots[idx];
            u.Stacks.Commit(idx, cmd.Count, slot.LastTriggered < cmd.Count ? slot.LastTriggered : cmd.Count);
            _ctx.Bus.Publish(CoreEvent.StackChanged(tick, u, u.Id, cmd.Stack, cmd.Count));
            return Receipt.Ok;
        }

        private Receipt DebugObstacle(in Command cmd)
        {
            if (!_map.Snapshot.InBounds(cmd.Cell)) return Receipt.Reject(RejectReason.OutOfBounds);
            _map.Obstacles.SetManual(cmd.Cell, cmd.Flag);
            return Receipt.Ok;
        }

        // 틱 문맥. 커맨드는 **틱 밖**(동기)에서 들어오는데 스폰 조립이 문맥(정의표·월드·풀)을
        // 요구한다. 판당 한 벌을 `BattleMatch` 가 넘겨 주므로 언제나 최신이다.
        private TickContext _ctx;

        public void Bind(TickContext ctx) => _ctx = ctx;
    }
}
