using UnityEngine;

namespace Wassup.Data.BattleView
{
    // battle-core-rebuild unit 5a — 캐릭터 뷰의 외형·모션 노브.
    // 옛 브리지의 `tilemapCharacterScale`·`tilemapBillboardTilt`·프랍 틸트 3 ·
    // `healthDisplayStyle`·`walkAnimSpeedStyle`·`unitHealthPresentationMode`·
    // `enemyDragDim*` 의 새 주인. E27(피격 팝)이 여기서 코드 상수를 벗는다.
    [CreateAssetMenu(menuName = "Wassup/BattleView/Character View Config", fileName = "CharacterViewConfig")]
    public sealed class CharacterViewConfig : ScriptableObject
    {
        [Header("외형")]
        [Tooltip("캐릭터 뷰 기본 스케일(월드).")]
        [SerializeField, Min(0.01f)] private float characterScale = 0.504f;

        [Tooltip("빌보드 틸트(도). 보드 pitch 와 짝이라 카메라를 바꾸면 같이 본다.")]
        [SerializeField, Range(0f, 90f)] private float billboardTilt = 45f;

        [Header("프랍 거리 틸트")]
        [SerializeField] private float propDistanceTiltFactor = 0.78f;
        [SerializeField] private float propDistanceTiltMin = 28f;
        [SerializeField] private float propDistanceTiltMax = 62f;

        [Header("체력 표시")]
        [SerializeField] private HealthDisplayStyle healthDisplayStyle;
        [SerializeField] private UnitHealthPresentationMode healthPresentationMode =
            UnitHealthPresentationMode.UnifiedOverhead;

        [Header("이동 애니 속도")]
        [Tooltip("비우면 이동 속도로 애니를 늘리지 않는다(등속 재생).")]
        [SerializeField] private WalkAnimSpeedStyle walkAnimSpeedStyle;

        [Header("배치 중 적 반투명")]
        [SerializeField, Range(0f, 1f)] private float enemyDragDimAlpha = 0.3f;
        [SerializeField, Min(0.01f)] private float enemyDragDimFadeSpeed = 8f;

        // ── E27 (규칙 장부) ───────────────────────────────────────────────────
        // 「피격하면 몸이 잠깐 커졌다 돌아온다」의 0.15초·0.2배는 옛 전투에서 **코드 상수**였고
        // (`HitFlashTag.duration` · `HitFlashSystem.PeakBonus`), 제약 6(모든 VFX 파라미터는
        // SO/프리팹)과 정면 충돌했다. 주석이 「튜닝이 필요해지면 SO 로 승격」이라고 적어 뒀을 뿐
        // 결정 이력이 없어 장부가 「보류」로 들고 있던 것을 여기서 닫는다.
        //
        // ⚠ **생산자는 투사체 착탄 하나뿐**이라는 성질은 그대로 옮긴다 — 근접 공격에는 안 뜬다.
        // 「이왕 노브로 뺀 김에 근접에도」는 플레이어가 겪는 규칙을 바꾸는 결정이라 이 unit 밖이다.
        [Header("피격 팝 (E27)")]
        [Tooltip("피격 시 몸이 커졌다 돌아오는 시간(초). 0 = 팝 없음.")]
        [SerializeField, Min(0f)] private float hitPopSeconds = 0.15f;

        [Tooltip("피격 팝의 최대 확대 비율. 0.2 = 20% 커졌다 돌아온다.")]
        [SerializeField, Range(0f, 1f)] private float hitPopOvershoot = 0.2f;

        public float CharacterScale => characterScale;
        public float BillboardTilt => billboardTilt;
        public float PropDistanceTiltFactor => propDistanceTiltFactor;
        public float PropDistanceTiltMin => propDistanceTiltMin;
        public float PropDistanceTiltMax => propDistanceTiltMax;
        public HealthDisplayStyle HealthDisplayStyle => healthDisplayStyle;
        public UnitHealthPresentationMode HealthPresentationMode => healthPresentationMode;
        public WalkAnimSpeedStyle WalkAnimSpeedStyle => walkAnimSpeedStyle;
        public float EnemyDragDimAlpha => enemyDragDimAlpha;
        public float EnemyDragDimFadeSpeed => enemyDragDimFadeSpeed;
        public float HitPopSeconds => hitPopSeconds;
        public float HitPopOvershoot => hitPopOvershoot;

        /// <summary>이동 속도로 애니를 늘리는가. 스타일이 없으면 끈다.</summary>
        public bool WalkAnimSpeedEnabled => walkAnimSpeedStyle != null;

        public float WalkAnimRefSpeed => walkAnimSpeedStyle != null ? walkAnimSpeedStyle.referenceSpeed : 2.5f;
        public float WalkAnimMinTimeScale => walkAnimSpeedStyle != null ? walkAnimSpeedStyle.minTimeScale : 0.15f;
        public float WalkAnimMaxTimeScale => walkAnimSpeedStyle != null ? walkAnimSpeedStyle.maxTimeScale : 2f;
        public float WalkAnimSmoothing => walkAnimSpeedStyle != null ? walkAnimSpeedStyle.smoothing : 0.2f;
        public float WalkAnimTeleportGuard => walkAnimSpeedStyle != null ? walkAnimSpeedStyle.teleportGuard : 1.5f;
    }
}
