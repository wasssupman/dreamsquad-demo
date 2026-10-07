using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Cards;
using Wassup.BattleCoreUnity.View;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 9 — 옛 `PlayMode/SpriteUnitBackendPlayTest` 의 규칙을 새 유닛 뷰 풀(`CoreUnitViewPool` →
    // `CoreSpriteUnitView`)로 옮긴 것. 검증 질문 그대로: 스프라이트 세트 저작 유닛이 Spine 유닛과 **같은 판**에서
    //   ① 스프라이트 뷰로 뜨고(세트 없는 유닛은 종전대로 Spine) · 스폰 직후 idle 루프
    //   ② 카드 픽킹에 잡히고(드림캐쳐 카드가 붙는 조건 — 새 층의 창구는 `CoreCardTargets.TryPickDefender`)
    //   ③ 공격 모션이 압축 재생 뒤 idle 로 돌아오고
    //   ④ 죽으면 풀 조회에서 사라진다 — **Unity fake-null 가드**: 파괴된 뷰가 생존 판정을 통과하면 안 된다
    //      (`CoreUnitViewPool.TryGet` 의 `view != null` — 풀 값 타입이 `UnityEngine.Object` 파생이라 성립한다).
    //
    // 라이브 에셋은 건드리지 않는다 — 카탈로그의 Spine 유닛 SO 를 복제해 세트를 꽂고, 세트는 런타임 합성(Texture2D + Sprite.Create)이다.
    // 부팅 편성은 기기 프로필을 상속하므로 그 둘만으로 판을 다시 짓는다(테스트 모드 프리셋 · 프로필 끊기).
    public sealed class RetiredSpriteBackendPortTest
    {
        private const string IdleName = "SynthFlipbook_idle";
        private const string AttackName = "SynthFlipbook_attack";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TimeManager.Instance.ResetAll();
            CoreSceneFixture.EndErrorWatch();
            yield return null;
        }

        // 옛 SpriteUnitBackendPlayTest::SpriteSet_SpawnsSpriteView_PicksAttacksAndDies_WhileSpineStaysSpine — 세트 유닛은 스프라이트 뷰로 뜨고·픽킹·공격 후 idle 복귀·사망 시 풀에서 사라지며 옆 Spine 유닛은 그대로
        [UnityTest]
        public IEnumerator 스프라이트_세트_유닛은_Spine_유닛과_같은_판에서_뜨고_잡히고_공격하고_사라진다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");
            var pool = Object.FindAnyObjectByType<CoreUnitViewPool>();
            Assert.IsNotNull(pool, "씬에 CoreUnitViewPool 이 없다");

            var catalog = (DefenderCatalog)typeof(BattleDriver)
                .GetField("_defenderCatalog", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(driver);
            Assert.IsNotNull(catalog, "드라이버에 유닛 카탈로그가 없다");
            // 스켈레톤 필드는 spine-unity 타입이다 — 이 어셈블리는 spine 을 참조하지 않으므로 `Object` 로만 읽는다.
            var skeletonField = typeof(DefenderUnitData).GetField("skeletonDataAsset", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(skeletonField, "DefenderUnitData.skeletonDataAsset — 이름이 바뀌면 이 테스트를 같이 고친다");
            DefenderUnitData spineData = null;
            foreach (var u in catalog.units)
                if (u != null && u.SpriteMotions == null && skeletonField.GetValue(u) as Object != null) { spineData = u; break; }
            Assert.IsNotNull(spineData, "카탈로그에 Spine 유닛(세트 없음 + 스켈레톤)이 없다 — 대조군이 없다");

            // 복제본에만 세트를 꽂는다 — 라이브 SO 무변경. id 는 겹치지 않게(유닛 표 줄이 둘이다).
            var spriteData = Object.Instantiate(spineData);
            spriteData.name = spineData.name + "_sprite";
            spriteData.id = spineData.id + "_sprite_port";
            spriteData.spriteMotions = BuildSyntheticSet();

            RebeginWithRoster(driver, new[] { spriteData, spineData });
            driver.Apply(Command.FinishPlacement());
            driver.Pause(true);
            yield return null;

            // 서로 멀리 — 한 렉트가 다른 유닛의 픽 지점을 덮지 않게.
            int2 spriteAnchor = FirstAnchor(driver, 0);
            SimEntityId spriteId = PlaceAndActivate(driver, 0, spriteAnchor);
            SimEntityId spineId = PlaceAndActivate(driver, 1, FarthestAnchor(driver, 1, spriteAnchor));
            for (int i = 0; i < 4; i++) yield return null;

            // 1) 백엔드 선택 — 같은 풀, 다른 concrete.
            Assert.IsTrue(pool.TryGet(spriteId, out var spriteView), "스프라이트 유닛 뷰가 풀에 없다");
            Assert.IsTrue(pool.TryGet(spineId, out var spineView), "Spine 유닛 뷰가 풀에 없다");
            Assert.IsInstanceOf<CoreSpriteUnitView>(spriteView, "세트가 있으면 스프라이트 뷰");
            Assert.IsInstanceOf<CoreSpineUnitView>(spineView, "세트가 없으면 종전대로 Spine 뷰");
            Assert.AreEqual(IdleName, spriteView.CurrentAnimationName, "스폰 직후 idle 루프");

            // 2) 픽킹 — 카드가 붙는 창구가 스프라이트 유닛을 잡는다.
            var cam = Camera.main;
            Assert.IsNotNull(cam, "main camera");
            var targets = new CoreCardTargets(driver, pool, () => Camera.main);
            Assert.IsTrue(spriteView.TryGetScreenRect(cam, out var rect), "SpriteRenderer 경계로 화면 렉트가 나와야 한다");
            Assert.IsTrue(targets.TryPickDefender(rect.center, out var picked), "렉트 중심에서 픽이 안 잡힌다");
            Assert.AreEqual(spriteId, picked, "픽킹이 스프라이트 유닛을 돌려준다");

            // 3) 공격 압축 재생 → idle 복귀. 재생기는 Battle 도메인 시계라 그 시계가 돌아야 진행한다.
            Assert.Greater(TimeManager.Instance.ScaleOf(TimeDomain.Battle), 0f,
                "Battle 도메인 시계가 멈춰 있다 — 재생기가 진행하지 않아 복귀를 잴 수 없다");
            spriteView.PlayAttack(attackAnimPeriod: 0.05f);   // 3프레임 30fps = 0.1초 / 주기 0.05초 → 배속 2
            Assert.AreEqual(AttackName, spriteView.CurrentAnimationName, "공격 원샷이 트랙을 잡는다");
            float deadline = Time.realtimeSinceStartup + 3f;
            while (spriteView != null && spriteView.CurrentAnimationName != IdleName && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(spriteView != null, "관찰 중 살아 있어야 판정이 유효하다");
            Assert.AreEqual(IdleName, spriteView.CurrentAnimationName, "원샷 완주 후 idle 로 복귀");

            // 4) 사망(death 미저작 = 즉시 파괴) → 풀 조회에서 사라진다. fake-null 회귀 가드.
            var pickPoint = rect.center;
            spriteView.Kill();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(pool.TryGet(spriteId, out _), "파괴된 뷰가 생존 판정을 통과했다(fake-null)");
            Assert.IsTrue(pool.TryGet(spineId, out _), "옆의 Spine 유닛은 그대로");
            Assert.IsFalse(targets.TryPickDefender(pickPoint, out var after) && after == spriteId,
                "파괴된 뷰의 렉트로 픽이 잡혔다 — 픽킹이 fake-null 뷰를 읽는다");

            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        // ── 픽스처 ──────────────────────────────────────────────────────────

        // demo-diet unit 0 — 편성을 **입력 값**으로 넘겨 다시 짓는다(옛 프로필 끊기·TestModeContext 반사 조작 제거).
        private static void RebeginWithRoster(BattleDriver driver, DefenderUnitData[] roster)
        {
            driver.Begin(ModeSelection.None, new MatchEntryInput { Kind = MatchEntryKind.TestMode, Defenders = roster });
            Assert.IsTrue(driver.Running, "편성을 바꿔 다시 지은 판이 안 걸렸다");
            for (int i = 0; i < roster.Length; i++)
                Assert.AreEqual(roster[i].id, driver.Definition.Units[i].Id, $"유닛 표 {i} 줄이 편성과 다르다");
        }

        private static SimEntityId PlaceAndActivate(BattleDriver driver, int defIndex, int2 anchor)
        {
            driver.Match.Cost.Gain((int)math.ceil(driver.Match.Cost.Max));
            var receipt = driver.Apply(Command.PlaceDefender(defIndex, anchor));
            Assert.IsTrue(receipt.Accepted, "판정이 통과한 자리인데 배치가 거절됐다: " + receipt.Reason);
            var id = SimEntityId.None;
            var units = driver.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Kind == UnitKind.Defender && units[i].DefIndex == defIndex) id = units[i].Id;
            Assert.IsTrue(id.IsEntity, "배치했는데 방어유닛이 안 섰다");
            driver.Apply(Command.LandDefender(id));   // 배치 페이즈가 없는 유닛이면 거절된다 — 상관없다
            int budget = Mathf.CeilToInt(driver.Definition.Units[defIndex].DeployMotionSeconds / BattleMatch.Dt) + 10;
            for (int i = 0; i < budget && driver.Find(id).Deploying; i++) driver.Match.Tick();
            Assert.IsFalse(driver.Find(id).Deploying, "배치 페이즈가 끝나지 않았다");
            return id;
        }

        // row-major 첫 합격. 「어디가 되나」는 코어에 묻는다.
        private static int2 FirstAnchor(BattleDriver driver, int defIndex)
        {
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var c = new int2(x, y);
                if (placement.Judge(defIndex, c) == RejectReason.None) return c;
            }
            Assert.Fail("놓을 수 있는 칸이 판에 없다");
            return default;
        }

        private static int2 FarthestAnchor(BattleDriver driver, int defIndex, int2 from)
        {
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            int2 best = default;
            int bestD = -1;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var c = new int2(x, y);
                if (placement.Judge(defIndex, c) != RejectReason.None) continue;
                int2 dd = c - from;
                int d = dd.x * dd.x + dd.y * dd.y;
                if (d > bestD) { bestD = d; best = c; }
            }
            Assert.GreaterOrEqual(bestD, 0, "둘째 유닛을 놓을 칸이 판에 없다");
            return best;
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
    }
}
