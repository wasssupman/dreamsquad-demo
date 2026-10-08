using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Combat.Projectile
{
    // battle-core-rebuild unit 3 — 「이런 탄을 쏴라」. **값 스냅샷**이다.
    //
    // 옛 전투는 이것을 캐리어 **엔티티 + ECB** 로 옮겼는데, 그 이유가 「host 가 같은 프레임에
    // 자기 공격으로도 요청을 올릴 수 있다」 하나뿐이었다. 나르는 규칙은 **한 틱에 같은 주체가
    // 서로 독립인 발사를 여러 개 낼 수 있다**이고, 새 코어에서는 요청 리스트로 충분하다.
    //
    // ⚠ **원점의 몸은 여기서부터 실린다.** 즉발 폭발이 투사체 파이프라인을 타면 판정 지점의
    // 국소 문맥에서는 「착탄점」으로 보여 칸 반폭이 옳아 보인다 — 그러나 그 착탄점은 트리거
    // 대상의 몸 중심이다. 실을 값이 없으면(자리에 떨어지는 것) **0 을 남긴다.**
    public struct ProjectileRequest
    {
        public int DefIndex;
        public MovementKind Movement;
        public PayloadKind Payload;

        public SimEntityId Owner;
        public Somnia.Battle.Skills.Faction OwnerFaction;
        public int TargetMask;
        public byte TargetLayers;

        public SimEntityId Target;
        public float3 Origin;
        public float3 Impact;
        public float2 Direction;

        public float Damage;
        /// <summary>**0 = 자리형.** 몸에서 나오는 것이면 발화 시점의 `HitRadius` 를 싣는다.</summary>
        public float OriginBodyRadius;

        /// <summary>요청이 직접 정하는 비행 시간(예고·굴림). 0 이면 궤적이 속도에서 유도한다.</summary>
        public float FlightTime;
        public float FuseSeconds;
        /// <summary>베지어 살포의 스윙 순번. 같은 대상으로 가는 여러 발이 각각 다른 곡선을 그린다.</summary>
        public int SwingIndex;
        /// <summary>궤도 전용 — 같은 궤도에 여러 구슬을 균등 배치하는 각도 오프셋.</summary>
        public float OrbitPhase;
        /// <summary>궤도·직선 전용 — 반경/편도 거리(월드). 0 이면 정의표 값.</summary>
        public float DistanceOverride;

        public int ImpactTileRange;
        public int AoeTargetCap;

        /// <summary>
        /// 이 탄이 **맞은 놈에게 거는** 군중 제어. unit 6a2 에서 이름이 `AoeCc` → `OnHitCc`
        /// 로 바뀌었다 — 칸 광역 전용이 아니라 **착탄 전부**(직격·비산·경로 스윕)가 건다.
        /// ⚠ **요청이 명시한 값은 부여가 덮지 않는다** — 관문이 이 칸을 먼저 잠근다.
        /// </summary>
        public CcRequestKind OnHitCc;
        public float OnHitCcSeconds;

        public float ImpactKnockbackDistance;
        public float ImpactKnockbackDuration;

        public int BounceCount;
        public int BounceTileRange;
        public float BounceDamageMul;
        public int RetargetTileRange;
        /// <summary>
        /// unit 7a — **착탄 예고 반경(칸)**. 0 = 예고 없음. 저작 필드가 아니라 **스킬의 판단**이다(옛
        /// `telegraphTileRange = intent.Telegraph ? intent.TileRange : 0`) — 탄 정의표에 옮길 저작이 없다.
        /// </summary>
        public int TelegraphTileRange;

        public static ProjectileRequest Empty => new ProjectileRequest
        {
            DefIndex = -1,
            Owner = SimEntityId.None,
            Target = SimEntityId.None,
            BounceDamageMul = 1f,
        };
    }
}
