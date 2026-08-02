using System.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Wassup.Battle.Combat;
using Wassup.Core.TimeControl;

namespace Wassup.Bridge
{
    // ultimate-leap unit 3 — 이탈/강습 연출. **sim 이 이미 끝낸 사실을 뒤따라 그리기만 한다** —
    // 피해도 텔레포트도 여기 없다(일반 도약이 착지 슬램을 브리지에서 쏘는 것과 다른 점).
    //
    // 별도 partial 인 이유는 BossLeap 과 같다: BattleBridge.cs 는 여러 세션이 동시 편집한다.
    // 공유 파일에는 lifecycle 3점 세트와 드레인 호출만 남긴다.
    public partial class BattleBridge
    {
        [Header("Ultimate leap flight (ultimate-leap unit 3)")]
        [Tooltip("이탈 상승 시간(초). 이 시간이 지나면 뷰가 화면 밖으로 사라진다.")]
        [SerializeField] private float ultimateLeapAscendSeconds = 0.45f;
        [Tooltip("강하 시간(초). 착지 프레임에 시작해 이만큼 뒤 지면에 닿는다 — 그때 슬램 VFX 가 뜬다.")]
        [SerializeField] private float ultimateLeapDescendSeconds = 0.25f;
        [Tooltip("이탈 정점 높이(view 공간). 화면 밖으로 나갈 만큼 충분히 크게.")]
        [SerializeField] private float ultimateLeapHeight = 14f;

        private NativeQueue<UltimateLeapVisualEvent> _ultimateLeapVisualQueue;

        // 강하 대기 중인 착지 신호. Ascend 코루틴이 끝난 뒤에도 뷰는 화면 밖에 머물러야 하므로
        // 상승/숨김/강하를 한 코루틴으로 잇는 대신, **sim 이 보내는 Descend 를 기다린다** —
        // 2초라는 시간을 브리지가 복제하지 않기 위해서다(복제하면 두 시계가 갈린다).
        private readonly System.Collections.Generic.HashSet<Entity> _ultimateLeapAirborne = new();

        // ── lifecycle 3점 세트 ──

        private void CreateUltimateLeapChannel()
        {
            if (_ultimateLeapVisualQueue.IsCreated) _ultimateLeapVisualQueue.Dispose();
            _ultimateLeapVisualQueue = new NativeQueue<UltimateLeapVisualEvent>(Allocator.Persistent);
            var singleton = _em.CreateEntity();
            _em.AddComponentData(singleton,
                new UltimateLeapVisualEventsSingleton { queue = _ultimateLeapVisualQueue });
        }

        private void DisposeUltimateLeapChannel()
        {
            if (_ultimateLeapVisualQueue.IsCreated) _ultimateLeapVisualQueue.Dispose();
            // 공중 집합을 비우면 진행 중 코루틴이 다음 프레임에 자진 종료한다(BossLeap 의
            // `_enemyViewOverride.Clear()` 와 같은 계약). 오버라이드도 함께 비워야 뷰가
            // 화면 밖에 멈춘 채 남지 않는다.
            _ultimateLeapAirborne.Clear();
            _enemyViewOverride.Clear();
        }

        // ── 드레인 ──

        private void DrainUltimateLeapVisualEvents()
        {
            if (!_ultimateLeapVisualQueue.IsCreated) return;
            while (_ultimateLeapVisualQueue.TryDequeue(out var evt))
            {
                if (evt.entity == Entity.Null || !_em.Exists(evt.entity)) continue;
                if (evt.kind == UltimateLeapVisualKind.Ascend)
                {
                    if (_ultimateLeapAirborne.Contains(evt.entity)) continue;
                    _ultimateLeapAirborne.Add(evt.entity);
                    // 첫 프레임 오버라이드를 여기서 건다 — 걸기 전에 sim 좌표가 한 프레임이라도
                    // 소비되면 팝이 보인다(BossLeap rev 3 실측 교훈).
                    _enemyViewOverride[evt.entity] = (evt.world, 0f);
                    StartCoroutine(RunUltimateLeapAscend(evt.entity, evt.world));
                }
                else
                {
                    // 강하는 sim 이 착지를 확정한 프레임에 온다. 상승이 없었으면(취소·teardown)
                    // 무시 — 허공에서 유닛이 떨어지는 그림을 만들지 않는다.
                    if (!_ultimateLeapAirborne.Remove(evt.entity)) continue;
                    StartCoroutine(RunUltimateLeapDescend(evt.entity, evt.world, evt.dataIndex));
                }
            }
        }

        // ── 상승: 이탈 위치에서 화면 밖으로 ──

        private IEnumerator RunUltimateLeapAscend(Entity entity, Unity.Mathematics.float3 from)
        {
            float duration = Mathf.Max(0.05f, ultimateLeapAscendSeconds);
            float t = 0f;
            while (t < duration)
            {
                // 오버라이드 키 부재 = 취소 신호(teardown). BossLeap 과 같은 계약.
                if (!_enemyViewOverride.ContainsKey(entity) || !_em.Exists(entity)) yield break;
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle);
                float k = Mathf.Clamp01(t / duration);
                // 가속 상승(ease-in) — 튀어오르는 맛. 수평은 이탈 위치 고정이라 sim 좌표 그대로.
                _enemyViewOverride[entity] = (from, ultimateLeapHeight * k * k);
                yield return null;
            }
            // 화면 밖 도달 — 강하 신호가 올 때까지 이 높이로 유지한다(오버라이드는 살아 있어야
            // 매 프레임 피드가 sim 좌표(착지 셀)를 그리지 않는다).
            while (_ultimateLeapAirborne.Contains(entity) && _em.Exists(entity))
            {
                if (!_enemyViewOverride.ContainsKey(entity)) yield break;
                _enemyViewOverride[entity] = (from, ultimateLeapHeight);
                yield return null;
            }
        }

        // ── 강하: 착지 셀 직상방에서 수직 낙하 ──

        private IEnumerator RunUltimateLeapDescend(Entity entity, Unity.Mathematics.float3 to, int dataIndex)
        {
            float duration = Mathf.Max(0.05f, ultimateLeapDescendSeconds);
            float t = 0f;
            bool abandoned = false;
            while (t < duration)
            {
                if (!_enemyViewOverride.ContainsKey(entity) || !_em.Exists(entity))
                {
                    abandoned = true;
                    break;
                }
                t += TimeManager.Instance.DeltaTime(TimeDomain.Battle);
                float k = Mathf.Clamp01(t / duration);
                // 가속 낙하(ease-in) — 끝속도 최대. 수평은 착지 셀 고정.
                _enemyViewOverride[entity] = (to, ultimateLeapHeight * (1f - k) * (1f - k));
                yield return null;
            }

            _enemyViewOverride.Remove(entity);
            if (abandoned) yield break;
            // 착지 순간 — 슬램 VFX 는 **뷰가 도착한 지금** 뜬다(sim 은 이미 2초 전 착지를 확정했다).
            // BossLeap 의 "착지 퍼프가 뷰 도착보다 먼저 터지지 않는다" 계약 미러.
            PlayLeapPuff(dataIndex, new Vector3(to.x, to.y, to.z));
            if (spineUnitPool != null && spineUnitPool.TryGet(entity, out var view) && view != null)
                view.PlayLandingSquash(0.14f, 0.06f);
        }
    }
}
