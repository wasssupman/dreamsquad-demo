using System.Collections;
using System.Reflection;
using NUnit.Framework;
using PrimeTween;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Tests.PlayMode
{
    // sprite-unit-backend unit 4 — 검증 질문의 e2e: 세트 저작 유닛이 Spine 유닛과 **같은 판**에서
    // 스프라이트 뷰로 뜨고 · 픽킹되고 · 공격 모션이 압축 재생 뒤 idle 로 돌아오고 · 죽으면 풀에서 사라진다.
    //
    // 라이브 에셋은 건드리지 않는다 — 카탈로그 유닛 SO 를 Instantiate 로 복제해 세트를 꽂고, 세트 자체는
    // 런타임 합성(Texture2D + Sprite.Create)이다. 시트 저작물(good_*)에 기대지 않아 아트가 바뀌어도 안 흔들린다.
    //
    // 마지막 단언(Kill 뒤 TryGetUnitView == false)은 critic C-1 의 회귀 가드다 — 풀 값 타입이 UnityEngine.Object
    // 파생이 아니면 파괴된 뷰가 생존 판정을 통과해 이 단언이 빨개진다.
    public class SpriteUnitBackendPlayTest
    {
        private const string IdleName = "SynthFlipbook_idle";
        private const string AttackName = "SynthFlipbook_attack";

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpriteSet_SpawnsSpriteView_PicksAttacksAndDies_WhileSpineStaysSpine()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LegacyBattleScene.Load();
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var catalog = FindCatalog();
            Assert.IsNotNull(bridge, "BattleBridge present");
            Assert.IsNotNull(gm, "GameManager present");
            Assert.IsNotNull(catalog, "DefenderCatalog present");

            var spineData = catalog.ById("healer");
            Assert.IsNotNull(spineData, "healer in catalog");
            Assert.IsNull(spineData.SpriteMotions, "테스트 전제: 라이브 healer 는 Spine 유닛이다");

            // 복제본에만 세트를 꽂는다 — 라이브 SO 무변경.
            var spriteData = Object.Instantiate(spineData);
            spriteData.name = spineData.name + "_sprite";
            spriteData.spriteMotions = BuildSyntheticSet();

            bridge.SetDefenderPool(new[] { spineData, spriteData });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, spriteData, out var spriteCell), "place sprite unit");
            Assert.IsTrue(PlaceFirstValid(bridge, spineData, out var spineCell), "place spine unit");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var spriteEntity = EntityAt(bridge, em, spriteCell);
            var spineEntity = EntityAt(bridge, em, spineCell);
            Assert.AreNotEqual(Entity.Null, spriteEntity);
            Assert.AreNotEqual(Entity.Null, spineEntity);
            for (int i = 0; i < 4; i++) yield return null;

            // 1) 백엔드 선택 — 같은 풀, 다른 concrete.
            Assert.IsTrue(bridge.TryGetUnitView(spriteEntity, out var spriteView), "sprite view in pool");
            Assert.IsTrue(bridge.TryGetUnitView(spineEntity, out var spineView), "spine view in pool");
            Assert.IsInstanceOf<SpriteUnitView>(spriteView, "세트가 있으면 스프라이트 뷰");
            Assert.IsInstanceOf<SpineUnitView>(spineView, "세트가 없으면 종전대로 Spine 뷰");
            Assert.AreEqual(IdleName, spriteView.CurrentAnimationName, "스폰 직후 idle 루프");

            // 2) 픽킹 seam — 브리지 변경 0 으로 스프라이트 유닛이 잡힌다(드림캐쳐 카드가 붙는 조건).
            var cam = Camera.main;
            Assert.IsNotNull(cam, "main camera");
            Assert.IsTrue(spriteView.TryGetScreenRect(cam, out var rect), "screen rect from SpriteRenderer bounds");
            Assert.IsTrue(bridge.TryPickDefenderAtScreen(cam, rect.center, out var picked, out _), "pick at rect center");
            Assert.AreEqual(spriteEntity, picked, "픽킹이 스프라이트 유닛을 돌려준다");

            // 3) 공격 압축 재생 → idle 복귀. 재생기는 Battle 도메인 클럭이라 판이 돌아야 진행한다.
            bridge.StartBattle();
            yield return null;
            spriteView.PlayAttack(attackAnimPeriod: 0.05f);   // duration 0.1s / period 0.05s → Speed 2
            Assert.AreEqual(AttackName, spriteView.CurrentAnimationName, "공격 원샷이 트랙을 잡는다");
            float deadline = Time.realtimeSinceStartup + 3f;
            while (spriteView != null && spriteView.CurrentAnimationName != IdleName && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(spriteView, "관찰 중 살아 있어야 판정이 유효하다");
            Assert.AreEqual(IdleName, spriteView.CurrentAnimationName, "원샷 완주 후 idle 로 복귀");

            // 4) 사망 → 풀에서 사라진다(death 미저작 = 즉시 파괴). Unity fake-null 회귀 가드.
            spriteView.Kill();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(bridge.TryGetUnitView(spriteEntity, out _), "파괴된 뷰는 생존 판정을 통과하면 안 된다");
            Assert.IsTrue(bridge.TryGetUnitView(spineEntity, out _), "옆의 Spine 유닛은 그대로");
        }

        // 런타임 합성 세트 — idle(2프레임 루프) + attack(3프레임 원샷, 30fps = 0.1s). 피벗은 발.
        private static UnitSpriteMotionSet BuildSyntheticSet()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var px = new Color32[64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            Sprite Frame() => Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0f), 8f);

            var idle = ScriptableObject.CreateInstance<SpriteFlipbookData>();
            idle.name = IdleName;
            SetFlipbook(idle, new[] { Frame(), Frame() }, 10f, loop: true);
            var attack = ScriptableObject.CreateInstance<SpriteFlipbookData>();
            attack.name = AttackName;
            SetFlipbook(attack, new[] { Frame(), Frame(), Frame() }, 30f, loop: false);

            var set = ScriptableObject.CreateInstance<UnitSpriteMotionSet>();
            SetPrivate(set, "idle", idle);
            SetPrivate(set, "attack", attack);
            Assert.IsTrue(set.HasIdle, "합성 세트가 유효하다");
            return set;
        }

        private static void SetFlipbook(SpriteFlipbookData data, Sprite[] frames, float fps, bool loop)
        {
            SetPrivate(data, "frames", frames);
            SetPrivate(data, "fps", fps);
            SetPrivate(data, "loop", loop);
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"직렬화 필드 '{field}' — 이름이 바뀌면 이 테스트를 같이 고친다");
            f.SetValue(target, value);
        }

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData data, out Vector2Int cell)
        {
            for (int x = -24; x < 48; x++)
            for (int y = -24; y < 48; y++)
            {
                if (!bridge.CanPlaceDefenderAt(x, y, data, out _)) continue;
                cell = new Vector2Int(x, y);
                return TestPlacement.PlaceActive(bridge, x, y, data);
            }
            cell = default;
            return false;
        }

        private static Entity EntityAt(BattleBridge bridge, EntityManager em, Vector2Int cell)
        {
            var field = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)field.GetValue(bridge);
            if (!dict.Contains(cell)) return Entity.Null;
            var tuple = dict[cell];
            var entity = (Entity)tuple.GetType().GetField("Item1").GetValue(tuple);
            return em.Exists(entity) ? entity : Entity.Null;
        }
    }
}
