using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;

// defender-deploy-phase unit 1 — 배치 페이즈의 시계와 종료를 sim 이 소유한다.
public class DeploymentActivationSystemTests
{
    private World _world;
    private EntityManager _em;
    private SimulationSystemGroup _group;
    private NativeQueue<DefenderActivatedEvent> _queue;

    [SetUp]
    public void SetUp()
    {
        _world = new World("DeploymentActivationSystemTests");
        _em = _world.EntityManager;
        _group = _world.CreateSystemManaged<SimulationSystemGroup>();
        _group.AddSystemToUpdateList(_world.CreateSystem<DeploymentActivationSystem>());
        _group.SortSystems();
        _queue = new NativeQueue<DefenderActivatedEvent>(Allocator.Persistent);
        _em.AddComponentData(_em.CreateEntity(), new DefenderActivatedEventsSingleton { queue = _queue });
    }

    [TearDown]
    public void TearDown()
    {
        if (_queue.IsCreated) _queue.Dispose();
        if (_world != null && _world.IsCreated) _world.Dispose();
    }

    private Entity Defender(byte stage, float remaining, bool withSlots)
    {
        var e = _em.CreateEntity();
        _em.AddComponent<DefenderUnitTag>(e);
        _em.AddComponentData(e, new PendingDeployment { stage = stage, remaining = remaining });
        if (withSlots) _em.AddBuffer<DcTriggerSlot>(e);
        return e;
    }

    private void Tick(int n = 1, float dt = 0.1f)
    {
        for (int i = 0; i < n; i++)
        {
            _world.SetTime(new Unity.Core.TimeData(_world.Time.ElapsedTime + dt, dt));
            _group.Update();
        }
    }

    [Test]
    public void InFlight_NeverActivates_WithoutLanding()
    {
        var e = Defender(PendingDeployment.InFlight, 0f, true);
        Tick(50);
        Assert.IsTrue(_em.HasComponent<PendingDeployment>(e), "비행 중엔 시계가 없다 — 착지 신호 없이는 영원히 pending");
        Assert.AreEqual(0, _queue.Count);
    }

    [Test]
    public void Deploying_CountsDown_ThenActivates_WithJustDeployedAndEvent()
    {
        // 0.45 = dt 의 배수가 아닌 값 — 부동소수 누적으로 「정확히 0」에 기대지 않는다(0.5/0.1 은 5.96e-8 이 남는다).
        var e = Defender(PendingDeployment.Deploying, 0.45f, true);
        Tick(4);
        Assert.IsTrue(_em.HasComponent<PendingDeployment>(e), "0.4s 경과 — 아직 배치 중");
        Assert.AreEqual(0, _queue.Count);
        Tick(1);
        Assert.IsFalse(_em.HasComponent<PendingDeployment>(e), "0.5s — 활성화");
        Assert.IsTrue(_em.HasComponent<JustDeployed>(e), "배치 스킬 엣지 = 활성화");
        Assert.AreEqual(1, _queue.Count);
        Assert.AreEqual(e, _queue.Dequeue().entity);
    }

    [Test]
    public void ZeroMotion_ActivatesWithinOneTick()
    {
        var e = Defender(PendingDeployment.Deploying, 0f, true);
        Tick(1);
        Assert.IsFalse(_em.HasComponent<PendingDeployment>(e));
        Assert.AreEqual(1, _queue.Count);
    }

    [Test]
    public void NoTriggerSlots_ActivatesWithoutJustDeployed()
    {
        var e = Defender(PendingDeployment.Deploying, 0f, withSlots: false);
        Tick(1);
        Assert.IsFalse(_em.HasComponent<PendingDeployment>(e));
        Assert.IsFalse(_em.HasComponent<JustDeployed>(e), "슬롯 버퍼 없는 유닛엔 태그를 남기지 않는다(소비 시스템의 RequireForUpdate 와 어긋난다)");
        Assert.AreEqual(1, _queue.Count);
    }

    [Test]
    public void Dead_DropsPending_WithoutEventOrJustDeployed()
    {
        var e = Defender(PendingDeployment.InFlight, 0f, true);
        _em.AddComponent<DeadTag>(e);
        Tick(1);
        Assert.IsFalse(_em.HasComponent<PendingDeployment>(e), "시체는 비행 중이어도 pending 을 걷는다");
        Assert.IsFalse(_em.HasComponent<JustDeployed>(e));
        Assert.AreEqual(0, _queue.Count, "시체는 활성화 사건이 아니다");
    }
}
