using Unity.Mathematics;
using Wassup.Battle.Units;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 4 — **마음.**
    //
    // ⚠ **체력을 담당자가 든다**(X29). 마음 거점 개체는 자리와 피격 대상일 뿐이고 체력을
    // 미러하지 않는다. 그 결정의 값은 「마음 N개가 저수지 하나를 공유한다」로 여는 날의
    // 이사 비용이 **0** 이라는 것이다 — 오늘 이미 저수지가 담당자에 있다.
    //
    // 스트레스는 별도 리소스가 아니라 이 체력의 **표시 반전**이다(`StressMath`).
    // 「100」은 표시 정규화이지 체력 최대치가 아니다.
    //
    // 끝내는 통로: **첫 붕괴가 곧 판의 끝**이다(X18). 마음이 몇 개든 처음 무너진 하나가
    // `MatchClock.EndMatch(StressFull)` 를 부른다 — 「패배 없음」이 거짓인 지점이고,
    // 그 판은 남은 시간을 전량 몰수당한다.
    //
    // 담당자 단계가 없다(`ITickPhase` 아님). 마음은 **사건으로만** 움직인다 —
    // 옛 전투의 매 프레임 `SyncGoalStability` 는 ECS 미러를 맞추는 일이었고,
    // 정본이 여기 있는 지금 그 일 자체가 사라졌다.
    public sealed class HeartMeter
    {
        private readonly EventBus _bus;
        private readonly BattleWorld _world;
        private readonly MatchClock _clock;
        private readonly MatchDefinition _def;

        private float _health;
        private float _max;
        private float _killHealPerAwakening;
        private int _leaks;
        private bool _collapsed;

        public HeartMeter(EventBus bus, BattleWorld world, MatchClock clock, MatchDefinition def)
        {
            _bus = bus;
            _world = world;
            _clock = clock;
            _def = def;

            // 골 도달 → 안정도. **보너스 제안보다 먼저**다(X2 ②) — 묵은 스트레스로 판정하면
            // 문턱에서 버튼이 떨린다. 그 순서는 여기서 내는 `HeartChanged` 가 만든다.
            _bus.Subscribe(CoreEventKind.GoalReached, EventOrder.Heart, OnGoalReached);
            // 공성형은 골에 서서 **거점을 때린다.** 그 피해가 여기로 온다 — 거점 개체는
            // 체력을 안 들고, 마음의 체력은 이 담당자 하나가 든다.
            _bus.Subscribe(CoreEventKind.DamageApplied, EventOrder.Heart, OnDamage);
            // 잡을수록 회복한다. 저울은 `KillHealPerAwakening` 하나이고 회복량은 그 적의
            // `AwakeningReward` 재사용이다 — per-enemy 회복 필드를 새로 만들지 않는다.
            _bus.Subscribe(CoreEventKind.UnitSlain, EventOrder.Heart, OnSlain);
        }

        public float Health => _health;
        public float MaxHealth => _max;
        public float Stress => StressMath.FromHealth(_health, _max);
        public bool Collapsed => _collapsed;

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
        }

        // 골에 닿은 적. `Arg` = 공성 가능(1) / 돌격(0).
        private void OnGoalReached(CoreEvent e)
        {
            // 공성형은 여기서 아무 일도 하지 않는다 — 마음 앞에 **서서 때린다**. 그 피해는
            // `OnDamage` 로 온다. 「마음 앞에서 아직 잡을 수 있으므로 놓친 것이 아니다」(Y9).
            if (e.Arg != 0) return;

            var u = _world.Find(e.A);
            int damage = TryEnemyDef(u, out var ed) ? ed.StabilityDamage : 0;

            _leaks++;
            Damage(damage, e.Tick);

            // 돌격형은 마음을 치고 **산화한다.** 여기서 지우는 이유: 「닿으면 사라진다」는
            // 마음의 규칙이지 이동의 규칙이 아니다. 처치가 아니므로 `UnitSlain` 은 나지
            // 않는다 — 점수도 각성도 주지 않는 것이 그 구분의 값이다.
            if (u != null) _world.Destroy(u.Id, e.Tick);
        }

        // 거점이 맞았다. 마음(`DefenderCore`)이 맞은 것만 여기로 흡수한다.
        //
        // ⚠ 생산자는 **거점 개체가 판에 서는 날** 생긴다. 지금 이 구독이 조용한 이유는
        // 그 개체가 아직 없어서이고, 규칙이 없어서가 아니다 — 배선을 나중에 «찾아서» 뚫으면
        // 그때는 피해가 어디로 가는지 아무도 모르는 상태에서 새 경로를 만들게 된다.
        private void OnDamage(CoreEvent e)
        {
            if (e.Faction != Faction.DefenderCore) return;
            Damage(e.Amount, e.Tick);
        }

        private void OnSlain(CoreEvent e)
        {
            if (e.Faction != Faction.EnemyUnit) return;
            if (_killHealPerAwakening <= 0f) return;

            if (!TryEnemyDef(_world.Find(e.B), out var ed)) return;
            if (ed.AwakeningReward <= 0) return;

            Heal(ed.AwakeningReward * _killHealPerAwakening, e.Tick);
        }

        /// <summary>
        /// 그 개체의 **정의표 줄**. 사건이 나른 id 로 개체를 찾는다.
        ///
        /// ⚠ 계약 4(「이벤트로 상태를 되묻지 않는다」)와의 관계: 여기서 읽는 것은 **정의표
        /// 인덱스**(불변 정체성)이지 그 틱에 변하는 상태가 아니고, 사망 개체가 보이는 것도
        /// 우연이 아니다 — 「표시 틱 ≠ 소멸 틱」(unit 3 구현 9)이 시체가 자기 자리를 읽을 수
        /// 있게 일부러 한 틱을 남기고, 처치 사건은 정확히 그 창 안에서 배달된다.
        /// 그래도 **없으면 0 으로 조용히 흐르지 않는다** — false 를 돌려 호출부가 멈춘다.
        /// </summary>
        private bool TryEnemyDef(Unit u, out EnemyDef def)
        {
            if (u != null && u.DefIndex >= 0 && u.DefIndex < _def.Enemies.Length)
            {
                def = _def.Enemies[u.DefIndex];
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
