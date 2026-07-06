using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Presentation
{
    // Prefab-only floating damage-number layer, mirroring VfxSpawner. BattleBridge
    // holds a SerializeField reference and calls Spawn() when draining
    // DamageNumberEvents. Pools popups to avoid GC spikes under heavy fire.
    //
    // Placement (damage-number-visual-upgrade unit 0):
    //  - Head anchor is applied in VIEW space (post-ToView, world-up) — sim-Y is
    //    dropped by BoardSpace.ToView so a sim-space offset would be a no-op.
    //  - Overlap avoidance uses an occupancy grid in the camera billboard basis
    //    (project onto camera right/up) so cells are screen-aligned regardless of
    //    board tilt; a deterministic upward-biased spiral finds the nearest free cell.
    //
    // Impact spark (unit 3):
    //  - Spawn is called per-event during BattleBridge drain; it only accumulates the
    //    frame's spawn positions. LateUpdate clusters them and plays ONE pooled spark
    //    per cluster (mobile draw-call safe — not one particle per number). Pure
    //    presentation: no ECS access, no new event channel.
    public class DamageNumberSpawner : MonoBehaviour
    {
        [Header("Required")]
        [SerializeField] private GameObject popupPrefab;
        [SerializeField] private DamageNumberStyle style = new DamageNumberStyle();

        [Header("Placement")]
        [Tooltip("미할당 시 Camera.main 사용")]
        [SerializeField] private Camera billboardCamera;

        [Header("Impact spark (unit 3)")]
        [Tooltip("클러스터당 스파크 파티클 _SKELETON 프리팹. 비우면 스파크 없이 진행(경고 후 스킵).")]
        [SerializeField] private GameObject sparkPrefab;

        private DamageNumberPool _pool;

        // Occupancy grid state (view lifetime reservation, camera-basis cells).
        private readonly HashSet<Vector2Int> _occupied = new HashSet<Vector2Int>();
        private readonly Dictionary<DamageNumberView, Vector2Int> _active = new Dictionary<DamageNumberView, Vector2Int>();
        private int _spawnSeq; // monotonic per session — feeds deterministic motion (unit 1).

        // Spark batching + pooling (unit 3).
        private readonly List<Vector3> _frameSparks = new List<Vector3>();
        private readonly Queue<ParticleSystem> _sparkIdle = new Queue<ParticleSystem>();
        private readonly List<ParticleSystem> _sparkActive = new List<ParticleSystem>();
        private bool[] _clusterUsed = new bool[0];
        private bool _sparkWarned;

        // Cached spiral offsets (ring-ordered, upward-biased). Rebuilt if ring count changes.
        private static Vector2Int[] _spiral;
        private static int _spiralRings = -1;

        private void Awake()
        {
            style.EnsureDefaults();
            if (popupPrefab != null)
                _pool = new DamageNumberPool(popupPrefab, transform);
        }

        private void OnValidate()
        {
            style?.EnsureDefaults();
            if (style != null && style.enableSpark && sparkPrefab == null)
                Debug.LogWarning("[DamageNumberSpawner] enableSpark=true 인데 sparkPrefab 슬롯이 비었습니다 — 스파크 없이 진행합니다.");
        }

        public void Spawn(Vector3 worldPos, float amount)
        {
            if (popupPrefab == null)
            {
                Debug.LogError("[DamageNumberSpawner] popupPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            var cam = billboardCamera != null ? billboardCamera : Camera.main;
            if (cam == null)
            {
                Debug.LogError("[DamageNumberSpawner] 빌보드 카메라를 찾을 수 없습니다 (billboardCamera/Camera.main 모두 null).");
                return;
            }
            if (_pool == null) _pool = new DamageNumberPool(popupPrefab, transform);

            int shown = Mathf.Max(1, Mathf.RoundToInt(amount));

            // sim → view: ToView applied here ONLY (View.Play receives view-space, no re-transform).
            Vector3 viewPos = (Vector3)Wassup.Core.BoardSpace.ToView(worldPos);
            // Head anchor: lift along world-up, post-ToView (sim-Y is dropped by ToView), matching driftUp's axis.
            Vector3 anchor = viewPos + Vector3.up * style.headViewOffset;

            // Occupancy grid in the camera billboard basis → screen-aligned non-overlap under any pitch.
            Vector3 camRight = cam.transform.right;
            Vector3 camUp = cam.transform.up;
            float cw = Mathf.Max(0.01f, style.cellSize.x);
            float ch = Mathf.Max(0.01f, style.cellSize.y);
            float u = Vector3.Dot(anchor, camRight);
            float v = Vector3.Dot(anchor, camUp);
            var intended = new Vector2Int(Mathf.FloorToInt(u / cw), Mathf.FloorToInt(v / ch));
            var slot = FindFreeCell(intended, _occupied, Mathf.Max(0, style.maxSearchRings));

            // Snap into the slot's cell center within the camera plane (preserve depth along camForward).
            float centerU = (slot.x + 0.5f) * cw;
            float centerV = (slot.y + 0.5f) * ch;
            Vector3 finalPos = anchor + camRight * (centerU - u) + camUp * (centerV - v);

            var view = _pool.Get();
            if (view == null)
            {
                Debug.LogError("[DamageNumberSpawner] popupPrefab 에 DamageNumberView 가 없습니다.");
                return;
            }
            _occupied.Add(slot);
            _active[view] = slot;
            view.Play(shown, finalPos, cam, style, OnViewComplete, _spawnSeq++);

            // Accumulate for this frame's spark clustering (flushed in LateUpdate).
            if (style.enableSpark && shown >= style.sparkMinDamage)
                _frameSparks.Add(finalPos);
        }

        // Completion callback (natural Finish or idempotent OnDisable): free the reserved
        // cell (self-healing) then recycle. Dictionary/HashSet removes are no-ops if absent.
        private void OnViewComplete(DamageNumberView view)
        {
            if (view != null && _active.TryGetValue(view, out var cell))
            {
                _occupied.Remove(cell);
                _active.Remove(view);
            }
            _pool.Return(view);
        }

        private void LateUpdate()
        {
            FlushFrameSparks();
            ReclaimSparks();
        }

        // Cluster this frame's spawn positions and play ONE pooled spark per cluster.
        private void FlushFrameSparks()
        {
            int n = _frameSparks.Count;
            if (n == 0) return;
            if (!style.enableSpark || sparkPrefab == null)
            {
                if (style.enableSpark && sparkPrefab == null && !_sparkWarned)
                {
                    Debug.LogWarning("[DamageNumberSpawner] sparkPrefab slot empty, skipping impact spark");
                    _sparkWarned = true;
                }
                _frameSparks.Clear();
                return;
            }
            if (_clusterUsed.Length < n) _clusterUsed = new bool[n];
            else Array.Clear(_clusterUsed, 0, n);
            float r2 = Mathf.Max(0.0001f, style.sparkClusterRadius);
            r2 *= r2;
            for (int i = 0; i < n; i++)
            {
                if (_clusterUsed[i]) continue;
                Vector3 sum = _frameSparks[i];
                int c = 1;
                _clusterUsed[i] = true;
                for (int j = i + 1; j < n; j++)
                {
                    if (_clusterUsed[j]) continue;
                    if ((_frameSparks[j] - _frameSparks[i]).sqrMagnitude <= r2)
                    {
                        sum += _frameSparks[j];
                        c++;
                        _clusterUsed[j] = true;
                    }
                }
                PlaySparkAt(sum / c);
            }
            _frameSparks.Clear();
        }

        private void PlaySparkAt(Vector3 pos)
        {
            var ps = GetSpark();
            if (ps == null) return;
            var tr = ps.transform;
            tr.position = pos;
            tr.localScale = Vector3.one * Mathf.Max(0.01f, style.sparkScale);
            ps.gameObject.SetActive(true);
            ps.Clear(true);
            ps.Play(true);
            _sparkActive.Add(ps);
        }

        private ParticleSystem GetSpark()
        {
            while (_sparkIdle.Count > 0)
            {
                var s = _sparkIdle.Dequeue();
                if (s != null) return s;
            }
            var go = Instantiate(sparkPrefab, transform);
            return go.GetComponent<ParticleSystem>();
        }

        private void ReclaimSparks()
        {
            for (int i = _sparkActive.Count - 1; i >= 0; i--)
            {
                var s = _sparkActive[i];
                if (s == null) { _sparkActive.RemoveAt(i); continue; }
                if (!s.IsAlive(true))
                {
                    _sparkActive.RemoveAt(i);
                    s.gameObject.SetActive(false);
                    _sparkIdle.Enqueue(s);
                }
            }
        }

        // Pure, deterministic (no RNG / no time): return the nearest free cell to `intended`
        // by an upward-biased ring spiral, or `intended` if all rings are occupied.
        public static Vector2Int FindFreeCell(Vector2Int intended, HashSet<Vector2Int> occupied, int maxRings)
        {
            var spiral = GetSpiral(Mathf.Max(0, maxRings));
            for (int i = 0; i < spiral.Length; i++)
            {
                var c = new Vector2Int(intended.x + spiral[i].x, intended.y + spiral[i].y);
                if (occupied == null || !occupied.Contains(c)) return c;
            }
            return intended;
        }

        private static Vector2Int[] GetSpiral(int maxRings)
        {
            if (_spiral != null && _spiralRings == maxRings) return _spiral;
            var list = new List<Vector2Int>();
            for (int dy = -maxRings; dy <= maxRings; dy++)
                for (int dx = -maxRings; dx <= maxRings; dx++)
                    list.Add(new Vector2Int(dx, dy));
            // Order: nearer ring first (Chebyshev), then upward (+y), then closer column, then dx sign.
            // Total order → fully deterministic; the (0,0) offset sorts first (ring 0).
            list.Sort((a, b) =>
            {
                int ra = Mathf.Max(Mathf.Abs(a.x), Mathf.Abs(a.y));
                int rb = Mathf.Max(Mathf.Abs(b.x), Mathf.Abs(b.y));
                if (ra != rb) return ra - rb;
                if (a.y != b.y) return b.y - a.y;      // higher y = screen-up first (bias up)
                int ax = Mathf.Abs(a.x), bx = Mathf.Abs(b.x);
                if (ax != bx) return ax - bx;
                return a.x - b.x;
            });
            _spiral = list.ToArray();
            _spiralRings = maxRings;
            return _spiral;
        }
    }
}
