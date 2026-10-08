using System.Collections.Generic;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.Core.TimeControl;
using Somnia.Battle.Presentation;

namespace Somnia.Battle.BattleCoreUnity.View
{
    // battle-core-rebuild unit 6c — **고속 틱 공격을 지속 빔으로** 번역하는 프리젠터. 옛
    // `Presentation.BeamPresenter`(238줄)의 후계다(키 `Entity` → `SimEntityId`, 해석기 → 유닛 뷰 풀).
    //
    // 코어에는 「빔」이 없다. 버스터즈는 짧은 주기로 피해를 넣는 유닛일 뿐이고, 빔은 그 공격 사건
    // (`AttackResolved`)들을 시간축에서 뭉쳐 하나의 지속 효과로 보이게 한 결과다. 하는 일은 **TTL
    // 세션 관리** 하나다: 사건 수신 → 세션 없으면 열고 있으면 TTL 갱신 → 매 프레임 양 끝을 다시
    // 읽고 → 만료 시 종료.
    //
    // 「빔 유닛인가」는 **그 유닛 저작의 프리팹 유무**가 정한다(`DefenderUnitData.beamVfxPrefab`) —
    // id·종류 분기 없음(옛 규약). TTL 은 이 공격의 **실주기**(`AttackResolved.Amount`)에서 온다 —
    // 상수로 박으면 공속 버프나 주기가 다른 두 번째 빔 유닛에서 깜빡인다.
    //
    // ⚠ **세션 키 = 쏘는 쪽**이다. spec 문면(「키 = 맞는 쪽」)은 **배치 스킬의 대상별 조사**를 가리킨다
    // (옛 코드 주석: 「공격 빔은 공격자, 대상별 조사는 대상 엔티티」). 그 생산자는 unit 7 이고, 이 unit 의
    // 유일한 생산자인 **공격 빔**을 맞는 쪽으로 키잉하면 두 버스터즈가 한 적을 쏠 때 빔이 하나로 접힌다.
    // 그래서 `Open(key, …)` 은 키를 호출자가 고르게 남겨 두고, 공격 경로는 옛 코드대로 공격자를 쓴다.
    //
    // 끝점은 **좌표가 아니라 개체**로 붙든다 — 사건은 공격 주기로만 오는데 그 사이 적은 계속 걷는다.
    [DisallowMultipleComponent]
    public sealed class CoreBeamPresenter : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreUnitViewPool _units;

        [Tooltip("빔 세션 TTL = 실주기 × 이 값. 사건이 조금 늦어도 빔이 끊기지 않을 만큼의 무차원 여유(옛 브리지 1.75).")]
        [SerializeField, Min(1f)] private float _ttlMargin = 1.75f;

        private sealed class Session
        {
            public GameObject prefab;
            public GameObject go;
            public Transform beamBody;
            public Transform beamCast;
            public Transform bodyTip;
            public Transform hit;
            public SimEntityId source;
            public SimEntityId target;
            public float ttl;
            // 마지막으로 성공한 배치. 뷰 조회가 한 프레임 실패해도 여기로 버틴다 — 실패를 종료 사유로
            // 삼으면 세션이 재생성되고 파티클이 0부터 다시 쌓여 빔이 끊겨 보인다(옛 계약).
            public Vector3 lastSource;
            public Vector3 lastEndpoint;
            public bool placedOnce;
        }

        private readonly Dictionary<int, Session> _sessions = new Dictionary<int, Session>();
        private readonly List<int> _expired = new List<int>();
        private readonly Stack<Session> _pool = new Stack<Session>();

        /// <summary>살아 있는 빔 세션 수(진단·테스트).</summary>
        public int LiveSessionCount => _sessions.Count;

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Effect, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            CloseAll();
        }

        private void OnDestroy() => CloseAll();

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted:
                    CloseAll();
                    break;

                case CoreEventKind.AttackResolved:
                {
                    if (((int)e.Faction & Somnia.Battle.Skills.Factions.AnyDefender) == 0) return;
                    var data = DefenderData(e.DefIndex);
                    if (data == null || data.beamVfxPrefab == null || e.Amount <= 0f || e.B.IsNone) return;
                    Open(e.A.Value, data.beamVfxPrefab, e.A, e.B, e.Amount * _ttlMargin);
                    break;
                }

                // unit 7c — **스킬의 대상별 빔**(버스터즈 개시 빔 — `AreaDot` 가 `SkillVisual(Beam)` 을 요청한다). 키 = **맞는 쪽**
                // (옛 규약: 공격 빔은 공격자, 대상별 조사는 대상 — 한 시전이 여러 적에게 빔을 동시에 낸다). 프리팹은 규칙 줄이
                // 실은 스킬 연출 번호(`DefIndex`)로 되찾는다(`MatchViewAssets.SkillVfx`), 수명 = 그 조사의 지속(초 — 배틀 시간).
                case CoreEventKind.SkillVisual:
                {
                    if ((Somnia.Battle.Skills.SkillVisualKind)e.Arg != Somnia.Battle.Skills.SkillVisualKind.Beam || e.B.IsNone || e.Amount <= 0f) return;
                    var prefab = _driver != null ? _driver.ViewAssets.SkillVfx(e.DefIndex) : null;
                    if (prefab == null) return;
                    Open(e.B.Value, prefab, e.A, e.B, e.Amount);
                    break;
                }

                // 쏘는 쪽이 사라지면 즉시 끊는다. 맞는 쪽이 사라지면 **마지막 끝점으로 TTL 까지** 버틴다 —
                // 끊으면 다음 대상으로 넘어가는 한 박자에 빔이 깜빡인다(옛 동작).
                case CoreEventKind.UnitSlain:
                    Close(e.B.Value);
                    break;
                case CoreEventKind.UnitDestroyed:
                    Close(e.A.Value);
                    break;
            }
        }

        private Somnia.Battle.Data.DefenderUnitData DefenderData(int defIndex)
        {
            if (_driver == null || defIndex < 0) return null;
            var list = _driver.DefenderAssets;
            return defIndex < list.Count ? list[defIndex] : null;
        }

        /// <summary>
        /// 빔 세션을 열거나 잇는다. 같은 키로 다시 부르면 TTL 과 대상만 갱신된다(코얼레스).
        /// 키는 세션 정체성이다 — 공격 빔은 쏘는 쪽(헤더).
        /// </summary>
        public void Open(int key, GameObject beamPrefab, SimEntityId source, SimEntityId target, float ttlSec)
        {
            if (beamPrefab == null || ttlSec <= 0f) return;
            if (!_sessions.TryGetValue(key, out var s))
            {
                s = Rent(beamPrefab);
                _sessions[key] = s;
            }
            s.source = source;
            s.target = target;
            s.ttl = ttlSec;
        }

        // ⚠ 시간은 **배틀 도메인**이다 — 공격 사건이 판의 시간으로 오므로 실시간으로 재면 슬로모에서
        // 사건 간격이 TTL 을 넘겨 빔이 깜빡인다(옛 계약).
        private void LateUpdate()
        {
            if (_sessions.Count == 0) return;
            float dt = TimeManager.Instance.DeltaTime(TimeDomain.Battle);
            _expired.Clear();
            foreach (var kv in _sessions)
            {
                var s = kv.Value;
                s.ttl -= dt;
                if (s.ttl <= 0f) { _expired.Add(kv.Key); continue; }
                if (!TryPlace(s) && !s.placedOnce) _expired.Add(kv.Key);
            }
            for (int i = 0; i < _expired.Count; i++) Close(_expired[i]);
        }

        public void Close(int key)
        {
            if (!_sessions.TryGetValue(key, out var s)) return;
            _sessions.Remove(key);
            Return(s);
        }

        public void CloseAll()
        {
            foreach (var kv in _sessions) Return(kv.Value);
            _sessions.Clear();
        }

        private bool TryPlace(Session s)
        {
            if (_units == null) return false;
            if (!_units.TryResolveViewPosition(s.source, useAnchor: true, out var sourceView))
            {
                if (!s.placedOnce) return false;
                sourceView = s.lastSource;
            }
            if (!_units.TryResolveViewPosition(s.target, useAnchor: false, out var endpoint))
            {
                if (!s.placedOnce) return false;
                endpoint = s.lastEndpoint;
            }

            s.lastSource = sourceView;
            s.lastEndpoint = endpoint;
            s.placedOnce = true;
            Vector3 dir = endpoint - sourceView;
            float length = dir.magnitude;
            if (length < 1e-4f) return true;
            Vector3 fwd = dir / length;

            // 롤을 카메라에 고정한다 — 이 보드는 XY 평면 정면 뷰라 빔 방향이 화면 평면 안에 놓이고,
            // forward 만 맞추면 빔 메시를 축 방향에서 보게 되어 납작한 직선이 된다(옛 제보).
            var cam = Camera.main;
            Quaternion rot = cam != null
                ? Quaternion.LookRotation(fwd, -cam.transform.forward)
                : Quaternion.LookRotation(fwd);

            if (s.beamBody != null)
            {
                s.beamBody.position = sourceView;
                s.beamBody.rotation = rot;
                var sc = s.beamBody.localScale;
                s.beamBody.localScale = new Vector3(sc.x, sc.y, length);
            }
            if (s.beamCast != null)
            {
                s.beamCast.position = sourceView;
                s.beamCast.rotation = rot;
            }
            if (s.bodyTip != null)
            {
                s.bodyTip.position = endpoint;
                s.bodyTip.rotation = rot;
            }
            if (s.hit != null)
            {
                s.hit.position = endpoint;
                s.hit.rotation = cam != null
                    ? Quaternion.LookRotation(-fwd, -cam.transform.forward)
                    : Quaternion.LookRotation(-fwd);
            }
            return true;
        }

        private Session Rent(GameObject prefab)
        {
            // 같은 프리팹에서 난 세션만 재사용한다(빔 유닛이 2종 이상이면 엉뚱한 외형이 나온다).
            if (_pool.Count > 0 && _pool.Peek().prefab == prefab && _pool.Peek().go != null)
            {
                var reused = _pool.Pop();
                reused.placedOnce = false;
                reused.go.SetActive(true);
                PlayAll(reused.go);
                return reused;
            }
            var go = Instantiate(prefab, transform);
            var s = new Session
            {
                prefab = prefab,
                go = go,
                beamBody = go.transform.Find("BeamBody"),
                beamCast = go.transform.Find("BeamCast"),
                bodyTip = go.transform.Find("BodyTip"),
                hit = go.transform.Find("Hit"),
            };
            if (s.beamBody == null)
                Debug.LogError("[CoreBeamPresenter] 빔 프리팹에 'BeamBody' 자식이 없다 — 빔이 안 보인다. 프리팹: " + prefab.name);
            // 벤더 프리팹은 sortingOrder 0~2 로 들어와 유닛 뒤에 깔린다 — 대역만 끌어올린다(새 인스턴스에만).
            var renderers = go.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder = BoardSortOrder.BeamOrder + renderers[i].sortingOrder;
            PlayAll(go);
            return s;
        }

        private void Return(Session s)
        {
            if (s?.go == null) return;
            StopAll(s.go);
            s.go.SetActive(false);
            _pool.Push(s);
        }

        private static void PlayAll(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++) systems[i].Play(false);
        }

        private static void StopAll(GameObject go)
        {
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
