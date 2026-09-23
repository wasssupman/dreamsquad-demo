using NUnit.Framework;
using UnityEditor;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 1 완료 기준 ⑥ — `configHash` 가 **조건**에만 반응한다.
    //
    // 이 테스트가 Assets lane 에 있는 이유: 실제 SO 를 읽어야 한다. 코어 lane 은
    // `Wassup.Runtime` 을 참조하지 않으므로 `DefenderUnitData` 를 이름조차 부를 수 없다.
    //
    // 이 해시가 답하는 질문은 하나다 — **「코드가 바뀐 건가, 값이 바뀐 건가」**.
    // 이 프로젝트에서 값은 조용히 바뀐다(로비 진입마다 시트 임포터가 SO 를 덮는다).
    // 그래서 아트 교체에 반응하면 판독 장치가 거짓말을 하고, 스탯 변경에 반응하지
    // 않으면 아무것도 증언하지 않는다. 아래 셋이 그 양쪽 벽이다.
    public class MatchDefinitionConfigHashTests
    {
        private static DefenderUnitData[] Defenders()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(
                "Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog, "DefenderCatalog 이 없다");
            return catalog.units;
        }

        private static AttackUnitData[] Enemies()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(
                "Assets/_Project/Data/EnemyCatalog.asset");
            Assert.IsNotNull(catalog, "EnemyCatalog 이 없다");
            return catalog.units;
        }

        [Test]
        public void 같은_SO_로_두_번_빌드하면_해시가_같다()
        {
            var a = MatchDefinitionBuilder.Build(Defenders(), Enemies(), 1234, ModeDef.Default());
            var b = MatchDefinitionBuilder.Build(Defenders(), Enemies(), 1234, ModeDef.Default());

            Assert.IsNotEmpty(a.ConfigHash);
            Assert.AreEqual(16, a.ConfigHash.Length, "SHA-256 앞 8바이트 = hex 16자");
            Assert.AreEqual(a.ConfigHash, b.ConfigHash);
            Assert.AreEqual(a.CanonicalText(), b.CanonicalText(), "canonical 텍스트도 같아야 한다");
        }

        [Test]
        public void 시드가_달라도_해시는_같다()
        {
            // 시드는 조건이 아니라 «그 조건으로 돌린 한 판»이다. 재현 축은 modeId + seed
            // 둘이고, 해시는 앞의 것만 답한다 — 그래야 「같은 값인데 판이 갈렸다」와
            // 「값이 바뀌었다」를 구분할 수 있다.
            var a = MatchDefinitionBuilder.Build(Defenders(), Enemies(), 1, ModeDef.Default());
            var b = MatchDefinitionBuilder.Build(Defenders(), Enemies(), 999, ModeDef.Default());

            Assert.AreEqual(a.ConfigHash, b.ConfigHash);
        }

        // ⚠ **실 카탈로그의 SO 를 고치지 않는다.** SO 는 프로세스 전역 상태라, 고쳤다가
        // 되돌리는 테스트는 중간에 예외가 나거나 시트 임포터가 끼어들면 그 값을 디스크로
        // 흘린다(이 프로젝트가 실제로 겪은 오염 경로다). 변형 축을 묻는 테스트는
        // **메모리 인스턴스 사본**으로 한다.
        private static DefenderUnitData CopyOf(DefenderUnitData src)
        {
            var copy = UnityEngine.ScriptableObject.CreateInstance<DefenderUnitData>();
            UnityEditor.EditorUtility.CopySerialized(src, copy);
            return copy;
        }

        [Test]
        public void 아트_참조를_바꿔도_해시는_같다()
        {
            var defenders = Defenders();
            Assert.Greater(defenders.Length, 0);

            var swapped = (DefenderUnitData[])defenders.Clone();
            var victim = CopyOf(defenders[0]);
            swapped[0] = victim;

            string before = MatchDefinitionBuilder
                .Build(defenders, Enemies(), 1234, ModeDef.Default()).ConfigHash;

            // 스킨 교체가 「조건이 바뀌었다」로 읽히면 판독 장치가 거짓말을 한다.
            // 정의표에는 아트 필드가 **아예 없어서** 원리적으로 샐 수 없다.
            victim.visualMesh = null;
            victim.visualMaterial = null;
            victim.deployVoiceClip = null;
            victim.attackVfxPrefab = null;

            string after = MatchDefinitionBuilder
                .Build(swapped, Enemies(), 1234, ModeDef.Default()).ConfigHash;
            Assert.AreEqual(before, after, "아트 참조가 해시에 샜다");

            UnityEngine.Object.DestroyImmediate(victim);
        }

        [Test]
        public void modeId_가_바뀌면_해시가_달라진다()
        {
            var mode = ModeDef.Default();
            string before = MatchDefinitionBuilder
                .Build(Defenders(), Enemies(), 1234, mode).ConfigHash;

            mode.ModeId = "wave_clear";
            string after = MatchDefinitionBuilder
                .Build(Defenders(), Enemies(), 1234, mode).ConfigHash;

            Assert.AreNotEqual(before, after, "모드가 조건의 일부다(제약 5)");
        }

        [Test]
        public void 스탯을_바꾸면_해시가_달라진다()
        {
            // 「스탯 하나를 바꿨는데 해시가 그대로」가 이 장치의 조용한 실패다.
            // 정의표에 필드를 추가하고 `Canonicalize` 를 안 고치면 정확히 그 상태가 된다.
            var defenders = Defenders();
            var bumped = (DefenderUnitData[])defenders.Clone();
            var victim = CopyOf(defenders[0]);
            bumped[0] = victim;

            string before = MatchDefinitionBuilder
                .Build(defenders, Enemies(), 1234, ModeDef.Default()).ConfigHash;

            victim.health += 7.5f;

            string after = MatchDefinitionBuilder
                .Build(bumped, Enemies(), 1234, ModeDef.Default()).ConfigHash;
            Assert.AreNotEqual(before, after);

            UnityEngine.Object.DestroyImmediate(victim);
        }
    }
}
