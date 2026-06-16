using Wassup.Data;

namespace Wassup.Core
{
    // wave-authoring-test-mode unit 3 — 아웃게임 "테스트 모드" 진입 carry-in.
    // GameManager 가 비영속(씬 전환 시 teardown)이라 씬 경계는 static 으로 넘긴다.
    // 아웃게임 버튼(unit 4)이 Set, GameManager.Start 가 읽고 즉시 Clear(1회 소비).
    public static class TestModeContext
    {
        public static bool Active { get; private set; }
        public static WavePlanAsset Plan { get; private set; }
        public static DefenderUnitData[] DefenderPreset { get; private set; }

        public static void Set(WavePlanAsset plan, DefenderUnitData[] defenderPreset)
        {
            Active = true;
            Plan = plan;
            DefenderPreset = defenderPreset;
        }

        public static void Clear()
        {
            Active = false;
            Plan = null;
            DefenderPreset = null;
        }
    }
}
