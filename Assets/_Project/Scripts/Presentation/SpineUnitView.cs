using Spine;
using Spine.Unity;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core.TimeControl;
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
        private string _attackAnimationName;
        private const float FacingMoveEpsilon = 0.001f;
        // tilemap-view-backend unit 3 — sim 좌표 보존. transform.position 은 view 좌표(ToView)라
        // sorting 셀 역산에 쓸 수 없다(z 소실). sorting 은 이 sim 좌표로 계산한다.
        private Vector3 _simWorld;
        // placement-enemy-see-through unit 2 — dim 페이드용 blob 참조.
        private BlobShadow _blob;
        private bool _shadowTransparent; // 실그림자 토글 상태 캐시(매 프레임 alloc 방지).

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

            // unit-parts-appearance 1 — 단일/조합 스킨 + 슬롯 틴트는 공용 헬퍼가 소유
            // (드래그 프리뷰와 동일 경로).
            if (_skeleton.Skeleton != null)
                SpineCombinedSkinCache.Apply(_skeleton.Skeleton, visualData);

            PlayIdleLooping();

            // tilted-billboard unit 0 — 틸트는 Billboard 컴포넌트가 소유. 스폰 시 1회 주입
            // (tilemapBillboardTilt, BattleBridge 가 세팅).
            // 카메라 yaw 고정(0) 전제라 월드 X 틸트로 충분. ScaleX(좌우반전)는 skeleton 채널이라 독립.
            var billboard = gameObject.AddComponent<Billboard>();
            billboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

            ApplyTilemapShadow();

            // time-manager Unit 4 — 스폰 순간의 Battle 스케일을 pull 해 초기화(슬로우모/정지 중
            // 스폰된 유닛도 즉시 동기화; ScaleChanged 이벤트는 스폰 이후 변화만 전달하므로 레이스 방지).
            SetAnimationTimeScale(TimeManager.Instance.ScaleOf(TimeDomain.Battle));
        }

        // time-manager Unit 4 — 전투 표현 재생 속도를 Battle 도메인 스케일에 맞춘다.
        // spine-unity 는 Time.deltaTime * timeScale 로 진행하는데 전역 timeScale 은 1 고정이라,
        // 이 값을 직접 세팅하지 않으면 슬로우모에서 애니만 풀스피드로 튀어 시뮬과 desync 된다.
        public void SetAnimationTimeScale(float scale)
        {
            if (_skeleton != null) _skeleton.timeScale = scale;
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
                    _blob = BlobShadow.Attach(transform, BattleBridge.BlobShadowSprite, BattleBridge.BlobShadowSize,
                        BattleBridge.BlobShadowColor,
                        BattleBridge.BlobShadowGroundY, BoardSortOrder.ShadowOrder, live: true); // 유닛은 이동 — 매 프레임 따라감
            }
        }

        public void UpdatePosition(Vector3 world)
        {
            FaceAlongMovement(world);
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

        // unit-health-display unit 1 — 적 저체력 틴트. BattleBridge 가 HealthDisplayStyle 로
        // ratio→Color 를 평가해 주입한다(뷰는 SO 를 모른다). _dying 중엔 마지막 틴트를 유지해
        // 죽음 연출 색을 덮지 않는다. 알파는 건드리지 않음(RGB 만).
        public void SetHealthTint(Color tint)
        {
            if (_dying || _skeleton == null || _skeleton.Skeleton == null) return;
            var skel = _skeleton.Skeleton;
            skel.R = tint.r;
            skel.G = tint.g;
            skel.B = tint.b;
        }

        // placement-enemy-see-through unit 2 — 드래그 배치 중 반투명 전환.
        // 적 Spine 머티리얼은 PMA transparent 라 블렌드 전환 없이 skeleton.A 로 페이드한다.
        // R/G/B(health tint)와 독립. _dying 중엔 사망 연출 색/알파를 덮지 않는다.
        public void SetDimmed(bool transparent, float alpha)
        {
            float a = Mathf.Clamp01(alpha);
            if (!_dying && _skeleton != null && _skeleton.Skeleton != null)
                _skeleton.Skeleton.A = a;
            _blob?.SetDimAlpha(transparent ? a : 1f);
            // 그림자 캐스팅 토글은 상태 변화 시에만 — QuadUnitView 와 일관, 매 프레임 GetComponentsInChildren alloc 방지.
            if (BattleBridge.UseRealShadows && transparent != _shadowTransparent)
            {
                var mode = transparent
                    ? UnityEngine.Rendering.ShadowCastingMode.Off
                    : UnityEngine.Rendering.ShadowCastingMode.TwoSided;
                var renderers = GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].shadowCastingMode = mode;
                _shadowTransparent = transparent;
            }
        }

        public void PlayAttack()
        {
            if (_dying || _skeleton == null) return;
            string attack = ResolveAnimation(_visualData.SpineAttackAnimation);
            if (string.IsNullOrEmpty(attack)) return;
            _attackAnimationName = attack;
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
            SetFacingByViewDelta(dx);
        }

        private void FaceAlongMovement(Vector3 world)
        {
            if (_dying || IsAttackAnimationPlaying()) return;
            float dx = ((Vector3)Wassup.Core.BoardSpace.ToView(world)).x
                       - ((Vector3)Wassup.Core.BoardSpace.ToView(_simWorld)).x;
            SetFacingByViewDelta(dx);
        }

        private bool IsAttackAnimationPlaying()
        {
            if (_skeleton == null || string.IsNullOrEmpty(_attackAnimationName)) return false;
            var current = _skeleton.AnimationState?.GetCurrent(0);
            return current != null
                   && !current.Loop
                   && current.Animation != null
                   && current.Animation.Name == _attackAnimationName;
        }

        private void SetFacingByViewDelta(float dx)
        {
            if (_dying || _skeleton == null || _skeleton.Skeleton == null) return;
            if (Mathf.Abs(dx) <= FacingMoveEpsilon) return;
            float currentAbs = Mathf.Abs(_skeleton.Skeleton.ScaleX);
            if (currentAbs < 0.001f) currentAbs = 1f;
            float desiredSign = IsEnemyView()
                ? (dx >= 0f ? 1f : -1f)
                : (dx >= 0f ? -1f : 1f);
            _skeleton.Skeleton.ScaleX = currentAbs * desiredSign;
        }

        private bool IsEnemyView() => _defenderExtras == null;

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
