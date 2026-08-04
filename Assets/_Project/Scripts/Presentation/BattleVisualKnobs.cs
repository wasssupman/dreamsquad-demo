using UnityEngine;

namespace Wassup.Presentation
{
    // battle-sim-extraction unit 11(선행 머지 1) — 배틀 씬 뷰 상수의 런타임 미러.
    //
    // 원래 BattleBridge 의 public static 21개였다. 값의 저작 지점(SerializeField/SO)은 여전히
    // Bridge 에 있고 **미러만** 여기로 옮겼다 — 이유는 이 값들이 sim 과 무관한 뷰 전용 표면이라
    // Bridge 를 sim/뷰로 가르기 전에 먼저 떼어내야 하기 때문이다(salvage 판정: 세션 계약 밖).
    //
    // 쓰기는 BattleBridge 가 유일하다(Awake/OnValidate/BuildMapForBattle/MirrorLiftKnobs 4지점).
    // setter 가 열려 있는 것은 CharacterBillboardTilt 의 기존 계약을 보존하기 위함이다 —
    // 런타임에 값을 찔러 리컴파일 없이 기울기를 튜닝하는 용도가 문서화돼 있었다.
    // 읽기는 뷰 7종: SpineUnitView · QuadUnitView · BlobShadow · PropBillboard · UnitLiftVisual ·
    // AllyMarkerDecal · DefenderDragPlacementController.
    public static class BattleVisualKnobs
    {
        // tilemap-mode-adoption unit 0 — 유닛 스케일. const 제거. 맵 빌드 시 설정.
        public static float CharacterVisualScale { get; set; } = 0.42f;

        // SpineUnitView 가 매 LateUpdate 읽는 tilemapBillboardTilt 의 live 미러.
        // Awake/OnValidate 에서 동기화되며 런타임 포크(툴링 튜닝)를 허용한다.
        public static float CharacterBillboardTilt { get; set; } = 45f;

        // tilted-billboard unit 6 — 배경 프랍 거리 기반 틸트 튜닝 미러(PropBillboard 가 읽음). factor=0=비활성.
        public static float PropDistanceTiltFactor { get; set; }
        public static float PropDistanceTiltMin { get; set; } = 28f;
        public static float PropDistanceTiltMax { get; set; } = 62f;

        // tilted-billboard unit 3 — 블롭 그림자 데이터(하드코딩 금지: serialized 필드에서 빌드 시 미러).
        public static Sprite BlobShadowSprite { get; set; }
        public static float BlobShadowSize { get; set; } = 1f;
        public static Color BlobShadowColor { get; set; } = new Color(0f, 0f, 0f, 0.45f);
        public static float BlobShadowGroundY { get; set; } = 0.02f;

        // flight-lift-feel unit 1 — 코드 기본값이 곧 초기값이라 미배선 씬에서도 동작한다.
        public static float LiftScalePerHeight { get; set; } = 0.14f;
        public static float LiftScaleMax { get; set; } = 1.35f;
        public static float LiftShadowFullHeight { get; set; } = 3f;
        public static float LiftShadowMinScale { get; set; } = 0.55f;
        public static float LiftShadowMinAlpha { get; set; } = 0.35f;

        // tilemap-real-shadows — 진짜 그림자 모드(데스크톱) vs 블롭(모바일/OFF). 빌드 시 모바일 강제 OFF.
        public static bool UseRealShadows { get; set; }

        // enemy-walk-anim-speed unit 0 — 걷기 애니 속도 변조 미러(SpineUnitView 가 읽음). SO 미할당 시
        // Enabled=false → 뷰는 배율 1.0(현행 동작, 회귀 없음). 빌드 시 serialized SO 에서 1회 복사.
        public static bool WalkAnimSpeedEnabled { get; set; }
        public static float WalkAnimRefSpeed { get; set; } = 2.5f;
        public static float WalkAnimMinTimeScale { get; set; } = 0.15f;
        public static float WalkAnimMaxTimeScale { get; set; } = 2f;
        public static float WalkAnimSmoothing { get; set; } = 0.2f;
        public static float WalkAnimTeleportGuard { get; set; } = 1.5f;
    }
}
