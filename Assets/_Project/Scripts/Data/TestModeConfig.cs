using UnityEngine;

namespace Wassup.Data
{
    // wave-authoring-test-mode unit 3 — 테스트 모드 설정. 드래프트를 스킵할 때
    // 반입할 디펜더 프리셋과, 아웃게임 피커에 노출할 작성 웨이브 플랜 목록.
    [CreateAssetMenu(fileName = "TestModeConfig", menuName = "Wassup/TestModeConfig", order = 13)]
    public class TestModeConfig : ScriptableObject
    {
        [Tooltip("테스트 모드에서 드래프트를 스킵하고 반입할 디펜더 프리셋.")]
        public DefenderUnitData[] defenderPreset;

        [Tooltip("아웃게임 테스트 모드 피커에 노출할 작성 웨이브 플랜 목록.")]
        public WavePlanAsset[] planCatalog;
    }
}
