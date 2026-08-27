using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Presentation
{
    // map-diorama-stage unit 6 — 스폰/골 마커 **공용** 프랍(포탈) 설치자. 사용자 결정(2026-08-27): 포탈 프랍은 맵에 상관없이
    // 공유한다 — 스테이지 프리팹은 마커만 두고, 스테이지가 켜지면(MapStage.Enabled) visualRoot 가 빈 마커에 스타일의 프랍을 얹는다.
    // Mono↔Mono 연출이라 BattleBridge(ECS 창구)를 거치지 않는다. 프랍은 마커의 자식이라 스테이지 teardown 이 함께 지운다.
    // 프리팹이 visualRoot 를 직접 채웠으면(맵 전용 연출) 그쪽이 이긴다.
    // 타이밍: Instantiate 중 OnEnable 이 동기로 불리므로 브리지가 마커 등록부(앵커·균열·스트레스 훅)를 읽기 전에 visualRoot 가 차 있다
    // (스테이지 루트가 활성일 때만 — 비활성 루트는 OnEnable 도 등록부도 없다). GoalMarker 는 visualRoot 가 바뀌면 렌더러 캐시를 다시 짓는다.
    // EditMode(테스트·프리뷰)에선 OnEnable 이 돌지 않는다 — 프리뷰는 MapStageAuthoringTools.ApplySharedMarkerProps 가 같은 Apply 를 부른다.
    [DisallowMultipleComponent]
    public sealed class MarkerPropInstaller : MonoBehaviour
    {
        [Tooltip("공용 프랍 스타일(Data/Maps/MarkerPropStyle). 비어 있거나 슬롯이 비면 경고 1회 — 마커가 조용히 안 보이는 결함을 드러낸다.")]
        [SerializeField] private MarkerPropStyle style;

        private bool _warned;

        void OnEnable()
        {
            // 에디터에서 열린 씬을 더럽히지 않는다(ExecuteAlways 가 붙는 날의 방어) — 프리뷰는 ApplySharedMarkerProps 가 담당.
            if (!Application.isPlaying) return;
            MapStage.Enabled += Install;
            // 먼저 켜진 스테이지(씬에 미리 놓인 dev 스테이지 등) — OnEnable 순서에 기대지 않는다.
            // 내 씬만 훑는다: 프리팹 스테이지·프리뷰 씬·다른 씬의 스테이지는 이 설치자의 몫이 아니다. 이후 인스턴스화되는 스테이지는 Enabled 로 온다.
            foreach (var stage in FindObjectsByType<MapStage>(FindObjectsInactive.Exclude))
                if (stage.gameObject.scene == gameObject.scene) Install(stage);
        }

        void OnDisable() => MapStage.Enabled -= Install;

        void Install(MapStage stage)
        {
            if (style == null || style.spawnProp == null || style.goalProp == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning(style == null
                        ? "[MarkerPropInstaller] style 미배선 — 스폰/골 마커에 포탈 프랍이 붙지 않는다(BattleScene 의 _MarkerProps 에 MarkerPropStyle 을 물릴 것)."
                        : $"[MarkerPropInstaller] MarkerPropStyle 슬롯 비어 있음(spawnProp={(style.spawnProp ? style.spawnProp.name : "null")}, goalProp={(style.goalProp ? style.goalProp.name : "null")}) — 빈 슬롯의 마커는 보이지 않는다(러너 marker_prop_style 로 채움).",
                        this);
                }
                if (style == null) return;
            }
            Apply(stage, style);
        }

        // 단일 규칙(런타임·에디터 프리뷰 공용): **활성** 서브트리의 visualRoot 가 빈 마커에만(스캐너·브리지 등록부와 같은 범위 — 비활성 마커는
        // 맵에 없다), 호스트 밑 identity(수직 포탈)로 얹고 visualRoot 로 등록한다. 멱등 — 이미 채워진 마커는 건너뛴다. 반환 = 이번에 얹은 수.
        public static int Apply(MapStage stage, MarkerPropStyle style)
        {
            int n = 0;
            if (style.spawnProp != null)
                foreach (var s in stage.GetComponentsInChildren<SpawnMarker>(false))
                    if (s.visualRoot == null) { s.visualRoot = Attach(s.transform, style.spawnProp); n++; }
            if (style.goalProp != null)
                foreach (var g in stage.GetComponentsInChildren<GoalMarker>(false))
                    if (g.visualRoot == null) { g.visualRoot = Attach(g.transform, style.goalProp); n++; }
            return n;
        }

        static Transform Attach(Transform host, GameObject prefab)
        {
            var t = Instantiate(prefab, host, false).transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }
    }
}
