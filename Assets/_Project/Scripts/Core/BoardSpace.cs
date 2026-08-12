using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Core
{
    // tilemap-view-backend unit 0 — sim(rect XZ 월드) ↔ view 변환의 유일한 지점.
    // MonoBehaviour 계층 전용. ECS/Burst 에서 호출 금지 (managed GridLayout 의존).
    // 셀↔월드 정합의 권위는 주입된 GridLayout 이다 — 셀 크기/회전/오프셋 수식을 여기 하드코딩하지
    // 않는다(그리드 구성이 바뀌면 조용히 어긋난다). BoardSpaceTests 가 이 위임을 못 박는다.
    // sim 좌표 규약은 GridMath 와 동일: 정수배 = 셀 중심 (Tilemap 의 셀 중심은 +0.5 보정).
    public static class BoardSpace
    {
        private static float3 _simOrigin;
        private static float _tileSize = 1f;
        private static GridLayout _grid;

        // BattleBridge 맵 빌드 시 1회 호출. 정적 상태 쓰기는 이 메서드가 유일하다.
        // grid 없는 잘못된 구성은 받지 않는다 — 마지막 유효 구성을 유지하고 명시 에러.
        // (identity 폴백 모드는 legacy-render-removal unit 3 에서 제거. 사용 전 Configure 가 계약.)
        public static void Configure(float3 simOrigin, float tileSize, GridLayout grid)
        {
            if (grid == null)
            {
                Debug.LogError("[BoardSpace] Tilemap mode requires a GridLayout; ignoring Configure.");
                return;
            }
            _simOrigin = simOrigin;
            _tileSize = tileSize > 0f ? tileSize : 1f;
            _grid = grid;
        }

        public static float3 ToView(float3 simWorld)
        {
            float cx = (simWorld.x - _simOrigin.x) / _tileSize + 0.5f;
            float cy = (simWorld.z - _simOrigin.z) / _tileSize + 0.5f;
            // Tilemap 은 보드를 정면으로 보는 평면 뷰다. 위치는 보드 평면(sim XZ) → 셀 로만 정한다.
            // sim 높이(simWorld.y)를 화면 세로(view.y)에 더하지 않는다 — 더하면 객체마다 다른 접지
            // 높이(유닛 0.5 / 해저드 0.05 / 투사체 등)가 제각각 셀에서 어긋난다. 평면 뷰에서
            // "높이"는 화면 위치가 아니다(필요하면 그건 연출 레이어가 따로 다룰 문제).
            return _grid.transform.TransformPoint(
                _grid.CellToLocalInterpolated(new Vector3(cx, cy, 0f)));
        }

        // 입력(레이캐스트 히트) 경계용 역변환. 입력 평면 위의 점을 전제하므로
        // sim 높이는 simOrigin.y 로 둔다 — ToView 의 높이 가산과 대칭이 아니다.
        public static float3 ToSim(float3 viewWorld)
        {
            Vector3 cell = _grid.LocalToCellInterpolated(
                _grid.transform.InverseTransformPoint(viewWorld));
            return new float3(
                _simOrigin.x + (cell.x - 0.5f) * _tileSize,
                _simOrigin.y,
                _simOrigin.z + (cell.y - 0.5f) * _tileSize);
        }

        // 방향/회전 벡터용 — 변환의 선형부만 적용 (facing, 투사체 회전, cast 방향).
        public static float3 ToViewVector(float3 simDir)
        {
            return ToView(_simOrigin + simDir) - ToView(_simOrigin);
        }

        // 포인터 입력 평면 = Grid 평면. 법선을 grid.transform.forward(로컬 +Z)에서 유도해 grid 회전을
        // 자동 추종 — XY 정면뷰든 XZ 바닥이든 동일 코드(틸트 빌보드 전환 후 XZ).
        public static Plane RaycastPlane()
        {
            return new Plane(_grid.transform.forward, _grid.transform.position);
        }
    }
}
