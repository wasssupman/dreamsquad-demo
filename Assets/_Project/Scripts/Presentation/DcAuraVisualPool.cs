using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Wassup.Presentation
{
    // nightmare-whip-aura unit 3 rev 2 — dreamcatcher 슬롯 부착 오라 연출.
    // 메커닉 데이터(DcPayloadSpec.auraPrefab/auraScale)가 룩을 선언하고, 드림캐쳐
    // 베이크가 (host, prefab, scale) 를 등록하면 host 엔티티가 사는 동안 루핑
    // 인스턴스 1개가 host 뷰를 따라다닌다. 의도적으로 payload-kind-blind —
    // auraPrefab 을 선언한 어떤 메커닉이든 kind 분기 0 으로 이 경로를 탄다
    // (메커닉 연출을 범용 인프라/bridge kind 분기로 넣지 않는다, 2026-07-12).
    // 씬 배선 없음: BattleBridge 가 런타임 소유(생성자 주입)·매 프레임 Sync 구동.
    // 뷰가 풀링이라 parenting 하지 않고 위치 추종(StatusFxView 관용구) — 풀 재사용
    // 오염 방지.
    public class DcAuraVisualPool
    {
        private struct Registration
        {
            public GameObject prefab;
            public float scale;
            public GameObject instance;
        }

        private readonly Dictionary<Entity, Registration> _regs = new();
        private readonly List<Entity> _keysScratch = new();
        private readonly Func<Entity, Transform> _resolveAnchor;
        private Transform _root;

        public DcAuraVisualPool(Func<Entity, Transform> resolveAnchor)
        {
            _resolveAnchor = resolveAnchor;
        }

        // 베이크 시점 등록. host 당 오라 1개(첫 선언 승리 — 다중 선언은 degenerate
        // authoring, 겹침 스팸 방지).
        public void Register(Entity host, GameObject prefab, float scale)
        {
            if (prefab == null || host == Entity.Null || _regs.ContainsKey(host)) return;
            _regs[host] = new Registration { prefab = prefab, scale = scale <= 0f ? 1f : scale };
        }

        // 매 프레임(뷰 좌표 갱신 후) 호출: 죽은 host 정리, 뷰 있는 host 는 인스턴스
        // 보장 + 위치 추종. 뷰 일시 부재(스폰 직후 등)는 비활성으로 대기.
        public void Sync(EntityManager em)
        {
            if (_regs.Count == 0) return;
            _keysScratch.Clear();
            _keysScratch.AddRange(_regs.Keys);
            for (int i = 0; i < _keysScratch.Count; i++)
            {
                var host = _keysScratch[i];
                var reg = _regs[host];
                if (!em.Exists(host))
                {
                    if (reg.instance != null) UnityEngine.Object.Destroy(reg.instance);
                    _regs.Remove(host);
                    continue;
                }
                var anchor = _resolveAnchor != null ? _resolveAnchor(host) : null;
                if (anchor == null)
                {
                    if (reg.instance != null) reg.instance.SetActive(false);
                    continue;
                }
                if (reg.instance == null)
                {
                    if (_root == null) _root = new GameObject("DcAuraVisuals").transform;
                    reg.instance = UnityEngine.Object.Instantiate(reg.prefab, _root);
                    reg.instance.transform.localScale = Vector3.one * reg.scale;
                    _regs[host] = reg;
                }
                if (!reg.instance.activeSelf) reg.instance.SetActive(true);
                reg.instance.transform.position = anchor.position;
            }
        }

        // 배틀 teardown — 잔여 인스턴스/루트 정리 (다른 뷰 풀들과 생명주기 대칭).
        public void Clear()
        {
            foreach (var kv in _regs)
                if (kv.Value.instance != null) UnityEngine.Object.Destroy(kv.Value.instance);
            _regs.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
                _root = null;
            }
        }
    }
}
