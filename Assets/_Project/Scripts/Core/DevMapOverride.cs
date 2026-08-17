using UnityEngine;

namespace Wassup.Core
{
    // map-play-feel unit 2 — 개발 확인용 맵 인덱스 강제.
    // 매판 시드로 배정되는 풀에서 특정 인덱스로 직접 진입하기 위한 런타임 override.
    // 모바일 개발 빌드에서 구동(에디터 전용 아님) — PlayerPrefs 로 앱 재시작에도 유지.
    // BattleBridge.BuildMapForBattle 우선순위: 이 override(≥0) > fixedMapSeed > 토너먼트 시드 > 폴백 0.
    // 노출 게이트는 UI 쪽(DevMapOverridePanel + DevOnlyGroup)이 담당한다 — 이 holder 자체는
    // 값만 보관하므로 릴리스에서도 존재하지만, 설정하는 UI 가 없어 항상 -1(off)로 남는다.
    public static class DevMapOverride
    {
        private const string Key = "dev_forceMapIndex";

        public static bool HasIndex => Index >= 0;

        // endless-mode-removal unit 0 — `Endless` 토글은 제거했다(모드 자체가 사라졌다).
        // 아래 맵 인덱스 강제와 dev 슬롯은 **별개 기능이라 그대로 남는다.**

        // -1 = off(기존 시드 로직). 0+ = 강제 풀 인덱스.
        public static int Index
        {
            get => PlayerPrefs.GetInt(Key, -1);
            set { PlayerPrefs.SetInt(Key, value); PlayerPrefs.Save(); }
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
