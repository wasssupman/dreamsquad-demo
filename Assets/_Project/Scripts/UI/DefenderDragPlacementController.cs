using System.Collections;
using Spine.Unity;
using TMPro;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Presentation;
using Wassup.Rendering;

namespace Wassup.UI
{
    public class DefenderDragPlacementController : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private PlacementInput placementInput;
        [SerializeField] private float previewHeight = 0.35f;
        [SerializeField] private float previewScale = 0.65f;
        // time-manager Unit 5 — 드래그 배치 중 전투만 이 배율로 느려진다. 드래그 프리뷰/입력은
        // Interaction 도메인(unscaledDeltaTime)이라 실시간 유지된다. 0=정지, 1=영향 없음.
        [SerializeField, Range(0f, 1f)] private float dragSlowmoScale = 0.2f;

        // 드래그 프리뷰 키링 튜닝값은 DragSwaySettings SO 에서 온다. 컨트롤러가 런타임 AddComponent 라
        // 인스펙터 튜닝이 안 되므로 SO 로 분리 — DefenderSelector 에 할당하면 Configure 로 주입. 미주입 시 기본값.
        private DragSwaySettings _cfg;
        private DragSwaySettings Cfg => _cfg != null ? _cfg : (_cfg = ScriptableObject.CreateInstance<DragSwaySettings>());

        private const int RingSegments = 14;

        private DragSession _session;
        private TimeLease _slowmoLease; // time-manager Unit 5 — 드래그 중 Battle 슬로우모 lease
        private Material _previewMaterial; // 폴백 capsule 용
        private Material _cordMaterial;    // 줄/고리 공유(세션마다 생성 금지)
        // action-tray unit 4 — 드래그 중 거부 사유 라벨(포인터 추종 오버레이).
        // 색만으로 알리지 않는다: 사유별 글리프+한글 label+색 3중 표기.
        private TMP_FontAsset _uiFont;
        private GameObject _rejectCanvasGO;
        private TextMeshProUGUI _rejectLabel;
        private Vector2 _lastScreenPos;

        // 키링 배치 상태: 고리 = 손가락(공중). 유닛 = 보드에 서서 무게추처럼 스프링 지연으로 뒤따라옴.
        private Vector3 _ringWorld;        // 고리(손가락, 공중)
        private Vector3 _unitTargetWorld;  // 유닛 발 목표(고리 바로 아래 보드) — 손가락 즉시 추종
        private Vector3 _unitPosWorld;     // 유닛 발 실제(스프링 지연)
        private Vector3 _unitVelWorld;
        private bool _posInit;
        private bool _onBoard;

        private struct DragSession
        {
            public bool active;
            public DefenderUnitData unit;
            public GameObject preview;      // root(scale 1). 자식이 고리/줄/실루엣.
            public LineRenderer cordLine;
            public Transform ring;
            public Transform endNode;       // 빌보드. 유닛 머리 위치.
            public Transform swingPivot;    // 머리 중심 기울임.
            public Transform spineChild;
            public float visualScale;
            public float unitHeight;        // 실루엣 월드 높이(발→머리). 머리 오프셋용.
            public Vector2Int? hoverTile;
            public bool isValidTile;
            // action-tray unit 4 — 마지막 hover 판정의 거부 사유(유효 칸이면 None).
            public PlacementRejectReason rejectReason;
        }

        public void Configure(BattleBridge battleBridge, Camera camera, PlacementInput input,
            DragSwaySettings swaySettings = null, TMP_FontAsset uiFont = null)
        {
            bridge = battleBridge;
            mainCamera = camera != null ? camera : Camera.main;
            placementInput = input;
            if (swaySettings != null) _cfg = swaySettings;
            if (uiFont != null) _uiFont = uiFont;
        }

        public void BeginDrag(DefenderUnitData unitData, Vector2 screenPosition)
        {
            if (unitData == null || bridge == null) return;
            CleanupSession();
            // time-manager Unit 5 — 드래그 시작 시 전투만 슬로우모. 드롭/취소 시 CleanupSession 에서 해제.
            _slowmoLease = TimeManager.Instance.Request(TimeDomain.Battle, dragSlowmoScale);
            if (mainCamera == null) mainCamera = Camera.main;

            _session = BuildSession(unitData);
            bridge?.SetEnemiesDimmed(true); // placement-enemy-see-through — 적 반투명 on
            bridge?.SetPlacementHighlightAboveUnits(true); // unit 6 — 배치 하이라이트를 적 위로
            if (placementInput != null) placementInput.SetClickPlacementEnabled(false);
            UpdateDrag(screenPosition);
        }

        private void Update()
        {
            if (!_session.active || _session.preview == null || _session.endNode == null || mainCamera == null) return;
            if (!_onBoard || !_posInit) return;
            var s = Cfg;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            var camT = mainCamera.transform;

            // 무게추 스프링(탄성) + 속도 상한: spring/damping 으로 지연·탄성을 유지하고, maxSpeed 로 빠른
            // 스와이프 시 속도만 제한 → 초기 튀어나감만 방지(탄성 자체는 spring/damping 으로 조절).
            KeyringSim.SpringStep(ref _unitPosWorld, ref _unitVelWorld, _unitTargetWorld,
                s.spring, s.damping, s.maxSpeed, dt);

            // camera-direction unit 5 rev 3 — 드래그 포커스 피드 = **터치/포인터 스크린 좌표 그대로**
            // (고리/유닛 월드 좌표 아님 — 카메라 되먹임·스프링 출렁임 원천 차단, 스무딩은 Director
            // 쪽 스프링-댐핑). 매 프레임 피드가 계약 — 끊기면(오프보드/세션 종료/파괴) staleness 해제.
            EnsureCameraDirector()?.SetDragFocus(_lastScreenPos);

            // 배치: 고리(공중) · 유닛 머리(발+높이) · 줄(고리→머리).
            if (_session.ring != null) _session.ring.position = _ringWorld;
            Vector3 headPos = _unitPosWorld + camT.up * _session.unitHeight;
            _session.endNode.position = headPos;

            // 유닛 기울임: 줄(고리→머리) 방향으로 기움(뒤로 처질수록 기욺). clamp maxAngle.
            if (_session.swingPivot != null)
            {
                Vector3 toRing = (_ringWorld - headPos).normalized; // 머리→고리 = 유닛 up 방향
                float lean = KeyringSim.LeanAngle(
                    Vector3.Dot(toRing, camT.right), Vector3.Dot(toRing, camT.up), s.maxAngle);
                _session.swingPivot.localRotation = Quaternion.Euler(0f, 0f, lean);
            }

            if (_session.cordLine != null)
            {
                if (_session.cordLine.positionCount != 2) _session.cordLine.positionCount = 2;
                _session.cordLine.SetPosition(0, _ringWorld);
                _session.cordLine.SetPosition(1, headPos);
            }

            // 하이라이트 = 마우스 바로 아래(안정) 칸. 흔들리는 유닛 위치가 아니라 마우스가 클램프 →
            // 유닛은 좌우로 흔들려도 배치 대상 칸은 고정(게임 배치 정확도). 유닛은 그 칸 위에서 흔들린다.
            UpdateHoverAtTarget();
        }

        // camera-direction unit 5 — Director 캐시 (miss 캐시 + 1회 경고, 기존 패턴).
        private Wassup.Presentation.CameraDirector _cameraDirector;
        private bool _cameraDirectorMissWarned;

        private Wassup.Presentation.CameraDirector EnsureCameraDirector()
        {
            if (_cameraDirector != null) return _cameraDirector;
            if (_cameraDirectorMissWarned) return null;
            if (mainCamera == null) return null;
            _cameraDirector = mainCamera.GetComponent<Wassup.Presentation.CameraDirector>();
            if (_cameraDirector == null)
            {
                Debug.LogWarning("[DefenderDragPlacementController] CameraDirector 미배선 — 드래그 포커스 생략.", this);
                _cameraDirectorMissWarned = true;
            }
            return _cameraDirector;
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (!_session.active) return;
            _lastScreenPos = screenPosition; // unit 4 — 거부 라벨 포인터 추종
            // 발↔고리 화면 세로 거리 = 유닛 키 + 줄 길이. 고리는 손가락에, 유닛은 그만큼 화면 아래 보드에.
            float totalDrop = _session.unitHeight + Cfg.ropeLength * _session.visualScale;

            if (TryComputeRingUnit(screenPosition, totalDrop, out Vector3 ringW, out Vector3 unitTargetW))
            {
                _ringWorld = ringW;
                _unitTargetWorld = unitTargetW;
                if (!_posInit) { _unitPosWorld = unitTargetW; _unitVelWorld = Vector3.zero; _posInit = true; }
                _onBoard = true;
                if (_session.preview != null && !_session.preview.activeSelf) _session.preview.SetActive(true);
            }
            else
            {
                _onBoard = false;
                ClearHover();
                if (_session.preview != null) _session.preview.SetActive(false);
            }
        }

        // 손가락 ray → 고리(손가락 위치) + 유닛 발 목표. 수직 분리는 카메라-up(화면 세로) 기준:
        // 고리는 손가락 ray 위, 발은 고리보다 화면상 totalDrop 아래이면서 보드 평면 위에 놓이도록 s 를 푼다.
        // (월드-up 으로 올리면 기울어진 카메라에서 화면상 거의 안 올라가 고리·유닛이 겹친다.)
        private bool TryComputeRingUnit(Vector2 screenPos, float totalDrop, out Vector3 ringW, out Vector3 unitTargetW)
        {
            ringW = default; unitTargetW = default;
            if (mainCamera == null) return false;
            var ray = mainCamera.ScreenPointToRay(screenPos);
            var boardPlane = BoardSpace.RaycastPlane();
            var camT = mainCamera.transform;
            Vector3 N = boardPlane.normal;
            float nd = Vector3.Dot(N, ray.direction);
            if (Mathf.Abs(nd) < 1e-6f) return false;
            // ring = camPos + s*rayDir(손가락 위), feet = ring - camUp*totalDrop 가 boardPlane 위가 되는 s.
            float s = -(Vector3.Dot(N, camT.position - camT.up * totalDrop) + boardPlane.distance) / nd;
            if (s <= 0f) return false;
            ringW = camT.position + ray.direction * s;
            Vector3 feet = ringW - camT.up * totalDrop;
            Vector3 nUp = N.normalized;
            if (Vector3.Dot(nUp, camT.position - feet) < 0f) nUp = -nUp;
            unitTargetW = feet + nUp * previewHeight; // 발 = 보드 표면 + 살짝 띄움
            return true;
        }

        private void UpdateHoverAtTarget()
        {
            // 스윙하는 _unitPosWorld 가 아니라 마우스 바로 아래 목표(_unitTargetWorld) 로 칸을 정한다 → 흔들림 없이 안정.
            var sim = BoardSpace.ToSim(_unitTargetWorld);
            Vector2Int cell;
            if (bridge != null)
            {
                var c = bridge.DebugWorldToCell((Vector3)sim);
                cell = new Vector2Int(c.x, c.y);
            }
            else
            {
                cell = new Vector2Int(Mathf.FloorToInt(sim.x + 0.5f), Mathf.FloorToInt(sim.z + 0.5f));
            }
            // action-tray unit 4 — reason 을 버리지 않고 세션에 보관, 라벨로 구분 표기.
            var reason = PlacementRejectReason.None;
            bool valid = bridge != null && bridge.CanPlaceDefenderAt(cell.x, cell.y, _session.unit, out reason);
            _session.rejectReason = valid ? PlacementRejectReason.None : reason;
            SetHover(cell, valid);
            UpdateRejectLabel();
        }

        // action-tray unit 4 — 사유 매핑: coral X(비용) / amber ■(점유) / neutral —(불가).
        private void UpdateRejectLabel()
        {
            bool show = _session.active && _onBoard && _session.hoverTile.HasValue
                        && !_session.isValidTile && _session.rejectReason != PlacementRejectReason.None;
            if (!show)
            {
                if (_rejectLabel != null && _rejectLabel.gameObject.activeSelf)
                    _rejectLabel.gameObject.SetActive(false);
                return;
            }

            EnsureRejectLabel();
            string text;
            Color color;
            switch (_session.rejectReason)
            {
                case PlacementRejectReason.InsufficientCost:
                    text = "X 코스트 부족"; color = new Color(1f, 0.42f, 0.36f, 1f); break;
                case PlacementRejectReason.Occupied:
                    text = "■ 점유됨"; color = new Color(1f, 0.76f, 0.30f, 1f); break;
                default:
                    text = "— 배치 불가"; color = new Color(0.82f, 0.83f, 0.88f, 1f); break;
            }
            if (!_rejectLabel.gameObject.activeSelf) _rejectLabel.gameObject.SetActive(true);
            _rejectLabel.text = text;
            _rejectLabel.color = color;
            _rejectLabel.transform.position = new Vector3(_lastScreenPos.x, _lastScreenPos.y + 96f, 0f);
        }

        private void EnsureRejectLabel()
        {
            if (_rejectLabel != null) return;
            _rejectCanvasGO = new GameObject("DragRejectCanvas", typeof(Canvas));
            _rejectCanvasGO.transform.SetParent(transform, false);
            var canvas = _rejectCanvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20001; // 드래그 프리뷰(20000) 위
            var go = new GameObject("RejectLabel", typeof(RectTransform));
            go.transform.SetParent(_rejectCanvasGO.transform, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(300f, 40f);
            _rejectLabel = go.AddComponent<TextMeshProUGUI>();
            if (_uiFont != null) _rejectLabel.font = _uiFont;
            _rejectLabel.fontSize = 26f;
            _rejectLabel.fontStyle = FontStyles.Bold;
            _rejectLabel.alignment = TextAlignmentOptions.Center;
            _rejectLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _rejectLabel.raycastTarget = false;
            var mat = _rejectLabel.fontMaterial;
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 0.9f));
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.16f);
            go.SetActive(false);
        }

        public void EndDrag(Vector2 screenPosition)
        {
            if (!_session.active) return;
            UpdateDrag(screenPosition);

            var session = _session;
            if (session.hoverTile.HasValue && session.isValidTile)
            {
                var cell = session.hoverTile.Value;
                if (bridge.TryBeginDefenderDeployment(cell.x, cell.y, session.unit, out var entity))
                {
                    CleanupSession();
                    StartCoroutine(RunDeployment(session.unit, cell, entity));
                    return;
                }
            }

            if (session.hoverTile.HasValue)
                bridge?.FlashPlacementReject(session.hoverTile.Value);
            CleanupSession();
        }

        private IEnumerator RunDeployment(DefenderUnitData unitData, Vector2Int cell, Unity.Entities.Entity entity)
        {
            float duration = 0f;
            if (bridge != null)
            {
                try
                {
                    duration = bridge.PlayDeploymentPresentation(unitData, cell, entity);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }

            if (duration > 0f) yield return new WaitForSeconds(duration);
            float skillDelay = unitData != null ? Mathf.Max(0f, unitData.placementSkillDelay) : 0f;
            if (skillDelay > 0f) yield return new WaitForSeconds(skillDelay);
            bridge?.ActivateDeployedDefender(cell, entity);
        }

        private DragSession BuildSession(DefenderUnitData unitData)
        {
            var session = new DragSession { active = true, unit = unitData };
            if (TryBuildKeyringPreview(unitData, ref session))
                return session;
            session.preview = CreateFallbackPreview(unitData);
            return session;
        }

        private bool TryBuildKeyringPreview(DefenderUnitData unitData, ref DragSession session)
        {
            if (unitData == null || unitData.skeletonDataAsset == null) return false;

            float scale = Mathf.Max(0.01f, unitData.spineVisualScale * BattleBridge.CharacterVisualScale);

            var root = new GameObject($"DragPreview_{unitData.displayName}");
            var st = Cfg.style; // keyring-unify 3 — 스타일. null/슬롯 null = 절차적 폴백.

            // 고리(ring): 스타일 스프라이트가 있으면 SpriteRenderer(홀로), 없으면 로컬 원 LineRenderer 루프.
            var ringGo = new GameObject($"{root.name}_Ring");
            ringGo.transform.SetParent(root.transform, false);
            if (st != null && st.ringSprite != null)
            {
                var ringSr = ringGo.AddComponent<SpriteRenderer>();
                ringSr.sprite = st.ringSprite;
                if (st.worldRingMaterial != null) ringSr.sharedMaterial = st.worldRingMaterial;
                ringSr.color = Color.white; // 계약 7 — 스타일 적용 시 틴트 중성화(cordColor 갈색 오염 방지)
                ringSr.sortingOrder = BoardSortOrder.DragPreviewOrder;
                // 지름 = ringRadius*2 — 절차적 원(반경 ringRadius*scale)과 크기 등가.
                float spriteWidth = st.ringSprite.bounds.size.x;
                if (spriteWidth > 1e-4f)
                    ringGo.transform.localScale = Vector3.one * (Cfg.ringRadius * 2f * scale / spriteWidth);
            }
            else
            {
                var ringLr = ringGo.AddComponent<LineRenderer>();
                ringLr.useWorldSpace = false;
                ringLr.loop = true;
                ringLr.numCapVertices = 2;
                ringLr.positionCount = RingSegments;
                for (int i = 0; i < RingSegments; i++)
                {
                    float a = (i / (float)RingSegments) * Mathf.PI * 2f;
                    ringLr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (Cfg.ringRadius * scale));
                }
                ringLr.sharedMaterial = CordMaterial();
                ringLr.widthMultiplier = Cfg.cordWidth * scale;
                ringLr.startColor = ringLr.endColor = Cfg.cordColor;
                ringLr.sortingOrder = BoardSortOrder.DragPreviewOrder;
            }
            var ringBillboard = ringGo.AddComponent<Billboard>();
            ringBillboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

            // 줄(cord): 월드 LineRenderer, 2점(고리→머리). 스타일 머티리얼(u=길이, _LengthAxis=1) 또는 절차적 단색.
            var cordGo = new GameObject($"{root.name}_Cord");
            cordGo.transform.SetParent(root.transform, false);
            var cordLr = cordGo.AddComponent<LineRenderer>();
            cordLr.useWorldSpace = true;
            cordLr.numCapVertices = 2;
            cordLr.positionCount = 2;
            bool styledCord = st != null && st.worldCordMaterial != null;
            cordLr.sharedMaterial = styledCord ? st.worldCordMaterial : CordMaterial();
            cordLr.widthMultiplier = Cfg.cordWidth * scale;
            cordLr.startColor = cordLr.endColor = styledCord ? Color.white : Cfg.cordColor;
            cordLr.sortingOrder = BoardSortOrder.DragPreviewOrder - 1;

            // endNode(머리 위치, 빌보드) → swingPivot(머리 중심 기울임) → spineChild(실루엣).
            var endNode = new GameObject($"{root.name}_End");
            endNode.transform.SetParent(root.transform, false);
            var endBillboard = endNode.AddComponent<Billboard>();
            endBillboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

            var swingPivot = new GameObject($"{root.name}_Swing");
            swingPivot.transform.SetParent(endNode.transform, false);

            var spineChild = new GameObject($"{root.name}_Spine");
            spineChild.transform.SetParent(swingPivot.transform, false);
            spineChild.transform.localScale = Vector3.one * scale;

            var skeleton = spineChild.AddComponent<SkeletonAnimation>();
            skeleton.skeletonDataAsset = unitData.skeletonDataAsset;
            skeleton.initialSkinName = string.IsNullOrEmpty(unitData.spineSkinName) ? "default" : unitData.spineSkinName;
            skeleton.Initialize(true);

            // unit-parts-appearance 1 — 스폰 경로(SpineUnitView)와 동일한 공용 헬퍼로 일원화.
            if (skeleton.Skeleton != null)
                SpineCombinedSkinCache.Apply(skeleton.Skeleton, unitData);

            string animation = ResolveAnimation(skeleton, unitData.dragAnimation, unitData.idleAnimation, unitData.attackAnimation);
            if (!string.IsNullOrEmpty(animation))
                skeleton.AnimationState.SetAnimation(0, animation, true);

            SetPreviewAlpha(skeleton, 1f); // placement-enemy-see-through unit 5 — 드래그 유닛은 불투명(적만 투명해져, 배치 유닛이 최상단 초점)
            var skelRenderer = skeleton.GetComponent<MeshRenderer>();
            if (skelRenderer != null) skelRenderer.sortingOrder = BoardSortOrder.DragPreviewOrder;

            // 실루엣 머리(mesh 상단)를 endNode(=머리 위치)에 자동정렬 — 몸통이 아래로 서고, 발이 보드에 닿는다.
            float unitHeight = scale; // 폴백
            Vector3 charmPos = Vector3.down * Cfg.charmDrop;
            if (skelRenderer != null && skelRenderer.localBounds.size.y > 0.01f)
            {
                var lb = skelRenderer.localBounds;
                charmPos += new Vector3(-lb.center.x * scale, -lb.max.y * scale, 0f);
                unitHeight = lb.size.y * scale;
            }
            spineChild.transform.localPosition = charmPos;

            session.preview = root;
            session.cordLine = cordLr;
            session.ring = ringGo.transform;
            session.endNode = endNode.transform;
            session.swingPivot = swingPivot.transform;
            session.spineChild = spineChild.transform;
            session.visualScale = scale;
            session.unitHeight = unitHeight;
            return true;
        }

        private Material CordMaterial()
        {
            if (_cordMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                _cordMaterial = new Material(shader) { name = "KeyringCordMat" };
            }
            return _cordMaterial;
        }

        private static string ResolveAnimation(SkeletonAnimation skeleton, params string[] candidates)
        {
            if (skeleton == null || skeleton.Skeleton == null || skeleton.Skeleton.Data == null) return null;
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                if (skeleton.Skeleton.Data.FindAnimation(candidate) != null)
                    return candidate;
            }
            return null;
        }

        private static void SetPreviewAlpha(SkeletonAnimation skeleton, float alpha)
        {
            if (skeleton == null || skeleton.Skeleton == null) return;
            var color = skeleton.Skeleton.GetColor();
            color.a = alpha;
            skeleton.Skeleton.SetColor(color);
        }

        private GameObject CreateFallbackPreview(DefenderUnitData unitData)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = $"DragPreview_{unitData.displayName}";
            go.transform.localScale = Vector3.one * (previewScale * BattleBridge.CharacterVisualScale);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (_previewMaterial == null)
                {
                    _previewMaterial = RuntimeMaterialFactory.CreateTransparent(Color.white);
                }
                Color color = Color.white;
                if (unitData.visualMaterial != null && unitData.visualMaterial.HasProperty("_BaseColor"))
                    color = unitData.visualMaterial.GetColor("_BaseColor");
                color.a = 1f; // placement-enemy-see-through unit 5 — 폴백 프리뷰도 불투명
                RuntimeMaterialFactory.ApplyColor(_previewMaterial, color);
                renderer.sharedMaterial = _previewMaterial;
            }
            return go;
        }

        private void SetHover(Vector2Int cell, bool valid)
        {
            bool changed = !_session.hoverTile.HasValue || _session.hoverTile.Value != cell;
            if (_session.hoverTile.HasValue && _session.hoverTile.Value != cell)
                bridge?.ClearPlacementHover(_session.hoverTile.Value);

            _session.hoverTile = cell;
            _session.isValidTile = valid;
            if (_session.preview != null && !_session.preview.activeSelf)
                _session.preview.SetActive(true);
            bridge?.SetPlacementHover(cell, valid);
            if (changed) bridge?.SetPlacementRange(cell, _session.unit);
        }

        private void ClearHover()
        {
            if (_session.hoverTile.HasValue)
                bridge?.ClearPlacementHover(_session.hoverTile.Value);
            bridge?.ClearPlacementRange();
            _session.hoverTile = null;
            _session.isValidTile = false;
            _session.rejectReason = PlacementRejectReason.None; // unit 4
            if (_rejectLabel != null && _rejectLabel.gameObject.activeSelf)
                _rejectLabel.gameObject.SetActive(false);
        }

        private void CleanupSession()
        {
            _slowmoLease.Dispose(); // time-manager Unit 5 — 슬로우모 해제(멱등)
            bridge?.SetEnemiesDimmed(false); // placement-enemy-see-through — 적 반투명 off(드롭·거부·비활성 모든 종료 경유)
            bridge?.SetPlacementHighlightAboveUnits(false); // unit 6 — 하이라이트 소팅 원복
            ClearHover();
            bridge?.ClearPlacementRange();
            if (_session.preview != null) Destroy(_session.preview);
            _session = default;
            _posInit = false;
            _onBoard = false;
            _unitVelWorld = Vector3.zero;
            // ui-tweak 2026-07-08 — 클릭 배치 은퇴. 드래그 종료 후 재활성화하지 않는다.
        }

        private void OnDisable()
        {
            CleanupSession();
        }

        private void OnDestroy()
        {
            CleanupSession();
            if (_previewMaterial != null) Destroy(_previewMaterial);
            if (_cordMaterial != null) Destroy(_cordMaterial);
        }
    }
}
