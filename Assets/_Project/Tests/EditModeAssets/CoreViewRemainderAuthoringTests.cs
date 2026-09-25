using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCoreUnity.View;
using Wassup.Data;
using Wassup.Data.BattleView;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 8a2 — 뷰 이전 잔여의 **순수 산식과 옮긴 저작 값**.
    //
    // ① 적 체력 틴트(행 5) — 옛 `BattleBridge.EvaluateEnemyHealthTint`(`:4084`) + 호출부 `:3863` 의 두 갈래.
    // ② 옮긴 값이 옛 씬 블록 값(`BattleScene.unity` 브리지·맵 뷰 블록 — 줄 번호를 단언 메시지에 적었다)과 같은가. 밸런스 값이 아니라
    //    이식 증언이다 — 옛 씬이 unit 9 에서 사라지면 이 핀이 옛 값의 유일한 기록이 된다.
    [TestFixture]
    public class CoreViewRemainderAuthoringTests
    {
        private HealthDisplayStyle _style;

        [SetUp] public void SetUp() => _style = ScriptableObject.CreateInstance<HealthDisplayStyle>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_style);

        [Test]
        public void 틴트_레거시_모드는_체력비로_그라디언트를_평가한다()
        {
            var c = CoreEnemyHealthTint.Resolve(UnitHealthPresentationMode.Legacy, _style, 30f, 100f);
            Assert.AreEqual(_style.EvaluateTint(0.3f), c);
            Assert.AreEqual(_style.EvaluateTint(1f),
                CoreEnemyHealthTint.Resolve(UnitHealthPresentationMode.Legacy, _style, 150f, 100f), "넘치면 1 로 접는다");
            Assert.AreEqual(_style.EvaluateTint(0f),
                CoreEnemyHealthTint.Resolve(UnitHealthPresentationMode.Legacy, _style, 5f, 0f), "최대 0 이면 빈사(옛 ComputeRatio)");
        }

        [Test]
        public void 틴트_통합_머리위_모드와_스타일_없음은_흰색이다()
        {
            Assert.AreEqual(Color.white,
                CoreEnemyHealthTint.Resolve(UnitHealthPresentationMode.UnifiedOverhead, _style, 10f, 100f),
                "통합 머리 위 = 몸을 물들이지 않는다(옛 `unifiedOverhead ? white`)");
            Assert.AreEqual(Color.white, CoreEnemyHealthTint.Resolve(UnitHealthPresentationMode.Legacy, null, 10f, 100f));
        }

        [Test]
        public void 라이브_캐릭터_뷰_저작의_체력_표시와_드래그_흐림은_옛_씬_값이다()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<CharacterViewConfig>("Assets/_Project/Data/BattleView/CharacterViewConfig.asset");
            Assert.IsNotNull(cfg);
            Assert.IsNotNull(cfg.HealthDisplayStyle, "healthDisplayStyle 이 비면 레거시 모드에서 틴트가 죽는다");
            // 라이브는 통합 머리 위 — 옛 판도 적 몸 틴트가 흰색이었다(옛 규칙 그대로, 플레이 4차에서 「틴트가 안 보인다」가 정상).
            Assert.AreEqual(UnitHealthPresentationMode.UnifiedOverhead, cfg.HealthPresentationMode);
            Assert.AreEqual(0.3f, cfg.EnemyDragDimAlpha, 1e-6f, "옛 BattleScene.unity:4682");
            Assert.AreEqual(8f, cfg.EnemyDragDimFadeSpeed, 1e-6f, "옛 BattleScene.unity:4683");
        }

        [Test]
        public void 착지_예고_색과_붕괴_박자는_옛_씬_값이다()
        {
            var leap = AssetDatabase.LoadAssetAtPath<LeapVisualConfig>("Assets/_Project/Data/BattleView/LeapVisualConfig.asset");
            Assert.IsNotNull(leap);
            Assert.AreEqual(new Color(1f, 0.45f, 0.08f, 0.42f), leap.LandingTelegraphColor, "옛 BattleScene.unity:588");
            var heart = AssetDatabase.LoadAssetAtPath<HeartHudConfig>("Assets/_Project/Data/BattleView/HeartHudConfig.asset");
            Assert.IsNotNull(heart);
            Assert.AreEqual(1.25f, heart.CoreBurstHoldSec, 1e-6f, "옛 BattleScene.unity:4738");
            Assert.AreEqual(0.3f, heart.CoreBurstTimeScale, 1e-6f, "옛 BattleScene.unity:4739");
        }
    }
}
