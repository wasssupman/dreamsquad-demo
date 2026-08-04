using UnityEngine;

namespace Wassup.Presentation
{
    // battle-sim-extraction unit 11(선행 머지 1) — 배틀 씬 뷰 상수의 런타임 미러.
    //
    // 원래 BattleBridge 의 public static 21개였다. 값의 저작 지점(SerializeField/SO)은 여전히
    // Bridge 에 있고 **미러만** 여기로 옮겼다 — 이유는 이 값들이 sim 과 무관한 뷰 전용 표면이라
    // Bridge 를 sim/뷰로 가르기 전에 먼저 떼어내야 하기 때문이다(salvage 판정: 세션 계약 밖).
    //
    // 읽기는 뷰 7종: SpineUnitView · QuadUnitView · BlobShadow · PropBillboard · UnitLiftVisual ·
    // AllyMarkerDecal · DefenderDragPlacementController — 전부 get 만 쓴다.
    //
    // **쓰기 표면은 타입이 강제한다**(M1 리뷰 MEDIUM 2): 이관 전 Bridge 에서 대부분이 private set
    // 이었으므로 여기서 public set 을 열면 "경계를 주석에만 의존"하게 된다. 그래서 값 갱신은
    // 아래 Apply* 3개(Bridge 의 실제 쓰기 지점 3종과 1:1)로만 가능하고, 예외는
    // `CharacterBillboardTilt` 하나다 — 런타임에 찔러 리컴파일 없이 기울기를 튜닝하는 용도가
    // 이관 전부터 문서화된 의도적 계약이다.
    public static class BattleVisualKnobs
    {
        // tilemap-mode-adoption unit 0 — 유닛 스케일. const 제거. 맵 빌드 시 설정.
        public static float CharacterVisualScale { get; private set; } = 0.42f;

        // SpineUnitView 가 매 LateUpdate 읽는 tilemapBillboardTilt 의 live 미러.
        // **유일하게 열린 setter** — 툴링 런타임 튜닝을 위한 기존 계약(이관 전 public field).
        public static float CharacterBillboardTilt { get; set; } = 45f;

        // tilted-billboard unit 6 — 배경 프랍 거리 기반 틸트 튜닝 미러(PropBillboard 가 읽음). factor=0=비활성.
        public static float PropDistanceTiltFactor { get; private set; }
        public static float PropDistanceTiltMin { get; private set; } = 28f;
        public static float PropDistanceTiltMax { get; private set; } = 62f;

        // tilted-billboard unit 3 — 블롭 그림자 데이터(하드코딩 금지: serialized 필드에서 빌드 시 미러).
        public static Sprite BlobShadowSprite { get; private set; }
        public static float BlobShadowSize { get; private set; } = 1f;
        public static Color BlobShadowColor { get; private set; } = new Color(0f, 0f, 0f, 0.45f);
        public static float BlobShadowGroundY { get; private set; } = 0.02f;

        // flight-lift-feel unit 1 — 코드 기본값이 곧 초기값이라 미배선 씬에서도 동작한다.
        public static float LiftScalePerHeight { get; private set; } = 0.14f;
        public static float LiftScaleMax { get; private set; } = 1.35f;
        public static float LiftShadowFullHeight { get; private set; } = 3f;
        public static float LiftShadowMinScale { get; private set; } = 0.55f;
        public static float LiftShadowMinAlpha { get; private set; } = 0.35f;

        // tilemap-real-shadows — 진짜 그림자 모드(데스크톱) vs 블롭(모바일/OFF). 빌드 시 모바일 강제 OFF.
        public static bool UseRealShadows { get; private set; }

        // enemy-walk-anim-speed unit 0 — 걷기 애니 속도 변조 미러(SpineUnitView 가 읽음). SO 미할당 시
        // Enabled=false → 뷰는 배율 1.0(현행 동작, 회귀 없음). 빌드 시 serialized SO 에서 1회 복사.
        public static bool WalkAnimSpeedEnabled { get; private set; }
        public static float WalkAnimRefSpeed { get; private set; } = 2.5f;
        public static float WalkAnimMinTimeScale { get; private set; } = 0.15f;
        public static float WalkAnimMaxTimeScale { get; private set; } = 2f;
        public static float WalkAnimSmoothing { get; private set; } = 0.2f;
        public static float WalkAnimTeleportGuard { get; private set; } = 1.5f;

        // ── 쓰기 표면 (Bridge 전용) ──────────────────────────────────────────────
        // 맵 빌드 시 1회 — 유닛 스폰 전에 확정돼야 하는 값들(스케일·프랍 틸트·블롭·그림자 모드).
        public static void ApplyMapKnobs(
            float characterVisualScale, float billboardTilt,
            float propTiltFactor, float propTiltMin, float propTiltMax,
            Sprite blobSprite, float blobSize, Color blobColor, float blobGroundY,
            bool useRealShadows)
        {
            CharacterVisualScale = characterVisualScale;
            CharacterBillboardTilt = billboardTilt;
            PropDistanceTiltFactor = propTiltFactor;
            PropDistanceTiltMin = propTiltMin;
            PropDistanceTiltMax = propTiltMax;
            BlobShadowSprite = blobSprite;
            BlobShadowSize = blobSize;
            BlobShadowColor = blobColor;
            BlobShadowGroundY = blobGroundY;
            UseRealShadows = useRealShadows;
        }

        // 매 LateUpdate — lift 노브는 뷰가 매 프레임 읽으므로 미러도 매 프레임이다(flight-lift-feel unit 3).
        public static void ApplyLiftKnobs(
            float scalePerHeight, float scaleMax,
            float shadowFullHeight, float shadowMinScale, float shadowMinAlpha)
        {
            LiftScalePerHeight = scalePerHeight;
            LiftScaleMax = scaleMax;
            LiftShadowFullHeight = shadowFullHeight;
            LiftShadowMinScale = shadowMinScale;
            LiftShadowMinAlpha = shadowMinAlpha;
        }

        // 걷기 애니 SO 미러. style == null 이면 비활성(배율 1.0 = 현행 무회귀 동작).
        public static void ApplyWalkAnimStyle(
            bool enabled, float refSpeed, float minTimeScale, float maxTimeScale,
            float smoothing, float teleportGuard)
        {
            WalkAnimSpeedEnabled = enabled;
            if (!enabled) return;
            WalkAnimRefSpeed = refSpeed;
            WalkAnimMinTimeScale = minTimeScale;
            WalkAnimMaxTimeScale = maxTimeScale;
            WalkAnimSmoothing = smoothing;
            WalkAnimTeleportGuard = teleportGuard;
        }
    }
}
