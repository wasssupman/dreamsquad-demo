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

            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;
            if (_camera == null) return;

            var target = visualRoot != null ? visualRoot : transform;
            if (billboardMode == PropBillboardMode.YAxis)
            {
                var direction = target.position - _camera.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f)
                    target.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                return;
            }

            target.rotation = _camera.transform.rotation;
        }

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
                spriteRenderer.sortingOrder = data.sortingOrder;
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
