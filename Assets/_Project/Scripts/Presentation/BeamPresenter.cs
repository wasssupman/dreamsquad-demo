using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Wassup.Presentation
{
    // beam-ranger-defender unit 1 — 고속 틱 공격을 **지속 빔**으로 번역하는 프레젠터.
    //
    // 심에는 "빔" 개념이 없다(계약 1). 버스터즈는 0.2초마다 직접 데미지를 넣는 유닛일 뿐이고,
    // 빔은 그 공격 사건들을 시간축에서 뭉쳐(coalesce) 하나의 지속 효과로 보이게 한 결과다.
    // 그래서 이 클래스가 하는 일은 사실상 하나 — **TTL 세션 관리**:
    //   공격 사건 수신 → 세션 없으면 생성 / 있으면 TTL·끝점 갱신 → TTL 만료되면 종료.
    //
    // 벤더(PixPlays) BeamVfx 는 쓰지 않는다. Destroy 기반 lifecycle(BaseVfx)이라 풀링과
    // 충돌하고, 재조준을 매 프레임 우리가 쥐어야 해서 프리팹 사본에서 걷어냈다.
    // 여기서는 프리팹의 파츠 4개(BeamBody/BeamCast/BodyTip/Hit)를 직접 배치한다.
    public class BeamPresenter : MonoBehaviour
    {
        [Tooltip("공격 사건이 이 시간 동안 안 들어오면 빔을 끊는다. 공격 주기보다 넉넉해야 " +
                 "틱 사이에 빔이 깜빡이지 않는다(주기 0.2 기준 0.35 권장).")]
        [SerializeField] private float sessionTtlSec = 0.35f;

        [Tooltip("끝점 추종 반응 속도(1/초). 공격 사건은 주기적으로만 오므로 그 사이를 보간해 " +
                 "끝점이 계단처럼 튀지 않게 한다. 클수록 즉각적.")]
        [SerializeField] private float endpointFollowSpeed = 18f;

        private sealed class Session
        {
            public GameObject go;
            public Transform beamBody;
            public Transform beamCast;
            public Transform bodyTip;
            public Transform hit;
            public Vector3 muzzle;        // 최근 사건이 알려온 발사점(view 공간)
            public Vector3 endpoint;      // 현재 표시 중인 끝점(보간된 값)
            public Vector3 targetPoint;   // 최근 사건이 알려온 끝점
            public float ttl;
            public bool endpointInitialized;
        }

        private readonly Dictionary<Entity, Session> _sessions = new();
        private readonly List<Entity> _expiredScratch = new();
        private readonly Stack<Session> _pool = new();

        /// <summary>
        /// 공격 사건 1건. 세션이 없으면 열고, 있으면 TTL 과 끝점을 갱신한다(코얼레스).
        /// sourceView/targetView 는 **view 공간** 좌표 — 호출측(BattleBridge)이 변환해 넘긴다.
        /// </summary>
        public void ReportAttack(Entity attacker, GameObject beamPrefab, Vector3 sourceView, Vector3 targetView)
        {
            if (beamPrefab == null) return;

            if (!_sessions.TryGetValue(attacker, out var s))
            {
                s = Rent(beamPrefab);
                if (s == null) return;
                _sessions[attacker] = s;
                s.endpointInitialized = false;
            }
            s.muzzle = sourceView;
            s.targetPoint = targetView;
            s.ttl = sessionTtlSec;
            if (!s.endpointInitialized)
            {
                s.endpoint = targetView;
                s.endpointInitialized = true;
            }
            Place(s, s.muzzle);
        }

        /// <summary>
        /// 고정 지속 세션. 공격 코얼레스(ReportAttack)와 달리 갱신 없이 durationSec 만큼만 산다
        /// — 배치 스킬처럼 "N초 동안 쏜다"가 이미 정해진 경우용. 키는 호출측이 정하며
        /// (배치 조사는 **대상 엔티티**를 쓴다) 공격 세션 키(공격자)와 겹치지 않는다.
        /// </summary>
        public void OpenTimed(Entity key, GameObject beamPrefab, Vector3 sourceView, Vector3 targetView, float durationSec)
        {
            if (beamPrefab == null || durationSec <= 0f) return;
            if (!_sessions.TryGetValue(key, out var s))
            {
                s = Rent(beamPrefab);
                if (s == null) return;
                _sessions[key] = s;
            }
            s.muzzle = sourceView;
            s.targetPoint = targetView;
            s.endpoint = targetView;
            s.endpointInitialized = true;
            s.ttl = durationSec;
            Place(s, s.muzzle);
        }

        /// <summary>
        /// 매 프레임 TTL 을 깎고 만료된 세션을 닫는다. 호출은 BattleBridge sync.
        /// ⚠ deltaTime 은 **배틀 도메인 시간**이어야 한다 — 공격 사건이 sim 시간으로 오므로
        /// 실시간으로 재면 슬로모에서 사건 간격이 TTL 을 넘겨 빔이 깜빡인다.
        /// </summary>
        public void Tick(float battleDeltaTime)
        {
            if (_sessions.Count == 0) return;
            _expiredScratch.Clear();
            foreach (var kv in _sessions)
            {
                var s = kv.Value;
                s.ttl -= battleDeltaTime;
                if (s.ttl <= 0f) { _expiredScratch.Add(kv.Key); continue; }
                // 끝점 보간 — 사건은 공격 주기로만 오므로 그 사이를 메운다.
                s.endpoint = Vector3.Lerp(s.endpoint, s.targetPoint,
                    1f - Mathf.Exp(-endpointFollowSpeed * Mathf.Max(0f, battleDeltaTime)));
                Place(s, s.muzzle);
            }
            for (int i = 0; i < _expiredScratch.Count; i++)
                Close(_expiredScratch[i]);
        }

        /// <summary>공격자가 사라졌을 때(사망/재배치) 즉시 끊는다.</summary>
        public void Close(Entity attacker)
        {
            if (!_sessions.TryGetValue(attacker, out var s)) return;
            _sessions.Remove(attacker);
            Return(s);
        }

        public void CloseAll()
        {
            foreach (var kv in _sessions) Return(kv.Value);
            _sessions.Clear();
        }

        private void OnDestroy() => CloseAll();

        // ── 내부 ────────────────────────────────────────────────────────────
        // 빔 파츠 배치: body 를 source→endpoint 로 늘이고, tip 은 끝, hit 은 대상 위에.
        // 벤더 BeamVfx 의 배치 규칙과 같은 형태지만 코루틴/Destroy 없이 프레임마다 직접 쓴다.
        private static void Place(Session s, Vector3 sourceView)
        {
            // 끝점을 머즐과 같은 깊이(z)로 눕힌다. 평면 정면뷰 보드라 두 점의 z 가 다르면 빔이
            // 화면 안쪽으로 기울어 짧아 보인다 — TrySpawnCastVfx 가 `dir.z = 0` 하는 것과 같은 이유.
            var endpoint = new Vector3(s.endpoint.x, s.endpoint.y, sourceView.z);
            Vector3 dir = endpoint - sourceView;
            float length = dir.magnitude;
            if (length < 1e-4f) return;
            Vector3 fwd = dir / length;

            if (s.beamBody != null)
            {
                s.beamBody.position = sourceView;
                s.beamBody.forward = fwd;
                var sc = s.beamBody.localScale;
                s.beamBody.localScale = new Vector3(sc.x, sc.y, length);
            }
            if (s.beamCast != null)
            {
                s.beamCast.position = sourceView;
                s.beamCast.forward = fwd;
            }
            if (s.bodyTip != null)
            {
                s.bodyTip.position = endpoint;
                s.bodyTip.forward = fwd;
            }
            if (s.hit != null)
            {
                s.hit.position = endpoint;
                s.hit.forward = -fwd;
            }
        }

        private Session Rent(GameObject prefab)
        {
            if (_pool.Count > 0)
            {
                var reused = _pool.Pop();
                reused.go.SetActive(true);
                PlayAll(reused.go);
                return reused;
            }
            var go = Instantiate(prefab, transform);
            var s = new Session
            {
                go = go,
                beamBody = go.transform.Find("BeamBody"),
                beamCast = go.transform.Find("BeamCast"),
                bodyTip = go.transform.Find("BodyTip"),
                hit = go.transform.Find("Hit"),
            };
            if (s.beamBody == null)
            {
                Debug.LogError("[BeamPresenter] 빔 프리팹에 'BeamBody' 자식이 없다 — 배치할 몸통이 없어 빔이 안 보인다. "
                               + "프리팹: " + prefab.name);
            }
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
