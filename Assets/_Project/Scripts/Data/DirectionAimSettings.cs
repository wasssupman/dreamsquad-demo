using UnityEngine;

namespace Wassup.Data
{
    // defender-directional-volley unit 6 — 공격방향 페이즈 튜닝값. 컨트롤러가 런타임
    // AddComponent 라 인스펙터 튜닝이 안 되므로 SO 로 분리(DragSwaySettings 선례).
    // DefenderSelector 에 할당하면 Configure 로 주입되고, 미주입 시 이 기본값.
    [CreateAssetMenu(fileName = "DirectionAimSettings", menuName = "Wassup/DirectionAimSettings", order = 21)]
    public class DirectionAimSettings : ScriptableObject
    {
        // unit 9 — 방향은 셀 탭으로 고른다(픽셀 데드존 없음): 화살표가 어포던스, 판정은
        // 레인 전체. 판정 규칙은 LaneMath(시뮬의 발사 게이트)가 소유하므로 튜닝값이 없다.

        [Header("Time / Camera")]
        [Tooltip("방향 지정 중 전투 시간 배율. 드래그 슬로우모를 이어받는 값 — 0 아님(전투가 멈추면 안 된다).")]
        [Range(0.01f, 1f)] public float slowmoScale = 0.2f;

        // unit 9 — 가이드는 화면이 아니라 보드 레인 타일이다. 색/펄스는 TileSetData 의
        // rangeColor·rangePulse* 가 소유하고(배치·스킬 범위와 같은 언어), 세기 차이만
        // BattleBridge 의 레인 페인터가 배율로 낸다. 여기 글리프 파라미터는 없다.
    }
}
