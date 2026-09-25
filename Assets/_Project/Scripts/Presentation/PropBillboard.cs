using Spine.Unity;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.BattleView;

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

        // battle-core-rebuild unit 8a — 거리 틸트 노브의 **주인을 직접 참조**한다(제약 12 ⓐ·ⓑ).
        // 이전엔 브리지의 거리 틸트 static 미러를 읽었고, 새 씬에는 브리지가 없어
        // factor 가 0(= 비활성)이라 스테이지 프랍의 거리 틸트가 통째로 꺼져 있었다.
        // 옛 씬도 같은 값이다(`CharacterViewConfig` = 옛 씬 브리지 블록 복사). 비면 틸트 비활성.
        [SerializeField] private CharacterViewConfig viewConfig;

        private Camera _camera;

        public PropData Data => data;

        /// <summary>거리 틸트 계수. 0 = 비활성. 테스트가 「새 씬 프랍 틸트 = 저작 값」을 증언하는 창이다.</summary>
        public float DistanceTiltFactor => viewConfig != null ? viewConfig.PropDistanceTiltFactor : 0f;

        public void Configure(PropData propData, Transform visual, SpriteRenderer sprite, SkeletonAnimation skeleton,
                              CharacterViewConfig tiltConfig)
        {
            viewConfig = tiltConfig;
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
            // 거리 틸트(unit 6)는 Tilted 에서도 카메라가 필요하다. factor=0 이면 비활성(고정).
            bool wantsDistance = facing == BillboardRotation.Facing.Tilted
                                 && DistanceTiltFactor != 0f;
            Camera cam = null;
            if (facing != BillboardRotation.Facing.Tilted || wantsDistance)
            {
                if (_camera == null || !_camera.isActiveAndEnabled)
                    _camera = Camera.main;
                // 카메라-페이싱(YAxis/Full)은 카메라 없으면 스킵. 거리 틸트는 base 로 폴백 후 다음 프레임 재시도.
                if (_camera == null && facing != BillboardRotation.Facing.Tilted) return;
                cam = _camera;
            }

            // tilt 출처는 데이터(캐릭터 레이어와 독립). 프랍은 flip180 미사용.
            float tilt = data != null ? data.tiltAngle : 0f;
            // 거리 보정은 라이브 재계산(카메라가 페이즈마다 pitch 변함 — 정적 아님). 프랍은 안 움직여 휘청 없음.
            if (wantsDistance && cam != null)
            {
                tilt = BillboardRotation.ResolveDistanceTilt(tilt,
                    viewConfig.PropDistanceTiltFactor,
                    viewConfig.PropDistanceTiltMin, viewConfig.PropDistanceTiltMax,
                    cam, transform.position);
            }
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
                skeletonAnimation.Renderer.InitialSkinName = string.IsNullOrEmpty(data.spineSkinName) ? "default" : data.spineSkinName;
                if (!skeletonAnimation.IsValid)
                    skeletonAnimation.Initialize(false);

                if (!string.IsNullOrEmpty(data.idleAnimation) &&
                    skeletonAnimation.Skeleton != null &&
                    skeletonAnimation.Skeleton.Data.FindAnimation(data.idleAnimation) != null &&
                    skeletonAnimation.AnimationState.GetTrack(0)?.Animation?.Name != data.idleAnimation)
                {
                    skeletonAnimation.AnimationState.SetAnimation(0, data.idleAnimation, true);
                }
            }
        }
    }
}
