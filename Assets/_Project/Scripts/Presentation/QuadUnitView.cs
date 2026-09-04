using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;

namespace Wassup.Presentation
{
    [DisallowMultipleComponent]
    public class QuadUnitView : MonoBehaviour
    {
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Transform _visual;       // tilted-billboard unit 4a: 발 피벗 자식.
        private Material _ownedMaterial; // unlit 복사본 (transform 이 방향을 제어). 직접 소유/파괴.
        private Color _baseColor = Color.white; // unit-health-display unit 1 — 틴트 곱 기준(원본 base color).
        private Entity _entity;
        // tilemap-view-backend unit 3 — sim 좌표 보존 (sorting 셀 역산용; view 좌표는 z 소실).
        private Vector3 _simWorld;
        // placement-enemy-see-through unit 1 — dim 상태(재스폰 시 Configure 가 리셋).
        private bool _transparentApplied;
        private float _dimAlpha = 1f;
        private BlobShadow _blob;

        public Entity Entity => _entity;

        // unit-overhead-ui — SpineUnitView와 동일한 실제 실루엣 screen bounds 계약.
        public bool TryGetScreenRect(Camera cam, out Rect rect)
        {
            rect = default;
            if (cam == null || _renderer == null) return false;
            var b = _renderer.bounds;
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 hi = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var sp = cam.WorldToScreenPoint(corner);
                if (sp.z <= 0f) return false;
                lo = Vector2.Min(lo, new Vector2(sp.x, sp.y));
                hi = Vector2.Max(hi, new Vector2(sp.x, sp.y));
            }
            rect = Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
            return rect.width > 0f && rect.height > 0f;
        }

        // unit 15 — bodyRadiusTiles = 판정 몸 반경(타일). 블롭 지름 = 2r(그림자는 UI 다).
        public void Configure(Entity entity, Mesh mesh, Material material, float visualScale,
            float bodyRadiusTiles)
        {
            _entity = entity;
            Mesh quad = mesh != null ? mesh : Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            // flight-lift-feel unit 1 — 비행 확대의 복귀 기준. 스케일 쓰기는 ApplyRenderScale 단일 지점.
            _baseScale = Vector3.one * Mathf.Max(0.01f, visualScale);
            ApplyRenderScale();                // 계약 4 — localScale 직접 대입은 여기서도 하지 않는다

            // tilted-billboard unit 4a — Spine 과 동일 메커니즘: object-space unlit + Billboard transform 틸트.
            // 발 피벗: Quad(센터, Y −0.5..0.5)를 +0.5 올려 밑동을 root 원점(셀)에 둔다 → 틸트가 발 기준.
            EnsureVisualChild(quad);
            _ownedMaterial = BuildUnlitMaterial(material, _ownedMaterial);
            _baseColor = _ownedMaterial.HasProperty("_BaseColor")
                ? _ownedMaterial.GetColor("_BaseColor")
                : Color.white;
            _renderer.sharedMaterial = _ownedMaterial;

            var billboard = GetComponent<Billboard>();
            if (billboard == null) billboard = gameObject.AddComponent<Billboard>();
            billboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

            // 리뷰 L-5 — 재호출 시 이전 블롭을 남기지 않는다(머티리얼과 같은 규율).
            // 오늘 풀이 인스턴스당 1회만 부르지만, 이 메서드의 다른 줄들은 이미 재호출을 전제한다.
            // 두 분기 **앞**에 둔다 — 진짜 그림자 모드로 전환된 재호출에서도 옛 블롭이 남지 않게.
            if (_blob != null) Destroy(_blob.gameObject);
            // tilemap-real-shadows — 진짜 그림자(실루엣 cast) vs 블롭(상호배타).
            if (BattleBridge.UseRealShadows)
            {
                // URP/Unlit + _ALPHATEST_ON → ShadowCaster 가 실루엣 cast. 평면이라 TwoSided.
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            }
            else
            {
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (BattleBridge.BlobShadowSprite != null)
                    // 리뷰 L-2 — `Mathf.Max(0.05f, r)` 클램프는 **삭제**. Spine 경로에 없는 비대칭이었고,
                    // 발동하면 몸이 0 인 유닛에 0.1칸 그림자를 그려 «없는 몸»을 화면이 주장한다.
                    // 몸 0 저작을 막는 것은 뷰가 아니라 저작 검증의 일이다.
                    _blob = BlobShadow.Attach(transform, BattleBridge.BlobShadowSprite,
                        2f * bodyRadiusTiles * BattleBridge.BlobShadowSize,
                        BattleBridge.BlobShadowColor,
                        BattleBridge.BlobShadowLift, BoardSortOrder.ShadowOrder, live: true); // 유닛은 이동 — 매 프레임 따라감
            }

            // 재스폰/머티리얼 리빌드는 cutout 기준으로 되돌린다 → 다음 SetDimmed 가 정확히 재적용.
            _transparentApplied = false;
            _dimAlpha = 1f;
        }

        private void EnsureVisualChild(Mesh quad)
        {
            if (_visual == null)
            {
                var go = new GameObject("Visual");
                _visual = go.transform;
                _visual.SetParent(transform, false);
                _visual.localPosition = new Vector3(0f, 0.5f, 0f); // 발 피벗
                _filter = go.AddComponent<MeshFilter>();
                _renderer = go.AddComponent<MeshRenderer>();
            }
            _filter.sharedMesh = quad;
        }

        // 전달받은(공유일 수 있는) 머티리얼을 변조하지 않고, object-space URP/Unlit 복사본을 소유한다.
        private static Material BuildUnlitMaterial(Material source, Material previousOwned)
        {
            if (previousOwned != null) Destroy(previousOwned);
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            Texture tex = source != null
                ? (source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture)
                : null;
            Color col = source != null && source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            // 알파 컷아웃 + 양면(빌보드 메시 뒷면도 보이게)
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // Cull Off
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            return m;
        }

        public void UpdatePosition(Vector3 world)
        {
            _simWorld = world;
            Vector3 view = Wassup.Core.BoardSpace.ToView(world);
            // 타일맵이 XZ 바닥. 빌보드 밑동이 바닥 Y 와 같으면 z-fighting → 살짝 띄운다.
            view.y += 0.01f + _flightHeight;
            transform.position = view;
            // flight-lift-feel unit 1 — hop 이 없는 뷰라 lift = _flightHeight 그대로.
            ApplyLift(_flightHeight);
        }

        // flight-lift-feel unit 1 — SpineUnitView 와 같은 계약(lift → 확대 + 그림자 축소·페이드).
        // 펀치·스쿼시가 없는 폴백 뷰라 슬롯은 비행 배율 하나뿐이다.
        private Vector3 _baseScale = Vector3.one;
        private float _flightScale = 1f;

        private void ApplyLift(float lift)
        {
            UnitLiftVisual.Resolve(lift, out float unitScale, out float shadowScale, out float shadowAlpha);
            _flightScale = unitScale;
            ApplyRenderScale();
            if (_blob != null) _blob.SetFlight(shadowScale, shadowAlpha);
        }

        private void ApplyRenderScale() => transform.localScale = _baseScale * _flightScale;

        // boss-jjangssen unit 7 — 뷰 비행 아치 높이. SpineUnitView 와 동일 계약:
        // ToView 는 sim-Y 를 버리므로 높이는 **변환 뒤** view 공간에서 더한다.
        // 매 프레임 피드가 값을 다시 쓰므로(비행 아니면 0) 별도 clear 불필요.
        private float _flightHeight;

        public void SetFlightHeight(float viewSpaceHeight) => _flightHeight = viewSpaceHeight;

        // unit-health-display unit 1 — 적 저체력 틴트(fallback quad 뷰). 원본 base color × tint.
        // Configure 가 매번 머티리얼을 새로 빌드하므로 재스폰 시 자동으로 무틴트로 리셋된다.
        public void SetHealthTint(Color tint)
        {
            if (_ownedMaterial == null || !_ownedMaterial.HasProperty("_BaseColor")) return;
            Color c = _baseColor * tint;
            c.a = _baseColor.a * _dimAlpha; // dim 알파는 tint 와 독립으로 접어 넣는다(매 프레임 재적용).
            _ownedMaterial.SetColor("_BaseColor", c);
        }

        // placement-enemy-see-through unit 1 — 드래그 배치 중 반투명 전환.
        // cutout(_ALPHATEST_ON) 은 알파를 낮춰도 안 비치므로 dim 동안 transparent 블렌드로 전환한다.
        // 블렌드 상태 플립은 변경 시 1회, 알파는 SetHealthTint(매 프레임)가 _BaseColor.a 로 반영.
        public void SetDimmed(bool transparent, float alpha)
        {
            _dimAlpha = Mathf.Clamp01(alpha);
            if (transparent != _transparentApplied && _ownedMaterial != null)
            {
                if (transparent)
                {
                    _ownedMaterial.SetFloat("_Surface", 1f);
                    _ownedMaterial.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _ownedMaterial.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _ownedMaterial.SetFloat("_ZWrite", 0f);
                    _ownedMaterial.SetFloat("_AlphaClip", 0f);
                    _ownedMaterial.DisableKeyword("_ALPHATEST_ON");
                    _ownedMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    _ownedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    if (_renderer != null && BattleBridge.UseRealShadows)
                        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else
                {
                    _ownedMaterial.SetFloat("_Surface", 0f);
                    _ownedMaterial.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    _ownedMaterial.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    _ownedMaterial.SetFloat("_ZWrite", 1f);
                    _ownedMaterial.SetFloat("_AlphaClip", 1f);
                    _ownedMaterial.EnableKeyword("_ALPHATEST_ON");
                    _ownedMaterial.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    _ownedMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    if (_renderer != null)
                        _renderer.shadowCastingMode = BattleBridge.UseRealShadows
                            ? UnityEngine.Rendering.ShadowCastingMode.TwoSided
                            : UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                _transparentApplied = transparent;
            }
            _blob?.SetDimAlpha(transparent ? _dimAlpha : 1f);
        }

        public void SetSortingOrder(int order)
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            // tilted-billboard unit 8 — 블롭은 자기 대역(ShadowOrder)을 소유한다. 이 스윕은 매
            // 프레임 돌아, 빼지 않으면 Attach 가 세운 -5 가 캐릭터 대역으로 덮여 복원되지 않는다.
            // 캐시 참조 비교인 이유는 SpineUnitView.UpdateSortingOrder 주석 참조.
            Transform blobRoot = _blob != null ? _blob.transform : null;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (blobRoot != null && renderers[i].transform == blobRoot) continue;
                renderers[i].sortingOrder = order;
            }
        }

        public void UpdateSortingOrder(Unity.Mathematics.int2 gridSize, float tileSize)
        {
            // sim 좌표로 셀 역산 — view 좌표(transform.position)는 z 소실로 행 정렬 붕괴.
            SetSortingOrder(BoardSortOrder.ComputeFromWorld(
                gridSize,
                _simWorld,
                tileSize,
                BoardSortOrder.CharacterOffset));
        }

        private void OnDestroy()
        {
            if (_ownedMaterial != null) Destroy(_ownedMaterial);
        }
    }
}
