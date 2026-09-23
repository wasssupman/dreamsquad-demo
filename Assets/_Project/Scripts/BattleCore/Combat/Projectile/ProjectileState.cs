using System.Collections.Generic;
using Unity.Mathematics;

namespace Wassup.BattleCore.Combat.Projectile
{
    // battle-core-rebuild unit 3 — 날아가는 것 하나.
    //
    // 옛 전투의 `ProjectileState` 컴포넌트가 **슬롯을 빌려 쓰던 구조**를 그대로 옮기지 않는다.
    // 저쪽은 「궤도의 `maxDistance` 는 반경, `speed` 는 각속도」처럼 같은 필드가 궤적마다 다른
    // 뜻을 겸직했고, 그 겸직표가 주석 40줄이었다 — unmanaged struct 크기를 아끼려는 ECS 의
    // 제약이 만든 형태이지 규칙이 아니다. 여기서는 **이름이 뜻을 말한다.**
    //
    // 대신 옮긴 것은 **규칙**이다:
    //   · 요청은 값 스냅샷이다 — 쏘고 나면 사수 스탯이 변해도 탄은 안 변한다.
    //   · 원점의 몸은 **경계 너머까지 실린다**(제약 13). 즉발 폭발이 투사체 길을 타도
    //     원점은 트리거 대상의 몸 중심이고, 실을 값이 없으면(자리형) 0 을 남긴다.
    //   · **한 탄에 조준은 하나다.** 궤적이 칸을 겨누는데 페이로드가 적을 겨누면
    //     예고 시간만큼 어긋나 헛방이 된다.
    //   · 「지금 어느 다리인가」(부메랑)는 **어디에도 저장하지 않는다.**
    public sealed class Projectile
    {
        public SimEntityId Id;
        /// <summary>`MatchDefinition.Projectiles` 인덱스. 뷰가 프리팹을 고르는 손잡이이기도 하다.</summary>
        public int DefIndex = -1;

        public MovementKind Movement;
        public PayloadKind Payload;

        // ── 귀속 ──
        /// <summary>쏜 자. 킬 귀속·위협 누적의 축이다. `None` = 판이 쏜 것(미귀속).</summary>
        public SimEntityId Owner = SimEntityId.None;
        public Wassup.Battle.Units.Faction OwnerFaction;
        /// <summary>때릴 수 있는 진영 비트. 발사 시점 스냅샷이다.</summary>
        public int TargetMask;
        /// <summary>때릴 수 있는 통행 층. 0 = 무필터.</summary>
        public byte TargetLayers;

        // ── 조준 ──
        /// <summary>엔티티 바인딩의 임자. 셀·방향 바인딩에서는 비어 있다.</summary>
        public SimEntityId Target = SimEntityId.None;

        // ── 자리 ──
        public float3 Position;
        /// <summary>직전 틱의 자리. 경로 스윕이 이 선분을 훑는다.</summary>
        public float3 PrevPos;
        /// <summary>발사점(왕복의 귀환점 · 궤도의 중심 · 포물선의 기점).</summary>
        public float3 Origin;
        /// <summary>칸 바인딩의 착탄점(발사 시점 고정).</summary>
        public float3 Impact;
        /// <summary>방향 바인딩의 **발사 축(불변)**. 궤도에서는 접선(파생값)이 여기 쓰인다.</summary>
        public float2 Direction;
        public float3 Control1, Control2;

        // ── 시간·속도 ──
        public float Speed;
        /// <summary>궤도 전용 각속도(rad/s). 음수 = 역회전.</summary>
        public float AngularSpeed;
        public float FlightTime;
        public float Elapsed;
        public float FuseSeconds;
        public float ArcHeight;
        public float OrbitRadius;
        public float OrbitPhase;
        /// <summary>직선·왕복의 편도 거리(월드).</summary>
        public float MaxDistance;

        // ── 판정 ──
        /// <summary>탄의 «관대함». 유효 피격 반경 = 이 값 + **대상의 몸**(월드↔타일 환산은 호출부).</summary>
        public float HitThreshold;
        /// <summary>
        /// 이 탄의 **원점 몸 반경**(타일). `0` = 자리형(칸 반폭으로 접힌다 — `ReachFromImpact`).
        /// ⚠ 국소 문맥에서는 착탄점이 「자리」로 보여 0 이 옳아 보인다. 트리거 대상의 몸
        /// 중심에서 나온 즉발 폭발이면 **그 몸을 여기 실어 보내라.**
        /// </summary>
        public float OriginBodyRadius;

        public float Damage;

        // SingleSplash
        public float SplashRadius;
        public float SplashDamageMul;

        // TileAoe
        public int ImpactTileRange;
        public int AoeTargetCap;
        public CcRequestKind AoeCc;
        public float AoeCcSeconds;

        // SpawnBlocker — 세울 설치물의 체력·몸(탄 정의에서 스냅샷).
        public float BlockerHealth;
        public float BlockerBodyRadius;

        // PathHit
        public int PierceRemaining;
        /// <summary>같은 피해자를 다시 때리기까지의 간격(초). 0 = 피해자당 영구 1회.</summary>
        public float RehitCooldown;
        /// <summary>피해자별 창. 슬롯을 **제자리에 덮어쓴다** — 매 바퀴 append 하면 버퍼가 자란다.</summary>
        public readonly List<PathHitRecord> HitRecords = new List<PathHitRecord>(4);
        /// <summary>스친 대상을 그 틱 진행 방향으로 미는 속도. 0 = 꺼짐.</summary>
        public float SweepKnockbackSpeed;
        public float SweepKnockbackDuration;

        // 재조준 · 튕김 — **다른 축이다.** 재조준은 「맞히기도 전에 대상이 사라진 경우」
        // (비소비형·감쇠 없음), 튕김은 「맞고 나서 남은 홉」(소비형·감쇠 있음). 한 탄이 둘 다
        // 가질 수 있어 합치지 않는다. ⚠ 방향 바인딩의 재조준 반경은 **0 이다**(겨눌 임자가 없다).
        public int RetargetTileRange;
        public int BounceRemaining;
        public int BounceTileRange;
        public float BounceDamageMul = 1f;

        /// <summary>착탄까지 미룬 넉백(유도탄). 발동은 착탄 단계가 한다.</summary>
        public float ImpactKnockbackDistance;
        public float ImpactKnockbackDuration;

        // ── 런타임 플래그 ──
        /// <summary>궤적이 끝점에 닿았다. 페이로드가 이 신호로 해결한다.</summary>
        public bool ImpactReached;
        /// <summary>이번 틱에 소멸 예약됐다. 실제 제거는 `BattleWorld.Destroy` 한 곳이다.</summary>
        public bool Expired;

        public void Reset()
        {
            Id = SimEntityId.None;
            DefIndex = -1;
            Movement = MovementKind.HomingToEntity;
            Payload = PayloadKind.SingleSplash;
            Owner = SimEntityId.None;
            OwnerFaction = Wassup.Battle.Units.Faction.None;
            TargetMask = 0;
            TargetLayers = 0;
            Target = SimEntityId.None;
            Position = float3.zero;
            PrevPos = float3.zero;
            Origin = float3.zero;
            Impact = float3.zero;
            Direction = float2.zero;
            Control1 = float3.zero;
            Control2 = float3.zero;
            Speed = 0f;
            AngularSpeed = 0f;
            FlightTime = 0f;
            Elapsed = 0f;
            FuseSeconds = 0f;
            ArcHeight = 0f;
            OrbitRadius = 0f;
            OrbitPhase = 0f;
            MaxDistance = 0f;
            HitThreshold = 0f;
            OriginBodyRadius = 0f;
            Damage = 0f;
            SplashRadius = 0f;
            SplashDamageMul = 0f;
            ImpactTileRange = 0;
            AoeTargetCap = 0;
            AoeCc = CcRequestKind.None;
            AoeCcSeconds = 0f;
            BlockerHealth = 0f;
            BlockerBodyRadius = 0f;
            PierceRemaining = 0;
            RehitCooldown = 0f;
            HitRecords.Clear();
            SweepKnockbackSpeed = 0f;
            SweepKnockbackDuration = 0f;
            RetargetTileRange = 0;
            BounceRemaining = 0;
            BounceTileRange = 0;
            BounceDamageMul = 1f;
            ImpactKnockbackDistance = 0f;
            ImpactKnockbackDuration = 0f;
            ImpactReached = false;
            Expired = false;
        }
    }

    /// <summary>
    /// 피해자별 재타격 창. 「피해자당 영구 1회」는 궤도 탄을 첫 바퀴 뒤 장식으로 만든다 —
    /// 같은 적을 매 바퀴 지나면서 딱 한 번만 때린다. 그래서 기록은 **창**이다.
    /// </summary>
    public struct PathHitRecord
    {
        public SimEntityId Victim;
        /// <summary>이 피해자가 다시 맞을 수 있게 되는 **탄 로컬 시각**(`Elapsed`).</summary>
        public float NextHitAt;
    }

    public static class PathHits
    {
        /// <summary>
        /// 「이 탄이 지금 `victim` 을 때려도 되나」의 단일 판정.
        ///   `cooldown &lt;= 0` → 기록됨 = 영구 소모(방향 난사의 무회귀)
        ///   `cooldown &gt; 0`  → 기록됨 = `NextHitAt` 까지만 소모
        /// `index` 는 그 피해자의 슬롯(-1 = 처음)이라, 호출부가 append 대신 **덮어쓸** 수 있다.
        /// </summary>
        public static bool CanHit(List<PathHitRecord> records, SimEntityId victim,
                                  float now, float cooldown, out int index)
        {
            index = IndexOf(records, victim);
            if (index < 0) return true;
            return cooldown > 0f && now >= records[index].NextHitAt;
        }

        public static int IndexOf(List<PathHitRecord> records, SimEntityId victim)
        {
            for (int i = 0; i < records.Count; i++)
                if (records[i].Victim == victim) return i;
            return -1;
        }
    }
}
