using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.View;
using Wassup.Core;
using Wassup.Data;
using Wassup.Presentation;
using Wassup.BattleCore.Trigger;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 9 — 옛 `PlayMode/BeamPresentationTest` 의 규칙을 새 빔 프리젠터(`CoreBeamPresenter`)로 옮긴 것.
    //
    //   · 빔 유닛이 공격하면 빔 몸통이 **총구 ↔ 대상**으로 늘어난다(프리팹 원본 길이 그대로면 배치가 한 번도 안 된 것)
    //     · 빔 렌더러는 유닛 대역 위(가려지면 끊겨 보인다 — 옛 제보)
    //   · 배치 일제 조사는 **대상당 빔 1**(초판은 배치 빔이 첫 프레임에 죽어 공격 빔 하나만 남았다)
    //   · 일제 조사 중에는 **평타를 하지 않는다**(조사 = 지속을 갖는 채널, `AreaDotSkill` 헤더)
    //
    // 옛 셋째 테스트(드래그 경로도 대상당 빔 1)는 따로 옮기지 않는다 — 새 전투에는 배치 경로가 `Command.PlaceDefender` 하나뿐이고
    // 드래그 입력도 같은 커맨드 + 착지 신호(`LandDefender`)를 낸다. 아래 둘째 테스트가 바로 그 경로(배치 → 착지 → 활성화)를 탄다.
    //
    // 빔 유닛은 **저작이 정한다**(`beamVfxPrefab` 유무 · 배치 스킬 `AreaDot`) — id 를 박지 않는다. 부팅 편성은 기기 프로필을
    // 상속하므로 카탈로그에서 골라 **그 유닛만으로** 판을 다시 짓는다(테스트 모드 프리셋 · 프로필 끊기).
    public sealed class RetiredBeamPortTest
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TestModeContext.Clear();
            CoreSceneFixture.EndErrorWatch();
            yield return null;
        }

        // 옛 BeamPresentationTest::BeamUnit_Attacking_StretchesBeamBodyBetweenMuzzleAndTarget — 공격 빔 몸통이 총구↔대상 길이로 늘어나고 유닛 대역 위에 그려진다
        [UnityTest]
        public IEnumerator 빔_유닛이_공격하면_빔_몸통이_총구와_대상_사이로_늘어난다()
        {
            BattleDriver driver = null;
            DefenderUnitData beamUnit = null;
            yield return BootWithBeamUnit(requireBarrage: false, (d, u) => { driver = d; beamUnit = u; });
            var presenter = Object.FindAnyObjectByType<CoreBeamPresenter>();
            var units = Object.FindAnyObjectByType<CoreUnitViewPool>();
            Assert.IsNotNull(presenter, "씬에 CoreBeamPresenter 가 없다");
            Assert.IsNotNull(units, "씬에 CoreUnitViewPool 이 없다");

            SimEntityId caster = PlaceAndActivate(driver, 0, out _);
            var casterUnit = driver.Find(caster);
            // 사거리 안 길 칸 하나에 적을 세우고 묶어 둔다(기절) — 끝점이 움직이지 않아야 길이를 잴 수 있다.
            var def = driver.Definition.Units[0];
            float reach = (def.AttackRange + def.BodyRadiusTiles) * driver.TileSize;
            var cells = NearestPathCells(driver.Match.Map, casterUnit.Position, 1);
            Assert.Less(math.distance(driver.Match.Map.CenterOf(cells[0]), casterUnit.Position), reach,
                "빔 유닛 사거리 안에 길 칸이 없다 — 공격 빔을 증언할 수 없다");
            var enemy = SpawnPinnedEnemy(driver, cells[0]);

            // 세션이 열리고 한 번 배치될 때까지 한 프레임에 한 틱씩.
            Transform body = null;
            for (int f = 0; f < 240 && body == null; f++)
            {
                driver.Match.Tick();
                yield return null;
                body = ActiveBeamBody(presenter);
            }
            Assert.IsNotNull(body, "공격 중인데 빔 세션이 안 열렸다(활성 BeamBody 없음)");
            for (int f = 0; f < 3; f++) { driver.Match.Tick(); yield return null; }
            body = ActiveBeamBody(presenter);
            Assert.IsNotNull(body, "빔 세션이 곧바로 닫혔다 — 공격 사건 사이에 끊긴다");

            Assert.IsTrue(units.TryResolveViewPosition(caster, useAnchor: true, out var muzzle), "시전자 뷰 자리가 없다");
            Assert.IsTrue(units.TryResolveViewPosition(enemy, useAnchor: false, out var end), "대상 뷰 자리가 없다");
            float length = Vector3.Distance(muzzle, end);
            var prefabBody = beamUnit.beamVfxPrefab.transform.Find("BeamBody");
            Assert.IsNotNull(prefabBody, "빔 프리팹에 BeamBody 가 없다");
            Assert.AreNotEqual(prefabBody.localScale.z, body.localScale.z,
                "BeamBody 길이가 프리팹 원본 그대로다 — 배치가 한 번도 성공하지 않았다");
            Assert.Greater(body.localScale.z, 0.01f, "빔 길이가 0 이면 몸통이 안 보인다");
            Assert.AreEqual(length, body.localScale.z, 0.02f * length + 1e-3f, "빔 몸통 길이 ≠ 총구↔대상 거리");
            Assert.Less(Vector3.Distance(muzzle, body.position), 0.02f * length + 1e-3f, "빔 몸통이 총구에서 시작하지 않는다");

            var renderers = body.parent.GetComponentsInChildren<ParticleSystemRenderer>(true);
            Assert.Greater(renderers.Length, 0, "빔 렌더러가 있어야 한다");
            foreach (var r in renderers)
                Assert.GreaterOrEqual(r.sortingOrder, BoardSortOrder.BeamOrder,
                    $"빔 렌더러 '{r.name}' 의 sortingOrder({r.sortingOrder})가 유닛 대역 아래다 — 가려진다");

            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        // 옛 BeamPresentationTest::OnPlaceBarrage_OpensOneBeamPerTarget_AndHoldsFire — 배치 일제 조사는 반경 안 대상마다 빔 하나를 연다
        [UnityTest]
        public IEnumerator 배치_일제_조사는_대상마다_빔을_하나씩_연다()
        {
            BattleDriver driver = null;
            yield return BootWithBeamUnit(requireBarrage: true, (d, _) => driver = d);
            var presenter = Object.FindAnyObjectByType<CoreBeamPresenter>();
            Assert.IsNotNull(presenter, "씬에 CoreBeamPresenter 가 없다");

            var beamTargets = new HashSet<SimEntityId>();
            SimEntityId caster = SimEntityId.None;
            System.Action<CoreEvent> probe = e =>
            {
                if (e.Kind == CoreEventKind.SkillVisual && e.A == caster
                    && (Wassup.Skills.SkillVisualKind)e.Arg == Wassup.Skills.SkillVisualKind.Beam)
                    beamTargets.Add(e.B);
            };
            driver.Subscribe(ViewOrder.Trace, probe);

            // 배치 **전에** 반경 후보를 세운다 — 배치 스킬은 활성화 순간의 스냅샷을 본다.
            int2 anchor = AnchorNearestPath(driver, 0);
            var near = NearestPathCells(driver.Match.Map, driver.Match.Map.CenterOf(anchor), 3);
            foreach (var c in near) SpawnPinnedEnemy(driver, c);
            caster = PlaceAt(driver, 0, anchor);

            int maxLive = 0;
            int framesAfterFire = -1;
            int budget = MotionTicks(driver, 0) + 240;
            for (int f = 0; f < budget && framesAfterFire < 20; f++)
            {
                driver.Match.Tick();
                yield return null;
                maxLive = Mathf.Max(maxLive, presenter.LiveSessionCount);
                if (framesAfterFire >= 0) framesAfterFire++;
                else if (beamTargets.Count > 0) framesAfterFire = 0;
            }
            driver.Unsubscribe(probe);

            Assert.GreaterOrEqual(beamTargets.Count, 2,
                $"코어가 조사 빔을 {beamTargets.Count}체에만 냈다 — 반경 안 대상이 둘 미만이면 「대상당 하나」를 증언할 수 없다");
            Assert.GreaterOrEqual(maxLive, beamTargets.Count,
                $"조사 대상 {beamTargets.Count}체 전원에게 빔이 이어져야 한다(실측 최대 {maxLive}). 1 이면 조사 빔이 죽고 공격 빔만 남은 것");
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        // 옛 BeamPresentationTest::OnPlaceBarrage_SuppressesBasicAttackForItsDuration — 일제 조사 지속 동안 기본 공격을 하지 않는다
        [UnityTest]
        public IEnumerator 일제_조사_지속_동안_평타를_하지_않는다()
        {
            BattleDriver driver = null;
            DefenderUnitData beamUnit = null;
            yield return BootWithBeamUnit(requireBarrage: true, (d, u) => { driver = d; beamUnit = u; });
            float duration = BarrageSpec(beamUnit).duration;
            Assert.Greater(duration, 0f, "조사 지속이 0 이다 — 저작이 비었다");

            SimEntityId caster = SimEntityId.None;
            int fireTick = -1;
            var attackTicks = new List<int>();
            System.Action<CoreEvent> probe = e =>
            {
                if (e.A != caster) return;
                if (e.Kind == CoreEventKind.SkillVisual && fireTick < 0) fireTick = e.Tick;
                else if (e.Kind == CoreEventKind.AttackResolved) attackTicks.Add(e.Tick);
            };
            driver.Subscribe(ViewOrder.Trace, probe);

            int2 anchor = AnchorNearestPath(driver, 0);
            var near = NearestPathCells(driver.Match.Map, driver.Match.Map.CenterOf(anchor), 3);
            var enemies = new List<SimEntityId>();
            foreach (var c in near) enemies.Add(SpawnPinnedEnemy(driver, c));
            caster = PlaceAt(driver, 0, anchor);

            int budget = MotionTicks(driver, 0) + 240;
            for (int f = 0; f < budget && fireTick < 0; f++) { driver.Match.Tick(); yield return null; }
            Assert.GreaterOrEqual(fireTick, 0, "배치 일제 조사가 발동하지 않았다(조사 빔 사건 없음)");

            // 발동 직후 쿨다운이 조사 지속만큼 밀려 있다 = 그동안 기본 공격 없음. 여유는 흘린 틱만큼.
            var u = driver.Find(caster);
            Assert.IsNotNull(u?.Attack, "시전자에 공격 상태가 없다");
            float elapsed = (driver.Match.Clock.Tick - fireTick + 1) * BattleMatch.Dt;
            Assert.GreaterOrEqual(u.Attack.CooldownRemaining, duration - elapsed - 1e-4f,
                $"조사 중인데 쿨다운({u.Attack.CooldownRemaining})이 지속({duration})만큼 안 밀렸다");

            // 지속 창 안에 평타 사건이 하나도 없어야 한다.
            int windowTicks = Mathf.FloorToInt(duration / BattleMatch.Dt);
            while (driver.Match.Clock.Tick < fireTick + windowTicks + 2) { driver.Match.Tick(); yield return null; }
            yield return null;
            // 발동 틱 자체는 뺀다 — 같은 틱에 스킬보다 먼저 돈 평타는 「조사 중」이 아니다.
            foreach (int t in attackTicks)
                Assert.IsFalse(t > fireTick && t < fireTick + windowTicks - 1,
                    $"조사 지속 중(틱 {fireTick}~{fireTick + windowTicks})에 평타가 나갔다(틱 {t})");

            // 대조: 창이 끝나면 평타가 돌아온다 — 안 돌아오면 위 「0 건」은 사거리 밖이라 공허했을 수 있다.
            bool anyAlive = false;
            foreach (var id in enemies) anyAlive |= driver.IsAlive(id);
            // 조사로 전멸했으면 사거리 안에 새 과녁을 세운다 — 대조는 「창이 끝나면 평타가 난다」만 묻는다.
            if (!anyAlive) SpawnPinnedEnemy(driver, near[0]);
            int before = attackTicks.Count;
            for (int f = 0; f < 120 && attackTicks.Count == before; f++) { driver.Match.Tick(); yield return null; }
            driver.Unsubscribe(probe);
            Assert.Greater(attackTicks.Count, before, "조사가 끝났는데 평타가 돌아오지 않는다 — 억제 판정이 공허했다");
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        // ── 픽스처 ──────────────────────────────────────────────────────────

        private static IEnumerator BootWithBeamUnit(bool requireBarrage, System.Action<BattleDriver, DefenderUnitData> found)
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");

            var catalog = (DefenderCatalog)typeof(BattleDriver)
                .GetField("_defenderCatalog", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(driver);
            Assert.IsNotNull(catalog, "드라이버에 유닛 카탈로그가 없다");
            DefenderUnitData pick = null;
            foreach (var u in catalog.units)
            {
                if (u == null || u.beamVfxPrefab == null) continue;
                if (requireBarrage && BarrageSpecOrNull(u) == null) continue;
                pick = u;
                break;
            }
            Assert.IsNotNull(pick, requireBarrage
                ? "카탈로그에 배치 일제 조사(AreaDot)를 가진 빔 유닛이 없다"
                : "카탈로그에 빔 유닛(beamVfxPrefab)이 없다");

            RebeginWithRoster(driver, new[] { pick });
            driver.Apply(Command.FinishPlacement());
            driver.Pause(true);
            driver.Match.Cost.Gain((int)math.ceil(driver.Match.Cost.Max));
            yield return null;
            found(driver, pick);
        }

        // 부팅 편성은 기기 프로필을 상속한다 — 프로필을 잠시 끊고 테스트 모드 프리셋으로 다시 짓는다.
        private static void RebeginWithRoster(BattleDriver driver, DefenderUnitData[] roster)
        {
            var profile = typeof(BattleDriver).GetField("_profile", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(profile, "BattleDriver._profile — 이름이 바뀌면 이 픽스처를 같이 고친다");
            var saved = profile.GetValue(driver);
            profile.SetValue(driver, null);
            TestModeContext.Set(null, roster);
            try { driver.Begin(); }
            finally
            {
                profile.SetValue(driver, saved);
                TestModeContext.Clear();
            }
            Assert.IsTrue(driver.Running, "편성을 바꿔 다시 지은 판이 안 걸렸다");
            for (int i = 0; i < roster.Length; i++)
                Assert.AreEqual(roster[i].id, driver.Definition.Units[i].Id, $"유닛 표 {i} 줄이 편성과 다르다");
        }

        private static DcPayloadSpec? BarrageSpecOrNull(DefenderUnitData u)
        {
            var ab = u.GetAbility<UnitSkillAbility>();
            if (ab == null || ab.mechanics == null) return null;
            foreach (var m in ab.mechanics)
                if (m.payload.kind == EffectKind.AreaDot) return m.payload;
            return null;
        }

        private static DcPayloadSpec BarrageSpec(DefenderUnitData u)
        {
            var spec = BarrageSpecOrNull(u);
            Assert.IsTrue(spec.HasValue, "배치 일제 조사 저작이 없다");
            return spec.Value;
        }

        private static int MotionTicks(BattleDriver driver, int defIndex)
            => Mathf.CeilToInt(driver.Definition.Units[defIndex].DeployMotionSeconds / BattleMatch.Dt) + 2;

        // 배치 → 착지 신호 → 활성화까지 틱을 민다(드래그 입력이 내는 것과 같은 커맨드 둘).
        private static SimEntityId PlaceAndActivate(BattleDriver driver, int defIndex, out int2 anchor)
        {
            anchor = AnchorNearestPath(driver, defIndex);
            var id = PlaceAt(driver, defIndex, anchor);
            int budget = MotionTicks(driver, defIndex) + 10;
            for (int i = 0; i < budget && driver.Find(id) != null && driver.Find(id).Deploying; i++) driver.Match.Tick();
            Assert.IsFalse(driver.Find(id).Deploying, "배치 페이즈가 끝나지 않았다");
            return id;
        }

        private static SimEntityId PlaceAt(BattleDriver driver, int defIndex, int2 anchor)
        {
            var receipt = driver.Apply(Command.PlaceDefender(defIndex, anchor));
            Assert.IsTrue(receipt.Accepted, "판정이 통과한 자리인데 배치가 거절됐다: " + receipt.Reason);
            var id = LastDefender(driver);
            Assert.IsTrue(id.IsEntity, "배치했는데 방어유닛이 안 섰다");
            driver.Apply(Command.LandDefender(id));   // 배치 페이즈가 없는 유닛이면 거절된다 — 상관없다
            return id;
        }

        private static SimEntityId LastDefender(BattleDriver driver)
        {
            var id = SimEntityId.None;
            var units = driver.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Kind == UnitKind.Defender && (id.IsNone || units[i].Id.Value > id.Value)) id = units[i].Id;
            return id;
        }

        // 놓을 수 있는 앵커 중 길 칸에 가장 가까운 것. 「어디가 되나」는 코어에 묻는다.
        private static int2 AnchorNearestPath(BattleDriver driver, int defIndex)
        {
            var placement = driver.Match.Placement;
            var map = driver.Match.Map;
            var size = driver.GridSize;
            int2 best = default;
            float bestD = float.MaxValue;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var c = new int2(x, y);
                if (placement.Judge(defIndex, c) != RejectReason.None) continue;
                var nearest = NearestPathCells(map, map.CenterOf(c), 1);
                if (nearest.Count == 0) continue;
                float d = math.distance(map.CenterOf(nearest[0]), map.CenterOf(c));
                if (d < bestD) { bestD = d; best = c; }
            }
            Assert.Less(bestD, float.MaxValue, "빔 유닛을 놓을 칸이 판에 없다");
            return best;
        }

        private static List<int2> NearestPathCells(MapRuntime map, float3 from, int count)
        {
            var snap = map.Snapshot;
            var all = new List<(float d, int2 c)>();
            for (int y = 0; y < snap.Height; y++)
            for (int x = 0; x < snap.Width; x++)
            {
                var c = new int2(x, y);
                if (snap.IsGoalCell(c) || (snap.TravelLayersAt(c) & LayerBits.Path) == 0) continue;
                all.Add((math.distance(map.CenterOf(c), from), c));
            }
            all.Sort((a, b) => a.d != b.d ? a.d.CompareTo(b.d) : a.c.y != b.c.y ? a.c.y.CompareTo(b.c.y) : a.c.x.CompareTo(b.c.x));
            var result = new List<int2>();
            for (int i = 0; i < all.Count && i < count; i++) result.Add(all[i].c);
            return result;
        }

        // 적을 세우고 기절로 묶는다(코어의 그 문 — `RequestCc`). 묶는 길이는 판 시간 60초 — 이 테스트의 창보다 넉넉하다.
        private static SimEntityId SpawnPinnedEnemy(BattleDriver driver, int2 cell)
        {
            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemy(0, cell)).Accepted, "적 스폰 거절");
            var world = driver.Match.World;
            var id = SimEntityId.None;
            for (int i = 0; i < world.Units.Count; i++)
                if (world.Units[i].Kind == UnitKind.Enemy && (id.IsNone || world.Units[i].Id.Value > id.Value)) id = world.Units[i].Id;
            Assert.IsTrue(id.IsEntity, "적이 안 섰다");
            world.RequestCc(CcRequest.Of(id, CcRequestKind.Stun, 60f, SimEntityId.None));
            return id;
        }

        private static Transform ActiveBeamBody(CoreBeamPresenter presenter)
        {
            foreach (Transform c in presenter.transform)
                if (c.gameObject.activeSelf)
                {
                    var body = c.Find("BeamBody");
                    if (body != null) return body;
                }
            return null;
        }
    }
}
