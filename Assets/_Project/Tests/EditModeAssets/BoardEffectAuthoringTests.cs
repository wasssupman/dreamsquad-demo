using System;
using NUnit.Framework;
using UnityEditor;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Data;
using Wassup.Data.Season;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 6b — 판 위에 깔리는 것의 **저작 → 정의표**.
    //
    // ① 저작 어휘(옛 enum) → 코어 어휘의 핀. 어셈블리가 갈려 컴파일러가 못 잡는다
    //    (`BuilderEnumPinTests` 와 같은 그물 — 이름 집합·값 · 매핑의 이름 보존).
    // ② 효과 타일 개수가 **시즌 맵 테마**에서 정의표로 실린다(드리프트 감사 — 새 코어에서
    //    라이브 3칸이 0 이 되던 결함). 값은 에셋에서 읽는다 — 리터럴로 박지 않는다.
    [TestFixture]
    public class BoardEffectAuthoringTests
    {
        private static void Pin<TAuthored, TCore>(Func<TAuthored, TCore> map)
            where TAuthored : Enum where TCore : Enum
        {
            CollectionAssert.AreEquivalent(Enum.GetNames(typeof(TCore)), Enum.GetNames(typeof(TAuthored)),
                $"{typeof(TAuthored).Name} ↔ {typeof(TCore).Name} — 한쪽에만 값이 늘었다");
            foreach (TAuthored a in Enum.GetValues(typeof(TAuthored)))
            {
                Assert.AreEqual(Convert.ToInt32(a), Convert.ToInt32(Enum.Parse(typeof(TCore), a.ToString())),
                                $"{typeof(TAuthored).Name}.{a} 의 번호가 갈렸다");
                Assert.AreEqual(a.ToString(), map(a).ToString(), $"{a} 가 다른 이름으로 옮겨진다");
            }
        }

        [Test]
        public void 장판_모양()
            => Pin<Wassup.Data.Authoring.HazardShape, HazardShapeKind>(BoardEffectDefinitionBuilder.ToCoreShape);

        [Test]
        public void 장판_효과_토큰()
            => Pin<Wassup.Data.Authoring.CcKind, HazardEffectKind>(BoardEffectDefinitionBuilder.ToCoreEffectKind);

        [Test]
        public void 지속_피해_원소()
            => Pin<Wassup.Data.Authoring.DotElement, Wassup.BattleCore.Effects.DotElement>(
                BoardEffectDefinitionBuilder.ToCoreDotElement);

        private static MapThemeData LiveTheme()
        {
            var guids = AssetDatabase.FindAssets("t:SeasonRegistry");
            Assert.IsNotEmpty(guids, "시즌 등록부가 없다");
            var reg = AssetDatabase.LoadAssetAtPath<SeasonRegistry>(AssetDatabase.GUIDToAssetPath(guids[0]));
            Assert.NotNull(reg?.activeSeason, "활성 시즌이 없다");
            Assert.NotNull(reg.activeSeason.mapTheme, "활성 시즌에 맵 테마가 없다");
            return reg.activeSeason.mapTheme;
        }

        [Test]
        public void 효과_타일_개수와_종류가_시즌_맵_테마에서_실린다()
        {
            var theme = LiveTheme();
            var def = new MatchDefinition();
            BoardEffectDefinitionBuilder.FillEffectTiles(def, theme, suppressed: false);

            Assert.AreEqual(theme.effectTileCount, def.EffectTileCount, "개수 = 테마 저작값");
            Assert.AreEqual(3, def.EffectTileCount, "라이브 테마는 3칸이다(옛 `forest.asset` · 감사 기준값)");
            Assert.AreEqual(theme.effectTiles.Length, def.EffectTiles.Length);
            for (int i = 0; i < def.EffectTiles.Length; i++)
                Assert.Greater(def.EffectTiles[i].EntryCount, 0, $"{def.EffectTiles[i].Id} 에 효과가 없다");

            // 해시가 이 둘을 감시한다 — 개수를 바꿨는데 해시가 그대로면 조용한 실패다.
            var other = new MatchDefinition();
            BoardEffectDefinitionBuilder.FillEffectTiles(other, theme, suppressed: true);
            Assert.AreEqual(0, other.EffectTileCount, "스테이지가 끄면 0");
            Assert.AreNotEqual(def.ComputeConfigHash(), other.ComputeConfigHash());
        }

        [Test]
        public void 라이브_장판_저작은_옛_게이트와_같이_적만_노린다()
        {
            // F34 — 축은 열렸고 오늘의 저작은 옛 하드 게이트(적만)와 같다 → 판이 안 바뀐다.
            var guids = AssetDatabase.FindAssets("t:HazardSO");
            Assert.IsNotEmpty(guids);
            var list = new System.Collections.Generic.List<HazardSO>();
            foreach (var g in guids) list.Add(AssetDatabase.LoadAssetAtPath<HazardSO>(AssetDatabase.GUIDToAssetPath(g)));
            var rows = BoardEffectDefinitionBuilder.ToHazardDefs(list.ToArray());
            foreach (var r in rows)
                foreach (var e in r.Effects)
                    Assert.AreEqual((int)Wassup.Skills.Faction.EnemyUnit, e.TargetFactions, r.Id);
        }

        [Test]
        public void 길막_폭발_저작은_탄_미배선이면_거절된다()
        {
            // F12 — 옛 전투는 경고만 내고 0번 탄 비주얼을 한 프레임 빌렸다.
            var so = UnityEngine.ScriptableObject.CreateInstance<Wassup.Data.Authoring.BlockingHazardSO>();
            try
            {
                so.maxHp = 50f;
                so.explodeDamage = 30f;
                so.explodeProjectile = null;
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                    new System.Text.RegularExpressions.Regex("폭발 탄이 미배선"));
                var row = BoardEffectDefinitionBuilder.ToBlockingHazardDef(so, Array.Empty<ProjectileDef>());
                Assert.AreEqual(0f, row.ExplodeDamage, "폭발 저작을 통째로 버린다");
                Assert.AreEqual(-1, row.ExplodeProjectileDefIndex, "0 이 아니라 -1 센티널");
            }
            finally { UnityEngine.Object.DestroyImmediate(so); }
        }
    }
}
