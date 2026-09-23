using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.Data;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild 5b 수정 — **배치 비행.** 옛 `DefenderDragPlacementController` 의
    // 드롭 하마(`StartDropDismount`·`RunDropDismount`, `defender-drop-dismount` +
    // `flight-lift-feel`)의 후계다.
    //
    // 사용자 플레이 1차의 문장: **「유닛이 툭 생긴다」**. 5b 는 드롭 → 착지를 `DragPlacementInput`
    // 안의 **타이머 하나**로만 옮겼고(그 씬 값이 0 이라 사실상 없었다), 그 사이에 **뷰가 나는**
    // 구간이 통째로 빠져 있었다. 여기가 그 구간의 주인이다.
    //
    // ⚠ **규칙은 하나도 없다.** 코어는 이미 유닛을 세웠고(`Placed`) 배치 페이즈(`Deploying`)도
    // 코어가 든다. 이 컴포넌트가 가진 것은 「뷰가 트레이에서 그 칸까지 어떻게 나는가」와
    // **「다 날았다」를 코어에 알리는 한 줄**(`Command.LandDefender`)뿐이다. 비행 길이는
    // 프레젠테이션 시간이라 코어가 모른다 — 그래서 끝을 아는 쪽이 신호를 낸다.
    //
    // 왜 `Placed` 를 직접 구독하지 않고 입력이 밀어 주나: 비행의 **출발점이 손가락**이기
    // 때문이다(트레이 칸의 화면 좌표). 사건에는 그 값이 없고, 실어 보내면 「배치 사건이
    // 화면 좌표를 안다」가 되어 디버그·테스트·골든의 배치가 전부 그 값을 지어내야 한다.
    // 그래서 **비행은 제스처의 연장**이고, 커맨드로 직접 놓는 경로(헤드리스·테스트)는
    // 지금까지처럼 즉시 착지한다.
    //
    // 값의 정본은 전부 `DragSwaySettings`(라이브 SO) 다 — 새 수치를 여기서 지어내지 않는다.
    // 궤적 수학도 새로 만들지 않는다: `KeyringSim.DismountPoint` 는 도약 연출(`CoreLeapPresenter`)
    // 이 이미 쓰는 같은 함수다.
    [DisallowMultipleComponent]
    public sealed class CoreDeployFlightPresenter : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Tooltip("착지 눌림을 재생할 유닛 뷰 풀. 비어 있으면 눌림 없이 진행한다.")]
        [SerializeField] private CoreUnitViewPool _units;

        [Tooltip("드롭 하마 노브(라이브 SO). 비어 있으면 비행하지 않고 즉시 착지한다.")]
        [SerializeField] private DragSwaySettings _config;

        [SerializeField] private Camera _boardCamera;

        [Tooltip("출발점을 보드 평면에서 띄우는 높이. 옛 컨트롤러의 previewHeight 와 같은 자리다.")]
        [SerializeField, Min(0f)] private float _startLift = 0.35f;

        // 비행 중 상태. **키의 존재 자체가 「비행 중」**이다(도약 연출과 같은 규약) — 같은
        // 생명주기를 두 컬렉션이 나눠 들면 진입/이탈이 쌍으로 유지돼야 하고 한쪽만 잊히는
        // 자리가 생긴다. 키를 지우면 코루틴이 다음 프레임에 자진 종료한다.
        private struct Flight
        {
            public Vector3 ViewPos;     // view 공간 절대 좌표(아치가 camUp 이라 sim 으로 못 접는다)
            public float Lift;          // 기저선 대비 뜬 높이 — 확대·그림자의 공통 입력
            public Vector3 Ground;      // 그림자가 남을 자리(아치 기저선)
        }

        private readonly Dictionary<int, Flight> _flight = new Dictionary<int, Flight>(4);
        private readonly List<int> _scratch = new List<int>(4);

        /// <summary>비행 중인 유닛 수. 스모크 테스트가 「연출이 남아 있지 않다」를 묻는 창구.</summary>
        public int FlightCount => _flight.Count;

        public bool IsFlying(SimEntityId id) => _flight.ContainsKey(id.Value);

        /// <summary>
        /// 비행 중이면 그 값이 유닛 동기보다 **이긴다**. 뷰 풀이 `LateUpdate` 에서 읽는다 —
        /// 코루틴은 모든 `Update` 뒤·`LateUpdate` 앞에 돌므로 이 순서는 프레임 규약이 보장한다.
        /// </summary>
        public bool TryGetFlightView(SimEntityId id, out Vector3 viewPos, out float lift, out Vector3 groundAnchor)
        {
            if (_flight.TryGetValue(id.Value, out var f))
            {
                viewPos = f.ViewPos;
                lift = f.Lift;
                groundAnchor = f.Ground;
                return true;
            }
            viewPos = default;
            lift = 0f;
            groundAnchor = default;
            return false;
        }

        /// <summary>
        /// 그 유닛을 `fromScreen`(손가락이 집었던 화면 자리)에서 제 칸까지 날려 보낸다.
        ///
        /// **false 를 돌려주면 호출자가 즉시 착지시킨다** — 저작이 없거나(설정 SO 미배선)
        /// 보드가 아직 안 섰으면 나는 시늉을 하다 공중에 멈추는 것보다 지금까지의 거동이 낫다.
        /// </summary>
        public bool Launch(SimEntityId id, Vector2 fromScreen)
        {
            if (_driver == null || !_driver.Running || _config == null) return false;
            if (_flight.ContainsKey(id.Value)) return false;          // 이미 난다(경계 동시 관통 방어)
            if (!Wassup.Core.BoardSpace.IsConfigured) return false;

            var u = _driver.Find(id);
            if (u == null) return false;

            var cam = EnsureCamera();
            if (cam == null) return false;

            Vector3 end = RestViewPos(u);
            Vector3 normal = BoardNormalToward(cam, end);
            Vector3 start = ScreenToBoardPoint(cam, fromScreen, end) + normal * _startLift;

            _flight[id.Value] = new Flight { ViewPos = start, Lift = 0f, Ground = start };
            StartCoroutine(Run(id, start, end, cam.transform.up));
            return true;
        }

        // ── 비행 ─────────────────────────────────────────────────────────────
        //
        // **시계 = unscaled 다.** 비행은 배치 조작의 연장이라 판의 슬로모·정지를 따르지 않는다
        // (도약 연출이 배틀 도메인을 따르는 것과 반대 — 그쪽은 판 안의 사건이다).
        //
        // 시간 이징 없음(선형). Out* 이징은 끝속도를 0 으로 죽여 내리찍는 착지가 물러진다 —
        // 착지 속도는 기하(끝접선)가 만든다. 체공 재매핑은 끝속도를 **키우므로** 충돌하지 않고,
        // 반동 구간은 재매핑에서 뺀다(힘 모으는 타이밍이 흔들리면 안 된다).
        private IEnumerator Run(SimEntityId id, Vector3 start, Vector3 end, Vector3 camUp)
        {
            int key = id.Value;
            float duration = Mathf.Max(0.05f, _config.dropTotalSeconds);
            float recoilFrac = Mathf.Clamp(_config.dropRecoilSeconds / duration, 0.02f, 0.6f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;

                // 키 부재 = 취소 신호다. 끊은 쪽(teardown)이 착지도 같이 낸다 — 여기서 또 내면
                // 두 번이 된다.
                if (!_flight.ContainsKey(key)) yield break;

                // 비행 중에 죽거나 사라졌다 — 공중에 뷰를 남기지 않고, 착지 신호는 그래도 낸다
                // (코어가 없는 개체를 거절한다. 조용히 삼키면 「영영 배치 중」이 생긴다).
                if (_driver == null || !_driver.IsAlive(id))
                {
                    _flight.Remove(key);
                    Land(id);
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                float f = raw <= recoilFrac
                    ? raw
                    : recoilFrac + (1f - recoilFrac) * Wassup.UI.KeyringSim.FlightTimeRemap(
                          (raw - recoilFrac) / (1f - recoilFrac), _config.dropHangPower);

                Vector3 p = Wassup.UI.KeyringSim.DismountPoint(
                    start, Vector3.zero, end, camUp,
                    recoilFrac, _config.dropRecoilDip,
                    _config.dropArcHeightFactor, _config.dropArcMinHeight,
                    _config.dropLaunchControl, _config.dropLandingHeight,
                    f);

                // 기저선(출발→도착 직선) 대비 뜬 높이. 반동 구간의 dip 은 음수라 Max(0) 이
                // 걷어낸다 — 내려앉을 때는 커지지 않는다. 그림자는 유닛이 아니라 기저선 위에
                // 남는다(아치가 camUp 이라 유닛의 화면 XZ 가 밀린다).
                Vector3 baseline = Vector3.Lerp(start, end, f);
                float lift = Mathf.Max(0f, Vector3.Dot(p - baseline, camUp));
                _flight[key] = new Flight { ViewPos = p, Lift = lift, Ground = baseline };
            }

            // 착지 — 최종점이 정상 피드의 좌표와 같아 오버라이드를 내린 다음 프레임이 그대로
            // 이어진다(팝 0).
            _flight.Remove(key);
            PlayLandingSquash(id);
            Land(id);
        }

        // ── 출구 ─────────────────────────────────────────────────────────────
        //
        // ⚠ **비행을 끝내는 출구는 전부 착지다.** 하나라도 안 부르면 그 유닛은 영영 「배치 중」
        // 으로 남아 사냥판의 소스도 표적도 아니게 된다 — 화면에는 멀쩡히 서 있으므로 그 버그는
        // 「가끔 한 놈이 아무것도 안 한다」로만 보인다.
        private void OnDisable()
        {
            if (_flight.Count == 0) return;
            _scratch.Clear();
            foreach (var k in _flight.Keys) _scratch.Add(k);
            _flight.Clear();                       // 살아남은 코루틴은 키 부재 가드로 자진 종료
            for (int i = 0; i < _scratch.Count; i++) Land(new SimEntityId(_scratch[i]));
            _scratch.Clear();
        }

        private void Land(SimEntityId id)
        {
            // 판이 이미 내려갔으면(씬 teardown) 알릴 곳이 없다 — 그 판은 통째로 사라진다.
            if (_driver == null || !_driver.Running) return;
            _driver.Apply(Command.LandDefender(id));
        }

        private void PlayLandingSquash(SimEntityId id)
        {
            if (_units == null || _config.dropLandingSquash <= 0f) return;
            if (_units.TryGet(id, out var view))
                view.PlayLandingSquash(_config.dropLandingSquash, _config.dropLandingSquashSeconds);
        }

        // ── 좌표 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 그 유닛이 착지해서 설 자리(view 공간). **정상 피드의 공식을 그대로 미러한다** —
        /// 피드가 더하는 것을 여기서 빠뜨리면 착지 프레임에 그만큼 팝이 생긴다.
        /// </summary>
        private Vector3 RestViewPos(Unit u)
        {
            Vector3 world = (Vector3)Wassup.Core.BoardSpace.ToView(u.Position);
            var assets = _driver.DefenderAssets;
            var visual = u.DefIndex >= 0 && u.DefIndex < assets.Count
                ? assets[u.DefIndex] as ISpineUnitVisualData
                : null;
            return visual != null ? world + (Vector3)visual.SpineVisualOffset : world;
        }

        private static Vector3 ScreenToBoardPoint(Camera cam, Vector2 screen, Vector3 fallback)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = Wassup.Core.BoardSpace.RaycastPlane();
            return plane.Raycast(ray, out float enter) && enter > 0f ? ray.GetPoint(enter) : fallback;
        }

        // 보드 평면 법선을 카메라 쪽으로 세운다(`BoardSpace` 의 법선은 아래를 향할 수 있다).
        private static Vector3 BoardNormalToward(Camera cam, Vector3 at)
        {
            Vector3 n = Wassup.Core.BoardSpace.RaycastPlane().normal.normalized;
            if (Vector3.Dot(n, cam.transform.position - at) < 0f) n = -n;
            return n;
        }

        private Camera EnsureCamera()
        {
            if (_boardCamera != null && _boardCamera.isActiveAndEnabled) return _boardCamera;
            _boardCamera = Camera.main;
            return _boardCamera;
        }
    }
}
