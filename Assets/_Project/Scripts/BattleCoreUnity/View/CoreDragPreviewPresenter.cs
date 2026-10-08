using Spine.Unity;
using Unity.Mathematics;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.Core.TimeControl;
using Somnia.Battle.Data;
using Somnia.Battle.Data.BattleView;
using Somnia.Battle.Presentation;

namespace Somnia.Battle.BattleCoreUnity.View
{
    // battle-core-rebuild 5b 수정(사용자 플레이 2차) — **드래그 실루엣.** 유닛을 끄는 동안 판 위,
    // 고스트 칸 자리에 **그 유닛의 그림**이 반투명으로 서서 손끝 칸을 따라간다.
    //
    // 사용자의 문장: 「배치 중에 하이라이트 타일 위로 배치할 유닛이 drag 모션을 하면서 실루엣이
    // 나와야 하는데 현재 미노출」. 5b 는 드래그 프리뷰를 「손끝 키링」으로 읽고 룩 보류로
    // 뺐는데, **라이브 옛 게임은 키링이 아니었다** — `DragSwaySettings.dndSilhouetteEnabled` 의
    // 클래스 기본값이 true 이고 에셋이 그 칸을 직렬화하지 않아 기본값이 산다(에셋에 ⑮ 그룹 0칸).
    // 그래서 옛 트레이 D&D 는 `BuildSession` 에서 키링을 만들지 않고(`DefenderDragPlacementController.cs:1722`)
    // **보드 실루엣**(`defender-footprint` unit 7·8)만 그렸다. 이 파일은 그것의 이식이다.
    //
    // 옮긴 것(옛 `DefenderDragPlacementController`, 줄 번호는 그 파일):
    //   · 등장 조건 — 트레이 D&D = `dndSilhouetteEnabled`(:2084) · 집어 든 뒤 판 드래그 =
    //     `armedSilhouetteEnabled` 이고 **드래그로 승격된 뒤에만**(:1130 — 탭 경로 무변 계약)
    //   · 자리 — footprint 발밑(하단 행 가로 중앙)의 view 좌표(`GridAnchorToViewCenter`, 브리지 :5351).
    //     여기서는 그 규칙의 주인인 코어 `Footprint.FootPosition` 을 **호출만** 한다
    //   · 움직임 — 등장/재진입 프레임 스냅, 그 뒤 `silhouetteFollowSpeed` 지수 lerp, 시계 unscaled(:1222-1232)
    //   · 그림 — Spine = idle 우선(없으면 공격) 루프(:1263) · 스프라이트 = **drag 시트**(없으면 idle,
    //     2026-09-15 사용자 결정 :1946) · 알파 `silhouetteAlpha` 고정(:1267 · :1949 — 유효성은 고스트 전담)
    //   · 틀 — 빌보드 Tilted + 캐릭터 틸트 · 스케일 = 유닛 스케일 × 캐릭터 스케일 · 발을 root 원점에(:1246-1275)
    //   · 수명 — 칸이 없으면 숨김(:2094 — 재진입 시 스냅) · 제스처가 끝나면 파기(:1202 · :2113)
    //
    // ⚠ **드롭 순간 실루엣은 사라지고 유닛은 트레이 칸에서 난다.** 비행을 실루엣 자리에서 띄우지
    // 않는 것은 옛 결정이다(unit 9, 2026-08-30 사용자 — `:1515` 「실루엣 모드는 손끝에 유닛이 없다」).
    // `DragPlacementInput` 이 이미 그 출발점(`_pressScreen`)을 넘긴다 — 여기서 바꾸지 않는다.
    //
    // 규칙은 하나도 없다. 유효성(초록/빨강)은 고스트가 말하고, 이 그림은 「이 유닛이 여기 선다」만 말한다.
    [DisallowMultipleComponent]
    public sealed class CoreDragPreviewPresenter : MonoBehaviour
    {
        // 고스트 칸(`CoreMapOverlay` ③)이 `DragPreviewOrder` 를 쓴다. 옛 화면에서 고스트(10001)는
        // 실루엣(20000) **아래**였다 — 같은 값을 주면 같은 order 안의 전후가 정해지지 않아
        // 고스트가 발을 덮는 프레임이 생긴다. 그래서 한 칸 위다(하이라이트 9998 < 고스트 < 실루엣).
        public const int SilhouetteOrder = BoardSortOrder.DragPreviewOrder + 1;

        [SerializeField] private BattleDriver _driver;

        [Tooltip("드래그 실루엣 노브(라이브 SO — ⑮ 그룹). 비어 있으면 실루엣을 그리지 않는다.")]
        [SerializeField] private DragSwaySettings _config;

        [Tooltip("캐릭터 스케일·빌보드 틸트. 유닛 뷰 풀과 같은 자산이어야 실루엣과 착지 유닛의 크기가 같다.")]
        [SerializeField] private CharacterViewConfig _characterView;

        private GameObject _root;
        private int _builtDefIndex = -1;
        private bool _placed;          // false = 다음 프레임 위치 스냅(등장/재진입)
        private bool _wanted;
        private Vector3 _target;
        private Renderer _renderer;
        private readonly Footprint _footprint = new Footprint();

        /// <summary>지금 판 위에 실루엣이 보이는가.</summary>
        public bool IsShowing => _root != null && _root.activeSelf;

        /// <summary>보이는 실루엣의 transform. 없으면 null.</summary>
        public Transform Current => IsShowing ? _root.transform : null;

        /// <summary>실루엣이 따라가는 목표(footprint 발밑의 view 좌표).</summary>
        public Vector3 TargetViewPos => _target;

        /// <summary>실루엣 렌더러의 정렬값. 없으면 int.MinValue.</summary>
        public int RenderSortingOrder => _renderer != null ? _renderer.sortingOrder : int.MinValue;

        /// <summary>
        /// 그 유닛을 그 앵커에 세운다. 매 프레임 불러도 된다. `armed` = 집어 든 뒤의 판 드래그
        /// (옛 두 토글이 제스처마다 따로 있다 — 두 주인의 조건을 한 칸으로 접지 않는다).
        /// </summary>
        public void Show(int defIndex, int2 anchor, bool armed)
        {
            if (_config == null || _driver == null || !_driver.Running) { Hide(); return; }
            if (!(armed ? _config.armedSilhouetteEnabled : _config.dndSilhouetteEnabled)) { Hide(); return; }

            var def = _driver.Definition;
            if (defIndex < 0 || defIndex >= def.Units.Length) { Hide(); return; }

            if (_root != null && _builtDefIndex != defIndex) End();
            if (_root == null && !TryBuild(defIndex)) return;   // 그림 없는 유닛 = 생략(자리는 고스트가 전담)

            _footprint.Anchor = anchor;
            _footprint.Width = math.max(1, def.Units[defIndex].FootprintWidth);
            _footprint.Height = math.max(1, def.Units[defIndex].FootprintHeight);
            _target = (Vector3)Somnia.Battle.Core.BoardSpace.ToView(_footprint.FootPosition(_driver.TileSize));
            _wanted = true;

            if (!_root.activeSelf)
            {
                _root.SetActive(true);
                _placed = false;
            }
            Follow();
        }

        /// <summary>칸이 없다(판 밖·취소 예고). 그림을 내린다 — 다시 들어오면 그 자리에 스냅.</summary>
        public void Hide()
        {
            _wanted = false;
            if (_root != null && _root.activeSelf) _root.SetActive(false);
            _placed = false;
        }

        /// <summary>제스처가 끝났다(드롭·취소·해제). 그림을 파기한다.</summary>
        public void End()
        {
            _wanted = false;
            if (_root != null) Destroy(_root);
            _root = null;
            _renderer = null;
            _builtDefIndex = -1;
            _placed = false;
        }

        private void OnDisable() => End();

        private void LateUpdate()
        {
            if (_wanted && IsShowing) Follow();
        }

        private void Follow()
        {
            float k = _config != null ? _config.silhouetteFollowSpeed : 0f;
            if (!_placed || k <= 0f)
            {
                _root.transform.position = _target;   // 등장/재진입 = 스냅(화면 밖에서 미끄러져 오지 않게)
                _placed = true;
                return;
            }
            // 드래그는 슬로모 중에도 손가락에 실시간으로 답해야 한다 — unscaled.
            float t = 1f - Mathf.Exp(-k * Time.unscaledDeltaTime);
            _root.transform.position = Vector3.Lerp(_root.transform.position, _target, t);
        }

        // ── 그림 ─────────────────────────────────────────────────────────────
        //
        // 백엔드 선택은 뷰 풀(`CoreUnitViewPool.TrySpawn`)과 **같은 게이트**다: 스프라이트 세트에
        // idle 이 있으면 스프라이트, 아니면 스켈레톤, 둘 다 없으면 그리지 않는다.
        private bool TryBuild(int defIndex)
        {
            var assets = _driver.DefenderAssets;
            var unit = defIndex < assets.Count ? assets[defIndex] : null;
            if (unit == null) return false;

            var knobs = new CoreViewKnobs(_characterView, null, null, _driver.TileSize);
            float scale = Mathf.Max(0.01f, unit.SpineVisualScale * knobs.CharacterVisualScale);
            float alpha = Mathf.Clamp01(_config.silhouetteAlpha);

            var set = unit.SpriteMotions;
            bool sprite = set != null && set.HasIdle;
            if (!sprite && unit.SpineSkeletonDataAsset == null) return false;

            var root = new GameObject($"DragSilhouette_{unit.SpineDisplayName}");
            root.transform.SetParent(transform, worldPositionStays: false);
            var billboard = root.AddComponent<Billboard>();
            billboard.Setup(BillboardMode.Tilted, knobs.CharacterBillboardTilt);

            var child = new GameObject($"{root.name}_{(sprite ? "Sprite" : "Spine")}");
            child.transform.SetParent(root.transform, false);
            child.transform.localScale = Vector3.one * scale;

            if (sprite) BuildSprite(child, set, alpha, scale);
            else BuildSpine(child, unit, alpha, scale);

            _root = root;
            _builtDefIndex = defIndex;
            _placed = false;
            return true;
        }

        private void BuildSprite(GameObject child, UnitSpriteMotionSet set, float alpha, float scale)
        {
            var sr = child.AddComponent<SpriteRenderer>();
            sr.flipX = false;   // 스폰 뷰와 같은 초기 방향 — 시트가 그려진 그대로
            sr.sortingOrder = SilhouetteOrder;
            var c = sr.color; c.a = alpha; sr.color = c;
            var player = child.AddComponent<SpriteFlipbookPlayer>();
            player.TimeDomain = TimeDomain.Interaction;   // 드래그는 슬로모 중에도 실시간
            var clip = set.ResolveDrag();
            if (clip != null) player.Play(clip);
            if (sr.sprite != null)
            {
                var b = sr.sprite.bounds;   // 발을 root 원점에
                child.transform.localPosition = new Vector3(-b.center.x * scale, -b.min.y * scale, 0f);
            }
            _renderer = sr;
        }

        private void BuildSpine(GameObject child, DefenderUnitData unit, float alpha, float scale)
        {
            var components = SkeletonAnimation.AddToGameObject(child, null);
            var skeleton = components.skeletonAnimation;
            components.skeletonRenderer.SkeletonDataAsset = unit.SpineSkeletonDataAsset;
            components.skeletonRenderer.InitialSkinName =
                string.IsNullOrEmpty(unit.SpineSkinName) ? "default" : unit.SpineSkinName;
            skeleton.Initialize(true);
            if (skeleton.Skeleton != null)
            {
                SpineCombinedSkinCache.Apply(skeleton.Skeleton, unit);
                // 서 있는 그림 — idle 우선(키링의 드래그 버둥 애니가 아니다).
                string anim = FirstAnimation(skeleton, unit.idleAnimation, unit.attackAnimation);
                if (anim != null) skeleton.AnimationState.SetAnimation(0, anim, true);
                var color = skeleton.Skeleton.GetColor();
                color.a = alpha;
                skeleton.Skeleton.SetColor(color);
            }

            var mr = child.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = SilhouetteOrder;
                var lb = mr.localBounds;
                if (lb.size.y > 0.01f)   // bounds 퇴화면 원점(Spine 발) 그대로
                    child.transform.localPosition = new Vector3(-lb.center.x * scale, -lb.min.y * scale, 0f);
            }
            _renderer = mr;
        }

        private static string FirstAnimation(SkeletonAnimation skeleton, params string[] candidates)
        {
            var data = skeleton.Skeleton != null ? skeleton.Skeleton.Data : null;
            if (data == null) return null;
            foreach (var name in candidates)
                if (!string.IsNullOrEmpty(name) && data.FindAnimation(name) != null) return name;
            return null;
        }
    }
}
