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
