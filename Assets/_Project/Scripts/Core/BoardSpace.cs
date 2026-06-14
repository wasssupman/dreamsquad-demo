using Unity.Mathematics;
using UnityEngine;

namespace Wassup.Core
{
    // tilemap-view-backend unit 0 — sim(rect XZ 월드) ↔ view 변환의 유일한 지점.
    // MonoBehaviour 계층 전용. ECS/Burst 에서 호출 금지 (managed GridLayout 의존).
    // Tilemap 모드의 셀↔월드 정합 권위는 주입된 GridLayout — iso 수식을 여기 하드코딩하지 않는다.
    // sim 좌표 규약은 GridMath 와 동일: 정수배 = 셀 중심 (Tilemap 의 셀 중심은 +0.5 보정).
    public static class BoardSpace
    {
        private static BoardViewMode _mode = BoardViewMode.Legacy3D;
        private static float3 _simOrigin;
        private static float _tileSize = 1f;
        private static GridLayout _grid;

        public static BoardViewMode Mode => _mode;

        // BattleBridge 맵 빌드 시 1회 호출. 정적 상태 쓰기는 이 메서드가 유일하다.
        // Tilemap 모드인데 grid 가 없으면 시뮬레이션이 보이지 않는 것보다 Legacy 로 사는 쪽을 택한다.
        public static void Configure(BoardViewMode mode, float3 simOrigin, float tileSize, GridLayout grid)
        {
            if (mode != BoardViewMode.Legacy3D && grid == null)
            {
                Debug.LogError("[BoardSpace] Tilemap mode requires a GridLayout; falling back to Legacy3D.");
                mode = BoardViewMode.Legacy3D;
            }
            _mode = mode;
            _simOrigin = simOrigin;
            _tileSize = tileSize > 0f ? tileSize : 1f;
            _grid = grid;
        }

        public static float3 ToView(float3 simWorld)
        {
            if (_mode == BoardViewMode.Legacy3D) return simWorld;
            float cx = (simWorld.x - _simOrigin.x) / _tileSize + 0.5f;
            float cy = (simWorld.z - _simOrigin.z) / _tileSize + 0.5f;
            Vector3 view = _grid.transform.TransformPoint(
                _grid.CellToLocalInterpolated(new Vector3(cx, cy, 0f)));
            // sim 높이(점프/낙하/부유 연출)는 화면 위 방향(Y) 가산으로 보존.
            view.y += simWorld.y - _simOrigin.y;
            return view;
        }

        // 입력(레이캐스트 히트) 경계용 역변환. 입력 평면 위의 점을 전제하므로
        // sim 높이는 simOrigin.y 로 둔다 — ToView 의 높이 가산과 대칭이 아니다.
        public static float3 ToSim(float3 viewWorld)
        {
            if (_mode == BoardViewMode.Legacy3D) return viewWorld;
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
            if (_mode == BoardViewMode.Legacy3D) return simDir;
            return ToView(_simOrigin + simDir) - ToView(_simOrigin);
        }

        // 모드별 포인터 입력 평면. Legacy3D = 보드 표면(XZ, 보드 원점 높이),
        // Tilemap 모드 = Grid 위치를 지나는 XY 평면.
        public static Plane RaycastPlane()
        {
            if (_mode == BoardViewMode.Legacy3D)
                return new Plane(Vector3.up, (Vector3)_simOrigin);
            return new Plane(Vector3.back, _grid.transform.position);
        }
    }
}
