using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine.SceneManagement;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // elite-enemy-tier — PlayMode e2e 가 BattleBridge 의 private 내부에 닿는 **한 곳**.
    //
    // 왜 모으나: 브리지에는 「적 1기를 지금 스폰」하는 public 경로가 없다(스폰은 웨이브 스케줄러가
    // 소유한다). 그래서 e2e 는 리플렉션으로 `SpawnUnit(PendingSpawnEntry)` 을 직접 부르고,
    // 그 레시피가 **7개 테스트 파일에 복제**돼 있었다.
    //
    // ★복제가 위험한 이유는 실제로 터졌다: 2026-08-11 에 `PendingSpawnEntry.deckIndex` 가
    // `laneIndex` 로 개명됐고, `KindlerFireStackE2ETest` 는 그 뒤로 **NRE 로 죽은 채 아무도
    // 몰랐다**. `GetField("deckIndex")` 가 null 을 반환하고 다음 줄의 `.SetValue` 가 터지는
    // 모양이라, 실패 메시지가 「무엇이 개명됐다」를 말해주지 않는다.
    //
    // 그래서 이 파일은 **리플렉션으로 집는 모든 멤버에 이름을 붙여 단언한다.** 개명이 나면
    // NRE 대신 「PendingSpawnEntry.laneIndex 를 찾지 못했다(이름 변경?)」가 뜬다. 복제본을
    // 줄이는 것보다 이쪽이 본질이다 — 자리가 하나면 고칠 곳도 하나다.
    //
    // 현재 사용: SlimeSplitE2ETest, DragonBreathE2ETest. 나머지 5개(Boss*·*Shield*·Kindler)는
    // 각자 사본을 갖고 있다 — 이관은 그 spec 들의 범위라 여기서 건드리지 않는다.
    internal static class BattleBridgeTestAccess
    {
        private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;

        // BattleScene 로드 + 브리지 Awake/Start 와 ECS 월드 준비까지 프레임을 흘린다.
        public static IEnumerator LoadBattleScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        // duel-live-focus — **맵은 이름으로 고른다.** 스테퍼 슬롯 번호(= 라이브 풀 크기 + dev
        // 순번)를 상수로 박으면 풀을 한 장이라도 옮길 때마다 테스트가 **조용히 다른 판을 잰다**
        // (빨간불이 아니라 «측정이 거짓말»이 되는 실패라 더 나쁘다). 실제로 라이브 풀이
        // 6장 → 1장(Duel)이 되면서 열 개의 상수가 한꺼번에 밀렸다.
        //
        // ⚠ 씬 로드 **전**에 불러야 한다 — 맵은 GameManager 부팅 흐름(PrepareDraftMap)에서
        // 서므로, 로드 뒤에 인덱스를 바꾸면 이미 다른 맵이 서 있다.
        public static int MapSlot(string mapName)
        {
            // map-diorama-stage US-004b — 문서 풀 → 스테이지 풀 포트. 이름 규약만
            // MapDocument_ → MapStage_ 로 바뀌고 «이름으로 고른다» 규율은 그대로다.
            const string poolPath = "Assets/_Project/Data/Maps/MapStagePool.asset";
            var pool = UnityEditor.AssetDatabase.LoadAssetAtPath<Wassup.Data.MapStagePool>(poolPath);
            Assert.IsNotNull(pool, $"맵 풀을 로드하지 못했다: {poolPath}");

            string assetName = "MapStage_" + mapName;
            // 슬롯 배치는 BattleBridge.BuildMapForBattle 과 같다: [0..Count-1] = 라이브 풀,
            // [Count..Count+DevCount-1] = dev 슬롯.
            for (int i = 0; i < pool.Count; i++)
                if (pool.Get(i).stage != null && pool.Get(i).stage.name == assetName) return i;
            for (int i = 0; i < pool.DevCount; i++)
                if (pool.GetDev(i).stage != null && pool.GetDev(i).stage.name == assetName)
                    return pool.Count + i;

            Assert.Fail($"'{assetName}' 이 스테이지 풀(라이브 {pool.Count} + dev {pool.DevCount})에 없다");
            return -1;
        }

        // 씬을 띄워 **전투를 계측하는 테스트는 자기 판을 선언한다.** 선언하지 않으면 그때그때
        // 라이브 풀 0번을 물려받고, 풀이 바뀌는 날 «계속 다른 판에서 재고 있었다» 가 된다 —
        // 2026-08-17 에 라이브 맵이 Serpent → Duel 로 바뀌며 4개 테스트가 정확히 그렇게 깨졌다
        // (Duel 은 본능 포탑 4기가 서 있어서 «반격할 게 없다» 같은 전제가 통째로 거짓이 된다).
        //
        // 기본값은 이 테스트들이 실제로 쓰여진 판이다. 판 자체가 논점인 테스트만 다른 이름을 준다.
        // map-diorama-stage unit 12 — Serpent 은퇴. 후계는 **Street**(거점 없는 열린 판): 기본판 테스트의
        // 전제(«반격할 거점이 없다», 유닛 간 사거리 계측)를 보존한다. Duel 은 본능 4기가 서 있어
        // Whirlpot_TakesNoDamage_WhenNothingCanHitBack 류의 전제가 거짓이 된다.
        public const string DefaultMap = "Street";

        public static int PinMap(string documentName = DefaultMap)
        {
            int saved = Wassup.Core.DevMapOverride.Index;
            Wassup.Core.DevMapOverride.Index = MapSlot(documentName);
            return saved;   // PlayerPrefs 는 머신 상태 — 반드시 원복한다
        }

        public static void RestoreMap(int saved) => Wassup.Core.DevMapOverride.Index = saved;

        // ⚠ AssetDatabase 직독이다. 라이브 덱 풀에 없는 적(엘리트 검증 대상)은 씬 로드로
        // 메모리에 올라오지 않아 Resources.FindObjectsOfTypeAll 로는 찾을 수 없다.
        public static AttackUnitData LoadEnemy(string path)
        {
            var u = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackUnitData>(path);
            Assert.IsNotNull(u, $"적 에셋을 로드하지 못했다: {path}");
            return u;
        }

        // 적 1기를 지금 스폰하고 새로 생긴 엔티티를 돌려준다.
        // 스폰 전후 스냅샷의 차집합으로 집는다 — SpawnUnit 은 void 라 엔티티를 반환하지 않는다.
        public static Entity SpawnEnemy(BattleBridge bridge, EntityManager em, AttackUnitData unit)
        {
            var bt = typeof(BattleBridge);

            var pendingType = bt.GetNestedType("PendingSpawnEntry", BindingFlags.NonPublic);
            Assert.IsNotNull(pendingType,
                "BattleBridge.PendingSpawnEntry 를 찾지 못했다(이름 변경?)");
            var pending = System.Activator.CreateInstance(pendingType);

            var entryField = pendingType.GetField("entry");
            Assert.IsNotNull(entryField, "PendingSpawnEntry.entry 를 찾지 못했다(이름 변경?)");
            entryField.SetValue(pending,
                new SpawnEntry { triggerTimeSec = 0f, unitType = unit, spawnIndex = 0 });

            // 이 필드가 2026-08-11 에 deckIndex → laneIndex 로 개명된 그 자리다.
            var laneField = pendingType.GetField("laneIndex");
            Assert.IsNotNull(laneField, "PendingSpawnEntry.laneIndex 를 찾지 못했다(이름 변경?)");
            laneField.SetValue(pending, 0);

            var spawn = bt.GetMethod("SpawnUnit", Instance);
            Assert.IsNotNull(spawn, "BattleBridge.SpawnUnit 을 찾지 못했다(이름 변경?)");

            var known = SnapshotAttackers(em);
            spawn.Invoke(bridge, new[] { pending });

            foreach (var e in SnapshotAttackers(em))
                if (!known.Contains(e)) return e;
            return Entity.Null;
        }

        public static HashSet<Entity> SnapshotAttackers(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var set = new HashSet<Entity>();
            for (int i = 0; i < arr.Length; i++) set.Add(arr[i]);
            arr.Dispose();
            return set;
        }

        // 어느 엔티티가 어느 SO 에서 나왔는지는 브리지의 _enemyTypeByEntity 만 안다.
        public static List<Entity> FindEnemiesOfType(
            BattleBridge bridge, EntityManager em, AttackUnitData so)
        {
            var dict = (Dictionary<Entity, AttackUnitData>)Field(bridge, "_enemyTypeByEntity");
            var result = new List<Entity>();
            foreach (var kv in dict)
                if (kv.Value == so && em.Exists(kv.Key) && !em.HasComponent<DeadTag>(kv.Key))
                    result.Add(kv.Key);
            return result;
        }

        public static object Field(BattleBridge bridge, string name)
        {
            var f = typeof(BattleBridge).GetField(name, Instance);
            Assert.IsNotNull(f, $"BattleBridge.{name} 을 찾지 못했다(이름 변경?)");
            return f.GetValue(bridge);
        }

        public static void SetField(BattleBridge bridge, string name, object value)
        {
            var f = typeof(BattleBridge).GetField(name, Instance);
            Assert.IsNotNull(f, $"BattleBridge.{name} 을 찾지 못했다(이름 변경?)");
            f.SetValue(bridge, value);
        }
    }
}
