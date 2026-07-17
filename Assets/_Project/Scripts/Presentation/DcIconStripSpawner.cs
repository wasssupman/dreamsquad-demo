using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Presentation
{
    // unit-dreamcatcher-icons unit 1 — 부착 카드 아이콘 스트립 스포너. StatusFxSpawner 와
    // 달리 매 프레임 reconcile 이 아니라 DreamcatcherHandController.AttachmentsChanged
    // 이벤트 시점에만 전체 리빌드한다(부착 변경은 이벤트로 정확히 잡히고, 배치 유닛 수는
    // 그리드 상한이라 전체 리빌드로 충분 — spec 계약 2). 앵커는 BattleBridge 게이트웨이
    // 경유(TryGetUnitViewAnchor) — 뷰 계층은 EntityManager 를 모른다.
    public class DcIconStripSpawner : MonoBehaviour
    {
        [SerializeField] private DreamcatcherHandController hand;
        [SerializeField] private BattleBridge bridge;
        [Tooltip("미할당 시 Camera.main 사용")]
        [SerializeField] private Camera billboardCamera;

        [Header("Layout")]
        [Tooltip("유닛 앵커 기준 스트립 오프셋. 카메라 평면 기준(월드 아님) — HeadAnchor 참조. Sleep Zz(StatusFx) 와 y 분리")]
        // 1.64 = 구 월드기준 2.6 과 화면상 같은 높이(HeadAnchor 등가식). 월드 기준일 때
        // 외곽 타일 아이콘이 바깥으로 밀리던 문제로 카메라 평면 전환 — 값 옮길 때 등가식 필수.
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.64f, 0f);
        [SerializeField] private float cardHeight = 0.42f;
        [SerializeField] private float spacing = 0.05f;

        [Header("Frame")]
        [SerializeField] private Color plateFill = new Color(0.09f, 0.11f, 0.2f, 0.9f);   // 인게임 HUD 네이비
        [SerializeField] private Color squadBorder = new Color(1f, 0.82f, 0.35f, 1f);     // 골드 — Squad hosted
        [SerializeField] private Color unitBorder = new Color(0.45f, 0.85f, 1f, 1f);      // 청록 — Unit 부착

        private readonly List<(Entity host, DreamcatcherCard card)> _attachments = new();
        private readonly Dictionary<Entity, List<DreamcatcherCard>> _byHost = new();
        private readonly Dictionary<Entity, DcIconStripView> _active = new();
        private readonly Queue<DcIconStripView> _pool = new();
        private readonly List<Entity> _toHide = new();
        private Sprite _squadFrame;
        private Sprite _unitFrame;
        private bool _presentationEnabled = true;

        private void OnEnable()
        {
            if (hand != null) hand.AttachmentsChanged += Rebuild;
            Rebuild(); // 활성화 시점의 현재 상태 반영 (보통 빈 목록)
        }

        private void OnDisable()
        {
            if (hand != null) hand.AttachmentsChanged -= Rebuild;
            Clear();
        }

        private void Rebuild()
        {
            if (!_presentationEnabled) { Clear(); return; }
            if (hand == null || bridge == null) return;
            var cam = billboardCamera != null ? billboardCamera : Camera.main;
            if (cam == null) return;
            EnsureFrameSprites();

            // host 별 그룹핑 (GetAttachments 의 entryId 순서 보존). 이벤트 구동 저빈도
            // 경로라 리스트는 직접 할당 — 풀링은 매 프레임 reconcile 인 StatusFx 에서만 정당.
            _byHost.Clear();
            hand.GetAttachments(_attachments);
            foreach (var (host, card) in _attachments)
            {
                if (!_byHost.TryGetValue(host, out var list))
                {
                    list = new List<DreamcatcherCard>();
                    _byHost[host] = list;
                }
                list.Add(card);
            }

            // 이번 리빌드에 없는 host 회수.
            _toHide.Clear();
            foreach (var kv in _active)
                if (!_byHost.ContainsKey(kv.Key)) _toHide.Add(kv.Key);
            for (int i = 0; i < _toHide.Count; i++)
            {
                var view = _active[_toHide[i]];
                if (view != null) { view.Hide(); _pool.Enqueue(view); }
                _active.Remove(_toHide[i]);
            }

            // host 마다 스트립 Ensure. 앵커 미해석(뷰 미스폰/사망 직후)은 스킵 —
            // 다음 이벤트 리빌드에서 재시도된다.
            foreach (var kv in _byHost)
            {
                if (!bridge.TryGetUnitViewAnchor(kv.Key, out var anchor)) continue;
                if (!_active.TryGetValue(kv.Key, out var view) || view == null)
                {
                    view = GetView();
                    _active[kv.Key] = view;
                }
                view.Show(anchor, offset, cam, kv.Value, _squadFrame, _unitFrame, cardHeight, spacing);
            }
        }

        // unit-overhead-ui — 신규 통합 행과 레거시 스트립의 중복 표시 방지.
        public void SetPresentationEnabled(bool enabled)
        {
            if (_presentationEnabled == enabled) return;
            _presentationEnabled = enabled;
            if (enabled) Rebuild();
            else Clear();
        }

        // 전투 teardown/비활성화 시 전량 파괴 (StatusFxSpawner.Clear 선례).
        public void Clear()
        {
            foreach (var kv in _active)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _active.Clear();
            while (_pool.Count > 0)
            {
                var v = _pool.Dequeue();
                if (v != null) Destroy(v.gameObject);
            }
            _byHost.Clear();
        }

        private DcIconStripView GetView()
        {
            while (_pool.Count > 0)
            {
                var pooled = _pool.Dequeue();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("DcIconStrip");
            go.transform.SetParent(transform, false);
            return go.AddComponent<DcIconStripView>();
        }

        private void EnsureFrameSprites()
        {
            if (_squadFrame == null) _squadFrame = UiRoundedSprite.Make(10f, 3f, plateFill, squadBorder);
            if (_unitFrame == null) _unitFrame = UiRoundedSprite.Make(10f, 3f, plateFill, unitBorder);
        }
    }
}
