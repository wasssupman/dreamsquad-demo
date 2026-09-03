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
        [Tooltip("착지 눌림 세기(0=없음). 일반 도약(bossLeapLandingSquash)보다 세게 — 궁극기 체급.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float ultimateLeapLandingSquash = 0.14f;
        [Tooltip("착지 눌림 복귀 시간(초).")]
        [Range(0.02f, 0.3f)]
        [SerializeField] private float ultimateLeapLandingSquashSeconds = 0.06f;

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
            tilemapMapView?.ClearTelegraphCells(); // 예고가 매치 너머로 살아남지 않게 — clear 와 co-locate
        }

        // 착지 예고. 셀 집합을 **직접 돌지 않는다** — `BuildZoneCells` 가 이 브리지의 사각 셀 열거
        // 단일 지점이다(보드 경계 클리핑 + 스크래치 재사용 포함). 액티브 장판 점등이 은퇴한 뒤(2026-09-03)
        // 소비처는 이 예고 하나지만, 손으로 다시 돌면 "예고 셀 = 피해 셀" 계약이 두 계산의 우연한 일치에
        // 기대게 되므로 그대로 둔다.
        private void ShowLandingTelegraph(Entity entity)
        {
            if (tilemapMapView == null || !_em.HasComponent<UltimateLeapState>(entity)) return;
            var leap = _em.GetComponentData<UltimateLeapState>(entity);
            BuildZoneCells(new Vector2Int(leap.landingCell.x, leap.landingCell.y), leap.slamTileRange,
                _zoneCellScratch);
            tilemapMapView.SetTelegraphCells(_zoneCellScratch);
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
                    ShowLandingTelegraph(evt.entity);
                    StartCoroutine(RunUltimateLeapAscend(evt.entity, evt.world));
                }
                else
                {
                    // 강하는 sim 이 착지를 확정한 프레임에 온다. 상승이 없었으면(취소·teardown)
                    // 무시 — 허공에서 유닛이 떨어지는 그림을 만들지 않는다.
                    if (!_ultimateLeapAirborne.Remove(evt.entity)) continue;
                    // 예고는 **여기서** 끈다 — sim 이 착지를 확정한 순간이고, 강하 연출이 끝날
                    // 때까지 붉은 타일을 남기면 "아직 피할 수 있다" 는 거짓 신호가 된다.
                    tilemapMapView?.ClearTelegraphCells();
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
            // 화면 밖 도달 — **여기서 코루틴을 끝낸다.** 대기 루프로 같은 값을 매 프레임 재기입할
            // 필요가 없다: 이 딕셔너리의 writer 는 도약 두 기능뿐이고 소비처는 읽기만 하므로,
            // 한 번 써 둔 값이 강하가 시작될 때까지 그대로 남는다(= 뷰가 화면 밖에 머문다).
            // 취소(teardown)는 `_enemyViewOverride.Clear()` 가 키를 지우는 것으로 성립한다.
            if (_enemyViewOverride.ContainsKey(entity))
                _enemyViewOverride[entity] = (from, ultimateLeapHeight);
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
            // 풀 조회·0 가드·폴백 뷰 스킵 규약은 공용 헬퍼가 소유한다(BattleBridge.Relocation.cs).
            // 여기서 재구현하면 그 규약이 두 곳으로 갈린다.
            PlayLandingSquash(entity, ultimateLeapLandingSquash, ultimateLeapLandingSquashSeconds);
        }
    }
}
