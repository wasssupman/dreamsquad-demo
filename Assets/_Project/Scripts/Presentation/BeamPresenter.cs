using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Wassup.Presentation
{
    // beam-ranger-defender unit 1 — 고속 틱 공격을 **지속 빔**으로 번역하는 프레젠터.
    //
    // 심에는 "빔" 개념이 없다(계약 1). 버스터즈는 0.2초마다 직접 데미지를 넣는 유닛일 뿐이고,
    // 빔은 그 공격 사건들을 시간축에서 뭉쳐(coalesce) 하나의 지속 효과로 보이게 한 결과다.
    // 이 클래스가 하는 일은 **TTL 세션 관리** 하나다:
    //   사건 수신 → 세션 없으면 열고 있으면 TTL 갱신 → 매 프레임 양 끝을 다시 읽고 → 만료 시 종료.
    //
    // 끝점은 **좌표가 아니라 엔티티**로 붙든다. 사건은 공격 주기로만 오는데(0.2s) 그 사이에
    // 적은 계속 걸어가므로, 사건 시점 좌표를 스냅샷하면 빔이 허공을 겨눈다. 배치 스킬처럼
    // 사건이 단 한 번뿐인 경우(2초 조사)는 스냅샷이면 2초 내내 어긋난다.
    //
    // 벤더(PixPlays) BeamVfx 는 쓰지 않는다. Destroy 기반 lifecycle(BaseVfx)이라 풀링과
    // 충돌하고, 재조준을 매 프레임 우리가 쥐어야 해서 프리팹 사본에서 걷어냈다.
    public class BeamPresenter : MonoBehaviour
    {
        private sealed class Session
        {
            public GameObject prefab;   // 어느 프리팹에서 났는지 — 풀 재사용 시 대조한다
            public GameObject go;
            public Transform beamBody;
            public Transform beamCast;
            public Transform bodyTip;
            public Transform hit;
            public Entity source;       // 발사 주체(디펜더)
            public Entity target;       // 겨눈 대상 — 매 프레임 view 위치를 다시 읽는다
            public float ttl;
        }

        private readonly Dictionary<Entity, Session> _sessions = new();
        private readonly List<Entity> _expiredScratch = new();
        private readonly Stack<Session> _pool = new();

        /// <summary>
        /// 빔 세션을 열거나 잇는다. 같은 key 로 다시 부르면 TTL 과 대상만 갱신된다(코얼레스).
        /// ttlSec 은 호출측이 정한다 — 공격 빔은 실발사 주기에서, 배치 스킬은 지속 시간에서.
        /// key 는 세션 정체성이다: 공격 빔은 공격자, 대상별 조사는 대상 엔티티를 쓴다.
        /// </summary>
        public void Open(Entity key, GameObject beamPrefab, Entity source, Entity target, float ttlSec)
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

        /// <summary>
        /// 매 프레임 TTL 을 깎고, 살아있는 세션의 양 끝을 다시 읽어 배치한다.
        /// ⚠ deltaTime 은 **배틀 도메인 시간**이어야 한다 — 공격 사건이 sim 시간으로 오므로
        /// 실시간으로 재면 슬로모에서 사건 간격이 TTL 을 넘겨 빔이 깜빡인다.
        /// </summary>
        public void Tick(float battleDeltaTime, SpineUnitPool pool)
        {
            if (_sessions.Count == 0) return;
            _expiredScratch.Clear();
            foreach (var kv in _sessions)
            {
                var s = kv.Value;
                s.ttl -= battleDeltaTime;
                // 대상이 사라졌으면(사망/디스폰) 빔이 허공에 남지 않게 즉시 만료 처리한다.
                if (s.ttl <= 0f || !TryPlace(s, pool)) _expiredScratch.Add(kv.Key);
            }
            for (int i = 0; i < _expiredScratch.Count; i++)
                Close(_expiredScratch[i]);
        }

        /// <summary>공격자/대상이 사라졌을 때 즉시 끊는다.</summary>
        public void Close(Entity key)
        {
            if (!_sessions.TryGetValue(key, out var s)) return;
            _sessions.Remove(key);
            Return(s);
        }

        /// <summary>매치 경계 전량 정리. 브리지는 매치 간 살아남으므로 안 끊으면 세션이 누적된다.</summary>
        public void CloseAll()
        {
            foreach (var kv in _sessions) Return(kv.Value);
            _sessions.Clear();
        }

        private void OnDestroy() => CloseAll();

        // ── 내부 ────────────────────────────────────────────────────────────
        // 양 끝을 뷰에서 다시 읽어 빔 파츠를 배치한다. 한쪽이라도 못 찾으면 false = 세션 종료.
        private static bool TryPlace(Session s, SpineUnitPool pool)
        {
            if (pool == null) return false;
            if (!TryViewPos(pool, s.source, useAnchor: true, out var sourceView)) return false;
            if (!TryViewPos(pool, s.target, useAnchor: false, out var endpoint)) return false;

            // 끝점을 머즐과 같은 깊이(z)로 눕힌다. 평면 정면뷰 보드라 두 점의 z 가 다르면 빔이
            // 화면 안쪽으로 기울어 짧아 보인다 — TrySpawnCastVfx 가 `dir.z = 0` 하는 것과 같은 이유.
            endpoint.z = sourceView.z;
            Vector3 dir = endpoint - sourceView;
            float length = dir.magnitude;
            if (length < 1e-4f) return true; // 겹친 프레임은 그리지 않고 세션만 유지
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
            return true;
        }

        // 엔티티의 현재 view 위치. 발사점은 cast anchor 우선(TrySpawnCastVfx 와 같은 경로),
        // 없으면 view transform — transform.position 은 이미 view 공간이다.
        // ⚠ Transform 을 붙들지 않고 **매 프레임 엔티티로 조회**한다: 풀이 뷰를 재사용하므로
        // 붙들면 대상 사망 후 그 자리에 들어온 다른 유닛에 빔이 옮겨 붙는다.
        private static bool TryViewPos(SpineUnitPool pool, Entity e, bool useAnchor, out Vector3 pos)
        {
            pos = default;
            if (e == Entity.Null) return false;
            if (useAnchor && pool.TryResolveAnchor(e, out pos)) return true;
            if (!pool.TryGet(e, out var view) || view == null) return false;
            pos = view.transform.position;
            return true;
        }

        private Session Rent(GameObject prefab)
        {
            // 같은 프리팹에서 난 세션만 재사용한다. 프리팹을 안 보고 꺼내면 빔 유닛이 2종
            // 이상일 때(후속 후보의 Ice/Lightning) 엉뚱한 외형이 나온다 — 지금은 소비자가
            // 하나라 드러나지 않을 뿐이다.
            if (_pool.Count > 0 && _pool.Peek().prefab == prefab && _pool.Peek().go != null)
            {
                var reused = _pool.Pop();
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
            {
                Debug.LogError("[BeamPresenter] 빔 프리팹에 'BeamBody' 자식이 없다 — 배치할 몸통이 없어 "
                               + "빔이 안 보인다. 프리팹: " + prefab.name);
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
