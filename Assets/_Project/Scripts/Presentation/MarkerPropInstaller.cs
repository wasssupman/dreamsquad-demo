using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Presentation
{
    // map-diorama-stage unit 6 — 스폰/골 마커 **공용** 프랍(포탈) 설치자. 사용자 결정(2026-08-27): 포탈 프랍은 맵에 상관없이
    // 공유한다 — 스테이지 프리팹은 마커만 두고, 스테이지가 켜지면(MapStage.Enabled) visualRoot 가 빈 마커에 스타일의 프랍을 얹는다.
    // Mono↔Mono 연출이라 BattleBridge(ECS 창구)를 거치지 않는다. 프랍은 마커의 자식이라 스테이지 teardown 이 함께 지운다.
    // 프리팹이 visualRoot 를 직접 채웠으면(맵 전용 연출) 그쪽이 이긴다.
    // 타이밍: Instantiate 중 OnEnable 이 동기로 불리므로 브리지가 마커 등록부(앵커·균열·스트레스 훅)를 읽기 전에 visualRoot 가 차 있다.
    // EditMode(테스트·프리뷰)에선 OnEnable 이 돌지 않는다 — 프리뷰는 MapStageAuthoringTools.ApplySharedMarkerProps 가 같은 Apply 를 부른다.
    public sealed class MarkerPropInstaller : MonoBehaviour
    {
        [Tooltip("공용 프랍 스타일(Data/Maps/MarkerPropStyle). 비면 경고 1회 — 마커가 조용히 안 보이는 결함을 드러낸다.")]
        [SerializeField] private MarkerPropStyle style;

        private bool _missWarned;

        void OnEnable()
        {
            MapStage.Enabled += Install;
            // 이미 켜져 있는 스테이지(씬에 미리 놓인 dev 스테이지 등) — OnEnable 순서에 기대지 않는다.
            foreach (var stage in FindObjectsByType<MapStage>(FindObjectsSortMode.None)) Install(stage);
        }

        void OnDisable() => MapStage.Enabled -= Install;

        void Install(MapStage stage)
        {
            if (style == null)
            {
                if (!_missWarned)
                {
                    _missWarned = true;
                    Debug.LogWarning("[MarkerPropInstaller] style 미배선 — 스폰/골 마커에 포탈 프랍이 붙지 않는다(BattleScene 의 _MarkerProps 에 MarkerPropStyle 을 물릴 것).", this);
                }
                return;
            }
            Apply(stage, style);
        }

        // 단일 규칙(런타임·에디터 프리뷰 공용): visualRoot 가 **빈** 마커에만, 호스트 밑 identity(수직 포탈)로 얹고 visualRoot 로 등록한다.
        // 멱등 — 두 번 불러도 이미 채워진 마커는 건너뛴다. 반환 = 이번에 얹은 수.
        public static int Apply(MapStage stage, MarkerPropStyle style)
        {
            int n = 0;
            if (style.spawnProp != null)
                foreach (var s in stage.GetComponentsInChildren<SpawnMarker>(true))
                    if (s.visualRoot == null) { s.visualRoot = Attach(s.transform, style.spawnProp); n++; }
            if (style.goalProp != null)
                foreach (var g in stage.GetComponentsInChildren<GoalMarker>(true))
                    if (g.visualRoot == null) { g.visualRoot = Attach(g.transform, style.goalProp); n++; }
            return n;
        }

        static Transform Attach(Transform host, GameObject prefab)
        {
            var go = Instantiate(prefab, host, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }
    }
}
