using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // lobby-neon-restyle unit 1 — 로비 메뉴 버튼(스쿼드/드림캐쳐/히스토리) 네온 칩 스킨.
    // 시안에서 실측한 팔레트를 기본값으로, Awake 에서 UiRoundedSprite 로 다크 남색 9-slice 칩을
    // 구워 대상 Image 에 할당한다. 지오메트리(버튼 rect·자식 배치)는 씬이 소유하고,
    // 이 컴포넌트는 스프라이트/색만 소유한다 — revert 시 씬만 되돌리면 스킨이 사라진다.
    // START CTA 는 형태가 전혀 달라(리본 배너) 별도 컴포넌트 LobbyNeonCta 가 맡는다.
    [DisallowMultipleComponent]
    public class LobbyNeonChip : MonoBehaviour
    {
        [Tooltip("구운 스프라이트를 받을 Image. 비우면 같은 GO 의 Image(=Button targetGraphic).")]
        [SerializeField] private Image background;

        [SerializeField] private float cornerRadius = 24f;
        [SerializeField] private float borderWidth = 3f;
        [Tooltip("칩 채움. 기본값=시안 실측.")]
        [SerializeField] private Color fill = new Color32(16, 15, 40, 235);
        [Tooltip("칩 테두리(네온).")]
        [SerializeField] private Color border = new Color32(150, 110, 220, 255);

        private Sprite _baked;

        private void Awake()
        {
            if (background == null) background = GetComponent<Image>(); // 관례: 버튼 루트 Image
            if (background == null)
            {
                Debug.LogWarning($"{nameof(LobbyNeonChip)}: background Image 없음 — 스킨 생략.", this);
                return;
            }

            _baked = UiRoundedSprite.Make(cornerRadius, borderWidth, fill, border);
            background.sprite = _baked;
            background.type = Image.Type.Sliced;
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
