using UnityEngine;

namespace Wassup.Data
{
    // lobby-keyring-drag — 로비 캐릭터 키링 드래그(스윙/낙하/비주얼) 튜닝값.
    // 인게임 DragSwaySettings 와 역할은 같지만 단위가 다르고(월드 → 캔버스 px)
    // 낙하/바운스 필드가 추가되어 별도 타입. hello/world 두 캐릭터가 에셋 1개를 공유한다.
    // 컨트롤러는 매 프레임 SO 를 읽으므로 에셋 편집이 런타임 즉시 반영된다.
    [CreateAssetMenu(menuName = "Wassup/Lobby Keyring Settings", fileName = "LobbyKeyringSettings")]
    public class LobbyKeyringSettings : ScriptableObject
    {
        [Header("추종 (무게추 스프링 + 속도 상한)")]
        [Tooltip("고리→캐릭터 머리 줄 길이(px). 고리 아래 매달리는 길이.")]
        public float ropeLength = 160f;
        [Tooltip("추종 강성/탄성(1/s²). ↑=팽팽/빠릿, ↓=늘어지고 부드럽게.")]
        public float spring = 100f;
        [Tooltip("감쇠(1/s). ↓=바운스·출렁 큼/floaty, ↑=빨리 멎음.")]
        public float damping = 2.5f;
        [Tooltip("추종 최대 속도(px/s). 빠른 스와이프 시 튀어나감 방지(탄성은 유지). 0=무제한.")]
        public float maxSpeed = 2400f;
        [Tooltip("캐릭터 최대 기울임각(deg). 흔들리는 정도. ↓=덜 흔들림.")]
        public float maxAngle = 8f;

        [Header("낙하 (놓기 → 중력 + 바운스)")]
        [Tooltip("낙하 가속(px/s²).")]
        public float gravity = 4000f;
        [Tooltip("착지 반발 계수(0~1). 기본 튜닝 기준 눈에 보이는 바운스 1회.")]
        [Range(0f, 1f)] public float bounceDamping = 0.35f;
        [Tooltip("이 속도(px/s) 미만으로 착지하면 반동 없이 정지.")]
        public float bounceMinSpeed = 300f;
        [Tooltip("낙하 중 기울임이 0(직립)으로 복귀하는 속도(deg/s).")]
        public float fallUprightSpeed = 90f;

        [Header("착지")]
        [Tooltip("착지 x 최소(anchoredPosition). 화면 밖 드롭 방지.")]
        public float landingMinX = -800f;
        [Tooltip("착지 x 최대(anchoredPosition).")]
        public float landingMaxX = 800f;

        [Header("줄/고리 비주얼")]
        [Tooltip("줄 폭(px).")]
        public float cordWidth = 8f;
        [Tooltip("줄/고리 색.")]
        public Color cordColor = new Color(0.45f, 0.38f, 0.28f, 1f);
        [Tooltip("고리(링) 반경(px).")]
        public float ringRadius = 28f;
        [Tooltip("줄 끝을 rect 상단에서 머리 안쪽으로 내리는 깊이(px). 줄이 캐릭터 뒤에 그려져 머리 뒤로 연결돼 보인다.")]
        public float cordAttachDrop = 60f;

        [Header("아트 (미할당 시 절차적 폴백)")]
        [Tooltip("고리 스프라이트. 비우면 절차적 annulus.")]
        public Sprite ringSprite;
        [Tooltip("줄 스프라이트(세로 스트레치 전제). 비우면 단색 사각형.")]
        public Sprite cordSprite;
        [Tooltip("줄 머티리얼(샤인 셰이더). 비우면 기본 UI 머티리얼.")]
        public Material cordMaterial;
    }
}
