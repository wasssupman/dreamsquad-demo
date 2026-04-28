using Spine;
using Spine.Unity;
using Wassup.Bridge;
using Unity.Entities;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // Phase 8 — one SpineDefenderView per live defender entity. Owns the
    // SkeletonAnimation GameObject, forwards ECS-side triggers (spawn/attack/die)
    // to Spine animations, and reads ECS state via a narrow bridge API rather
    // than touching EntityManager directly (TRD context-boundary rule).
    //
    // Lifecycle: instantiated from SpineDefenderPool.Spawn → lives until Kill
    // (die animation completes) or Dispose (teardown). No pooling/recycling in
    // Phase 8 — a fresh GameObject per defender keeps the code path simple and
    // the cost is low since defenders are placed once and die once per match.
    [DisallowMultipleComponent]
    public class SpineDefenderView : MonoBehaviour
    {
        private SkeletonAnimation _skeleton;
        private DefenderUnitData _unitData;
        private Entity _entity;
        private bool _dying;

        public Entity Entity => _entity;

        // Called once by SpineDefenderPool right after AddComponent. The view
        // takes ownership of the GameObject transform/position and replaces any
        // placeholder rendering with a SkeletonAnimation.
        public void Spawn(DefenderUnitData unitData, Entity entity, Vector3 worldPos)
        {
            _unitData = unitData;
            _entity = entity;
            transform.position = worldPos;
            float s = Mathf.Max(0.01f, unitData.spineVisualScale * BattleBridge.CharacterVisualScale);
            transform.localScale = new Vector3(s, s, s);

            _skeleton = gameObject.AddComponent<SkeletonAnimation>();
            _skeleton.skeletonDataAsset = unitData.skeletonDataAsset;
            _skeleton.initialSkinName = string.IsNullOrEmpty(unitData.spineSkinName) ? "default" : unitData.spineSkinName;
            _skeleton.Initialize(true);

            if (!string.IsNullOrEmpty(unitData.spineSkinName) && _skeleton.Skeleton != null)
            {
                var skin = _skeleton.Skeleton.Data.FindSkin(unitData.spineSkinName);
                if (skin != null)
                {
                    _skeleton.Skeleton.SetSkin(skin);
                    _skeleton.Skeleton.SetSlotsToSetupPose();
                }
                else
                {
                    Debug.LogWarning(
                        $"[SpineDefenderView] Skin '{unitData.spineSkinName}' not found in skeleton data for '{unitData.displayName}'.",
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
            if (_dying || _skeleton == null || string.IsNullOrEmpty(_unitData.attackAnimation)) return;
            var state = _skeleton.AnimationState;
            state.SetAnimation(0, _unitData.attackAnimation, false);
            // Chain back to idle so the next attack trigger starts cleanly.
            state.AddAnimation(0, _unitData.idleAnimation, true, 0f);
        }

        public bool PlayDeploy()
        {
            if (_dying || _skeleton == null) return false;
            string animation = ResolveAnimation(_unitData.deployAnimation, _unitData.dragAnimation, _unitData.attackAnimation, _unitData.idleAnimation);
            if (string.IsNullOrEmpty(animation)) return false;
            var state = _skeleton.AnimationState;
            state.SetAnimation(0, animation, false);
            if (!string.IsNullOrEmpty(_unitData.idleAnimation))
                state.AddAnimation(0, _unitData.idleAnimation, true, 0f);
            return true;
        }

        // Triggers the death animation. When it completes the GameObject
        // destroys itself — callers should *not* Destroy() the object directly,
        // or the ECS bridge keeps dead entries in its view dictionary.
        public void Kill()
        {
            if (_dying) return;
            _dying = true;
            if (_skeleton == null || string.IsNullOrEmpty(_unitData.deathAnimation))
            {
                Destroy(gameObject);
                return;
            }
            var state = _skeleton.AnimationState;
            var track = state.SetAnimation(0, _unitData.deathAnimation, false);
            track.Complete += _ => { if (this != null) Destroy(gameObject); };
        }

        // Forces immediate teardown without playing the death animation. Used
        // by BattleBridge.TeardownCurrentBattle to clear state between rounds.
        public void Dispose()
        {
            _dying = true;
            if (this != null) Destroy(gameObject);
        }

        // Snaps the skeleton to face a world-space point. Called at the exact
        // fire frame by SpineDefenderPool.NotifyAttack so the attack animation
        // always plays toward the actual victim. Ignores Y — facing is left/right
        // only. Preserves the absolute value of ScaleX so rigs that ship with
        // non-1 default scale (e.g. small portraits) keep their intended size.
        //
        // player-main.skel ships with the rig already facing LEFT at ScaleX=+1,
        // so to face a target on the right (dx > 0) we invert ScaleX; a target
        // on the left keeps the default orientation.
        public void FaceToward(Vector3 worldPoint)
        {
            if (_dying || _skeleton == null || _skeleton.Skeleton == null) return;
            float dx = worldPoint.x - transform.position.x;
            if (Mathf.Approximately(dx, 0f)) return;
            float currentAbs = Mathf.Abs(_skeleton.Skeleton.ScaleX);
            if (currentAbs < 0.001f) currentAbs = 1f;
            float desiredSign = dx >= 0f ? -1f : 1f;
            float desired = currentAbs * desiredSign;
            if (!Mathf.Approximately(_skeleton.Skeleton.ScaleX, desired))
            {
                _skeleton.Skeleton.ScaleX = desired;
            }
        }

        public Vector3 ResolveCastAnchor()
        {
            if (_skeleton != null && _skeleton.Skeleton != null && !string.IsNullOrEmpty(_unitData.castAnchorBone))
            {
                var bone = _skeleton.Skeleton.FindBone(_unitData.castAnchorBone);
                if (bone != null)
                    return transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
            }
            var off = _unitData.castAnchorLocalOffset;
            if (_skeleton != null && _skeleton.Skeleton != null && _skeleton.Skeleton.ScaleX < 0f)
                off.x = -off.x;
            return transform.TransformPoint(off);
        }

        private void PlayIdleLooping()
        {
            if (_skeleton == null || string.IsNullOrEmpty(_unitData.idleAnimation)) return;
            _skeleton.AnimationState.SetAnimation(0, _unitData.idleAnimation, true);
        }

        private string ResolveAnimation(params string[] candidates)
        {
            if (_skeleton == null || _skeleton.Skeleton == null || _skeleton.Skeleton.Data == null) return null;
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                if (_skeleton.Skeleton.Data.FindAnimation(candidate) != null)
                    return candidate;
            }
            return null;
        }
    }
}
