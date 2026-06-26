using UnityEngine;

namespace Wassup.Presentation
{
    // tilted-billboard unit 0 — 틸트/페이싱 회전의 유일 소유자.
    // 틸트 각도는 호출측/데이터에서 주입(Setup)받는다 — 컴포넌트는 레이어별 각도 정책을 모른다.
    // 회전(transform.rotation)만 담당. 위치/스케일/Spine ScaleX(좌우반전)는 건드리지 않는다(채널 분리).
    // 피벗=발(transform 원점=셀 위치)이라 틸트해도 접지/정렬 불변.
    public enum BillboardMode { None, YAxis, Full, Tilted, TiltedDynamic }

    [DisallowMultipleComponent]
    public class Billboard : MonoBehaviour
    {
        [SerializeField] private BillboardMode mode = BillboardMode.Tilted;
        [Tooltip("Tilted 모드의 월드 X 틸트각(도). 레이어/데이터에서 주입.")]
        [SerializeField] private float tiltAngle;
        [Tooltip("아트 앞면이 카메라 반대로 나올 때 마지막에 180° 뒤집기.")]
        [SerializeField] private bool flip180;

        private Camera _camera;

        // 스폰/Configure 시 1회 주입. 모드 전환은 맵 재빌드(=재스폰)이므로 매 프레임 갱신 불필요.
        public void Setup(BillboardMode billboardMode, float tilt, bool flip = false)
        {
            mode = billboardMode;
            tiltAngle = tilt;
            flip180 = flip;
        }

        private void LateUpdate()
        {
            if (mode == BillboardMode.None) return;

            var facing = ToFacing(mode);
            Camera cam = null;
            if (facing != BillboardRotation.Facing.Tilted)
            {
                if (!EnsureCamera()) return;
                cam = _camera;
            }

            var rot = BillboardRotation.Compute(facing, tiltAngle, cam, transform.position, flip180);
            if (rot.HasValue) transform.rotation = rot.Value;
        }

        // TiltedDynamic 은 후속(README). 현재 Tilted 와 동일.
        private static BillboardRotation.Facing ToFacing(BillboardMode m) => m switch
        {
            BillboardMode.YAxis => BillboardRotation.Facing.YAxis,
            BillboardMode.Full => BillboardRotation.Facing.Camera,
            _ => BillboardRotation.Facing.Tilted,
        };

        private bool EnsureCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;
            return _camera != null;
        }
    }
}
