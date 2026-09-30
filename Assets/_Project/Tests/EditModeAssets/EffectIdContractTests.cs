using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // skill-data-table 감사 — README 계약 10 「id 는 표 안에서 유일」 · `tables.md` §10 「새 effect_id = 소문자 스네이크 `^[a-z][a-z0-9_]*$`」.
    // ① 라이브 효과 에셋 전부가 규칙을 지킨다(서버 어휘 — 계약 6) ② 굽기는 같은 id 가 다른 해석 값으로 두 번 오면 크게 짖는다(값이 같으면 한 줄로 합친다).
    public class EffectIdContractTests
    {
        private static readonly Regex IdRule = new Regex("^[a-z][a-z0-9_]*$");
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        [Test]
        public void LiveEffectAssets_HaveUniqueSnakeCaseIds()
        {
            var seen = new Dictionary<string, string>();
            var bad = new List<string>();
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EffectData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var e = AssetDatabase.LoadAssetAtPath<EffectData>(path);
                if (e == null) continue;
                count++;
                if (string.IsNullOrEmpty(e.id) || !IdRule.IsMatch(e.id)) bad.Add($"{path}: id '{e.id}' 가 규칙 밖(^[a-z][a-z0-9_]*$)");
                else if (seen.TryGetValue(e.id, out var other)) bad.Add($"{path}: id '{e.id}' 가 {other} 와 겹친다");
                else seen[e.id] = path;
            }
            Assert.Greater(count, 0, "효과 에셋이 없다");
            CollectionAssert.IsEmpty(bad, "효과 id 계약(유일 · 소문자 스네이크) 위반");
        }

        private EffectData Effect(string id, float percent)
        {
            var e = ScriptableObject.CreateInstance<EffectData>();
            e.id = id;
            e.values = new EffectValues { kind = EffectKind.FactionStatBuff, buffStat = CardBuffKind.AttackDamage, percent = percent, allyFilter = CardTargetAxis.All };
            _made.Add(e);
            return e;
        }

        private DreamcatcherCard Squad(string id, EffectData e)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = id;
            c.type = CardType.Squad;
            c.hostKinds = HostKinds.Defender;
            c.bindings = new[] { new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.None }, effect = e } };
            _made.Add(c);
            return c;
        }

        private MatchDefinition Bake(params DreamcatcherCard[] cards)
        {
            var awakening = ScriptableObject.CreateInstance<AwakeningConfig>();
            _made.Add(awakening);
            var def = new MatchDefinition();
            CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = new List<DreamcatcherCard>(cards), Awakening = awakening },
                                       new List<ProjectileData>(), new List<ProjectilePatternData>(), System.Array.Empty<HazardSO>());
            return def;
        }

        [Test]
        public void Bake_SameIdDifferentValues_ErrorsLoudly()
        {
            LogAssert.Expect(LogType.Error, new Regex("효과 id 'dup' 가 다른 해석 값으로 두 번 구워졌다"));
            var def = Bake(Squad("a", Effect("dup", 10f)), Squad("b", Effect("dup", 20f)));
            Assert.AreEqual(2, def.Effects.Length, "줄은 그대로 싣는다(굽기 결과 무변 — 저작을 고칠 일)");
        }

        [Test]
        public void Bake_SameIdSameValues_MergesSilently()
        {
            var shared = Effect("shared", 10f);
            var def = Bake(Squad("a", shared), Squad("b", shared));
            Assert.AreEqual(1, def.Effects.Length, "같은 id · 같은 값 = 한 효과 줄(복사 0)");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
