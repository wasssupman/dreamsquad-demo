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
        /// <summary>
        /// 엔티티의 현재 **view 공간** 위치 해석기. 프레젠터는 ECS 를 볼 수 없으므로(제약 1)
        /// BattleBridge 가 넘긴다. useAnchor = 발사점(cast anchor 우선).
        /// ⚠ 뷰 풀만 보면 안 된다: 풀에 없는 유닛(폴백 뷰·워밍업 중)이 끝점이면 빔이 통째로
        /// 죽는다 — 배치 스킬 빔이 한 프레임 만에 전멸했던 원인이 정확히 이것이었다.
        /// </summary>
        public delegate bool ViewPosResolver(Entity entity, bool useAnchor, out Vector3 pos);

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
            // 마지막으로 성공한 배치. 뷰 조회가 한 프레임 실패해도(풀 워밍업·리사이클 타이밍)
            // 여기로 버틴다 — 실패를 종료 사유로 삼으면 세션이 재생성되고 파티클이 0부터
            // 다시 쌓여(초당 20개·수명 1초) 빔이 끊겨 보인다.
            public Vector3 lastSource;
            public Vector3 lastEndpoint;
            public bool placedOnce;
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
        public void Tick(float battleDeltaTime, ViewPosResolver resolve)
        {
            if (_sessions.Count == 0) return;
            _expiredScratch.Clear();
            foreach (var kv in _sessions)
            {
                var s = kv.Value;
                s.ttl -= battleDeltaTime;
                if (s.ttl <= 0f) { _expiredScratch.Add(kv.Key); continue; }
                // 배치 실패는 **종료 사유가 아니다.** 뷰가 아직 없거나 한 프레임 비는 경우가
                // 있는데, 그때마다 세션을 닫으면 다음 공격에 새 세션이 열려 파티클이 0부터
                // 다시 쌓인다 = 빔이 끊겨 보인다. 마지막 유효 배치로 버티고 TTL 로만 끝낸다.
                // 단 **한 번도** 배치에 성공하지 못한 세션은 그릴 것이 없으니 정리한다.
                if (!TryPlace(s, resolve) && !s.placedOnce) _expiredScratch.Add(kv.Key);
            }
            for (int i = 0; i < _expiredScratch.Count; i++)
                Close(_expiredScratch[i]);
        }

        /// <summary>현재 살아있는 빔 세션 수(진단·테스트용).</summary>
        public int LiveSessionCount => _sessions.Count;

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
        private static bool TryPlace(Session s, ViewPosResolver resolve)
        {
            if (resolve == null) return false;
            // 조회에 실패하면 마지막 유효값으로 버틴다(위 Tick 주석 — 깜빡임 방지).
            if (!resolve(s.source, true, out var sourceView))
            {
                if (!s.placedOnce) return false;
                sourceView = s.lastSource;
            }
            if (!resolve(s.target, false, out var endpoint))
            {
                if (!s.placedOnce) return false;
                endpoint = s.lastEndpoint;
            }

            s.lastSource = sourceView;
            s.lastEndpoint = endpoint;
            s.placedOnce = true;
            Vector3 dir = endpoint - sourceView;
            float length = dir.magnitude;
            if (length < 1e-4f) return true; // 겹친 프레임은 그리지 않고 세션만 유지
            Vector3 fwd = dir / length;

            // 롤(축 회전)을 카메라에 고정한다. forward 만 맞추면 Unity 가 world-up 으로 롤을
            // 임의 결정하는데, 이 보드는 **XY 평면 정면 뷰**라 빔 방향이 화면 평면 안에 놓인다
            // → 옆에서 보도록 만들어진 빔 메시를 축 방향에서 보게 되어 납작한 직선으로만
            // 보인다(사용자 제보: "가로세로 직선으로만 표현되는 느낌"). up 을 카메라 정면의
            // 반대로 주면 빔의 면이 카메라를 향한다.
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
            // 같은 프리팹에서 난 세션만 재사용한다. 프리팹을 안 보고 꺼내면 빔 유닛이 2종
            // 이상일 때(후속 후보의 Ice/Lightning) 엉뚱한 외형이 나온다 — 지금은 소비자가
            // 하나라 드러나지 않을 뿐이다.
            if (_pool.Count > 0 && _pool.Peek().prefab == prefab && _pool.Peek().go != null)
            {
                var reused = _pool.Pop();
                reused.placedOnce = false; // 재사용 세션은 이전 대상의 좌표를 물려받으면 안 된다
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
            // 벤더 프리팹은 sortingOrder 0~2 로 들어온다. 그대로 두면 유닛(수백대) 뒤에 깔려
            // 빈 땅 구간만 보인다 — 프리팹 내부 상대 순서는 보존하고 보드 대역만 끌어올린다.
            // 새로 만든 인스턴스에만 적용한다(풀 재사용분은 이미 적용돼 있어 두 번 더하면 안 됨).
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
