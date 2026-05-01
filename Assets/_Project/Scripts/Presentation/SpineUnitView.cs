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

        public Entity Entity => _entity;

        public void Spawn(ISpineUnitVisualData visualData, IDefenderSpineExtras defenderExtras, Entity entity, Vector3 worldPos)
        {
            _visualData = visualData;
            _defenderExtras = defenderExtras;
            _entity = entity;
            transform.position = worldPos;
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
        }

        public void UpdatePosition(Vector3 world)
        {
            transform.position = world;
        }

        public void UpdateSortingOrder(Unity.Mathematics.int2 gridSize, float tileSize)
        {
            int order = BoardSortOrder.ComputeFromWorld(
                gridSize,
                transform.position,
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
            float dx = worldPoint.x - transform.position.x;
            if (Mathf.Approximately(dx, 0f)) return;
            float currentAbs = Mathf.Abs(_skeleton.Skeleton.ScaleX);
            if (currentAbs < 0.001f) currentAbs = 1f;
            float desiredSign = dx >= 0f ? -1f : 1f;
            _skeleton.Skeleton.ScaleX = currentAbs * desiredSign;
        }

        public Vector3 ResolveCastAnchor()
        {
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
