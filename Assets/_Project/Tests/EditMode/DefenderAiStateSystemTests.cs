using NUnit.Framework;
using Unity.Entities;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.UnitAi;

// defender-autobattle-ai unit 1 — 적용 레이어: 컴포넌트 → 스냅샷 → DefenderAi.Resolve → 저장(유일 writer).
public class DefenderAiStateSystemTests
{
    private World _world; private EntityManager _em; private SimulationSystemGroup _group;

    [SetUp]
    public void SetUp()
    {
        _world = new World("DefenderAiStateSystemTests"); _em = _world.EntityManager;
        _group = _world.CreateSystemManaged<SimulationSystemGroup>();
        _group.AddSystemToUpdateList(_world.CreateSystem<DefenderAiStateSystem>());
        _group.SortSystems();
    }

    [TearDown]
    public void TearDown() { if (_world != null && _world.IsCreated) _world.Dispose(); }

    private Entity Defender(DefenderAttackPolicy policy = DefenderAttackPolicy.Target)
    {
        var e = _em.CreateEntity();
        _em.AddComponent<DefenderUnitTag>(e);
        _em.AddComponentData(e, new DefenderAiStatus { value = Wassup.UnitAi.DefenderAiState.Deploying });
        _em.AddComponentData(e, new DefenderAiPolicy { value = policy });
        _em.AddComponentData(e, new AttackState { cooldownDuration = 1f });
        return e;
    }

    private Wassup.UnitAi.DefenderAiState StateOf(Entity e) => _em.GetComponentData<DefenderAiStatus>(e).value;
    private void Tick() { _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + 0.1, 0.1f)); _group.Update(); }

    [Test]
    public void Pending_IsDeploying_ThenReady()
    {
        var e = Defender(); _em.AddComponent<PendingDeployment>(e);
        Tick(); Assert.AreEqual(Wassup.UnitAi.DefenderAiState.Deploying, StateOf(e));
        _em.RemoveComponent<PendingDeployment>(e);
        Tick(); Assert.AreEqual(Wassup.UnitAi.DefenderAiState.Ready, StateOf(e));
    }

    [Test]
    public void Swinging_IsEngaging()
    {
        var e = Defender(); _em.SetComponentData(e, new AttackState { hitDelayRemaining = 0.5f });
        Tick(); Assert.AreEqual(Wassup.UnitAi.DefenderAiState.Engaging, StateOf(e));
    }

    [Test]
    public void Summoner_WithLivePatrol_IsSustaining_AndDropsOnPatrolDeath()
    {
        var patrol = _em.CreateEntity(); _em.AddComponentData(patrol, new Health { value = 10f, max = 10f });
        var e = Defender(DefenderAttackPolicy.Summon);
        _em.AddComponentData(e, new SummonerState { current = patrol, patrolDataIndex = 0 });
        Tick(); Assert.AreEqual(Wassup.UnitAi.DefenderAiState.Sustaining, StateOf(e));
        _em.AddComponent<DeadTag>(patrol);   // 사망 프레임(파괴 전) — 3중 술어의 DeadTag 축
        Tick(); Assert.AreEqual(Wassup.UnitAi.DefenderAiState.Ready, StateOf(e), "DeadTag 만 붙어도 유지 종료(상실 모션이 늦지 않게)");
    }

    [Test]
    public void TargetPolicy_IgnoresSummoner()
    {
        var patrol = _em.CreateEntity(); _em.AddComponentData(patrol, new Health { value = 10f, max = 10f });
        var e = Defender(DefenderAttackPolicy.Target);
        _em.AddComponentData(e, new SummonerState { current = patrol, patrolDataIndex = 0 });
        Tick(); Assert.AreEqual(Wassup.UnitAi.DefenderAiState.Ready, StateOf(e));
    }

    [Test]
    public void Dead_KeepsStaleValue()
    {
        var e = Defender(); _em.AddComponent<DeadTag>(e);
        Tick(); Assert.AreEqual(Wassup.UnitAi.DefenderAiState.Deploying, StateOf(e), "시체는 쓰지 않는다(파괴 대기)");
    }
}
