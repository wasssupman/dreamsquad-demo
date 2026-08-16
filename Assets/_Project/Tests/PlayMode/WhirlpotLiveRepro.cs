using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // elite-whirlpot — 회오리가 «베이크부터 화면까지» 살아 있는지 경계별로 고정한다.
    //
    // 이 파일이 지키는 불변식 4개:
    //   ① 적 SO 의 공격 축이 AttackState/AttackOutputElement 로 베이크된다
    //   ② 사거리 안 방어유닛이 실제로 깎인다 (걸어와서 멈춘 뒤에도)
    //   ③ 공격 1회당 회오리 VFX 가 뜨고, 그 폭이 판정 반경을 덮는다
    //   ④ 연타가 유지된다 — 저작 DPS 에 수렴한다
    //
    // ★④ 가 이 파일의 값어치다. 「한 대는 때린다」는 ②가 이미 보지만, 그것만으로는
    // 「돌고 있는데 아무 일도 안 일어난다」를 못 잡는다. 실제로 이 축이 밸런스 결함
    // (대상당 8.3 DPS = 웨이브 1 잡몹의 절반)을 드러냈다.
    public class WhirlpotLiveRepro
    {
        private const string WhirlpotPath = "Assets/_Project/Data/Enemies/Enemy_Whirlpot.asset";

        private EntityManager _em;
        private BattleBridge _bridge;
        private Entity _defender;
        private EntityQuery _defenderQuery;
        private EntityQuery _attackerQuery;
        private bool _queriesCreated;
        private float _tileSize = 1f;

        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_queriesCreated)
            {
                _defenderQuery.Dispose();
                _attackerQuery.Dispose();
                _queriesCreated = false;
            }
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        // ── ① 베이크 + ② 인접 피격 ──────────────────────────────────────────
        [UnityTest]
        public IEnumerator Whirlpot_AdjacentDefender_TakesDamage()
        {
            yield return SetupBattle("ranger");

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);
            var whirlpot = BattleBridgeTestAccess.SpawnEnemy(_bridge, _em, so);
            Assert.AreNotEqual(Entity.Null, whirlpot, "Whirlpot 스폰 실패");

            Assert.IsTrue(_em.HasComponent<AttackState>(whirlpot),
                "★AttackState 가 없다 = 무장 해제로 베이크됐다(wantsAttack false). 걷기만 한다.");
            var atk = _em.GetComponentData<AttackState>(whirlpot);
            Assert.AreEqual(so.attackTargetCount, atk.attackTargetCount, "attackTargetCount 가 저작값과 다르다");
            Assert.AreEqual(so.attackRange, atk.range, 0.001f, "range 가 저작값과 다르다");
            Assert.AreNotEqual(0, atk.targetMask, "targetMask 0 = 아무도 후보로 안 본다");
            Assert.IsTrue(_em.HasBuffer<AttackOutputElement>(whirlpot),
                "★outputs 버퍼가 없다 = 때려도 아무 효과가 없다");
            Assert.Greater(_em.GetBuffer<AttackOutputElement>(whirlpot).Length, 0, "outputs 비었다");
            Assert.IsTrue(_em.HasComponent<EnemyAiState>(whirlpot), "EnemyAiState 미부착");

            // 방어유닛 바로 위로 옮긴다 — 이동/경로를 변수에서 제거하고 «사거리 안» 만 만든다.
            TeleportTo(whirlpot, DefenderPos());

            float hpBefore = Hp(_defender);
            AiState seenState = AiState.Marching;
            bool sawEngaging = false;
            float hpAfter = hpBefore;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                if (!_em.Exists(whirlpot) || !_em.Exists(_defender)) break;
                seenState = _em.GetComponentData<EnemyAiState>(whirlpot).value;
                if (seenState == AiState.Engaging || seenState == AiState.Standoff) sawEngaging = true;
                hpAfter = Hp(_defender);
                if (hpAfter < hpBefore) break;
            }

            Assert.IsTrue(sawEngaging,
                $"★FSM 이 40프레임 동안 한 번도 Engaging/Standoff 에 못 갔다(마지막={seenState}). "
                + "그러면 AttackSystem 의 stateAllowsFire 가 false 라 영영 발사하지 않는다.");
            Assert.Less(hpAfter, hpBefore,
                $"★붙어 있는 방어유닛의 HP 가 40프레임 동안 그대로다({hpBefore}). FSM 상태={seenState}");
        }

        // ── ② 접근 구간 ── 걸어와서 멈춘 뒤에도 교전이 성사되는가.
        [UnityTest]
        public IEnumerator Whirlpot_WalksIn_ThenEngages()
        {
            yield return SetupBattle("ranger");

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);
            var whirlpot = BattleBridgeTestAccess.SpawnEnemy(_bridge, _em, so);
            Assert.AreNotEqual(Entity.Null, whirlpot, "Whirlpot 스폰 실패");

            // 방어유닛에서 5타일 떨어뜨린다 — 마지막 접근 구간만 재현한다.
            TeleportTo(whirlpot, DefenderPos() + new float3(5f * TileSize(), 0f, 0f));

            float hp0 = Hp(_defender);
            var trace = new System.Text.StringBuilder();
            float minDist = float.MaxValue;
            bool damaged = false;

            for (int i = 0; i < 400 && !damaged; i++)
            {
                yield return null;
                if (!_em.Exists(whirlpot) || !_em.Exists(_defender)) { trace.Append("[소멸]"); break; }

                float3 p = _em.GetComponentData<LocalTransform>(whirlpot).Position;
                float3 d = _em.GetComponentData<LocalTransform>(_defender).Position;
                float dist = math.max(math.abs(p.x - d.x), math.abs(p.z - d.z)) / TileSize();
                minDist = math.min(minDist, dist);
                damaged = Hp(_defender) < hp0;

                if (i % 40 == 0 || damaged)
                    trace.Append($"f{i} d={dist:F2} {_em.GetComponentData<EnemyAiState>(whirlpot).value} " +
                                 $"hp={Hp(_defender):F0} | ");
            }

            Assert.IsTrue(damaged,
                $"★걸어온 Whirlpot 이 400프레임 동안 방어유닛을 한 대도 못 때렸다. "
                + $"최소접근={minDist:F2}타일(사거리 {so.attackRange}) 궤적: {trace}");
        }

        // ── ③ 연출 ── Whirlpot 은 attack 애니가 빈 값이라 화면의 유일한 신호가 이 VFX 다.
        [UnityTest]
        public IEnumerator Whirlpot_Attack_SpawnsWhirlVfx()
        {
            yield return SetupBattle("ranger");

            var spawner = Object.FindObjectOfType<Wassup.Presentation.VfxSpawner>();
            Assert.IsTrue(spawner != null, "VfxSpawner 없음");

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);
            Assert.IsTrue(so.attackVfxPrefab != null, "회오리 프리팹 미배선");
            var whirlpot = BattleBridgeTestAccess.SpawnEnemy(_bridge, _em, so);
            TeleportTo(whirlpot, DefenderPos());

            int before = spawner.transform.childCount;
            float hp0 = Hp(_defender);
            bool damaged = false;
            int maxChildren = before;

            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (!_em.Exists(_defender)) break;
                maxChildren = Mathf.Max(maxChildren, spawner.transform.childCount);
                if (Hp(_defender) < hp0) damaged = true;
            }

            Assert.IsTrue(damaged, "선행 조건: 공격 자체가 성사돼야 한다");
            Assert.Greater(maxChildren, before,
                "★공격은 성사됐는데 VfxSpawner 밑에 회오리 인스턴스가 하나도 안 생겼다.");
        }

        // ── ③ 연출 크기 ── 판정(Chebyshev attackRange)을 연출이 덮는가.
        [UnityTest]
        public IEnumerator WhirlVfx_VisualSize_MatchesHitRadius()
        {
            yield return BattleBridgeTestAccess.LoadBattleScene();
            LogAssert.ignoreFailingMessages = true;
            _tileSize = ResolveTileSize(World.DefaultGameObjectInjectionWorld.EntityManager);

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);

            // VfxSpawner 와 같은 계산으로 인스턴스를 만든다(브리지가 넘기는 인자 그대로).
            float s = so.attackRange * so.attackVfxScalePerTile;
            var go = Object.Instantiate(so.attackVfxPrefab, Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one * s;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main; main.loop = true; main.playOnAwake = true;
                ps.Clear(true); ps.Play(true);
            }
            for (int i = 0; i < 45; i++) yield return null;   // 파티클이 차오를 시간

            var rends = go.GetComponentsInChildren<ParticleSystemRenderer>(true);
            Assert.Greater(rends.Length, 0, "파티클 렌더러 없음");
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float widthTiles = Mathf.Max(b.size.x, b.size.z) / TileSize();
            Object.Destroy(go);

            // 판정은 중심에서 ±attackRange 타일 = 폭 (2R+1). 연출이 그보다 작으면
            // 「회오리가 안 닿는 곳의 유닛이 깎인다」가 된다. 경계 타일은 절반만 걸치므로 2R 을 기준으로 본다.
            float needed = 2f * so.attackRange;
            Assert.GreaterOrEqual(widthTiles, needed,
                $"★회오리 연출 폭이 {widthTiles:F2}타일인데 판정은 반경 {so.attackRange}(폭 {2 * so.attackRange + 1})다. "
                + $"localScale={s} (attackRange {so.attackRange} × scalePerTile {so.attackVfxScalePerTile}).");
        }

        // ── ④ 지속 화력 ── 「한 대」가 아니라 「연타」를 잰다.
        [UnityTest]
        public IEnumerator Whirlpot_SustainsDps_NotJustOneHit()
        {
            var cat = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            // 측정 창 동안 죽지 않을 탱커. Unity 의 fake-null 때문에 `??` 를 쓰지 않는다.
            yield return SetupBattle(cat.ById("bastion") != null ? "bastion" : "guardian");

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);
            Assert.IsTrue(so.outputs != null && so.outputs.Length > 0, "outputs 미저작 — 기대치를 유도할 수 없다");
            var whirlpot = BattleBridgeTestAccess.SpawnEnemy(_bridge, _em, so);
            TeleportTo(whirlpot, DefenderPos());

            // ★웨이브 적이 같은 방어유닛을 때린다 — 초판 측정이 그것 때문에 23 DPS 로 부풀었다.
            PurgeOtherEnemies(whirlpot);

            // 첫 타를 기다린 뒤부터 잰다(스폰~교전 진입 지연을 측정에서 뺀다).
            float hpStart = Hp(_defender);
            bool firstHit = false;
            for (int i = 0; i < 120 && !firstHit; i++)
            {
                PurgeOtherEnemies(whirlpot);
                yield return null;
                firstHit = Hp(_defender) < hpStart;
            }
            Assert.IsTrue(firstHit, "★120프레임 안에 첫 타가 없다 — 이건 지속 화력 이전의 문제다");

            hpStart = Hp(_defender);
            float t0 = Time.time;
            while (Time.time - t0 < 6f)      // 3초 창은 «9회냐 10회냐» 위상 오차가 20% 로 보인다
            {
                PurgeOtherEnemies(whirlpot);
                yield return null;
            }
            float elapsed = Time.time - t0;

            AssertNoGimmickConfig("측정 창 종료 시점");
            // ★측정 도중 팽이가 죽으면 DPS 가 조용히 낮게 나온다(탱커가 반격한다).
            Assert.IsTrue(_em.Exists(whirlpot) && !_em.HasComponent<DeadTag>(whirlpot)
                          && Hp(whirlpot) > 0f,
                "측정 창 안에서 Whirlpot 이 죽었다 — 이 회차의 DPS 는 신뢰할 수 없다.");

            float dealt = hpStart - Hp(_defender);
            float dps = dealt / elapsed;
            // 기대치는 **SO 에서 유도한다** — 리터럴로 박으면 밸런스를 조정할 때마다 낡는다.
            float authoredDps = so.outputs[0].magnitude / so.attackCooldown;

            Debug.Log($"[WhirlpotRepro] 실측 {dps:F2} DPS / 저작 {authoredDps:F2} DPS · {elapsed:F2}초 {dealt:F0} 피해");
            Assert.Greater(dps, authoredDps * 0.5f,
                $"★{elapsed:F1}초 동안 {dealt:F0} 피해 = {dps:F1} DPS. 저작은 {authoredDps:F1} DPS "
                + $"(magnitude {so.outputs[0].magnitude} / cooldown {so.attackCooldown}) 다. "
                + "절반 미만이면 회오리가 연타되지 않고 간헐적으로만 성사되는 것 — "
                + "「돌고 있는데 아무 일도 안 일어난다」의 실체다.");
        }

        // ── 자기 피해 ── 「셀프 데미지를 입는 느낌」 보고(2026-08-16)의 라이브 판정.
        //
        // EditMode 는 합성 월드라 「라이브에도 없다」를 말해주지 못한다. 여기서는 실제 씬에서
        // **방어유닛의 AttackState 를 떼어 반격을 없앤 뒤** 팽이 HP 를 본다 — 회오리가 도는 동안
        // 팽이 HP 가 1 이라도 줄면 출처는 자기 공격이거나 필드다.
        [UnityTest]
        public IEnumerator Whirlpot_TakesNoDamage_WhenNothingCanHitBack()
        {
            yield return SetupBattle("ranger");

            // 반격 제거 — 여전히 합법 타겟이지만 쏘지는 못한다.
            _em.RemoveComponent<AttackState>(_defender);

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);
            var whirlpot = BattleBridgeTestAccess.SpawnEnemy(_bridge, _em, so);
            TeleportTo(whirlpot, DefenderPos());
            PurgeOtherEnemies(whirlpot);

            float potHp0 = Hp(whirlpot);
            float defHp0 = Hp(_defender);
            for (int i = 0; i < 180; i++)
            {
                PurgeOtherEnemies(whirlpot);
                yield return null;
                if (!_em.Exists(whirlpot) || !_em.Exists(_defender)) break;
            }

            Assert.Less(Hp(_defender), defHp0, "선행 조건: 회오리가 실제로 돌아야 한다");
            Assert.AreEqual(potHp0, Hp(whirlpot), 0.001f,
                $"★반격할 수 있는 것이 없는데 팽이 HP 가 {potHp0} → {Hp(whirlpot)} 로 줄었다 = 자기 피해.");
            Assert.AreEqual(0, _em.GetBuffer<IncomingDamage>(whirlpot).Length,
                "★팽이 자신의 IncomingDamage 에 항목이 남아 있다.");

            Debug.Log($"[WhirlpotRepro] 자기피해 검사: 팽이 {potHp0:F0}→{Hp(whirlpot):F0} · " +
                      $"방어유닛 {defHp0:F0}→{Hp(_defender):F0}");
        }

        // ── setup ───────────────────────────────────────────────────────────
        private IEnumerator SetupBattle(string defenderId)
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();

            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsTrue(_bridge != null, "BattleBridge 없음");

            _defenderQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<DefenderUnitTag>(), ComponentType.ReadOnly<Health>());
            _attackerQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<AttackUnitTag>(), ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadWrite<Health>());
            _queriesCreated = true;
            _tileSize = ResolveTileSize(_em);

            var cat = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var u = cat.ById(defenderId);
            // Unity 의 == 오버로드를 타야 한다. NUnit 의 IsNotNull 은 fake-null 을 통과시킨다.
            Assert.IsTrue(u != null, $"방어유닛 '{defenderId}' 를 카탈로그에서 찾지 못했다");

            _bridge.SetDefenderPool(new[] { u });
            _bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart(); gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(_bridge, u), $"'{defenderId}' 배치 실패");
            _defender = FirstDefender();
            Assert.AreNotEqual(Entity.Null, _defender, "방어유닛 엔티티를 찾지 못했다");

            _bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            // ★기믹을 걷어낸다 — 배정은 matchSeed 로 매 실행 달라지고(실측: 한 세션에서
            // G1·G2·G3·G4 가 전부 나왔다), 그 중 둘이 이 파일의 HP 델타 단언을 직접 오염시킨다:
            // 온천은 방어유닛을 max HP 의 10% 씩 **회복**시키고(배스티온이면 5초마다 207 —
            // 측정 창의 팽이 총딜보다 크다), 사직서는 메테오 40 을 얹는다.
            //
            // ⚠ `SetAssignedGimmick(null)` **만으로는 안 된다.** config 주입은
            // `CreateGimmickConfigIfActive()` 가 `BuildMapForBattle`(= BeginPlacement) 에서 이미
            // 끝냈고, Effects 시스템은 그 플래그가 아니라 **config 엔티티**를 읽는다. 엔티티를
            // 지워야 실제로 꺼진다. 플래그는 이후 맵 재빌드가 재주입하지 않게 같이 비운다.
            _bridge.SetAssignedGimmick(null);
            ClearGimmickConfigs();
            yield return null;
            AssertNoGimmickConfig("SetupBattle 직후");
        }

        // ── helpers ─────────────────────────────────────────────────────────
        private float Hp(Entity e) => _em.GetComponentData<Health>(e).value;
        private float3 DefenderPos() => _em.GetComponentData<LocalTransform>(_defender).Position;
        private void TeleportTo(Entity e, float3 pos) => _em.SetComponentData(e, LocalTransform.FromPosition(pos));

        // 씬마다 한 번만 푼다 — 프레임 루프 안에서 쿼리를 만들면 그대로 누수다.
        private float TileSize() => _tileSize;

        private static float ResolveTileSize(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Effects.FlowFieldSingleton>());
            float t = q.TryGetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>(out var ff) ? ff.tileSize : 1f;
            q.Dispose();
            return t;
        }

        // 기믹 config 엔티티 4종을 지운다(브리지의 private DestroyEntitiesByType 대응).
        // 이 엔티티들은 뷰/등록부를 갖지 않는 순수 sim config 라 직접 파괴가 안전하다.
        private void ClearGimmickConfigs()
        {
            DestroyAllWith<Wassup.Battle.Effects.BurnoutGimmickConfig>();
            DestroyAllWith<Wassup.Battle.Effects.RedBullGimmickConfig>();
            DestroyAllWith<Wassup.Battle.Effects.ClockOutGimmickConfig>();
            DestroyAllWith<Wassup.Battle.Effects.OnsenGimmickConfig>();
        }

        private void DestroyAllWith<T>() where T : unmanaged, IComponentData
        {
            var q = _em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            _em.DestroyEntity(q);
            q.Dispose();
        }

        // 측정 «끝» 에도 확인한다 — 지운 직후만 보면 도중 재주입(맵 재빌드)을 놓친다.
        // 초판은 SetAssignedGimmick(null) 만 부르고 검증을 안 해서 그 호출이 no-op 인 걸 몰랐다.
        private void AssertNoGimmickConfig(string when)
        {
            AssertEmpty<Wassup.Battle.Effects.BurnoutGimmickConfig>(when);
            AssertEmpty<Wassup.Battle.Effects.RedBullGimmickConfig>(when);
            AssertEmpty<Wassup.Battle.Effects.ClockOutGimmickConfig>(when);
            AssertEmpty<Wassup.Battle.Effects.OnsenGimmickConfig>(when);
        }

        private void AssertEmpty<T>(string when) where T : unmanaged, IComponentData
        {
            var q = _em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            bool empty = q.IsEmpty;
            q.Dispose();
            Assert.IsTrue(empty,
                $"{when}: {typeof(T).Name} 이 살아 있다 — 기믹이 HP 델타를 흔들어 이 측정은 신뢰할 수 없다.");
        }

        // 팽이 외의 «적» 을 정상 사망 경로로 치운다(HP 0 → DeathSystem). DestroyEntity 를
        // 직접 부르면 브리지의 뷰/등록부 정리를 건너뛴다.
        private void PurgeOtherEnemies(Entity keep)
        {
            var ents = _attackerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                if (ents[i] == keep) continue;
                if (_em.GetComponentData<FactionTag>(ents[i]).value != Faction.EnemyUnit) continue;
                var h = _em.GetComponentData<Health>(ents[i]);
                if (h.value <= 0f) continue;
                h.value = 0f;
                _em.SetComponentData(ents[i], h);
            }
            ents.Dispose();
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private Entity FirstDefender()
        {
            var arr = _defenderQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var r = arr.Length > 0 ? arr[0] : Entity.Null;
            arr.Dispose();
            return r;
        }
    }
}
