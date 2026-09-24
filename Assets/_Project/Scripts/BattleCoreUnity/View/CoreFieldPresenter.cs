using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 7d — **회오리·포탈 장의 그림.** 6c 가 「까는 자가 없다」로 미룬 풀이다(6c 「아직 안 보이는
    // 것」). 까는 자는 액티브 카드(7b — `PullFieldSkill`·`PortalSkill` → `IntentApplier.SpawnField`)이고, 이 풀은
    // `FieldSpawned`/`FieldDespawned` 만 듣는다.
    //
    // 옛 연출: `VfxSpawner.SpawnTornado`(중심 · 반경 = (칸 수 + 칸 반폭) × 타일 · 지속) · `SpawnPortal`(입구·출구 소용돌이 +
    // 잇는 빔). 옛 전투는 **시전 지점**(브리지 `CastSkillAtTile`/`CastPortal`)이 그렸다 — 그 값은 이제 사건이 싣는다
    // (포탈 출구 = `SiteTarget` · 반경 칸 = `AreaTiles` · 지속 = `Amount`). 뷰는 반경을 재지 않고 `CoreDrawRadius` 로 합친다.
    //
    // ⚠ **수명은 코어가 준다** — 옛 뷰는 `Destroy(go, 지속 + 0.1)` 로 제 시계를 돌렸다. 여기서는 소멸 사건에 걷는다
    // (계약 7 — 모든 소멸은 소멸 사건을 낸다). 벤더 이펙트(PixPlays)의 재생 길이만 지속 값을 쓴다.
    // ⚠ 아군 버프 장은 여기서 안 그린다 — 옛 시전 연출이 회오리·포탈 둘뿐이었다(오라는 `CoreDcAuraVisualPool`).
    public sealed class CoreFieldPresenter : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Header("프리팹 (옛 씬 VfxSpawner 블록의 값 그대로 — 씬 배선 참조)")]
        [SerializeField] private GameObject _tornadoPrefab;
        [SerializeField] private GameObject _portalPrefab;

        private readonly Dictionary<int, GameObject> _live = new Dictionary<int, GameObject>();
        private readonly HashSet<string> _missingLogged = new HashSet<string>();

        /// <summary>지금 그려진 장 수(테스트 — 「사건 1 → 그림 1」의 오른쪽 항).</summary>
        public int LiveCount => _live.Count;

        private void OnEnable()
        {
            if (_driver == null) _driver = FindAnyObjectByType<BattleDriver>();
            if (_driver != null) _driver.Subscribe(ViewOrder.Effect, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            ClearAll();
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted:
                case CoreEventKind.MatchEnded:
                    ClearAll();
                    break;
                case CoreEventKind.FieldSpawned: OnSpawned(e); break;
                case CoreEventKind.FieldDespawned:
                    if (_live.TryGetValue(e.A.Value, out var go)) { if (go != null) Destroy(go); _live.Remove(e.A.Value); }
                    break;
            }
        }

        private void OnSpawned(CoreEvent e)
        {
            var kind = (FieldKind)e.Arg;
            GameObject go;
            switch (kind)
            {
                case FieldKind.Pull: go = SpawnTornado(e); break;
                case FieldKind.Portal: go = SpawnPortal(e); break;
                default: return;
            }
            if (go != null) _live[e.A.Value] = go;
        }

        private GameObject SpawnTornado(CoreEvent e)
        {
            if (_tornadoPrefab == null) { Missing(nameof(_tornadoPrefab)); return null; }
            float tile = _driver != null ? _driver.TileSize : 1f;
            float radiusWorld = CoreDrawRadius.AreaTiles(e.AreaTiles, e.SiteFired.OriginBody) * tile;
            var center = (Vector3)Wassup.Core.BoardSpace.ToView(new float3(e.SiteFired.Pos.x, 0f, e.SiteFired.Pos.z));
            var pos = new Vector3(center.x, center.y + 0.05f, center.z);
            var go = Instantiate(_tornadoPrefab, pos, Quaternion.identity, transform);
            if (FindPixPlaysVfx(go).Length > 0)
                PlayPixPlaysVfx(go, pos, pos + Vector3.forward, e.Amount, radiusWorld);
            else
                go.transform.localScale = Vector3.one * Mathf.Max(0.1f, radiusWorld);
            return go;
        }

        private GameObject SpawnPortal(CoreEvent e)
        {
            if (_portalPrefab == null) { Missing(nameof(_portalPrefab)); return null; }
            var entry = (Vector3)Wassup.Core.BoardSpace.ToView(new float3(e.SiteFired.Pos.x, 0f, e.SiteFired.Pos.z));
            var exit = (Vector3)Wassup.Core.BoardSpace.ToView(new float3(e.SiteTarget.Pos.x, 0f, e.SiteTarget.Pos.z));
            var root = Instantiate(_portalPrefab, Vector3.zero, Quaternion.identity, transform);
            var entryT = root.transform.Find("Entry");
            var exitT = root.transform.Find("Exit");
            if (entryT != null) entryT.position = new Vector3(entry.x, entry.y + 0.05f, entry.z);
            if (exitT != null) exitT.position = new Vector3(exit.x, exit.y + 0.05f, exit.z);
            var linkBeam = root.transform.Find("LinkBeam");
            var beams = linkBeam != null ? linkBeam.GetComponentsInChildren<LineRenderer>(true) : null;
            if (beams != null)
            {
                var a = new Vector3(entry.x, entry.y + 0.15f, entry.z);
                var b = new Vector3(exit.x, exit.y + 0.15f, exit.z);
                for (int i = 0; i < beams.Length; i++)
                {
                    if (beams[i] == null) continue;
                    beams[i].positionCount = 2;
                    beams[i].SetPosition(0, a);
                    beams[i].SetPosition(1, b);
                }
            }
            PlayPixPlaysPortalVfx(root, entry, exit, e.Amount);
            return root;
        }

        private void ClearAll()
        {
            foreach (var kv in _live) if (kv.Value != null) Destroy(kv.Value);
            _live.Clear();
        }

        // 조용한 리턴 금지 — 슬롯이 비면 「사건은 나는데 화면만 조용한」 상태가 된다(옛 규약: 슬롯 null = 에러). 슬롯당 1회.
        private void Missing(string slot)
        {
            if (_missingLogged.Add(slot))
                Debug.LogError($"[CoreFieldPresenter] {slot} 미할당 — 인스펙터에서 프리팹을 연결할 것.", this);
        }

        // ── 벤더(PixPlays) 재생 — 옛 `VfxSpawner` 의 사설 헬퍼 그대로(리플렉션: 벤더 어셈블리를 참조하지 않는다) ──

        private static void PlayPixPlaysVfx(GameObject root, Vector3 source, Vector3 target, float durationSec, float radiusWorld)
        {
            var effects = FindPixPlaysVfx(root);
            for (int i = 0; i < effects.Length; i++)
                if (effects[i] != null)
                    PlayPixPlaysEffect(effects[i], source, target, source, durationSec, Mathf.Max(0.1f, radiusWorld));
        }

        private static void PlayPixPlaysPortalVfx(GameObject root, Vector3 entryWorld, Vector3 exitWorld, float durationSec)
        {
            var effects = FindPixPlaysVfx(root);
            var source = new Vector3(entryWorld.x, entryWorld.y + 0.15f, entryWorld.z);
            var target = new Vector3(exitWorld.x, exitWorld.y + 0.15f, exitWorld.z);
            var entry = root.transform.Find("Entry");
            var exit = root.transform.Find("Exit");
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null) continue;
                bool fromExit = exit != null && effect.transform.IsChildOf(exit);
                PlayPixPlaysEffect(effect, fromExit ? target : source, fromExit ? source : target,
                                   fromExit ? target : source, durationSec, 1f);
            }
        }

        private static MonoBehaviour[] FindPixPlaysVfx(GameObject root)
        {
            if (root == null) return System.Array.Empty<MonoBehaviour>();
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            var result = new List<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null) continue;
                for (var type = behaviours[i].GetType(); type != null; type = type.BaseType)
                    if (type.FullName == "PixPlays.ElementalVFX.BaseVfx") { result.Add(behaviours[i]); break; }
            }
            return result.ToArray();
        }

        private static void PlayPixPlaysEffect(MonoBehaviour effect, Vector3 source, Vector3 target, Vector3 ground,
                                               float durationSec, float radiusWorld)
        {
            var vfxDataType = effect.GetType().Assembly.GetType("PixPlays.ElementalVFX.VfxData");
            if (vfxDataType == null) return;
            var data = System.Activator.CreateInstance(vfxDataType, source, target, durationSec, radiusWorld);
            vfxDataType.GetMethod("SetGround", new[] { typeof(Vector3) })?.Invoke(data, new object[] { ground });
            effect.GetType().GetMethod("Play", new[] { vfxDataType })?.Invoke(effect, new[] { data });
        }
    }
}
