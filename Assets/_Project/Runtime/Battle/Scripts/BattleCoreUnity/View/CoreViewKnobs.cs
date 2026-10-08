using UnityEngine;
using Somnia.Battle.Data.BattleView;

namespace Somnia.Battle.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5a — 뷰가 읽는 저작값 묶음.
    //
    // 옛 전투에서 이 값들은 `BattleBridge` 의 **static 미러**였다. 편의로 얹은 진입점 하나가
    // 다음 사람에게 「그 값의 주인은 브리지」라는 잘못된 신호가 됐고, 브리지는 그렇게 비대해졌다
    // (제약 12 가 쓰인 이유). 새 층에서는 값의 주인이 SO 이고, 뷰 풀이 그것을 들고 **스폰할 때
    // 뷰에 넘긴다.** static 이 없으므로 「어디서든 읽을 수 있다」가 성립하지 않는다.
    //
    // `readonly struct` 인 이유: 뷰가 이 값을 고칠 수 없어야 한다. 뷰 하나가 노브를 바꾸면
    // 그 다음에 스폰되는 뷰들이 다른 세계를 본다.
    public readonly struct CoreViewKnobs
    {
        public readonly CharacterViewConfig Character;
        public readonly BlobShadowConfig Blob;
        public readonly UnitLiftKnobs Lift;

        /// <summary>타일 → 월드 환산. 저작하지 않고 **타일 크기에서 파생**한다.</summary>
        public readonly float TileToWorld;

        public CoreViewKnobs(CharacterViewConfig character, BlobShadowConfig blob,
                             UnitLiftKnobs lift, float tileToWorld)
        {
            Character = character;
            Blob = blob;
            Lift = lift;
            TileToWorld = tileToWorld > 0f ? tileToWorld : 1f;
        }

        public bool IsValid => Character != null && Lift != null;

        // ── 캐릭터 ──
        public float CharacterVisualScale => Character != null ? Character.CharacterScale : 1f;
        public float CharacterBillboardTilt => Character != null ? Character.BillboardTilt : 45f;

        // ── 블롭 그림자 ──
        public Sprite BlobShadowSprite => Blob != null ? Blob.Sprite : null;
        public Color BlobShadowColor => Blob != null ? Blob.Color : new Color(0f, 0f, 0f, 0.45f);
        public float BlobShadowLift => Blob != null ? Blob.Lift : 0.026f;
        public bool UseRealShadows => Blob != null && Blob.UseRealShadows;

        // ── 이동 애니 속도 ──
        public bool WalkAnimSpeedEnabled => Character != null && Character.WalkAnimSpeedEnabled;
        public float WalkAnimRefSpeed => Character != null ? Character.WalkAnimRefSpeed : 2.5f;
        public float WalkAnimMinTimeScale => Character != null ? Character.WalkAnimMinTimeScale : 0.15f;
        public float WalkAnimMaxTimeScale => Character != null ? Character.WalkAnimMaxTimeScale : 2f;
        public float WalkAnimSmoothing => Character != null ? Character.WalkAnimSmoothing : 0.2f;
        public float WalkAnimTeleportGuard => Character != null ? Character.WalkAnimTeleportGuard : 1.5f;

        /// <summary>
        /// 뜬 높이 → 시각 반응 세 배율. 노브 자산이 비어 있으면 **항등**이다 — 그 경우는
        /// 배선 실수이지 「반응 없음」 저작이 아니라서, 뷰가 크기 0 으로 사라지면 안 된다.
        /// </summary>
        public void ResolveLift(float lift, out float unitScale, out float shadowScale, out float shadowAlpha)
        {
            if (Lift == null)
            {
                unitScale = 1f;
                shadowScale = 1f;
                shadowAlpha = 1f;
                return;
            }
            Lift.Resolve(lift, out unitScale, out shadowScale, out shadowAlpha);
        }
    }
}
