using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Data.Authoring;
using Wassup.BattleCore;
using Wassup.Data;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 6c — **바닥에 놓이는 것**: 존 장판 + 길막 설치물. 옛 브리지의
    // `SpawnHazardWithVisual`·`SpawnBlockingHazardWithVisual`·`DrainHazardDestroyedEvents` +
    // `BlockingHazardPresenter`(278줄) + `HazardVisualLifetime` 의 후계다.
    //
    // 둘을 한 풀에 두는 이유: 둘 다 「판 위 칸에 물건이 선다 → 사라진다」이고 방출 순서가 같다
    // (`ViewOrder.Board` — 유닛보다 먼저 바닥에 깔린다). 사건 문은 다르다:
    //   · 존 장판 — `HazardSpawned` / `HazardDestroyed`(수명 만료). 유닛이 아니다.
    //   · 길막 — `UnitSpawned`(`UnitKind.BlockingHazard`) / `UnitDestroyed`. **유닛**이다(체력이 있고
    //     맞고 부서진다). 그래서 유닛 뷰 풀은 이 종류를 건너뛴다.
    //
    // ⚠ **옛 수명 컴포넌트(`HazardVisualLifetime`)는 안 옮긴다.** 그것은 뷰가 자기 시계로 장판을
    // 지우던 것이라, 판이 느려지거나(카드 슬로모) 멈추면 **규칙보다 먼저** 그림이 사라졌다. 이제
    // 그림의 수명 = 코어 개체의 수명이다 — 소멸 사건이 지운다(계약 7).
    //
    // ⚠ **크기는 사건의 짝으로만** 그린다(제약 13, 구현 6). 존의 판정은 「중심 칸 + 반경 N(칸) +
    // 칸 반폭 + 대상의 몸」이고 장판은 자리형이라 원점 항 = 칸 반폭(`OriginBody == 0`)이다. 그러므로
    // 그림의 지름 = 2 × (N + 칸 반폭) — 옛 모양→한 변 표(1칸 1 · 3×3 3 · 반경 r 은 2r+1)와 **정확히 같은
    // 값**이 규칙 쪽 식에서 나온다. 대상의 몸은 그리지 않는다(그것은 적의 그림자가 말한다).
    //
    // ⚠ 옛 **절차 폴백 VFX**(프리팹 없는 길막의 떨어지는 돌·먼지)는 안 옮긴다 — 파티클 수치가 전부 코드
    // 리터럴이고 머티리얼이 `Shader.Find` 였다(제약 6 · 추가 제약). 프리팹이 비면 **경고하고 그림 없이** 선다.
    [DisallowMultipleComponent]
    public sealed class CoreHazardViewPool : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        private sealed class BlockerView
        {
            public GameObject Visual;
            public BlockingHazardSO Authoring;
        }

        private readonly Dictionary<int, GameObject> _zones = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, BlockerView> _blockers = new Dictionary<int, BlockerView>();
        private readonly HashSet<string> _warned = new HashSet<string>();
        private readonly List<int> _scratch = new List<int>();
        private float _nextSweep;

        /// <summary>선 존 장판 뷰 수. 「뷰 수 = 코어 개체 수」의 오른쪽 항(존).</summary>
        public int ZoneViewCount
        {
            get { int n = 0; foreach (var kv in _zones) if (kv.Value != null) n++; return n; }
        }

        /// <summary>선 길막 뷰 수. 「뷰 수 = 코어 개체 수」의 오른쪽 항(길막).</summary>
        public int BlockerViewCount => _blockers.Count;   // 그림 없는 길막(프리팹 미저작)도 자리를 잡으므로 사전 수가 곧 개체 수

        /// <summary>그 존 장판의 그림 지름(월드). 테스트가 「그림 = 판정 자」를 묻는 창구.</summary>
        public bool TryGetZoneDiameter(SimEntityId id, out float diameter)
        {
            diameter = 0f;
            if (!_zones.TryGetValue(id.Value, out var go) || go == null) return false;
            diameter = go.transform.localScale.x;
            return true;
        }

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Board, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            Clear();
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted: Clear(); break;
                case CoreEventKind.HazardSpawned: SpawnZone(e); break;
                case CoreEventKind.HazardDestroyed: DespawnZone(e.A.Value); break;
                case CoreEventKind.UnitSpawned:
                    if ((UnitKind)e.Arg == UnitKind.BlockingHazard) SpawnBlocker(e);
                    break;
                case CoreEventKind.UnitDestroyed:
                    if (_blockers.ContainsKey(e.A.Value)) DespawnBlocker(e.A.Value, e.SiteFired.Pos, destroyed: true);
                    break;
            }
        }

        // ── 존 장판 ──────────────────────────────────────────────────────────
        private void SpawnZone(CoreEvent e)
        {
            if (_zones.ContainsKey(e.A.Value)) return;
            var so = _driver != null ? _driver.ViewAssets.Hazard(e.DefIndex) : null;
            if (so == null || so.visualPrefab == null)
            {
                Warn(so != null ? so.name : $"hazard#{e.DefIndex}",
                     $"[CoreHazardViewPool] 존 장판 '{(so != null ? so.name : e.DefIndex.ToString())}' 에 visualPrefab 이 없다 — 보이지 않는 장판이 선다.");
                return;
            }

            var center = e.SiteFired.Pos;
            // 옛 브리지는 sim 높이 0.05 를 줬지만 `BoardSpace.ToView` 가 sim-Y 를 버리므로 무동작이었다 —
            // 그 값을 view 높이로 «살려» 옮기지 않는다(그림이 달라진다).
            var view = (Vector3)Wassup.Core.BoardSpace.ToView(new float3(center.x, 0f, center.z));
            var go = Instantiate(so.visualPrefab, view, Quaternion.identity, transform);
            // ⚠ 옛 장판 프리팹은 **자기 시계**(`HazardVisualLifetime`, 실시간 `Time.deltaTime`)를 달고 있다 —
            // 그대로 두면 판이 멈춰도(카드 슬로모·일시정지) 그림이 저작 수명 뒤에 **스스로 파괴**되고,
            // 이 풀의 사전에는 죽은 참조가 남는다(6c Play 스모크에서 실측: 뷰 수 2, 선 오브젝트 0).
            // 그림의 수명 = 코어 개체의 수명이다 — 그 시계를 떼고 소멸 사건만 지운다.
            var ownClock = go.GetComponent<Wassup.Presentation.HazardVisualLifetime>();
            if (ownClock != null) DestroyImmediate(ownClock);
            // `Arg` = 반경(칸). 음수 = 존 효과 없음(F18) — 그림은 칸 하나로 선다(옛: 한 변 1).
            float diameterTiles = 2f * CoreDrawRadius.AreaTiles(Mathf.Max(0, e.Arg), e.SiteFired.OriginBody);
            float tile = _driver.TileSize;
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(diameterTiles * tile, s.y * tile, diameterTiles * tile);
            go.name = $"CoreZone_{so.name}_{e.A.Value}";
            _zones[e.A.Value] = go;
        }

        private void DespawnZone(int id)
        {
            if (!_zones.TryGetValue(id, out var go)) return;
            _zones.Remove(id);
            if (go != null) Destroy(go);
        }

        // ── 길막 ─────────────────────────────────────────────────────────────
        private void SpawnBlocker(CoreEvent e)
        {
            if (_blockers.ContainsKey(e.A.Value)) return;
            var so = _driver != null ? _driver.ViewAssets.Blocker(e.DefIndex) : null;
            if (so == null || so.visualPrefab == null)
            {
                Warn(so != null ? so.name : $"blocker#{e.DefIndex}",
                     $"[CoreHazardViewPool] 길막 '{(so != null ? so.name : e.DefIndex.ToString())}' 에 visualPrefab 이 없다 — 보이지 않는 길막이 선다.");
                // 그림이 없어도 **자리는 잡는다** — 소멸 사건에서 파괴 VFX 를 낼 수 있어야 한다.
                _blockers[e.A.Value] = new BlockerView { Visual = null, Authoring = so };
                return;
            }

            var view = (Vector3)Wassup.Core.BoardSpace.ToView(e.SiteFired.Pos);
            var go = Instantiate(so.visualPrefab, view, Quaternion.identity, transform);
            go.name = $"CoreBlocker_{so.name}_{e.A.Value}";
            // ⚠ 스폰 VFX 는 **SO 의 것**이다 — 옛 초판은 이것을 프리젠터에 안 넘겨 죽은 저작이 됐다.
            if (so.spawnVfxPrefab != null)
                Instantiate(so.spawnVfxPrefab, go.transform.position, Quaternion.identity, go.transform);
            _blockers[e.A.Value] = new BlockerView { Visual = go, Authoring = so };
        }

        private void DespawnBlocker(int id, float3 simPos, bool destroyed)
        {
            if (!_blockers.TryGetValue(id, out var b)) return;
            _blockers.Remove(id);
            var at = b.Visual != null ? b.Visual.transform.position : (Vector3)Wassup.Core.BoardSpace.ToView(simPos);
            if (destroyed && b.Authoring != null && b.Authoring.destructionVfxPrefab != null)
            {
                // ⚠ 파괴 VFX 는 **부모 없이** 뜬다(설치물이 곧 사라진다). 스스로 치우지 않으면 판에 영구히
                // 쌓인다 — 옛 실측으로 42개까지 누적됐다. 벤더 VFX 는 stopAction 이 None 이라 자멸을 기대 못 한다.
                var fx = Instantiate(b.Authoring.destructionVfxPrefab, at, Quaternion.identity);
                Destroy(fx, EstimateVfxLifetime(fx));
            }
            if (b.Visual != null) Destroy(b.Visual);
        }

        // 프리팹이 다 재생되는 시간의 상한(옛 `BlockingHazardPresenter.EstimateVfxLifetime` 그대로).
        private static float EstimateVfxLifetime(GameObject instance)
        {
            const float fallback = 2f;
            const float cap = 12f;
            if (instance == null) return fallback;
            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            float longest = 0f;
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                float total = main.duration + main.startLifetime.constantMax + main.startDelay.constantMax;
                if (total > longest) longest = total;
            }
            if (longest <= 0f) return fallback;
            return Mathf.Min(longest + 0.5f, cap);
        }

        /// <summary>길막의 저작(오버헤드 게이지가 높이를 묻는다). 없으면 null.</summary>
        public BlockingHazardSO BlockerAuthoringOf(SimEntityId id)
            => _blockers.TryGetValue(id.Value, out var b) ? b.Authoring : null;

        // ── 자가 치유(초당 1회 — 정상 경로가 아니다) ───────────────────────────
        private void LateUpdate()
        {
            if (_driver == null || !_driver.Running) return;
            if (Time.unscaledTime < _nextSweep) return;
            _nextSweep = Time.unscaledTime + 1f;

            _scratch.Clear();
            var hazards = _driver.Match.World.Hazards;
            foreach (var id in _zones.Keys)
            {
                bool live = false;
                for (int i = 0; i < hazards.Count; i++)
                    if (hazards[i].Id.Value == id) { live = true; break; }
                if (!live) _scratch.Add(id);
            }
            for (int i = 0; i < _scratch.Count; i++)
            {
                Debug.LogWarning($"[CoreHazardViewPool] 유령 장판 회수 — id {_scratch[i]}. 소멸 사건을 안 낸 경로가 있다(계약 7).", this);
                DespawnZone(_scratch[i]);
            }

            _scratch.Clear();
            foreach (var id in _blockers.Keys)
                if (!_driver.IsAlive(new SimEntityId(id))) _scratch.Add(id);
            for (int i = 0; i < _scratch.Count; i++)
            {
                Debug.LogWarning($"[CoreHazardViewPool] 유령 길막 회수 — id {_scratch[i]}. 소멸 사건을 안 낸 경로가 있다(계약 7).", this);
                DespawnBlocker(_scratch[i], default, destroyed: false);
            }
            _scratch.Clear();
        }

        private void Warn(string key, string message)
        {
            if (_warned.Add(key)) Debug.LogWarning(message, this);
        }

        public void Clear()
        {
            foreach (var kv in _zones) if (kv.Value != null) Destroy(kv.Value);
            _zones.Clear();
            foreach (var kv in _blockers) if (kv.Value.Visual != null) Destroy(kv.Value.Visual);
            _blockers.Clear();
        }
    }
}
