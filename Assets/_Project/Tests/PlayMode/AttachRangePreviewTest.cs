using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // dreamcatcher-attach-range-preview unit 2 — 부착 프리뷰 채널의 판 흐름 통합.
    //
    // 손가락 없이 검증할 수 있는 것만 잰다: ① host 를 넘기면 링이 **host 의 sim 중심**에 카탈로그 반경으로
    // 뜬다(표준 상대 항 없음 — 계약 2) ② 비공간 spec 은 채널을 **건드리지 않는다**(계약 3) ③ Placement 가
    // 소유 중이면 **양보**한다(계약 4) ④ Clear 로 사라진다. 링은 `TilemapMapView` 가 grid 자식으로 만드는
    // "PlacementRangeRing" 스프라이트라 외부 관찰(머티리얼 `_Range`·위치)만으로 판정한다 — 프로덕션에 테스트
    // seam 을 넣지 않는다. 가독성·색·엄지 판독은 unit 4 실기기 몫.
    public class AttachRangePreviewTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SetAttachPreview_DrawsRadiusAtHostCenter_AndClears()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var unit = FindCatalog().ById("cannon");
            yield return BeginWith(bridge, new[] { unit });
            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place cannon");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell = CellOf(bridge, em, unit.id);
            Assert.IsTrue(bridge.TryGetDefenderAt(cell, out var host), "host entity");
            yield return null;

            var spec = new DcRangeSpec(DcRangeShape.Circle, 1.5f);   // cornered_burst 류: 1 + 칸 반폭
            var style = new RangeRingStyle(Color.cyan, 0.4f, 0.9f);
            bridge.SetAttachPreview(host, spec, style);
            yield return null; yield return null;

            var ring = GameObject.Find("PlacementRangeRing");
            Assert.IsNotNull(ring, "링 쿼드가 활성이어야 한다");
            var sr = ring.GetComponent<SpriteRenderer>();
            Assert.AreEqual(1.5f, sr.sharedMaterial.GetFloat("_Range"), 1e-3f,
                "반경 = 카탈로그 값 그대로(표준 상대 항 없음 — 계약 2)");
            Assert.AreEqual(0f, sr.sharedMaterial.GetVector("_HalfExtent").x, 1e-4f, "몸 = 원");

            // 중심 = host 의 sim 위치(기하 중심). 링은 타일 좌표로 그려지지만 월드에서는 ToView 와 같은 점이다
            // (z 는 접지 리프트 0.02 만 다르다). 2×2 라 셀 경계 교점에 온다 — 앵커 셀 중심이 아니다.
            var simPos = em.GetComponentData<LocalTransform>(host).Position;
            var expected = (Vector3)BoardSpace.ToView(simPos);
            Assert.AreEqual(expected.x, ring.transform.position.x, 0.05f, "링 중심 x = host 중심");
            Assert.AreEqual(expected.z, ring.transform.position.z, 0.05f, "링 중심 z = host 중심");

            bridge.ClearAttachPreview();
            yield return null;
            Assert.IsFalse(ring.activeSelf, "Clear 뒤 링이 사라진다");
        }

        [UnityTest]
        public IEnumerator NonSpatialSpec_AndPlacementOwner_LeaveTheChannelAlone()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var unit = FindCatalog().ById("cannon");
            yield return BeginWith(bridge, new[] { unit });
            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place cannon");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var cell = CellOf(bridge, em, unit.id);
            Assert.IsTrue(bridge.TryGetDefenderAt(cell, out var host));
            yield return null;

            // 배치 링이 채널을 소유한 상태를 만든다(사거리 + 내몸 + 표준 상대).
            bridge.SetPlacementRange(cell, unit);
            yield return null;
            var ring = GameObject.Find("PlacementRangeRing");
            Assert.IsNotNull(ring, "배치 링 활성");
            var sr = ring.GetComponent<SpriteRenderer>();
            float placementRadius = sr.sharedMaterial.GetFloat("_Range");
            Assert.Greater(placementRadius, 1.5f, "배치 링 반경은 프리뷰(1.5)와 구별되는 값이어야 시험이 의미 있다");

            var style = new RangeRingStyle(Color.cyan, 0.4f, 0.9f);
            // ② 비공간 — 채널 무접촉.
            bridge.SetAttachPreview(host, DcRangeSpec.None, style);
            yield return null;
            Assert.IsTrue(ring.activeSelf, "비공간 spec 은 링을 지우지 않는다");
            Assert.AreEqual(placementRadius, sr.sharedMaterial.GetFloat("_Range"), 1e-4f, "비공간 spec 은 반경을 건드리지 않는다");
            // ③ Placement 소유 중 — 양보.
            bridge.SetAttachPreview(host, new DcRangeSpec(DcRangeShape.Circle, 1.5f), style);
            yield return null;
            Assert.AreEqual(placementRadius, sr.sharedMaterial.GetFloat("_Range"), 1e-4f, "Placement 소유 중엔 프리뷰가 양보한다");
            // Clear(비소유자) 도 배치 링을 지우지 못한다.
            bridge.ClearAttachPreview();
            yield return null;
            Assert.IsTrue(ring.activeSelf, "비소유자의 Clear 는 배치 링을 지우지 않는다");

            // 배치가 반납하면 프리뷰가 채널을 얻는다.
            bridge.ClearPlacementRange();
            bridge.SetAttachPreview(host, new DcRangeSpec(DcRangeShape.Circle, 1.5f), style);
            yield return null;
            Assert.IsTrue(ring.activeSelf);
            Assert.AreEqual(1.5f, sr.sharedMaterial.GetFloat("_Range"), 1e-3f, "반납 뒤엔 프리뷰 반경");
            bridge.ClearAttachPreview();
        }

        // ── 픽스처(ActiveAllyZoneTest 관용구) ─────────────────────────────────────────
        private static IEnumerator BeginWith(BattleBridge bridge, DefenderUnitData[] pool)
        {
            bridge.SetDefenderPool(pool);
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;
        }

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            Assert.IsTrue(all.Length > 0, "DefenderCatalog");
            return all[0];
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private static Vector2Int CellOf(BattleBridge bridge, EntityManager em, string id)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var t = de.Value.GetType();
                var data = (DefenderUnitData)t.GetField("Item2").GetValue(de.Value);
                if (data != null && data.id == id) return (Vector2Int)de.Key;
            }
            Assert.Fail($"defender '{id}' not placed");
            return default;
        }
    }
}
