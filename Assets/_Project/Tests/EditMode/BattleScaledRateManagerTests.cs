using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Wassup.Battle;

namespace Wassup.Tests.EditMode
{
    // time-manager Unit 1/2 — BattleSimGroup 이 자식 시스템을 돌리는지(Unit 1) + RateManager 가
    // BattleTimeScale 로 정지/슬로우모/elapsed 누적을 거는지(Unit 2)를 focused Play 없이 결정론적으로
    // 검증. 그룹에 probe 시스템 하나를 붙이고 월드 시간을 직접 set → group.Update() 로 tick 한다.
    public class BattleScaledRateManagerTests
    {
        private World _world;
        private BattleSimGroup _group;
        private RateProbeSystem _probe;
        private Entity _scaleEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World("BattleRateTestWorld");
            _group = _world.GetOrCreateSystemManaged<BattleSimGroup>(); // OnCreate 가 RateManager 부착
            _probe = _world.GetOrCreateSystemManaged<RateProbeSystem>();
            _group.AddSystemToUpdateList(_probe);
            _group.SortSystems();
            _scaleEntity = Entity.Null; // 새 월드마다 리셋(픽스처 인스턴스는 테스트 간 재사용됨)
        }

        [TearDown]
        public void TearDown()
        {
            if (_world != null && _world.IsCreated) _world.Dispose();
        }

        // 단일 BattleTimeScale 싱글턴을 재사용해 set(중복 생성 시 TryGetSingleton 이 깨진다).
        private void SetScale(float value)
        {
            if (_scaleEntity == Entity.Null)
                _scaleEntity = _world.EntityManager.CreateEntity(typeof(BattleTimeScale));
            _world.EntityManager.SetComponentData(_scaleEntity, new BattleTimeScale { Value = value });
        }

        [Test]
        public void NoSingleton_RunsAtFullSpeed()
        {
            _world.SetTime(new TimeData(0, 0.1f));
            _probe.Updates = 0;
            _group.Update();
            Assert.AreEqual(1, _probe.Updates, "그룹이 자식 시스템을 정확히 1회 돌려야 한다");
            Assert.AreEqual(0.1f, _probe.LastDelta, 1e-5f, "싱글턴 없으면 정상 델타");
        }

        [Test]
        public void ScaleHalf_HalvesChildDeltaTime()
        {
            SetScale(0.5f);
            _world.SetTime(new TimeData(0, 0.1f));
            _probe.Updates = 0;
            _group.Update();
            Assert.AreEqual(1, _probe.Updates);
            Assert.AreEqual(0.05f, _probe.LastDelta, 1e-5f, "자식 델타가 스케일돼야 한다");
        }

        [Test]
        public void ScaleZero_SkipsChildUpdate()
        {
            SetScale(0f);
            _world.SetTime(new TimeData(0, 0.1f));
            _probe.Updates = 0;
            _group.Update();
            Assert.AreEqual(0, _probe.Updates, "정지 시 자식 시스템은 실행되지 않아야 한다");
        }

        [Test]
        public void RestoreFromPause_ResumesFullSpeed()
        {
            SetScale(0f);
            _world.SetTime(new TimeData(0, 0.1f));
            _probe.Updates = 0;
            _group.Update();
            Assert.AreEqual(0, _probe.Updates);

            SetScale(1f);
            _group.Update();
            Assert.AreEqual(1, _probe.Updates, "재개 시 다시 돌아야 한다");
            Assert.AreEqual(0.1f, _probe.LastDelta, 1e-5f);
        }

        [Test]
        public void ElapsedAccumulatesScaled_NotWorldElapsed()
        {
            // M1 회귀 방지: 월드 elapsed 를 크게 둬도 자식이 보는 elapsed 는 스케일 로컬 누산기여야
            // 한다(월드 elapsed 를 읽으면 정지 후 재개 시 한 프레임에 점프한다).
            SetScale(0.5f);
            _world.SetTime(new TimeData(100.0, 0.1f));
            _group.Update();
            Assert.AreEqual(0.05, _probe.LastElapsed, 1e-5, "elapsed 는 월드(100)가 아니라 스케일 누산이어야");

            _world.SetTime(new TimeData(200.0, 0.1f));
            _group.Update();
            Assert.AreEqual(0.10, _probe.LastElapsed, 1e-5, "스케일 델타만 누적돼야");
        }

        [Test]
        public void MultiplePauseResumeCycles_NeverStuck()
        {
            // L2 회귀 방지: _didPushTime 상태가 사이클 간 stuck 되지 않는지.
            for (int cycle = 0; cycle < 3; cycle++)
            {
                SetScale(0f);
                _world.SetTime(new TimeData(0, 0.1f));
                _probe.Updates = 0;
                _group.Update();
                Assert.AreEqual(0, _probe.Updates, $"cycle {cycle}: 정지 상태여야");

                SetScale(1f);
                _group.Update();
                Assert.AreEqual(1, _probe.Updates, $"cycle {cycle}: 재개돼 1회 돌아야");
            }
        }
    }

    // 그룹이 자식을 돌린 횟수와 자식이 본 SystemAPI.Time 을 기록하는 프로브.
    [DisableAutoCreation]
    public partial class RateProbeSystem : SystemBase
    {
        public int Updates;
        public float LastDelta;
        public double LastElapsed;

        protected override void OnUpdate()
        {
            Updates++;
            LastDelta = SystemAPI.Time.DeltaTime;
            LastElapsed = SystemAPI.Time.ElapsedTime;
        }
    }
}
