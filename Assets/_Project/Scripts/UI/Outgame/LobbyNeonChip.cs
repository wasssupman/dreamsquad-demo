using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // lobby-neon-restyle unit 1 — 로비 메뉴 버튼 네온 스킨.
    // 시안에서 실측한 팔레트를 기본값으로, Awake 에서 UiRoundedSprite 로 배경 스프라이트를
    // 구워 대상 Image 에 할당한다. 지오메트리(버튼 rect·자식 배치)는 씬이 소유하고,
    // 이 컴포넌트는 스프라이트/색만 소유한다 — revert 시 씬만 되돌리면 스킨이 사라진다.
    //   Chip: 다크 남색 9-slice 칩 + 네온 퍼플 테두리 (스쿼드/드림캐쳐/히스토리)
    //   Cta : 핑크→퍼플 수평 그라디언트 + 라이트블루 림, 대상 rect 크기로 full-rect 베이크 (START)
    [DisallowMultipleComponent]
    public class LobbyNeonChip : MonoBehaviour
    {
        public enum Kind { Chip, Cta }

        [SerializeField] private Kind kind = Kind.Chip;
        [Tooltip("구운 스프라이트를 받을 Image. 보통 버튼 루트(=Button targetGraphic).")]
        [SerializeField] private Image background;

        [Header("공통")]
        [SerializeField] private float cornerRadius = 24f;
        [SerializeField] private float borderWidth = 3f;
        [Tooltip("Chip 채움 / Cta 그라디언트 왼쪽. 기본값=시안 실측.")]
        [SerializeField] private Color fill = new Color32(16, 15, 40, 235);
        [Tooltip("Chip 테두리 / Cta 림.")]
        [SerializeField] private Color border = new Color32(150, 110, 220, 255);

        [Header("Cta 전용")]
        [Tooltip("그라디언트 오른쪽 색. 왼쪽은 fill.")]
        [SerializeField] private Color fillRight = new Color32(125, 97, 233, 255);

        private Sprite _baked;

        private void Awake()
        {
            if (background == null) background = GetComponent<Image>(); // 관례: 버튼 루트 Image
            if (background == null)
            {
                Debug.LogWarning($"{nameof(LobbyNeonChip)}: background Image 없음 — 스킨 생략.", this);
                return;
            }

            if (kind == Kind.Chip)
            {
                _baked = UiRoundedSprite.Make(cornerRadius, borderWidth, fill, border);
                background.sprite = _baked;
                background.type = Image.Type.Sliced;
            }
            else
            {
                var rect = ((RectTransform)background.transform).rect;
                _baked = UiRoundedSprite.MakeHorizontalGradient(
                    Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height),
                    cornerRadius, borderWidth, fill, fillRight, border);
                background.sprite = _baked;
                background.type = Image.Type.Simple;
                background.preserveAspect = false;
            }
            background.color = Color.white; // 루트가 투명 클릭 타깃이던 경우 대비
        }

        private void OnDestroy()
        {
            if (_baked != null)
            {
                Destroy(_baked.texture);
                Destroy(_baked);
                _baked = null;
            }
        }
    }
}
