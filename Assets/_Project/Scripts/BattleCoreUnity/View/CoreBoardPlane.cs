using UnityEngine;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5a — **보드 평면을 선언하는 곳.**
    //
    // 뷰는 전부 sim 좌표를 받아 `BoardSpace.ToView` 로 화면에 놓는다. 그 변환의 권위는
    // 격자(`Grid`)이고, 격자를 세우는 자리가 여기다. 옛 전투는 이 일을 타일맵 뷰가
    // 겸했는데(바닥 페인팅 + 오버레이 + 평면 선언), 그 셋 중 **평면 선언만** 이 unit 에서
    // 필요하다 — 바닥 비주얼은 스테이지 프리팹(디오라마)이 이미 소유하고, 오버레이·범위
    // 타일은 5b 의 것이다.
    //
    // ⚠ 이 컴포넌트는 규칙을 하나도 모른다. 셀 크기와 원점을 받아 격자를 세우고
    // `BoardSpace` 에 넘기는 것이 전부다. 판정이 여기 들어오면 그것이 새 브리지다.
    [DisallowMultipleComponent]
    public sealed class CoreBoardPlane : MonoBehaviour
    {
        [Tooltip("격자. 비어 있으면 이 오브젝트에 만든다.")]
        [SerializeField] private Grid _grid;

        public Grid Grid => _grid;

        /// <summary>
        /// 평면을 선언한다. `cellZeroMinCornerWorld` 는 셀 (0,0)의 최소 모서리가 놓일
        /// 월드 위치 = 스테이지의 `gridOriginLocal` 을 월드로 옮긴 점이다.
        ///
        /// ⚠ **격자 transform 의 writer 는 여기 하나다.** 옛 전투는 writer 가 둘이던 시절
        /// 프랍과 논리 셀이 조용히 어긋났고, 격자 기준 검증은 전부 통과한 채로 깨졌다.
        /// </summary>
        public void Declare(float tileSize, Vector3 cellZeroMinCornerWorld)
        {
            EnsureGrid();
            _grid.cellLayout = GridLayout.CellLayout.Rectangle;
            _grid.cellSize = new Vector3(tileSize, tileSize, 1f);
            // 격자를 XZ 바닥에 눕힌다(퍼스펙티브 3D 룩). 격자 로컬 XY → 월드 XZ.
            // `BoardSpace` 의 ToView/ToSim/RaycastPlane 가 전부 격자 기준이라 회전을 자동 추종한다.
            _grid.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _grid.transform.position = cellZeroMinCornerWorld;

            // sim origin 은 무조건 zero 다(맵 계약) — 격자가 어디 있든 sim 좌표계는 안 움직인다.
            Wassup.Core.BoardSpace.Configure(Unity.Mathematics.float3.zero, tileSize, _grid);
        }

        private void EnsureGrid()
        {
            if (_grid != null) return;
            _grid = GetComponent<Grid>();
            if (_grid == null) _grid = gameObject.AddComponent<Grid>();
        }
    }
}
