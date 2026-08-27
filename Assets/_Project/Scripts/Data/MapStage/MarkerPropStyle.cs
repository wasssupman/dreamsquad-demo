using UnityEngine;

namespace Wassup.Data
{
    // map-diorama-stage unit 6 — 스폰/골 마커의 **공용** 프랍. 사용자 결정(2026-08-27): 포탈 프랍은 맵에 상관없이
    // 공유하는 구조 — 스테이지 프리팹은 마커만 두고, Presentation.MarkerPropInstaller 가 스테이지가 켜질 때 visualRoot 가 빈
    // 마커에 이 프랍을 얹는다. 프리팹이 visualRoot 를 직접 채웠으면(맵 전용 연출) 그쪽이 이긴다.
    // 런타임(설치자)과 에디터 프리뷰(MapStageAuthoringTools.ApplySharedMarkerProps)가 같은 에셋을 읽는다. 정본 = Data/Maps/MarkerPropStyle.asset.
    [CreateAssetMenu(fileName = "MarkerPropStyle", menuName = "Wassup/Map/MarkerPropStyle", order = 4)]
    public class MarkerPropStyle : ScriptableObject
    {
        [Tooltip("스폰 마커 프랍(수직 빨간 포탈). 마커 호스트 밑에 identity 로 인스턴스화.")]
        public GameObject spawnProp;

        [Tooltip("골 마커 프랍(수직 노란 포탈). GoalMarker 의 균열/붕괴/스트레스 틴트 훅이 이 서브트리를 본다.")]
        public GameObject goalProp;

        [Tooltip("프랍 루트의 로컬 회전(오일러). 포탈 링의 «정면»을 카메라 쪽으로 돌리는 용도 — 수직 포탈은 Y(yaw)만 쓴다(X/Z = 0). 스폰·골 공용.")]
        public Vector3 propEulerAngles;
    }
}
