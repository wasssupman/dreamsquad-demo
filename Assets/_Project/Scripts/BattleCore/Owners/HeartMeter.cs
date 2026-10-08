using System.Collections.Generic;
using Unity.Mathematics;
using Somnia.Battle.Skills;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 4 — **마음.**
    //
    // ⚠ **체력을 담당자가 든다**(X29). 마음 거점 개체는 자리와 피격 대상일 뿐이고 체력을
    // 미러하지 않는다(`Unit.HealthExternal`). 그 결정의 값은 「마음 N개가 저수지 하나를
    // 공유한다」로 여는 날의 이사 비용이 **0** 이라는 것이다 — 오늘 이미 저수지가 담당자에 있다.
    //
    // 스트레스는 별도 리소스가 아니라 이 체력의 **표시 반전**이다(`StressMath`).
    // 「100」은 표시 정규화이지 체력 최대치가 아니다.
    //
    // 끝내는 통로: **첫 붕괴가 곧 판의 끝**이다(X18). 마음이 몇 개든 처음 무너진 하나가
    // `MatchClock.EndMatch(StressFull)` 를 부른다 — 「패배 없음」이 거짓인 지점이고,
    // 그 판은 남은 시간을 전량 몰수당한다.
    //
    // 틱 단계가 **생겼다**(unit 4 거점 스폰). 하는 일은 둘뿐이고 둘 다 「사건으로는 못 하는
    // 것」이다:
    //   ① **방패 관찰** — 본능이 살아 있는 동안 마음을 표적에서 뺀다. 플래그가 아니라
    //      관찰인 이유는 아래 `CoreShielded` 주석에 있다.
    //   ② **타워 인박스 드레인** — 체력이 여기 있으므로 피해 단계가 그 개체를 건너뛴다.
    //      「세운 쪽이 드레인을 진다」가 `Unit.HealthExternal` 의 계약이고, 세운 쪽이 여기다.
    // 옛 전투의 매 프레임 `SyncGoalStability`(ECS 미러 맞추기)는 사라졌다 — 정본이 여기다.
    public sealed class HeartMeter : ITickPhase
    {
        public string Name => "Heart";

        private readonly EventBus _bus;
        private readonly BattleWorld _world;
        private readonly MatchClock _clock;
        private readonly MatchDefinition _def;

        /// <summary>이 판의 마음 타워들. 골 하나당 하나이고 판 중에 늘거나 줄지 않는다.</summary>
        private readonly List<SimEntityId> _towers = new List<SimEntityId>(4);

        private float _health;
        private float _max;
        private float _killHealPerAwakening;
        private int _leaks;
        private bool _collapsed;
        private bool _shielded;

        public HeartMeter(EventBus bus, BattleWorld world, MatchClock clock, MatchDefinition def)
        {
            _bus = bus;
            _world = world;
            _clock = clock;
            _def = def;

            // 골 도달 → 안정도. **보너스 제안보다 먼저**다(X2 ②) — 묵은 스트레스로 판정하면
            // 문턱에서 버튼이 떨린다. 그 순서는 여기서 내는 `HeartChanged` 가 만든다.
            _bus.Subscribe(CoreEventKind.GoalReached, EventOrder.Heart, OnGoalReached);
            // 잡을수록 회복한다. 저울은 `KillHealPerAwakening` 하나이고 회복량은 그 적의
            // `AwakeningReward` 재사용이다 — per-enemy 회복 필드를 새로 만들지 않는다.
            _bus.Subscribe(CoreEventKind.UnitSlain, EventOrder.Heart, OnSlain);
        }

        public float Health => _health;
        public float MaxHealth => _max;
        public float Stress => StressMath.FromHealth(_health, _max);
        public bool Collapsed => _collapsed;

        /// <summary>이 판의 마음 타워 수. 0 = 마음 미저작(체력 0) — 타워를 세우지 않는다.</summary>
        public int TowerCount => _towers.Count;

        /// <summary>
        /// **돌격형이 마음을 치고 산화한 수** = 이 판의 「놓쳤다」(Y9).
        /// ⚠ 화면에 「유출」이라 쓰면 거짓말이다 — 옛 뜻(부서진 마음으로 적이 흘러듦)은
        /// 첫 붕괴에 판이 끝나므로 구조적으로 발생하지 않는다. 점수와도 무관하다.
        /// </summary>
        public int Leaks => _leaks;

        /// <summary>
        /// 방어 본능이 살아 있는 동안 마음은 표적에서 빠진다.
        /// 「본능 개체가 하나라도 있나」라는 **관찰**이지 플래그가 아니다 — 플래그였다면
        /// 마지막 본능이 죽는 경로를 하나 빠뜨렸을 때 마음이 영영 무적으로 남는다.
        ///
        /// ⚠ **막는 게 아니라 시선을 돌린다.** 피해만 막으면 적이 마음 앞에 붙어 아무 일도
        /// 일어나지 않는 그림이 되고, 플레이어에겐 버그로 읽힌다. 그래서 표적 후보에서 뺀다
        /// (`Unit.Untargetable`) — 적이 애초에 본능·방어유닛을 조준하게 된다.
        /// </summary>
        public bool CoreShielded
        {
            get
            {
                var units = _world.Units;
                for (int i = 0; i < units.Count; i++)
                    if (units[i].Faction == Faction.DefenderInstinct && !units[i].Dead) return true;
                return false;
            }
        }

        public void Begin(in HeartDef config)
        {
            _max = math.max(0f, config.MaxHealth);
            _health = _max;
            _killHealPerAwakening = math.max(0f, config.KillHealPerAwakening);
            _leaks = 0;
            _collapsed = false;
            _towers.Clear();

            // ⚠ **마음이 미저작(체력 0)이면 타워를 안 세운다.** 옛 전투의 `goalStabilityMax > 0`
            // 게이트를 그대로 옮긴 것이고, 근거는 「체력 0 짜리 건물은 세우자마자 무너진다」가
            // 아니라 **덱이 마음을 저작하지 않은 판은 마음이 없는 판**이라는 것이다
            // (`StressMath.FromHealth(0, 0) = 0` 이 같은 문장의 다른 절반이다).
            if (_max <= 0f) return;

            var map = _def.Map;
            for (int i = 0; i < map.Goals.Length; i++)
            {
                var u = _world.SpawnStructure(Faction.DefenderCore, defIndex: -1,
                                              cell: map.Goals[i],
                                              position: map.CellCenter(map.Goals[i]),
                                              footprint: StructureSize.Core,
                                              maxHealth: 0f, healthExternal: true, tick: 0);
                _towers.Add(u.Id);
            }

            // 방패의 초기값은 **여기서** 정한다 — 첫 틱을 기다리면 본능이 선 판의 첫 틱에만
            // 마음이 조준 가능한 창이 생긴다.
            RefreshShield();
        }

        // ── 틱 ───────────────────────────────────────────────────────────────

        public void Run(TickContext ctx)
        {
            if (_towers.Count == 0) return;
            RefreshShield();
            DrainTowers(ctx);
        }

        private void RefreshShield()
        {
            _shielded = CoreShielded;
            for (int i = 0; i < _towers.Count; i++)
            {
                var t = _world.Find(_towers[i]);
                if (t != null) t.Untargetable = _shielded;
            }
        }

        /// <summary>
        /// **거점이 맞았다 → 마음이 깎인다.** 옛 `BattleBridge.EnqueueGoalTowerDamage` 의 후계이고,
        /// 그쪽이 「최근접 타워를 골라 큐에 넣던」 일이 여기서는 그냥 인박스 드레인이다.
        ///
        /// 피해 단계가 이 개체를 건너뛰는 것과 **짝**이다(`Unit.HealthExternal`). 짝이 깨지면
        /// 둘 중 하나가 난다: 체력이 두 벌이 되거나(피해 단계가 0 짜리 체력을 깎아 매번 죽는다),
        /// 인박스가 영영 안 비어 피해가 무한히 쌓인다.
        ///
        /// ⚠ **회복 인박스도 여기서 비운다.** 마음의 회복은 처치 사건이 주는 것(`OnSlain`)이고
        /// 인박스 회복의 생산자는 아직 없지만, 안 비우면 그 생산자가 생기는 날 조용히 샌다.
        /// </summary>
        private void DrainTowers(TickContext ctx)
        {
            for (int i = 0; i < _towers.Count; i++)
            {
                var t = _world.Find(_towers[i]);
                if (t == null) continue;
                var inbox = t.Inbox;
                if (inbox.Damage.Count == 0 && inbox.Heal.Count == 0) continue;

                // **방패 백스톱**(옛 `CoreShielded` 소비처 5 — `DamageApplicationSystem.cs:139-144`).
                // 조준 제외(`Untargetable`)는 마음을 «겨누는» 것만 막는다. 옆에 떨어진 광역·미래의 스킬
                // 페이로드처럼 조준을 안 지나는 피해는 여기서 **버린다** — 생산자마다 거르지 않는 것이
                // 옛 설계의 선택이고, 그래야 새 피해 경로가 자동으로 덮인다(생산자 쪽 중복 필터 금지).
                // ⚠ 적립하지 않고 **비운다.** 쌓아 두면 방패가 깨지는 틱에 통째로 터진다. 회복도 같이 비우고
                // 숫자도 안 띄운다(옛 것도 `continue` 로 피해 숫자 앞에서 빠졌다).
                if (_shielded)
                {
                    inbox.Damage.Clear();
                    inbox.Heal.Clear();
                    continue;
                }

                float damage = 0f;
                for (int k = 0; k < inbox.Damage.Count; k++) damage += inbox.Damage[k].Amount;
                float heal = 0f;
                for (int k = 0; k < inbox.Heal.Count; k++) heal += inbox.Heal[k];

                // 피해 숫자는 **인박스 항목당 하나**이고 비율은 그 틱 최종값이다(C7) —
                // 유닛의 피해 단계와 같은 규약이라 뷰가 두 어휘를 배우지 않는다.
                // 비율의 분모가 개체의 `MaxHealth` 가 아니라 **마음의 최대치**인 것이
                // 체력이 여기 있다는 사실의 표면이다.
                float after = math.clamp(_health - damage + heal, 0f, _max);
                float ratio = _max > 0f ? after / _max : 0f;
                for (int k = 0; k < inbox.Damage.Count; k++)
                {
                    var e = inbox.Damage[k];
                    if (e.Amount <= 0f) continue;
                    ctx.Bus.Publish(CoreEvent.DamageApplied(ctx.Tick, t, e.Source, e.Amount, 0f, ratio));
                }
                if (heal > 0f) ctx.Bus.Publish(CoreEvent.HealApplied(ctx.Tick, t, heal));

                inbox.Damage.Clear();
                inbox.Heal.Clear();

                if (heal > 0f) Heal(heal, ctx.Tick);
                if (damage > 0f) Damage(damage, ctx.Tick);
            }
        }

        // ── 사건 ─────────────────────────────────────────────────────────────

        // 골에 닿은 적. `Arg` = 공성 가능(1) / 돌격(0).
        private void OnGoalReached(CoreEvent e)
        {
            // 공성형은 여기서 아무 일도 하지 않는다 — 마음 앞에 **서서 때린다**. 그 피해는
            // 타워 인박스로 들어와 `DrainTowers` 가 받는다. 「마음 앞에서 아직 잡을 수
            // 있으므로 놓친 것이 아니다」(Y9).
            if (e.Arg != 0) return;

            // ⚠ 정의표 줄을 **사건이 실어 온 인덱스**로 찾는다. 드레인 시점에 개체를 되묻지
            // 않는 이유는 계약 7 이다 — 이 사건의 주체는 곧 사라지는 적이고, 되물으면 그
            // 순서가 바뀌는 날 안정도 피해가 조용히 0 이 된다.
            int damage = TryEnemyDef(e.DefIndex, out var ed) ? ed.StabilityDamage : 0;

            _leaks++;
            Damage(damage, e.Tick);

            // 돌격형은 마음을 치고 **산화한다.** 여기서 지우는 이유: 「닿으면 사라진다」는
            // 마음의 규칙이지 이동의 규칙이 아니다. 처치가 아니므로 `UnitSlain` 은 나지
            // 않는다 — 점수도 각성도 주지 않는 것이 그 구분의 값이다.
            _world.Destroy(e.A, e.Tick);
        }

        private void OnSlain(CoreEvent e)
        {
            if (e.Faction != Faction.EnemyUnit) return;
            if (_killHealPerAwakening <= 0f) return;

            if (!TryEnemyDef(e.DefIndex, out var ed)) return;
            if (ed.AwakeningReward <= 0) return;

            Heal(ed.AwakeningReward * _killHealPerAwakening, e.Tick);
        }

        /// <summary>
        /// 그 개체의 **정의표 줄**. 사건이 값으로 실어 온 인덱스로 찾는다 —
        /// 인덱스는 불변 정체성이라 사건에 실리는 것이 맞고, 실려 있으므로 드레인 시점에
        /// 개체가 없어도(사망·산화) 값이 온전하다. 범위 밖이면 **조용히 0 으로 흐르지 않고**
        /// false 를 돌려 호출부가 멈춘다.
        /// </summary>
        private bool TryEnemyDef(int defIndex, out EnemyDef def)
        {
            if (defIndex >= 0 && defIndex < _def.Enemies.Length)
            {
                def = _def.Enemies[defIndex];
                return true;
            }
            def = default;
            return false;
        }

        private void Heal(float amount, int tick)
        {
            if (amount <= 0f || _collapsed || _max <= 0f) return;
            float before = _health;
            _health = math.min(_max, _health + amount);
            if (_health != before) _bus.Publish(CoreEvent.HeartChanged(tick, _health, Stress));
        }

        private void Damage(float amount, int tick)
        {
            if (_collapsed || _max <= 0f) return;
            if (amount > 0f) _health = math.max(0f, _health - amount);
            _bus.Publish(CoreEvent.HeartChanged(tick, _health, Stress));

            if (_health > 0f) return;

            // **첫 붕괴가 곧 판의 끝.** 마음이 몇 개든 처음 무너진 하나가 통로를 연다.
            _collapsed = true;
            _bus.Publish(CoreEvent.HeartCollapsed(tick));
            _clock.EndMatch(MatchEndReason.StressFull);
        }
    }
}
