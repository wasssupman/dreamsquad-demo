using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.Core.TimeControl;
using Wassup.Data.BattleView;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5a — 도약 연출. 옛 `BattleBridge.BossLeap.cs` ·
    // `BattleBridge.UltimateLeap.cs` 의 후계다.
    //
    // ⚠ **규칙은 하나도 없다.** 코어가 이미 순간이동했고 피해도 코어가 냈다. 이 컴포넌트가 가진
    // 것은 「뷰가 그 사이를 어떻게 나는가」뿐이다. 슬램 투사체를 여기서 쏘지 않는다 —
    // 옛 브리지는 그것까지 들고 있었고, 그래서 뷰가 전투 규칙의 생산자였다.
    //
    // 비행 중 위치는 **키의 존재 자체가 「비행 중」**이다. 별도 집합을 두지 않는다 — 같은
    // 생명주기를 두 컬렉션이 나눠 들면 진입/이탈이 쌍으로 유지돼야 하고 한쪽만 잊히는 자리가
    // 생긴다. 이 단일 진실 덕에 취소도 공짜다: 키를 지우면 코루틴이 다음 프레임에 자진 종료한다.
    //
    // 값은 **(보드 평면 sim 좌표, view 공간 아치 높이)** 두 축으로 분리해서 든다 —
    // `BoardSpace.ToView` 가 sim-Y 를 버리므로 높이를 sim 좌표에 섞으면 화면에서 평면화되고
    // 옆으로 미끄러진다. 수평은 sim 으로 흘려 셀 정합을 잡고, 높이는 뷰가 변환 뒤에 더한다.
    [DisallowMultipleComponent]
    public sealed class CoreLeapPresenter : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private LeapVisualConfig _config;

        [Tooltip("착지 눌림을 재생할 유닛 뷰 풀. 비어 있으면 눌림 없이 진행한다.")]
        [SerializeField] private CoreUnitViewPool _units;

        [Tooltip("궁극기 착지 예고 링을 그리는 오버레이(unit 8a2 — 전용 채널). 비면 씬에서 한 번 찾는다.")]
        [SerializeField] private CoreMapOverlay _overlay;
        private bool _overlayMissWarned;

        private readonly Dictionary<int, (float3 simPos, float viewHeight)> _flight =
            new Dictionary<int, (float3, float)>();

        /// <summary>비행 중인 유닛 수. 스모크 테스트가 「연출이 남아 있지 않다」를 묻는 창구.</summary>
        public int FlightCount => _flight.Count;

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Leap, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            // 예고가 판 너머로 살아남지 않게(옛 teardown `ClearTelegraphRing` — `BattleBridge.UltimateLeap.cs:62`).
            if (_overlay != null)
                foreach (var kv in _flight) _overlay.HideLandingTelegraph(new SimEntityId(kv.Key));
            // 오버라이드를 비우면 진행 중 코루틴이 다음 프레임에 자진 종료한다 —
            // 공중에 뷰가 멈춘 채 남지 않는다.
            _flight.Clear();
        }

        /// <summary>
        /// 비행 중이면 그 값이 유닛 동기보다 **이긴다**(X3). 뷰 풀이 `ViewOrder.Unit` 에서 읽는다.
        /// </summary>
        public bool TryGetFlightOverride(SimEntityId id, out float3 simPos, out float viewHeight)
        {
            if (_flight.TryGetValue(id.Value, out var v))
            {
                simPos = v.simPos;
                viewHeight = v.viewHeight;
                return true;
            }
            simPos = default;
            viewHeight = 0f;
            return false;
        }

        private void OnCoreEvent(CoreEvent e)
        {
            if (_config == null) return;
            switch (e.Kind)
            {
                case CoreEventKind.LeapAscend:
                    // 키가 있으면 이미 비행 중 — 무시(경계 동시 관통 방어).
                    if (_flight.ContainsKey(e.A.Value)) return;
                    if (e.Arg == 1) { ShowLandingTelegraph(e); StartCoroutine(RunUltimateAscend(e)); }
                    else StartCoroutine(RunBossLeap(e));
                    break;

                case CoreEventKind.LeapDescend:
                    if (e.Arg == 1)
                    {
                        // 예고는 **여기서** 끈다 — 코어가 착지를 확정한 순간이다(옛 `BattleBridge.UltimateLeap.cs:117-120`).
                        ResolveOverlay()?.HideLandingTelegraph(e.A);
                        StartCoroutine(RunUltimateDescend(e));
                    }
                    break;
            }
        }

        // ── 일반 도약 ────────────────────────────────────────────────────────
        //
        // 심은 이미 착지했고 **뷰만** 출발지에서 착지점까지 난다. 궤적은 드롭 하마(D&D)와
        // 같은 함수다 — 신규 궤적 수학 0. 같은 문제라 같은 함수를 쓴다.
        private IEnumerator RunBossLeap(CoreEvent e)
        {
            int key = e.A.Value;
            var start = (Vector3)e.SiteFired.Pos;
            var end = (Vector3)e.SiteTarget.Pos;

            // **아치의 기저축은 카메라가 아니라 순수 +Y 다.** 앵커의 y 를 0 으로 눕히고 up 축으로
            // 궤적을 풀면 반환점의 xz 는 보드 평면 수평 경로, y 는 **순수 아치 높이**로 분리된다.
            var flatStart = new Vector3(start.x, 0f, start.z);
            var flatEnd = new Vector3(end.x, 0f, end.z);

            // unit 7d — 비행 창은 **코어의 규칙**이다(창 끝 = 착지 슬램). 길이는 사건이 나른다 — 뷰가 제 설정으로 재면
            // 슬램이 뷰 도착보다 먼저/늦게 터진다. 설정값은 사건에 길이가 없을 때(옛 트레이스 재생 등)의 폴백뿐이다.
            float duration = Mathf.Max(0.05f, e.Amount > 0f ? e.Amount : _config.BossTotalSeconds);
            float recoilFrac = Mathf.Clamp(_config.BossRecoilSeconds / duration, 1e-4f, 0.9f);

            _flight[key] = (e.SiteFired.Pos, 0f);

            float t = 0f;
            bool abandoned = false;
            while (t < duration)
            {
                // 키 부재도 취소 신호다 — teardown 이 `_flight.Clear()` 로 비행을 끊는다.
                if (!_flight.ContainsKey(key) || _driver == null || !_driver.IsAlive(e.A))
                {
                    abandoned = true;
                    break;
                }

                // 배틀 도메인 델타 — 카드 슬로모 중에는 도약도 같이 느려져야 판과 어긋나지 않는다.
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle);

                // 시간 이징 없음(선형). Out* 이징은 끝속도를 0 으로 죽여 내리찍는 임팩트가 물러진다 —
                // 착지 속도는 기하(끝접선)가 만든다. 체공 재매핑은 끝속도를 **키우므로** 충돌하지 않는다.
                float raw = Mathf.Clamp01(t / duration);
                float t01 = raw <= recoilFrac
                    ? raw
                    : recoilFrac + (1f - recoilFrac) * Wassup.UI.KeyringSim.FlightTimeRemap(
                          (raw - recoilFrac) / (1f - recoilFrac), _config.BossHangPower);

                Vector3 p = Wassup.UI.KeyringSim.DismountPoint(
                    flatStart, Vector3.zero, flatEnd, Vector3.up,
                    recoilFrac, _config.BossRecoilDip,
                    _config.BossArcHeightFactor, _config.BossArcMinHeight,
                    _config.BossLaunchControl, _config.BossLandingHeight,
                    t01);

                float groundY = Mathf.Lerp(start.y, end.y, t01);
                _flight[key] = (new float3(p.x, groundY, p.z), p.y);
                yield return null;
            }

            _flight.Remove(key);
            // **abandon 이면 착지 처리를 하지 않는다** — 끊긴 비행에 눌림이 터지면 이미 사라진
            // 뷰에 연출을 건다.
            if (!abandoned)
                PlayLandingSquash(e.A, _config.BossLandingSquash, _config.BossLandingSquashSeconds);
        }

        // ── 궁극기 도약 ──────────────────────────────────────────────────────
        //
        // 이탈과 강하는 **예고 시간만큼 떨어진 별개 사건**이라 한 코루틴으로 묶지 않는다.
        // ⚠ 예고 시간을 뷰가 **복제하지 않는다** — 그 시각은 코어 시퀀스가 결정하고,
        // 복제하면 두 시계가 갈린다. 뷰는 이탈을 받아 올라가 **머무르고**, 강하 사건을 받아 내린다.
        private IEnumerator RunUltimateAscend(CoreEvent e)
        {
            int key = e.A.Value;
            var ground = e.SiteFired.Pos;
            float duration = Mathf.Max(0.05f, _config.UltimateAscendSeconds);
            float height = _config.UltimateHeight;

            _flight[key] = (ground, 0f);
            float t = 0f;
            while (t < duration)
            {
                if (!_flight.ContainsKey(key)) yield break;
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle);
                _flight[key] = (ground, Mathf.Lerp(0f, height, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            // 판 밖에 **머무른다.** 내려오는 것은 강하 사건의 일이다.
            if (_flight.ContainsKey(key)) _flight[key] = (ground, height);
        }

        private IEnumerator RunUltimateDescend(CoreEvent e)
        {
            int key = e.A.Value;
            var landing = e.SiteTarget.Pos;
            float duration = Mathf.Max(0.05f, _config.UltimateDescendSeconds);
            // 이탈을 못 본 채 강하만 온 경우(구독 전 발동)에도 화면이 멀쩡하도록 높이에서 시작한다.
            float from = _flight.TryGetValue(key, out var cur) ? cur.viewHeight : _config.UltimateHeight;

            _flight[key] = (landing, from);
            float t = 0f;
            while (t < duration)
            {
                if (!_flight.ContainsKey(key)) yield break;
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle);
                _flight[key] = (landing, Mathf.Lerp(from, 0f, Mathf.Clamp01(t / duration)));
                yield return null;
            }

            _flight.Remove(key);
            PlayLandingSquash(e.A, _config.UltimateLandingSquash, _config.UltimateLandingSquashSeconds);
        }

        // unit 8a2 행 2 — 착지 예고(옛 `BattleBridge.UltimateLeap.cs:87 ShowLandingTelegraph`). 중심 = 착지 **칸** 중심
        // (옛 `leap.landingCell`), 반경 = 슬램 칸 수(사건 값 `AreaTiles`) + 원점 항(자리형 → 칸 반폭 — 보스의 몸을 안 읽는다).
        // 규칙이 아니라 그림이다 — 피해·텔레포트는 코어가 끝냈다.
        private void ShowLandingTelegraph(CoreEvent e)
        {
            var overlay = ResolveOverlay();
            var map = _driver != null ? _driver.Match?.Map : null;
            if (overlay == null || map == null) return;
            var center = map.CenterOf(map.CellOf(e.SiteTarget.Pos));
            overlay.ShowLandingTelegraph(e.A, center, CoreDrawRadius.AreaTiles(e.AreaTiles, e.SiteTarget.OriginBody),
                                         _config.LandingTelegraphColor);
        }

        private CoreMapOverlay ResolveOverlay()
        {
            if (_overlay != null) return _overlay;
            _overlay = FindAnyObjectByType<CoreMapOverlay>();
            if (_overlay == null && !_overlayMissWarned)
            {
                _overlayMissWarned = true;
                // T17 — 예고가 아예 안 뜨면 회피가 불가능하다 = 불공정. 한 번은 시끄럽게.
                Debug.LogWarning("[CoreLeapPresenter] CoreMapOverlay 가 없다 — 궁극기 착지 예고를 그릴 수 없다.", this);
            }
            return _overlay;
        }

        private void PlayLandingSquash(SimEntityId id, float amount, float seconds)
        {
            if (_units == null || amount <= 0f) return;
            if (_units.TryGet(id, out var view)) view.PlayLandingSquash(amount, seconds);
        }
    }
}
