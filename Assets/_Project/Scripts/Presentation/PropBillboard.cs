using Spine.Unity;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    [DisallowMultipleComponent]
    public class PropBillboard : MonoBehaviour
    {
        [SerializeField] private PropData data;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private SkeletonAnimation skeletonAnimation;
        [SerializeField] private PropBillboardMode billboardMode = PropBillboardMode.FullCamera;

        private Camera _camera;

        public PropData Data => data;

        public void Configure(PropData propData, Transform visual, SpriteRenderer sprite, SkeletonAnimation skeleton)
        {
            data = propData;
            visualRoot = visual;
            spriteRenderer = sprite;
            skeletonAnimation = skeleton;
            billboardMode = propData != null ? propData.billboardMode : PropBillboardMode.FullCamera;
        }

        private void Awake()
        {
            ApplyData();
        }

        private void LateUpdate()
        {
            if (billboardMode == PropBillboardMode.None) return;

            var target = visualRoot != null ? visualRoot : transform;
            var facing = ToFacing(billboardMode);
            Camera cam = null;
            if (facing != BillboardRotation.Facing.Tilted)
            {
                if (_camera == null || !_camera.isActiveAndEnabled)
                    _camera = Camera.main;
                if (_camera == null) return;
                cam = _camera;
            }

            // tilt 출처는 데이터(캐릭터 레이어와 독립). 프랍은 flip180 미사용.
            float tilt = data != null ? data.tiltAngle : 0f;
            var rot = BillboardRotation.Compute(facing, tilt, cam, target.position, flip180: false);
            if (rot.HasValue) target.rotation = rot.Value;
        }

        private static BillboardRotation.Facing ToFacing(PropBillboardMode m) => m switch
        {
            PropBillboardMode.YAxis => BillboardRotation.Facing.YAxis,
            PropBillboardMode.FullCamera => BillboardRotation.Facing.Camera,
            _ => BillboardRotation.Facing.Tilted,
        };

        private void ApplyData()
        {
            if (data == null) return;

            billboardMode = data.billboardMode;
            var target = visualRoot != null ? visualRoot : transform;
            target.localPosition = data.visualOffset;
            target.localScale = Vector3.one * Mathf.Max(0.01f, data.visualScale);

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = data.sprite;
                spriteRenderer.color = data.spriteColor;
            }

            if (skeletonAnimation != null && data.skeletonDataAsset != null)
            {
                skeletonAnimation.skeletonDataAsset = data.skeletonDataAsset;
                skeletonAnimation.initialSkinName = string.IsNullOrEmpty(data.spineSkinName) ? "default" : data.spineSkinName;
                if (!skeletonAnimation.valid)
                    skeletonAnimation.Initialize(false);

                if (!string.IsNullOrEmpty(data.idleAnimation) &&
                    skeletonAnimation.Skeleton != null &&
                    skeletonAnimation.Skeleton.Data.FindAnimation(data.idleAnimation) != null &&
                    skeletonAnimation.AnimationState.GetCurrent(0)?.Animation?.Name != data.idleAnimation)
                {
                    skeletonAnimation.AnimationState.SetAnimation(0, data.idleAnimation, true);
                }
            }
        }
    }
}
