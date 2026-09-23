using UnityEngine;

namespace Wassup.Data.BattleView
{
    // battle-core-rebuild unit 5a — 발밑 블롭 그림자의 외형. 옛 브리지 필드
    // `blobShadowSprite`·`blobShadowColor`·`blobShadowLift`·`useRealShadows` 의 새 주인.
    //
    // ⚠ 지름은 여기 없다 — **판정 몸 반경에서 파생**된다(2r). 저작 가능한 지름 노브를 두면
    // 화면이 몸보다 크거나 작은 그림자를 그려 「그림자가 링에 닿으면 사거리 안」이 거짓이 된다.
    [CreateAssetMenu(menuName = "Wassup/BattleView/Blob Shadow Config", fileName = "BlobShadowConfig")]
    public sealed class BlobShadowConfig : ScriptableObject
    {
        [Tooltip("블롭 스프라이트. 비우면 그림자를 달지 않는다.")]
        [SerializeField] private Sprite sprite;

        [SerializeField] private Color color = new Color(0f, 0f, 0.08f, 0.75f);

        [Tooltip("보드 평면에서 띄우는 높이(월드 +Y). z-fighting 만 피하는 값이다.")]
        [SerializeField] private float lift = 0.026f;

        [Tooltip("진짜 그림자(ShadowCaster)를 켤까. 모바일에서는 이 값과 무관하게 꺼진다.")]
        [SerializeField] private bool useRealShadows;

        public Sprite Sprite => sprite;
        public Color Color => color;
        public float Lift => lift;

        /// <summary>
        /// 모바일에서는 저작과 무관하게 꺼진다 — 옛 브리지의 `useRealShadows && !isMobile`
        /// 그대로다. 블롭은 **상시**이고 캐스트는 더하기다(상호배타 아님).
        /// </summary>
        public bool UseRealShadows => useRealShadows && !Application.isMobilePlatform;
    }
}
