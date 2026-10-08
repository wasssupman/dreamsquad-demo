using Unity.Mathematics;
using UnityEngine;

namespace Somnia.Battle.Core
{
    // tilemap-view-backend unit 0 — sim(rect XZ 월드) ↔ view 변환의 유일한 지점.
    // MonoBehaviour 계층 전용. ECS/Burst 에서 호출 금지 (managed Transform 의존).
    //
    // 셀↔월드 정합의 권위는 **주입된 보드 평면 Transform + tileSize** 다: 셀 (0,0) 의 최소 모서리가 평면 원점,
    // 평면 로컬 X/Y 가 셀 축, 로컬 +Z 가 법선. 셀 정의는 `MapStageMath`(gridOriginLocal + tileSize)와 같은 식이고
    // 그 원점을 월드로 옮긴 것이 이 평면이다(`CoreBoardPlane.Declare`). 수식을 두 군데 두지 않는다 —
    // 옛 Tilemap 전투의 `GridLayout` 은 tilemap-untangle 단위 1(2026-10-07)에서 뗐다(Rectangle · 간격 0
    // 그리드에서 그것이 하던 일은 `셀 × cellSize` 하나였다). BoardSpaceAuthorityTests 가 이 계약을 못 박는다.
    // sim 좌표 규약은 GridMath 와 동일: 정수배 = 셀 중심(평면 로컬에서는 +0.5 칸 보정).
    public static class BoardSpace
    {
        private static float3 _simOrigin;
        private static float _tileSize = 1f;
        private static Transform _plane;

        // `CoreBoardPlane` 이 맵 빌드 시 1회 호출. 정적 상태 쓰기는 이 메서드가 유일하다.
        // 평면 없는 잘못된 구성은 받지 않는다 — 마지막 유효 구성을 유지하고 명시 에러.
        // (identity 폴백 모드는 legacy-render-removal unit 3 에서 제거. 사용 전 Configure 가 계약.)
        public static void Configure(float3 simOrigin, float tileSize, Transform boardPlane)
        {
            if (boardPlane == null)
            {
                Debug.LogError("[BoardSpace] 보드 평면 Transform 이 필요하다; Configure 를 무시한다.");
                return;
            }
            _simOrigin = simOrigin;
            _tileSize = tileSize > 0f ? tileSize : 1f;
            _plane = boardPlane;
        }

        public static float3 ToView(float3 simWorld)
        {
            // 평면 로컬 = (sim 오프셋 + 반 칸). sim 정수배가 셀 중심이므로 로컬에서는 +0.5 칸이 셀 중심이다.
            float lx = (simWorld.x - _simOrigin.x) + 0.5f * _tileSize;
            float ly = (simWorld.z - _simOrigin.z) + 0.5f * _tileSize;
            // 보드를 정면으로 보는 평면 뷰다. 위치는 보드 평면(sim XZ) → 셀 로만 정한다.
            // sim 높이(simWorld.y)를 화면 세로(view.y)에 더하지 않는다 — 더하면 객체마다 다른 접지
            // 높이(유닛 0.5 / 해저드 0.05 / 투사체 등)가 제각각 셀에서 어긋난다. 평면 뷰에서
            // "높이"는 화면 위치가 아니다(필요하면 그건 연출 레이어가 따로 다룰 문제).
            return _plane.TransformPoint(new Vector3(lx, ly, 0f));
        }

        // 입력(레이캐스트 히트) 경계용 역변환. 입력 평면 위의 점을 전제하므로
        // sim 높이는 simOrigin.y 로 둔다 — ToView 의 높이 가산과 대칭이 아니다.
        public static float3 ToSim(float3 viewWorld)
        {
            Vector3 local = _plane.InverseTransformPoint(viewWorld);
            return new float3(
                _simOrigin.x + local.x - 0.5f * _tileSize,
                _simOrigin.y,
                _simOrigin.z + local.y - 0.5f * _tileSize);
        }

        // 방향/회전 벡터용 — 변환의 선형부만 적용 (facing, 투사체 회전, cast 방향).
        public static float3 ToViewVector(float3 simDir)
        {
            return ToView(_simOrigin + simDir) - ToView(_simOrigin);
        }

        // tilted-billboard unit 7 — Configure 전(헤드리스·맵 미빌드 씬)인지. RaycastPlane 은
        // _plane 을 역참조하므로 Play 밖 하네스에서 부르기 전에 이걸로 묻는다.
        public static bool IsConfigured => _plane != null;

        // 포인터 입력 평면 = 보드 평면. 법선을 plane.forward(로컬 +Z)에서 유도해 평면 회전을
        // 자동 추종 — XY 정면뷰든 XZ 바닥이든 동일 코드(틸트 빌보드 전환 후 XZ).
        public static Plane RaycastPlane()
        {
            return new Plane(_plane.forward, _plane.position);
        }
    }
}
