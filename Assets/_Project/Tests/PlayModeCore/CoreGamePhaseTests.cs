using NUnit.Framework;
using Wassup.Core;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 5a — 규칙 장부 X16.
    //
    // `GamePhase` 는 **카메라 설정 에셋에 정수로 직렬화**돼 있다(`CameraDirectionConfig.breathPhases`).
    // 값을 빼거나 사이에 끼우면 저장된 정수의 의미가 통째로 밀려, 브리딩이 엉뚱한 페이즈에서
    // 켜진다. 그 사고는 에셋을 열어 보기 전에는 안 보인다 — 컴파일도 테스트도 다 통과한다.
    //
    // 그래서 **append-only 임을 여기서 못 박는다.** 새 페이즈는 맨 뒤에 붙인다.
    public sealed class CoreGamePhaseTests
    {
        [Test]
        public void GamePhase_IntegerValues_AreAppendOnly()
        {
            Assert.AreEqual(0, (int)GamePhase.None);
            Assert.AreEqual(1, (int)GamePhase.Draft);
            Assert.AreEqual(2, (int)GamePhase.Placement);
            Assert.AreEqual(3, (int)GamePhase.Battle);
            Assert.AreEqual(4, (int)GamePhase.Result);
            Assert.AreEqual(5, (int)GamePhase.Tally);
            Assert.AreEqual(6, (int)GamePhase.Gimmick);
        }

#if UNITY_EDITOR
        // 그리고 **에셋이 든 정수가 여전히 페이즈를 가리키는지** 한 줄로 대조한다. 위 단언은
        // enum 이 안 밀렸다고만 말하고, 에셋 쪽이 범위 밖 정수로 굳었을 가능성은 남는다.
        //
        // ⚠ 오늘 이 배열은 **비어 있다**(camera-direction unit 16 — 브리딩을 끄는 방법이
        // 「배열을 비운다」이다). 그래서 지금은 무증언에 가깝지만, 누가 브리딩을 다시 켜는
        // 날 이 줄이 그 정수를 검사한다. 비었다는 사실 자체를 단언하지 않는 이유는 그것이
        // 규칙이 아니라 **오늘의 저작 선택**이기 때문이다.
        [Test]
        public void CameraDirectionConfig_BreathPhases_AreAllDefinedGamePhases()
        {
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<Wassup.Data.CameraDirectionConfig>(
                "Assets/_Project/Data/Camera/CameraDirectionConfig.asset");
            Assert.IsNotNull(cfg, "CameraDirectionConfig.asset 을 찾을 수 없다");
            Assert.IsNotNull(cfg.breathPhases);
            for (int i = 0; i < cfg.breathPhases.Length; i++)
                Assert.IsTrue(System.Enum.IsDefined(typeof(GamePhase), cfg.breathPhases[i]),
                    $"breathPhases[{i}] = {(int)cfg.breathPhases[i]} 가 GamePhase 범위 밖이다 — "
                    + "enum 에서 값을 빼거나 끼웠는지 확인");
        }
#endif
    }
}
