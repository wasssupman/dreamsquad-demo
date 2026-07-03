using Spine;
using Spine.Unity;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Presentation
{
    [DisallowMultipleComponent]
    public class SpineUnitView : MonoBehaviour
    {
        private SkeletonAnimation _skeleton;
        private ISpineUnitVisualData _visualData;
        private IDefenderSpineExtras _defenderExtras;
        private Entity _entity;
        private bool _dying;
        // tilemap-view-backend unit 3 — sim 좌표 보존. transform.position 은 view 좌표(ToView)라
        // sorting 셀 역산에 쓸 수 없다(z 소실). sorting 은 이 sim 좌표로 계산한다.
        private Vector3 _simWorld;

        public Entity Entity => _entity;

        public void Spawn(ISpineUnitVisualData visualData, IDefenderSpineExtras defenderExtras, Entity entity, Vector3 worldPos)
        {
            _visualData = visualData;
            _defenderExtras = defenderExtras;
            _entity = entity;
            ApplyRenderPosition(worldPos);
            float s = Mathf.Max(0.01f, visualData.SpineVisualScale * BattleBridge.CharacterVisualScale);
            transform.localScale = new Vector3(s, s, s);

            _skeleton = gameObject.AddComponent<SkeletonAnimation>();
            _skeleton.skeletonDataAsset = visualData.SpineSkeletonDataAsset;
            _skeleton.initialSkinName = string.IsNullOrEmpty(visualData.SpineSkinName) ? "default" : visualData.SpineSkinName;
            _skeleton.Initialize(true);

            if (!string.IsNullOrEmpty(visualData.SpineSkinName) && _skeleton.Skeleton != null)
            {
                Skin skin = _skeleton.Skeleton.Data.FindSkin(visualData.SpineSkinName);
                if (skin != null)
                {
                    _skeleton.Skeleton.SetSkin(skin);
                    _skeleton.Skeleton.SetSlotsToSetupPose();
                }
                else
                {
                    Debug.LogWarning(
                        $"[SpineUnitView] Skin '{visualData.SpineSkinName}' not found for '{visualData.SpineDisplayName}'.",
                        this);
                }
            }

            PlayIdleLooping();

            // tilted-billboard unit 0 — 틸트는 Billboard 컴포넌트가 소유. 스폰 시 1회 주입
            // (tilemapBillboardTilt, BattleBridge 가 세팅).
            // 카메라 yaw 고정(0) 전제라 월드 X 틸트로 충분. ScaleX(좌우반전)는 skeleton 채널이라 독립.
            var billboard = gameObject.AddComponent<Billboard>();
            billboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

            ApplyTilemapShadow();
        }

        // tilemap-real-shadows — Tilemap 모드 그림자: 진짜(빌보드 cast) vs 블롭(상호배타).
        // 진짜 = renderer 가 실루엣 그림자 cast(평면이라 TwoSided). 블롭 = 발밑 타원 + cast OFF.
        private void ApplyTilemapShadow()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (BattleBridge.UseRealShadows)
            {
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            }
            else
            {
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (BattleBridge.BlobShadowSprite != null)
                    BlobShadow.Attach(transform, BattleBridge.BlobShadowSprite, BattleBridge.BlobShadowSize,
                        BattleBridge.BlobShadowColor,
                        BattleBridge.BlobShadowGroundY, BoardSortOrder.ShadowOrder, live: true); // 유닛은 이동 — 매 프레임 따라감
            }
        }

        public void UpdatePosition(Vector3 world)
        {
            ApplyRenderPosition(world);
        }

        // enemy-spawn-positioning 0 — 렌더 위치의 단일 지점: sim 좌표(ToView) + 유닛 타입별 피봇 오프셋.
        // _simWorld 은 순수 sim 좌표 유지(정렬/셀 역산용). visualOffset 은 view-space 시각 보정만(sim 무영향).
        private void ApplyRenderPosition(Vector3 world)
        {
            _simWorld = world;
            Vector3 offset = _visualData != null ? (Vector3)_visualData.SpineVisualOffset : Vector3.zero;
            transform.position = (Vector3)Wassup.Core.BoardSpace.ToView(world) + offset;
        }

        public void UpdateSortingOrder(Unity.Mathematics.int2 gridSize, float tileSize)
        {
            // sim 좌표로 셀 역산 — view 좌표(transform.position)는 z 가 소실돼 행 정렬이 붕괴한다.
            int order = BoardSortOrder.ComputeFromWorld(
                gridSize,
                _simWorld,
                tileSize,
                BoardSortOrder.CharacterOffset);
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder = order;
        }

        public void PlayAttack()
        {
            if (_dying || _skeleton == null) return;
            string attack = ResolveAnimation(_visualData.SpineAttackAnimation);
            if (string.IsNullOrEmpty(attack)) return;
            var state = _skeleton.AnimationState;
            state.SetAnimation(0, attack, false);
            string idle = ResolveAnimation(_visualData.SpineIdleAnimation, "idle", "Idle", "walk", "Walk");
            if (!string.IsNullOrEmpty(idle))
                state.AddAnimation(0, idle, true, 0f);
        }

        public bool PlayDeploy()
        {
            if (_dying || _skeleton == null) return false;
            // Defender-only feedback. Enemies spawn without IDefenderSpineExtras
            // and never invoke this path; guarding here prevents accidental
            // animation triggers if an enemy entity ever reaches it.
            if (_defenderExtras == null) return false;
            string animation = ResolveAnimation(
                _defenderExtras.SpineDeployAnimation,
                _defenderExtras.SpineDragAnimation,
                _visualData.SpineAttackAnimation,
                _visualData.SpineIdleAnimation,
                "idle",
                "walk");
            if (string.IsNullOrEmpty(animation)) return false;
            var state = _skeleton.AnimationState;
            state.SetAnimation(0, animation, false);
            string idle = ResolveAnimation(_visualData.SpineIdleAnimation, "idle", "Idle", "walk", "Walk");
            if (!string.IsNullOrEmpty(idle))
                state.AddAnimation(0, idle, true, 0f);
            return true;
        }

        public void Kill()
        {
            if (_dying) return;
            _dying = true;
            string death = ResolveAnimation(_visualData.SpineDeathAnimation);
            if (_skeleton == null || string.IsNullOrEmpty(death))
            {
                Destroy(gameObject);
                return;
            }
            var track = _skeleton.AnimationState.SetAnimation(0, death, false);
            track.Complete += _ => { if (this != null) Destroy(gameObject); };
        }

        public void Dispose()
        {
            _dying = true;
            if (this != null) Destroy(gameObject);
        }

        public void FaceToward(Vector3 worldPoint)
        {
            if (_dying || _skeleton == null || _skeleton.Skeleton == null) return;
            // worldPoint 는 sim 좌표(NotifyAttack 경유) — view 좌표로 변환해 view transform 과 같은 공간에서 비교.
            float dx = ((Vector3)Wassup.Core.BoardSpace.ToView(worldPoint)).x - transform.position.x;
            if (Mathf.Approximately(dx, 0f)) return;
            float currentAbs = Mathf.Abs(_skeleton.Skeleton.ScaleX);
            if (currentAbs < 0.001f) currentAbs = 1f;
            float desiredSign = dx >= 0f ? -1f : 1f;
            _skeleton.Skeleton.ScaleX = currentAbs * desiredSign;
        }

        public Vector3 ResolveCastAnchor()
        {
            // 반환값은 view 공간(transform 기반, transform.position 은 이미 ToView). 호출측은 view 끼리 비교/빼기.
            // Cast anchor is only meaningful for defenders firing projectiles.
            // Without IDefenderSpineExtras (enemies), fall back to the unit's
            // transform origin so callers still get a sensible world position.
            if (_defenderExtras == null) return transform.position;

            if (_skeleton != null && _skeleton.Skeleton != null && !string.IsNullOrEmpty(_defenderExtras.SpineCastAnchorBone))
            {
                var bone = _skeleton.Skeleton.FindBone(_defenderExtras.SpineCastAnchorBone);
                if (bone != null)
                    return transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
            }
            var off = _defenderExtras.SpineCastAnchorLocalOffset;
            if (_skeleton != null && _skeleton.Skeleton != null && _skeleton.Skeleton.ScaleX < 0f)
                off.x = -off.x;
            return transform.TransformPoint(off);
        }

        private void PlayIdleLooping()
        {
            if (_skeleton == null) return;
            string animation = ResolveAnimation(_visualData.SpineIdleAnimation, "idle", "Idle", "walk", "Walk");
            if (!string.IsNullOrEmpty(animation))
                _skeleton.AnimationState.SetAnimation(0, animation, true);
        }

        private string ResolveAnimation(params string[] candidates)
        {
            if (_skeleton == null || _skeleton.Skeleton == null || _skeleton.Skeleton.Data == null) return null;
            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate)) continue;
                if (_skeleton.Skeleton.Data.FindAnimation(candidate) != null)
                    return candidate;
            }
            return null;
        }
    }
}
