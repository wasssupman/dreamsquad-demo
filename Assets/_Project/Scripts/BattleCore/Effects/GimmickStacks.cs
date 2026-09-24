namespace Wassup.BattleCore.Effects
{
    /// <summary>
    /// 스택 누적 **요청** 한 줄(`BattleWorld.StackAccruals`). 「해 달라」이고 게이트는 받는 쪽
    /// (`EffectApply.Stack`)이 갖는다 — 같은 줄의 발사·군중 제어 요청과 같은 규율이다.
    /// </summary>
    public struct StackAccrual
    {
        public SimEntityId Target;
        /// <summary>누가 쌓았나. 기믹 피로는 **자기 자신**이다(옛 `source = entity`) — 스택 병합 키의 출처 축.</summary>
        public SimEntityId Source;
        public StackKind Kind;
        public int Amount;
        /// <summary>`MatchDefinition.StackRules` 줄(-1 = 종류 첫 줄로 폴백).</summary>
        public int RuleIndex;
    }

    // battle-core-rebuild unit 6b2 — **시간으로 쌓이는 두 기믹의 한 걸음.**
    //
    // 여기 있는 것은 「한 주기가 지났을 때 무슨 일이 일어나나」뿐이다. **언제(주기)·누구에게
    // (대상 필터)는 unit 7** 의 유닛 호스트 바인딩이 준다(rev 3 정정 2 — 위상이 유닛마다 다르다).
    // 그래서 이 파일에 타이머가 없다 — 있으면 unit 7 이 그것을 걷어내야 한다.
    //
    // ⚠ 두 걸음의 **반영 시점이 다르다** — 옛 단계 위치가 달랐기 때문이다:
    //   · 열기 — 옛 `HeatAccrualSystem`(23) 은 피해 적용 **앞**이었다 → 인박스에 바로 넣는다.
    //     `[Periodic]` seam(장 준비 끝)에서 불러도 같은 틱의 피해 단계가 정산한다.
    //   · 피로 — 옛 `FatigueAccrualSystem`(30) 은 스탯 적용 **뒤**였다 → 요청 줄에 넣고, 소비는
    //     `TickProjectilePhase` 의 스탯 적용 뒤 단계다. 그래서 **한 틱 뒤에** 임계를 본다.
    public static class GimmickStacks
    {
        /// <summary>
        /// 열기 +1 → `HeatMath.Delta` → **부호만 보고** 회복/피해 인박스로 보낸다. 반환 = 델타.
        ///
        /// ⚠ 열기는 **개체의 스택 집합에 산다**(`StackSet.Heat`) — 스폰 경로를 안 고치는 lazy 방식이고
        /// (첫 누적이 곧 부착), 개체가 사라지면 같이 사라진다. `StackKind` 값을 새로 따지 않은 이유는
        /// `StackSet` 헤더 참조.
        /// ⚠ 피해의 출처는 **없다** — 환경이라 처치 귀속이 없다(옛 `source = Entity.Null`).
        /// </summary>
        public static float AccrueHeat(Unit u, in OnsenSpec spec)
        {
            if (u == null || u.Dead || u.HealthExternal) return 0f;
            int stacks = u.Stacks.AddHeat(spec.HeatMaxStack);
            float delta = HeatMath.Delta(stacks, spec.FlipThreshold, u.MaxHealth, u.Health,
                                         spec.HealPercent, spec.LossPercent);
            if (delta > 0f) u.Inbox.Heal.Add(delta);
            else if (delta < 0f) u.Inbox.Damage.Add(new DamageEntry { Amount = -delta, Source = SimEntityId.None });
            return delta;
        }

        /// <summary>
        /// 피로 +`FatigueAmount` 를 **요청**한다. 실제로 쌓는 것은 스탯 적용 뒤 단계다(1틱 지연 박제).
        /// 임계 파생(번아웃)은 6a 의 스택 규칙 줄을 그대로 타고, 출처 꼬리표는 `ModifierOrigin.Burnout` 이다.
        /// </summary>
        public static void RequestFatigue(BattleWorld world, Unit u, in BurnoutSpec spec)
        {
            if (world == null || u == null || u.Dead || spec.FatigueAmount <= 0) return;
            world.StackAccruals.Add(new StackAccrual
            {
                Target = u.Id,
                Source = u.Id,
                Kind = StackKind.Fatigue,
                Amount = spec.FatigueAmount,
                RuleIndex = spec.FatigueStackRule,
            });
        }
    }
}
