using UnityEngine;

namespace Wassup.Data
{
    // tilemap-view-backend unit 4 — Tilemap 모드 카메라 프리셋. 하드코딩 금지: 투영/거리/각도/정렬축 전부 에셋.
    // orthographicSize 는 런타임에 gridSize+tileSize 로 계산.
    [CreateAssetMenu(menuName = "Wassup/Board Camera Preset", fileName = "CameraPreset")]
    public class BoardCameraPreset : ScriptableObject
    {
        public bool orthographic = true;
        [Tooltip("보드 절반 크기에 더할 여유(월드 유닛). orthographicSize = 보드 half-extent + 이 값.")]
        public float orthoSizePadding = 1.5f;
        [Header("Perspective (tilted-billboard — XZ 바닥 + 퍼스펙티브)")]
        [Tooltip("orthographic=false 일 때 수직 FOV(도).")]
        public float fieldOfView = 40f;
        [Tooltip("퍼스펙티브 framing 여유 배율 (보드 바운딩 구가 화면에 차는 정도). 1=딱맞음.")]
        public float perspectiveFitMargin = 1.12f;
        [Tooltip("보드 중심(view 공간) 기준 카메라 위치 오프셋.")]
        public Vector3 positionOffset = new Vector3(0f, 0f, -20f);
        public Vector3 rotationEuler = Vector3.zero;
        public TransparencySortMode transparencySortMode = TransparencySortMode.CustomAxis;
        public Vector3 sortAxis = new Vector3(0f, 1f, 0f);
        public float nearClip = 0.1f;
        public float farClip = 100f;
        [Header("Background (tilemap-mode-adoption unit 1)")]
        [Tooltip("Tilemap 모드에서 skybox 제거하고 단색 배경 사용.")]
        public bool solidColorBackground = true;
        public Color backgroundColor = new Color(0.09f, 0.10f, 0.13f, 1f);
    }
}
