using Unity.Entities;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-status-fx Unit 1 — 상태 연출 뷰(구 AggroIconView 일반화). registry 프리팹을
    // Instantiate 해 유닛을 따라가고(옵션 빌보드), prefab 없으면 절차적 "!" 폴백(현
    // 어그로 외형 유지, 약한 펄스). 스포너가 kind별 풀링하며 Show/Hide 로 구동.
    public class StatusFxView : MonoBehaviour
    {
        // unit-status-fx 5 — 글리프별 절차 폴백 스프라이트 캐시 (인덱스 = FallbackGlyph).
        // 크기는 enum 에서 유도 — 새 글리프 추가 시 자동 확장 (code-review LOW1).
        private static readonly Sprite[] _fallbackSprites =
            new Sprite[System.Enum.GetValues(typeof(StatusFxRegistry.FallbackGlyph)).Length];

        private Camera _camera;
        private Transform _anchor;
        private Vector3 _lastBasePos; // anchor 원위치(offset 미포함) — 파괴 후에도 offset 누적 방지(critic H1)
        private Vector3 _offset;
        private float _scale;
        private bool _billboard;
        private bool _usesFallback;

        private Entity _entity;
        private StatusFxKind _kind;
        private bool _built;
        private bool _active;
        private GameObject _prefabInstance;
        private SpriteRenderer _fallbackSr;

        public Entity Entity => _entity;
        public StatusFxKind Kind => _kind;

        public void Show(Entity entity, StatusFxKind kind, Transform anchor, StatusFxRegistry.Entry entry, Camera cam)
        {
            _entity = entity;
            _kind = kind;
            _anchor = anchor;
            _camera = cam;
            _offset = entry.localOffset;
            _scale = entry.scale <= 0f ? 1f : entry.scale;
            _billboard = entry.billboard;
            EnsureBuilt(entry);
            ApplyMutable(entry); // 풀 재사용 시에도 매 Show 재적용(critic M1 — registry 값 변경 대비)
            _lastBasePos = anchor != null ? anchor.position : Vector3.zero;
            _active = true;
            gameObject.SetActive(true);
            Follow();
        }

        // 재사용마다 갱신될 수 있는 시각 속성(폴백 틴트/글리프/정렬). 프리팹은 자체 정의.
        private void ApplyMutable(StatusFxRegistry.Entry entry)
        {
            if (_usesFallback && _fallbackSr != null)
            {
                _fallbackSr.sprite = FallbackSprite(entry.fallbackGlyph); // registry 값 변경 대비(캐시라 저비용)
                _fallbackSr.color = entry.fallbackTint.a > 0f ? entry.fallbackTint : Color.white;
                _fallbackSr.sortingOrder = 15000;
            }
        }

        // 같은 유닛 유지: anchor 갱신만.
        public void Refresh(Transform anchor) => _anchor = anchor;

        private void EnsureBuilt(StatusFxRegistry.Entry entry)
        {
            if (_built) return; // 풀은 kind별이라 재사용 뷰는 이미 올바른 프리팹 보유.
            _built = true;
            if (entry.prefab != null)
            {
                _usesFallback = false;
                _prefabInstance = Instantiate(entry.prefab, transform);
                _prefabInstance.transform.localPosition = Vector3.zero;
            }
            else
            {
                _usesFallback = true;
                var go = new GameObject("Fallback");
                go.transform.SetParent(transform, false);
                _fallbackSr = go.AddComponent<SpriteRenderer>();
                // 스프라이트/색/정렬은 ApplyMutable 이 매 Show 재적용.
            }
        }

        private void Follow()
        {
            if (_anchor != null) _lastBasePos = _anchor.position; // 파괴되면 마지막 위치 유지
            // billboard=true = 화면을 보는 머리 위 뱃지 → 오프셋도 카메라 평면(HeadAnchor 함정 참조).
            // billboard=false = 월드에 앉는 연출(지면 링 등) 의도 → 월드 오프셋 유지.
            transform.position = _billboard
                ? HeadAnchor.Lift(_lastBasePos, _offset, _camera)
                : _lastBasePos + _offset;
            // 폴백 "!"만 약한 펄스(현 어그로 외형 보존). 프리팹은 자체 애니메이션.
            float s = _scale;
            if (_usesFallback) s *= 1f + 0.12f * Mathf.Sin(Time.time * 3f);
            transform.localScale = new Vector3(s, s, s);
        }

        // 위치/회전 모두 LateUpdate — 카메라 포즈는 CameraDirector(-90)가 LateUpdate 에서 확정한다.
        // Update 에서 Follow 하면 billboard 엔트리의 HeadAnchor 리프트가 지난 프레임 회전을 읽어
        // 카메라 이동 중 위치만 1프레임 뒤처진다.
        private void LateUpdate()
        {
            if (!_active) return;
            Follow();
            if (_billboard && _camera != null)
                transform.rotation = _camera.transform.rotation;
        }

        public void Hide()
        {
            _active = false;
            _anchor = null;
            gameObject.SetActive(false);
        }

        // 글리프별 흰색 스프라이트 절차 생성(글리프당 1회 캐시). 색은 fallbackTint 로 곱.
        private static Sprite FallbackSprite(StatusFxRegistry.FallbackGlyph glyph)
        {
            int gi = (int)glyph;
            if (gi < 0 || gi >= _fallbackSprites.Length) gi = 0;
            if (_fallbackSprites[gi] != null) return _fallbackSprites[gi];

            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var clear = new Color(1, 1, 1, 0);
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = clear;

            // 인덱스 컨벤션 주의: x 는 [x0, x1] 양끝 포함, y 는 [y0, y1) 상단 미포함
            // (code-review LOW2 — 새 글리프 작성 시 이 비대칭을 감안할 것).
            void FillRect(int x0, int x1, int y0, int y1)
            {
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x <= x1; x++)
                        px[y * S + x] = Color.white;
            }
            // Z 한 글자: 상/하단 바 + 우상→좌하 대각선.
            void DrawZ(int x0, int x1, int y0, int y1, int bar, int halfW)
            {
                FillRect(x0, x1, y1 - bar, y1);          // 상단 바
                FillRect(x0, x1, y0, y0 + bar);          // 하단 바
                for (int y = y0 + bar; y < y1 - bar; y++) // 대각선(위=오른쪽)
                {
                    float t = (y - (y0 + bar)) / (float)(y1 - bar - (y0 + bar));
                    int xc = Mathf.RoundToInt(Mathf.Lerp(x0 + halfW, x1 - halfW, t));
                    for (int x = xc - halfW; x <= xc + halfW; x++)
                        px[y * S + x] = Color.white;
                }
            }

            // unit-status-fx 6 — +자 별 한 개: 세로바 + 가로바(스턴 "핑핑 도는 별").
            void DrawStar(int cx, int cy, int arm, int halfW)
            {
                FillRect(cx - halfW, cx + halfW, cy - arm, cy + arm); // 세로
                FillRect(cx - arm, cx + arm, cy - halfW, cy + halfW); // 가로
            }

            if (glyph == StatusFxRegistry.FallbackGlyph.Zz)
            {
                DrawZ(8, 40, 8, 50, 8, 4);   // 큰 Z
                DrawZ(44, 60, 36, 60, 5, 3); // 우상단 작은 z
            }
            else if (glyph == StatusFxRegistry.FallbackGlyph.Stars)
            {
                DrawStar(32, 44, 13, 3); // 큰 별(위 중앙)
                DrawStar(16, 22, 8, 2);  // 작은 별(좌하)
                DrawStar(48, 26, 8, 2);  // 작은 별(우하)
            }
            else
            {
                int cx = S / 2;
                FillRect(cx - 5, cx + 5, 22, 52); // 세로 막대(위)
                FillRect(cx - 5, cx + 5, 8, 18);  // 점(아래)
            }

            tex.SetPixels(px);
            tex.Apply();
            _fallbackSprites[gi] = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
            return _fallbackSprites[gi];
        }
    }
}
