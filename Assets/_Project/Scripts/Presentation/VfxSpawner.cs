using UnityEngine;

namespace Wassup.Presentation
{
    // Phase 8 §13 — prefab-only VFX layer. Creates particle effects at world
    // positions in response to ECS events drained by BattleBridge. Non-singleton;
    // BattleBridge holds a SerializeField reference.
    //
    // Design notes:
    //   - Shuriken only (VFX Graph deliberately out of scope for Android compat).
    //   - All prefab slots must be assigned in the Inspector. If a slot is null,
    //     the method logs an error and silently returns — there is no longer a
    //     code-authored fallback (Step 4 이후 단일화, 2026-04-19).
    //   - Prefab authoring policy lives in .claude/skills/unity-vfx-authoring.
    public class VfxSpawner : MonoBehaviour
    {
        [Header("Phase 8 §13 — Prefab slots (all required)")]
        [SerializeField] private GameObject placementRingPrefab;
        [SerializeField] private GameObject meteorBurstPrefab;
        [SerializeField] private GameObject tornadoPrefab;
        [SerializeField] private GameObject portalPrefab;
        [SerializeField] private GameObject healAppliedPrefab;
        // shield-guardian-defender unit 4 — 실드 부여 원샷. 벤더 프리팹(VFX_Fire_Green)이
        // 루프형이라 SpawnShieldGranted 가 스폰 인스턴스에서 loop off + burst 로 단발화한다.
        [SerializeField] private GameObject shieldGrantedPrefab;
        [Tooltip("실드 부여 이펙트 스케일(타일 1 유닛 기준)")]
        [SerializeField] private float shieldGrantedScale = 0.7f;

        [Header("card-fly-to-target-absorb — 카드 흡수 임팩트")]
        [Tooltip("카드가 유닛/타일에 내리찍힐 때 터지는 이펙트(GA vfx_Hit_Rock03: 코어 플래시+충격파 링+파편). Null → ring+burst 폴백.")]
        [SerializeField] private GameObject cardAbsorbPrefab;
        [Tooltip("흡수 이펙트 스케일(타일 1 유닛 기준 축소)")]
        [SerializeField] private float cardAbsorbScale = 0.6f;

        [Header("goal-stability unit 5 — 골 붕괴 원샷")]
        [Tooltip("안정도 0 붕괴 순간 이펙트. v1 은 blocking hazard 파괴 VFX 재사용(정식 아트 후속)")]
        // elite-enemy-tier unit 4 — 화염 브레스. 튜닝 knob 을 인스펙터에 두는 이유: 구현자가
        // 화면을 볼 수 없어 코드에 박으면 조정마다 재컴파일 왕복이 된다.
        [SerializeField] private GameObject areaBreathPrefab;
        [SerializeField] private float areaBreathScalePerTile = 0.55f;
        [SerializeField] private float areaBreathScaleMax = 2.4f;
        // 사거리 대비 전방 오프셋. 0 = 시전자에 겹침.
        [SerializeField] private float areaBreathForwardFactor = 0.45f;
        // 분사 축 보정각. **분사가 뒤로 나가면 부호를 뒤집는다**(−90 ↔ +90).
        [SerializeField] private float areaBreathAngleOffset = 90f;

        [SerializeField] private GameObject goalCollapsePrefab;
        [Tooltip("붕괴 이펙트 스케일(타일 1 유닛 기준)")]
        [SerializeField] private float goalCollapseScale = 1.2f;

        // V1 — Placement pulse. One-shot outward radial burst that fades in 0.35s.
        public void SpawnPlacementRing(Vector3 worldPos)
        {
            if (placementRingPrefab == null)
            {
                Debug.LogError("[VfxSpawner] placementRingPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            worldPos = Wassup.Core.BoardSpace.ToView(worldPos); // tilemap-view-backend: sim→view 1회 (진입부)
            var go = Instantiate(placementRingPrefab,
                new Vector3(worldPos.x, worldPos.y + 0.02f, worldPos.z),
                Quaternion.identity, transform);
            Destroy(go, 0.6f);
        }

        // V2b — Meteor explosion burst at impact.
        public void SpawnMeteorBurst(Vector3 worldPos, float radiusWorld)
        {
            if (meteorBurstPrefab == null)
            {
                Debug.LogError("[VfxSpawner] meteorBurstPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            worldPos = Wassup.Core.BoardSpace.ToView(worldPos); // sim→view 1회
            var pos = new Vector3(worldPos.x, worldPos.y + 0.05f, worldPos.z);
            var go = Instantiate(meteorBurstPrefab, pos, Quaternion.identity, transform);
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, radiusWorld);
            Destroy(go, 1.2f);
        }

        // card-fly-to-target-absorb unit 1 — 카드 흡수 임팩트(링+버스트). 다른 Spawn* 과 달리
        // 입력이 **이미 view 좌표**(유닛 뷰 transform.position)라 ToView 하지 않는다 — 이중변환 방지
        // (sim/view 경계: docs/reference lessons). 기존 프리팹 재사용(전용 프리팹은 후속 후보).
        public void SpawnCardAbsorb(Vector3 viewPos)
        {
            // 전용 임팩트 이펙트(GA hit) 우선 — 코어 플래시+충격파 링+파편이 한 프리팹에.
            if (cardAbsorbPrefab != null)
            {
                var go = Instantiate(cardAbsorbPrefab,
                    new Vector3(viewPos.x, viewPos.y + 0.05f, viewPos.z), Quaternion.identity, transform);
                go.transform.localScale = Vector3.one * Mathf.Max(0.05f, cardAbsorbScale);
                Destroy(go, 1.6f);
                return;
            }
            // 폴백(프리팹 미할당) — 기존 링+버스트 재사용.
            if (placementRingPrefab != null)
            {
                var ring = Instantiate(placementRingPrefab,
                    new Vector3(viewPos.x, viewPos.y + 0.02f, viewPos.z), Quaternion.identity, transform);
                Destroy(ring, 0.6f);
            }
            if (meteorBurstPrefab != null)
            {
                var burst = Instantiate(meteorBurstPrefab,
                    new Vector3(viewPos.x, viewPos.y + 0.05f, viewPos.z), Quaternion.identity, transform);
                burst.transform.localScale = Vector3.one * 0.6f;
                Destroy(burst, 1.0f);
            }
        }

        // V3 — Tornado swirl held for durationSec.
        public void SpawnTornado(Vector3 centerWorld, float radiusWorld, float durationSec)
        {
            if (tornadoPrefab == null)
            {
                Debug.LogError("[VfxSpawner] tornadoPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            centerWorld = Wassup.Core.BoardSpace.ToView(centerWorld); // sim→view 1회
            var pos = new Vector3(centerWorld.x, centerWorld.y + 0.05f, centerWorld.z);
            var go = Instantiate(tornadoPrefab, pos, Quaternion.identity, transform);
            if (HasPixPlaysVfx(go))
                PlayPixPlaysVfx(go, pos, pos + Vector3.forward, durationSec, radiusWorld);
            else
                go.transform.localScale = Vector3.one * Mathf.Max(0.1f, radiusWorld);
            Destroy(go, durationSec + 0.1f);
        }

        // V4 — Portal swirl + link beam for durationSec.
        public void SpawnPortal(Vector3 entryWorld, Vector3 exitWorld, float durationSec)
        {
            if (portalPrefab == null)
            {
                Debug.LogError("[VfxSpawner] portalPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            entryWorld = Wassup.Core.BoardSpace.ToView(entryWorld); // sim→view 1회
            exitWorld = Wassup.Core.BoardSpace.ToView(exitWorld);
            var root = Instantiate(portalPrefab, Vector3.zero, Quaternion.identity, transform);
            var entryT = root.transform.Find("Entry");
            var exitT = root.transform.Find("Exit");
            if (entryT != null) entryT.position = new Vector3(entryWorld.x, entryWorld.y + 0.05f, entryWorld.z);
            if (exitT != null) exitT.position = new Vector3(exitWorld.x, exitWorld.y + 0.05f, exitWorld.z);
            var linkBeam = root.transform.Find("LinkBeam");
            var beamLines = linkBeam != null
                ? linkBeam.GetComponentsInChildren<LineRenderer>(true)
                : null;
            if (beamLines != null)
            {
                var start = new Vector3(entryWorld.x, entryWorld.y + 0.15f, entryWorld.z);
                var end = new Vector3(exitWorld.x, exitWorld.y + 0.15f, exitWorld.z);
                for (int i = 0; i < beamLines.Length; i++)
                {
                    var beamLine = beamLines[i];
                    if (beamLine == null) continue;
                    beamLine.positionCount = 2;
                    beamLine.SetPosition(0, start);
                    beamLine.SetPosition(1, end);
                }
            }
            PlayPixPlaysPortalVfx(root, entryWorld, exitWorld, durationSec);
            Destroy(root, durationSec + 0.1f);
        }

        // amount is reserved for future VFX scaling (e.g. large heal → larger burst).
        // Follow-up candidate: map amount → ParticleSystem.main.startSize.
        public void SpawnHealApplied(Vector3 worldPos, float amount = 1f)
        {
            if (healAppliedPrefab == null)
            {
                Debug.LogError("[VfxSpawner] healAppliedPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            worldPos = Wassup.Core.BoardSpace.ToView(worldPos); // sim→view 1회
            var pos = new Vector3(worldPos.x, worldPos.y + 0.08f, worldPos.z);
            var go = Instantiate(healAppliedPrefab, pos, Quaternion.identity, transform);
            Destroy(go, 1.1f);
        }

        // shield-guardian-defender unit 4 — 실드 부여 원샷. 벤더 프리팹은 루프형이라
        // 스폰 인스턴스에서만 loop off + t0 burst 로 단발화(공유 에셋 무접촉, lessons 규칙).
        public void SpawnShieldGranted(Vector3 worldPos)
        {
            if (shieldGrantedPrefab == null)
            {
                Debug.LogError("[VfxSpawner] shieldGrantedPrefab 미할당 — Inspector에서 prefab을 연결해주세요.");
                return;
            }
            worldPos = Wassup.Core.BoardSpace.ToView(worldPos); // sim→view 1회
            var pos = new Vector3(worldPos.x, worldPos.y + 0.08f, worldPos.z);
            var go = Instantiate(shieldGrantedPrefab, pos, Quaternion.identity, transform);
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, shieldGrantedScale);
            float lifetime = ConfigureOneShot(go);
            Destroy(go, lifetime);
        }

        // goal-stability unit 5 — 골 붕괴 원샷. 루프형 벤더 프리팹이 와도 ConfigureOneShot
        // 으로 단발화(공유 에셋 무접촉). 호출 = BattleBridge.DrainGoalCollapsedEvents.
        public void SpawnGoalCollapse(Vector3 worldPos)
        {
            if (goalCollapsePrefab == null)
            {
                Debug.LogWarning("[VfxSpawner] goalCollapse prefab slot empty, using code fallback");
                SpawnPlacementRing(worldPos); // 폴백: 최소한 붕괴 지점 링 펄스
                return;
            }
            worldPos = Wassup.Core.BoardSpace.ToView(worldPos); // sim→view 1회
            var pos = new Vector3(worldPos.x, worldPos.y + 0.08f, worldPos.z);
            var go = Instantiate(goalCollapsePrefab, pos, Quaternion.identity, transform);
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, goalCollapseScale);
            float lifetime = ConfigureOneShot(go);
            Destroy(go, lifetime);
        }

        // elite-enemy-tier unit 4 — 드래곤 화염 브레스 원샷. 호출 = BattleBridge 의
        // UnitAttackVisualEvents 드레인(브레스 플래그 분기).
        //
        // ★**초판은 이 전부를 BattleBridge 안에 넣었다** — 프리팹 슬롯·Instantiate·정렬 변이·
        // Destroy 타이머·튜닝 knob 4개가 브리지에 있었다. 브리지는 ECS 창구이고 원샷 VFX 의
        // 프리팹 슬롯·스폰·수명은 이 클래스가 소유한다(object-pipeline-map 의 VFX 아키타입).
        // 2026-08-13 사용자 지적으로 이관.
        //
        // `originView` 만 **이미 view 공간**이다 — Spine 뷰 앵커(입 위치)에서 나오므로 브리지가
        // spineUnitPool 로 풀어서 넘긴다. 방향은 sim XZ 로 받아 여기서 view 로 옮긴다(ToView 1회).
        public void SpawnAreaBreath(Vector3 originView, Vector2 aimDirXZ, float rangeWorld, float halfAngleDeg)
        {
            if (areaBreathPrefab == null)
            {
                // 조용한 리턴 금지 — 슬롯이 비면 「피해는 들어가는데 화면에 아무것도 없는」
                // 상태가 되고 그게 버그처럼 읽힌다(unity-vfx-integration red flag).
                Debug.LogWarning("[VfxSpawner] areaBreath prefab slot empty, using code fallback");
                SpawnPlacementRing(originView); // 폴백: 최소한 발동 지점 링 펄스
                return;
            }

            // sim XZ 방향 → view 방향. 두 점을 옮겨 차분을 쓴다(평면 보드에서 축이 돌기 때문).
            var aheadSim = new Vector3(aimDirXZ.x, 0f, aimDirXZ.y);
            Vector3 aheadView = (Vector3)Wassup.Core.BoardSpace.ToView(aheadSim)
                                - (Vector3)Wassup.Core.BoardSpace.ToView(Vector3.zero);
            if (aheadView.sqrMagnitude < 1e-6f) aheadView = Vector3.right;
            aheadView.Normalize();
            float angle = Mathf.Atan2(aheadView.y, aheadView.x) * Mathf.Rad2Deg;

            // 시전자 앞으로 내보낸다 — 원점에 두면 드래곤에 겹쳐 「발밑에 깔린 불」이 된다.
            Vector3 pos = originView + aheadView * (rangeWorld * areaBreathForwardFactor);

            // 프리팹 분사 축 보정. 축 부호는 프리팹 저작(ShapeModule.m_Rotation)에 달려 있어
            // 화면 없이 확정할 수 없다 → 인스펙터 값으로 둔다(뒤로 나가면 부호만 뒤집는다).
            var go = Instantiate(areaBreathPrefab, pos,
                                 Quaternion.Euler(0f, 0f, angle + areaBreathAngleOffset), transform);

            // 연출 크기는 **저작값**이다. 콘 기하(rangeWorld × tan(반각) × 2)를 그대로 쓰면
            // 사거리 3 · 반각 50° 에서 폭 7.15 유닛이 되어 화면을 덮는다. 반각은 판정
            // 파라미터일 뿐이고 화면 크기에 관여하지 않는다.
            float s = Mathf.Clamp(rangeWorld * areaBreathScalePerTile, 0.1f,
                                  Mathf.Max(0.1f, areaBreathScaleMax));
            go.transform.localScale = Vector3.one * s;

            // 벤더 프리팹이 order 0~2 로 들어와 유닛(Compute = 수백대) 뒤에 깔린다. 빔이 겪은
            // 것과 같은 증상이라 같은 규약 — 대역을 **더해서** 프리팹 내부 상대 순서를 보존한다.
            var renderers = go.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder += BoardSortOrder.AreaBreathOrder;

            float lifetime = ConfigureOneShot(go);
            Destroy(go, lifetime);
        }

        // 루프형 파티클 프리팹을 스폰 인스턴스 단위로 단발화한다. 각 ParticleSystem 의
        // loop 를 끄고 rateOverTime→t0 burst 로 치환해 "펑 터지고 페이드"로 만든 뒤,
        // 자가 파괴 시점(최대 duration+startLifetime)을 반환한다. 공유 에셋은 건드리지 않는다.
        private static float ConfigureOneShot(GameObject root)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            float maxLife = 0f;
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = true;
                float burst = Mathf.Max(1f, main.duration) * Mathf.Max(1f, ps.emission.rateOverTime.constant);
                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(Mathf.RoundToInt(burst), 4, 24)) });
                float life = main.duration + main.startLifetime.constantMax;
                if (life > maxLife) maxLife = life;
                ps.Clear(true);
                ps.Play(true);
            }
            return maxLife > 0f ? maxLife + 0.2f : 1.5f;
        }

        private static void PlayPixPlaysVfx(GameObject root, Vector3 source, Vector3 target, float durationSec, float radiusWorld)
        {
            if (root == null) return;
            var effects = FindPixPlaysVfx(root);
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null) continue;
                PlayPixPlaysEffect(effect, source, target, source, durationSec, Mathf.Max(0.1f, radiusWorld));
            }
        }

        private static bool HasPixPlaysVfx(GameObject root)
        {
            return root != null && FindPixPlaysVfx(root).Length > 0;
        }

        private static void PlayPixPlaysPortalVfx(GameObject root, Vector3 entryWorld, Vector3 exitWorld, float durationSec)
        {
            if (root == null) return;
            var effects = FindPixPlaysVfx(root);
            var source = new Vector3(entryWorld.x, entryWorld.y + 0.15f, entryWorld.z);
            var target = new Vector3(exitWorld.x, exitWorld.y + 0.15f, exitWorld.z);
            var entry = root.transform.Find("Entry");
            var exit = root.transform.Find("Exit");
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null) continue;
                Vector3 effectSource = source;
                Vector3 effectTarget = target;
                float radius = 1f;

                if (exit != null && effect.transform.IsChildOf(exit))
                {
                    effectSource = target;
                    effectTarget = source;
                }
                else if (entry != null && effect.transform.IsChildOf(entry))
                {
                    effectSource = source;
                    effectTarget = target;
                }

                PlayPixPlaysEffect(effect, effectSource, effectTarget, effectSource, durationSec, radius);
            }
        }

        private static MonoBehaviour[] FindPixPlaysVfx(GameObject root)
        {
            if (root == null) return System.Array.Empty<MonoBehaviour>();
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            var result = new System.Collections.Generic.List<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null) continue;
                var type = behaviour.GetType();
                while (type != null)
                {
                    if (type.FullName == "PixPlays.ElementalVFX.BaseVfx")
                    {
                        result.Add(behaviour);
                        break;
                    }
                    type = type.BaseType;
                }
            }
            return result.ToArray();
        }

        private static void PlayPixPlaysEffect(MonoBehaviour effect, Vector3 source, Vector3 target, Vector3 ground, float durationSec, float radiusWorld)
        {
            if (effect == null) return;
            var vfxDataType = effect.GetType().Assembly.GetType("PixPlays.ElementalVFX.VfxData");
            if (vfxDataType == null) return;
            var data = System.Activator.CreateInstance(vfxDataType, source, target, durationSec, radiusWorld);
            var setGround = vfxDataType.GetMethod("SetGround", new[] { typeof(Vector3) });
            setGround?.Invoke(data, new object[] { ground });
            var play = effect.GetType().GetMethod("Play", new[] { vfxDataType });
            play?.Invoke(effect, new[] { data });
        }
    }
}
