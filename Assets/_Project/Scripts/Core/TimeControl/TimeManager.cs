using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Core.TimeControl
{
    // 도메인 스코프 시간 스케일의 단일 소유자.
    //
    // 의도된 예외적 싱글턴 — 프로젝트 제약 #5(Manager 싱글턴은 GameManager 1개)의
    // 명시적 예외. 시간 제어는 전투 시뮬(ECS)·BattleBridge 웨이브/타이머·전투 표현·UI
    // 다수 계층에 걸쳐 널리 소비되므로 단일 권한이 필요하다. TRD 섹션 5 에 기록.
    //
    // 순수 C# 싱글턴(MonoBehaviour 아님): 자체 Update 가 없다. 스케일은 요청 목록에서
    // on-demand 로 계산되고, 델타는 Time.unscaledDeltaTime 에서 파생된다. 따라서 씬 배선이
    // 필요 없다. 글로벌 UnityEngine.Time.timeScale 은 절대 건드리지 않는다(항상 1).
    public sealed class TimeManager
    {
        public static TimeManager Instance { get; } = new TimeManager();

        private struct ScaleRequest
        {
            public int id;
            public float scale;
            public int priority;
        }

        // 도메인 수는 작고 고정 → 인덱스 = (int)domain.
        private readonly List<ScaleRequest>[] _requests;
        private int _nextId = 1; // 단조 증가, 재사용 없음 → 멱등 해제의 근거.

        // 유효 스케일이 실제로 바뀔 때만 발화. (domain, newScale).
        public event Action<TimeDomain, float> ScaleChanged;

        private TimeManager()
        {
            int n = Enum.GetValues(typeof(TimeDomain)).Length;
            _requests = new List<ScaleRequest>[n];
            for (int i = 0; i < n; i++) _requests[i] = new List<ScaleRequest>();
        }

        // 도메인에 스케일 요청을 push. 반환된 lease 를 Dispose 하면 해제된다.
        // 같은 도메인 다중 요청 시 승자 = (priority desc, 동률 scale asc).
        public TimeLease Request(TimeDomain domain, float scale, int priority = 0)
        {
            float before = ScaleOf(domain);
            int id = _nextId++;
            _requests[(int)domain].Add(new ScaleRequest { id = id, scale = scale, priority = priority });
            RaiseIfChanged(domain, before);
            return new TimeLease(this, domain, id);
        }

        // 활성 요청 중 승자 스케일. 요청 없으면 1.
        public float ScaleOf(TimeDomain domain)
        {
            var list = _requests[(int)domain];
            if (list.Count == 0) return 1f;

            // 승자 = priority desc, 동률 시 scale asc. list[0] 로 시드해 극단적 priority/scale
            // 값에서도(예: priority int.MinValue) 첫 요청이 올바로 선택되게 한다.
            var best = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                var r = list[i];
                if (r.priority > best.priority || (r.priority == best.priority && r.scale < best.scale))
                    best = r;
            }
            return best.scale;
        }

        // 해당 도메인의 스케일된 프레임 델타. 시간 의존 코드는 Time.deltaTime 대신 이걸 쓴다.
        public float DeltaTime(TimeDomain domain)
        {
            return UnityEngine.Time.unscaledDeltaTime * ScaleOf(domain);
        }

        // lease 해제. id 는 재사용되지 않으므로 이미 제거됐거나 복사본이면 조용히 no-op(멱등).
        internal void Release(TimeDomain domain, int id)
        {
            var list = _requests[(int)domain];
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id == id)
                {
                    float before = ScaleOf(domain);
                    list.RemoveAt(i);
                    RaiseIfChanged(domain, before);
                    return;
                }
            }
        }

        // 매치 경계에서 모든 요청을 비운다(고아 lease 누수 방지). 각 도메인 변화 시 발화.
        public void ResetAll()
        {
            for (int d = 0; d < _requests.Length; d++)
            {
                if (_requests[d].Count == 0) continue;
                float before = ScaleOf((TimeDomain)d);
                _requests[d].Clear();
                RaiseIfChanged((TimeDomain)d, before);
            }
        }

        private void RaiseIfChanged(TimeDomain domain, float before)
        {
            float after = ScaleOf(domain);
            if (!Mathf.Approximately(before, after))
                ScaleChanged?.Invoke(domain, after);
        }
    }

    // 스케일 요청 핸들. readonly struct 지만 id 가 재사용되지 않아 이중 Dispose·복사본
    // Dispose 모두 안전한 no-op 이다(원 요청 하나만 해제).
    public readonly struct TimeLease : IDisposable
    {
        private readonly TimeManager _manager;
        private readonly TimeDomain _domain;
        private readonly int _id;

        internal TimeLease(TimeManager manager, TimeDomain domain, int id)
        {
            _manager = manager;
            _domain = domain;
            _id = id;
        }

        public void Dispose()
        {
            _manager?.Release(_domain, _id);
        }
    }
}
