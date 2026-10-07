using UnityEngine;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5a — **보드 평면을 선언하는 곳.**
    //
    // 뷰는 전부 sim 좌표를 받아 `BoardSpace.ToView` 로 화면에 놓는다. 그 변환의 권위는
    // 이 오브젝트의 Transform(셀 (0,0) 최소 모서리 = 원점, 로컬 X/Y = 셀 축)과 tileSize 이고,
    // 평면을 세우는 자리가 여기다. 옛 전투는 이 일을 타일맵 뷰가 겸했는데(바닥 페인팅 +
    // 오버레이 + 평면 선언), 그 셋 중 **평면 선언만** 이 unit 에서 필요하다 — 바닥 비주얼은
    // 스테이지 프리팹(디오라마)이 이미 소유하고, 오버레이·범위 표시는 5b 의 것이다.
    // 옛 `Grid` 컴포넌트는 tilemap-untangle 단위 1(2026-10-07)에서 뗐다 — 셀 정의는 `MapStageMath` 하나다.
    //
    // ⚠ 이 컴포넌트는 규칙을 하나도 모른다. 셀 크기와 원점을 받아 평면을 세우고
    // `BoardSpace` 에 넘기는 것이 전부다. 판정이 여기 들어오면 그것이 새 브리지다.
    [DisallowMultipleComponent]
    public sealed class CoreBoardPlane : MonoBehaviour
    {
        /// <summary>보드 평면 Transform — 셀 (0,0) 최소 모서리가 원점, 로컬 X/Y 가 셀 축, 로컬 +Z 가 법선.</summary>
        public Transform Plane => transform;

        /// <summary>마지막 `Declare` 의 셀 한 변(월드 단위). Declare 전엔 0.</summary>
        public float TileSize { get; private set; }

        /// <summary>
        /// 평면을 선언한다. `cellZeroMinCornerWorld` 는 셀 (0,0)의 최소 모서리가 놓일
        /// 월드 위치 = 스테이지의 `gridOriginLocal` 을 월드로 옮긴 점이다.
        ///
        /// ⚠ **평면 transform 의 writer 는 여기 하나다.** 옛 전투는 writer 가 둘이던 시절
        /// 프랍과 논리 셀이 조용히 어긋났고, 격자 기준 검증은 전부 통과한 채로 깨졌다.
        /// </summary>
        public void Declare(float tileSize, Vector3 cellZeroMinCornerWorld)
        {
            TileSize = tileSize;
            // 평면을 XZ 바닥에 눕힌다(퍼스펙티브 3D 룩). 평면 로컬 XY → 월드 XZ.
            // `BoardSpace` 의 ToView/ToSim/RaycastPlane 가 전부 이 Transform 기준이라 회전을 자동 추종한다.
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            transform.position = cellZeroMinCornerWorld;

            // sim origin 은 무조건 zero 다(맵 계약) — 평면이 어디 있든 sim 좌표계는 안 움직인다.
            Wassup.Core.BoardSpace.Configure(Unity.Mathematics.float3.zero, tileSize, transform);
        }
    }
}
