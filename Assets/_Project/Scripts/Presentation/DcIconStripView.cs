using System.Collections.Generic;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-dreamcatcher-icons unit 1 — 한 유닛의 부착 카드 목록을 머리 위 미니 카드
    // 행으로 렌더하는 뷰. 스포너가 entity 별로 풀링하며 Show/Hide 로 구동. 슬롯 내용은
    // Show 시점에만 바뀌고(이벤트 리빌드), per-frame 은 앵커 추종/빌보드만 —
    // StatusFxView 의 follow/billboard 패턴을 따른다.
    public class DcIconStripView : MonoBehaviour
    {
        private const int FrameSortingOrder = 14500; // StatusFx(15000) 아래
        private const int ArtSortingOrder = 14501;

        private struct Slot
        {
            public GameObject root;
            public SpriteRenderer frame;
            public SpriteRenderer art;
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private Camera _camera;
        private Transform _anchor;
        private Vector3 _lastBasePos; // anchor 파괴 후에도 마지막 위치 유지 (StatusFxView 선례)
        private Vector3 _offset;
        private bool _active;

        public void Show(Transform anchor, Vector3 offset, Camera cam,
            List<DreamcatcherCard> cards, Sprite squadFrame, Sprite unitFrame,
            float cardHeight, float spacing)
        {
            _anchor = anchor;
            _offset = offset;
            _camera = cam;
            EnsureSlots(cards.Count);

            float cardWidth = cardHeight * (2f / 3f); // 타로 아트 2:3 세로 비율
            float stepX = cardWidth + spacing;
            float originX = -0.5f * stepX * (cards.Count - 1); // 가로 중앙 정렬

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                bool used = i < cards.Count;
                slot.root.SetActive(used);
                if (!used) continue;

                var card = cards[i];
                slot.root.transform.localPosition = new Vector3(originX + stepX * i, 0f, 0f);

                var frameSprite = card.type == CardType.Squad ? squadFrame : unitFrame;
                slot.frame.sprite = frameSprite;
                // 프레임은 카드보다 살짝 크게 — 테두리가 아트 밖으로 보이는 링이 된다.
                ApplyWorldSize(slot.frame, frameSprite, cardWidth * 1.12f, cardHeight * 1.08f);

                bool hasArt = card.art != null;
                slot.art.gameObject.SetActive(hasArt);
                if (hasArt)
                {
                    slot.art.sprite = card.art;
                    ApplyWorldSize(slot.art, card.art, cardWidth, cardHeight);
                }
            }

            _lastBasePos = anchor != null ? anchor.position : _lastBasePos;
            _active = true;
            gameObject.SetActive(true);
            Follow();
        }

        public void Hide()
        {
            _active = false;
            _anchor = null;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_active) Follow();
        }

        private void LateUpdate()
        {
            if (_active && _camera != null)
                transform.rotation = _camera.transform.rotation;
        }

        private void Follow()
        {
            if (_anchor != null) _lastBasePos = _anchor.position;
            transform.position = _lastBasePos + _offset;
        }

        // 스프라이트 PPU/텍스처 크기와 무관하게 목표 월드 크기로 정규화.
        private static void ApplyWorldSize(SpriteRenderer sr, Sprite sprite, float width, float height)
        {
            if (sprite == null) return;
            var b = sprite.bounds.size;
            sr.transform.localScale = new Vector3(
                b.x > 0f ? width / b.x : 1f,
                b.y > 0f ? height / b.y : 1f,
                1f);
        }

        private void EnsureSlots(int count)
        {
            while (_slots.Count < count)
            {
                var root = new GameObject("Slot" + _slots.Count);
                root.transform.SetParent(transform, false);

                var frameGo = new GameObject("Frame");
                frameGo.transform.SetParent(root.transform, false);
                var frame = frameGo.AddComponent<SpriteRenderer>();
                frame.sortingOrder = FrameSortingOrder;

                var artGo = new GameObject("Art");
                artGo.transform.SetParent(root.transform, false);
                var art = artGo.AddComponent<SpriteRenderer>();
                art.sortingOrder = ArtSortingOrder;

                _slots.Add(new Slot { root = root, frame = frame, art = art });
            }
        }
    }
}
