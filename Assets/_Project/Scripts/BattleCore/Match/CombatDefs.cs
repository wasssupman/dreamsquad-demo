using System.Globalization;
using System.Text;
using Somnia.Battle.BattleCore.Combat.Emission;
using Somnia.Battle.BattleCore.Combat.Projectile;

namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 3 — 전투가 쓰는 정의표 줄.
    //
    // 유닛 정의표와 같은 규율이다: **plain 수치·열거형만** 오고, 아트 참조는 타입이 없어서
    // 들어올 수 없다. 「값의 정본은 판 밖」(계약 6)의 도착지이고 `configHash` 가 감시한다.
    //
    // ⚠ 필드를 더하면 `Canonicalize` 도 같이 고친다. 안 고치면 「스탯을 바꿨는데 해시가
    // 그대로」라는 조용한 실패가 된다.

    // 탄 한 종류. 옛 `ProjectileData` 의 **수치 부분**이다(프리팹·텍스처·틴트는 뷰가 갖는다).
    public struct ProjectileDef
    {
        public string Id;

        /// <summary>선속도(월드/초). 궤도에서는 이 값을 반경으로 나눠 각속도가 된다.</summary>
        public float Speed;
        /// <summary>탄의 «관대함». 유효 피격 반경 = 이 값 + 대상의 몸.</summary>
        public float HitThreshold;
        public float ArcHeight;
        /// <summary>포물선 비행 시간의 바닥. 코앞 사격도 한 틱에 끝나지 않게 한다.</summary>
        public float MinFlightTime;

        public int Movement;   // `MovementKind`
        public int Payload;    // `PayloadKind`

        // SingleSplash
        public float SplashRadius;
        public float SplashDamageMul;

        // TileAoe
        public int ImpactTileRange;

        // PathHit
        public int PierceCount;
        public float RehitCooldownSec;
        public float KnockbackDistance;
        public float KnockbackDuration;

        // 곡선
        public float BezierLateral;
        public float BezierForwardBias;

        /// <summary>직선·왕복의 편도 거리(월드). 0 = 저작 없음 → 사거리에서 유도한다.</summary>
        public float MaxDistance;

        // `SpawnBlocker` 가 세울 길막 설치물. **탄이 직접 들고 있다** — 옛 저작도 탄 SO 가
        // 길막 SO 를 참조했고, 그 참조를 정의표에서는 수치 둘로 편다(해저드 본체는 unit 6).
        public float BlockerHealth;
        public float BlockerBodyRadius;

        public static ProjectileDef Default() => new ProjectileDef
        {
            Id = "",
            Speed = 10f,
            HitThreshold = 0.3f,
            ArcHeight = 2f,
            MinFlightTime = 0.3f,
            Movement = (int)MovementKind.HomingToEntity,
            Payload = (int)PayloadKind.SingleSplash,
            SplashDamageMul = 0.5f,
            ImpactTileRange = 1,
            PierceCount = 1,
            BezierLateral = 1.2f,
            BezierForwardBias = 0.35f,
            BlockerBodyRadius = 0.5f,
        };

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "speed", Speed, inv);
            MatchDefinition.Put(sb, "hitThreshold", HitThreshold, inv);
            MatchDefinition.Put(sb, "arcHeight", ArcHeight, inv);
            MatchDefinition.Put(sb, "minFlightTime", MinFlightTime, inv);
            MatchDefinition.Put(sb, "movement", Movement, inv);
            MatchDefinition.Put(sb, "payload", Payload, inv);
            MatchDefinition.Put(sb, "splashRadius", SplashRadius, inv);
            MatchDefinition.Put(sb, "splashDamageMul", SplashDamageMul, inv);
            MatchDefinition.Put(sb, "impactTileRange", ImpactTileRange, inv);
            MatchDefinition.Put(sb, "pierceCount", PierceCount, inv);
            MatchDefinition.Put(sb, "rehitCooldownSec", RehitCooldownSec, inv);
            MatchDefinition.Put(sb, "knockbackDistance", KnockbackDistance, inv);
            MatchDefinition.Put(sb, "knockbackDuration", KnockbackDuration, inv);
            MatchDefinition.Put(sb, "bezierLateral", BezierLateral, inv);
            MatchDefinition.Put(sb, "bezierForwardBias", BezierForwardBias, inv);
            MatchDefinition.Put(sb, "maxDistance", MaxDistance, inv);
            MatchDefinition.Put(sb, "blockerHealth", BlockerHealth, inv);
            MatchDefinition.Put(sb, "blockerBodyRadius", BlockerBodyRadius, inv);
        }
    }

    /// <summary>발사 명세 한 발. 「얼마나 벌려 · 직전 탄 뒤 얼마 만에」.</summary>
    public struct PatternShotDef
    {
        /// <summary>0~1 정규화 방향. `min~maxAngleDeg` 사이를 보간한다.</summary>
        public float DirectionT;
        /// <summary>직전 탄 이후 간격(초). **첫 발의 값은 계약상 무시된다.**</summary>
        public float IntervalAfterPreviousSec;
    }

    // 발사 명세 한 종류. **탄의 성질은 복제하지 않는다** — 「누구를·몇 발·어떤 간격·얼마나 벌려」뿐이다.
    public struct PatternDef
    {
        public string Id;
        /// <summary>이 패턴이 쏘는 탄(`MatchDefinition.Projectiles` 인덱스). -1 = 미저작.</summary>
        public int BarrelProjectileDefIndex;
        // skill-data-table 1b(U10) — 피해 칸 없음. 스킬 경로의 탄 피해 = 그 명세를 쓰는 **효과 줄** `EffectDef.Damage`.
        public int Selection;   // `PatternSelectionRule`
        public float MinAngleDeg;
        public float MaxAngleDeg;
        public PatternShotDef[] Shots;
        public bool RandomizeShotsPerTrigger;
        public float RandomIntervalMinSec;
        public float RandomIntervalMaxSec;
        /// <summary>false = 버스트 내내 한 대상을 잠근다(잠금은 인덱스가 아니라 id 다).</summary>
        public bool ReselectPerShot;
        public float TelegraphSec;
        /// <summary>후보 풀을 host 주변으로 좁히는 반경. 0 = 맵 전체.</summary>
        public int ScopeTileRange;
        /// <summary>반경 안 후보 **전원**에게 한 발씩. 발수가 적 수를 따라간다.</summary>
        public bool FanOutToAllCandidates;
        public float FanOutStaggerSec;

        public int ShotCount => Shots != null ? Shots.Length : 0;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "id", Id);
            MatchDefinition.Put(sb, "barrel", BarrelProjectileDefIndex, inv);
            MatchDefinition.Put(sb, "selection", Selection, inv);
            MatchDefinition.Put(sb, "minAngleDeg", MinAngleDeg, inv);
            MatchDefinition.Put(sb, "maxAngleDeg", MaxAngleDeg, inv);
            MatchDefinition.Put(sb, "randomize", RandomizeShotsPerTrigger ? 1 : 0, inv);
            MatchDefinition.Put(sb, "randomMin", RandomIntervalMinSec, inv);
            MatchDefinition.Put(sb, "randomMax", RandomIntervalMaxSec, inv);
            MatchDefinition.Put(sb, "reselect", ReselectPerShot ? 1 : 0, inv);
            MatchDefinition.Put(sb, "telegraphSec", TelegraphSec, inv);
            MatchDefinition.Put(sb, "scopeTileRange", ScopeTileRange, inv);
            MatchDefinition.Put(sb, "fanOut", FanOutToAllCandidates ? 1 : 0, inv);
            MatchDefinition.Put(sb, "fanOutStaggerSec", FanOutStaggerSec, inv);
            for (int i = 0; i < ShotCount; i++)
                MatchDefinition.Put(sb, "shot" + i.ToString(inv),
                    Shots[i].DirectionT.ToString("R", inv) + ","
                    + Shots[i].IntervalAfterPreviousSec.ToString("R", inv));
        }
    }

    // 정의표 줄들이 공유하는 공격 저작. `UnitDef`·`EnemyDef` 가 **같은 모양**을 갖는 이유는
    // 통합 공격 루프가 둘을 구분하지 않기 때문이다 — 진영 판정은 마스크가 하고, 아키타입
    // 판정은 정책 값이 한다(census 「HasComponent 자연 분기」의 후계).
    public struct AttackDef
    {
        /// <summary>때릴 수 있는 통행 층. 0 = 미저작 → 기본(근접은 지상 전용).</summary>
        public int TargetLayers;
        // ⚠ **0 이 안전해야 한다.** 저작을 안 한 줄(`default(AttackDef)`)이 조용히 다른 규칙이
        // 되면 안 된다 — `AttackShapeBaked` 가 `kind 0 = Omni` 로 같은 문제를 푼 선례를 따른다.
        // 그래서 두 필드 모두 **0 = 미저작 = 제약 없음**이고, 표에서는 인덱스를 1 빼지 않는다.

        /// <summary>우선 클래스. **0 = 없음**(그 값이 곧 `DefenderClass.None` 이다).</summary>
        public int PriorityClass;
        /// <summary>
        /// 허용 클래스 비트. **`HasClassFilter` 가 거짓이면 읽지 않는다**(제약 없음).
        /// 필터가 있으면 **0 = 아무도 못 때린다** — 옛 `AttackSystem` 의 게이트는 필터의
        /// **존재**였지 값 0 이 아니었다(2026-09-24 드리프트 감사). 값으로 「없음」을 겸하면
        /// 저작자가 비트를 전부 끈 적이 「전부 허용」으로 뒤집힌다.
        /// </summary>
        public int ClassMask;
        /// <summary>직업 필터를 저작했나. 적은 저작 칸이 있어 참이고, 방어유닛은 축이 없어 거짓이다.</summary>
        public bool HasClassFilter;
        /// <summary>
        /// **걷기만 하는 적**(무장 해제). 옛 `attackMethod: None` 또는 산출물 없음 → 공격 상태
        /// 없이 구웠다(「피해 0 짜리 공격자」를 만들지 않는다). 공격·감지·어그로가 전부 닫힌다.
        /// 공격 상태 자체는 남긴다 — 보스 면역이 그 자리에 산다.
        /// </summary>
        public bool Unarmed;
        /// <summary>지속 락 모드(`TargetMode`).</summary>
        public int Mode;
        /// <summary>정책(`AttackPolicy`).</summary>
        public int Policy;
        /// <summary>
        /// 탄 정의 인덱스. -1 = 근접(즉시 해결).
        /// ⚠ 0 은 유효 인덱스라 여기만 -1 센티널이다. 대신 **범위 밖은 빌드 시점에 근접으로
        /// 접히고 경고가 난다**(`CombatPhase.BuildAttackState`) — 미저작 0 이 탄 0번을 조용히
        /// 쏘는 일이 없다.
        /// </summary>
        public int ProjectileDefIndex;

        // 도형 bake — sim 은 각도를 모른다.
        public int ShapeKind;
        public float ShapeSinHalf;
        public float ShapeCosHalf;
        public float ShapeHalfWidth;

        // 때릴 때 함께 거는 것(부여 측).
        public float KnockbackDistance;
        public float KnockbackDuration;
        public float SleepOnHitSec;
        public float KnockupOnHitSec;
        public float KnockupVisualHeight;

        public AttackOutputDef[] Outputs;

        /// <summary>발사 명세 슬롯(`MatchDefinition.Patterns` 인덱스들). 없으면 빈 배열.</summary>
        public int[] PatternDefIndices;

        /// <summary>unit 7a — 저작 공격 수식자(강공 등). 없으면 빈 배열. 카드가 붙이는 것은 런타임(7b).</summary>
        public Combat.AttackModDef[] Mods;

        // 폭탄맨(정책 `Bomb`).
        public int BombProjectileDefIndex;
        public float BombDamage;
        public int BombAoeTileRange;
        public int BombAoeTargetCap;
        public float BombTravelSeconds;
        public float BombFuseSeconds;
        public float BombArcHeight;

        /// <summary>소환사(정책 `Summon`) — 순찰 소환물의 `Units` 인덱스. -1 = 미저작.</summary>
        public int SummonPatrolDefIndex;

        /// <summary>보스인가. 기절·수면·넉백 면역 집합의 유일한 축이다(출처 불문).</summary>
        public bool BossImmune;

        public static AttackDef Default() => new AttackDef
        {
            TargetLayers = 0,
            PriorityClass = 0,
            ClassMask = 0,
            Mode = (int)TargetMode.None,
            Policy = (int)AttackPolicy.Target,
            ProjectileDefIndex = -1,
            ShapeKind = Combat.AttackShapeBaked.OmniKind,
            Outputs = System.Array.Empty<AttackOutputDef>(),
            PatternDefIndices = System.Array.Empty<int>(),
            BombProjectileDefIndex = -1,
            SummonPatrolDefIndex = -1,
        };

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "atkTargetLayers", TargetLayers, inv);
            MatchDefinition.Put(sb, "priorityClass", PriorityClass, inv);
            MatchDefinition.Put(sb, "classMask", ClassMask, inv);
            // ⚠ 두 칸은 **기본값이면 줄을 안 쓴다** — 칸을 더했다는 사실만으로 저작을 안 건드린
            // 판(골든 코퍼스)의 해시가 바뀌면 「조건 드리프트」 오보가 난다. 값이 켜지면 반응한다.
            if (HasClassFilter) MatchDefinition.Put(sb, "classFilter", 1, inv);
            if (Unarmed) MatchDefinition.Put(sb, "unarmed", 1, inv);
            MatchDefinition.Put(sb, "targetMode", Mode, inv);
            MatchDefinition.Put(sb, "policy", Policy, inv);
            MatchDefinition.Put(sb, "projectileDef", ProjectileDefIndex, inv);
            MatchDefinition.Put(sb, "shapeKind", ShapeKind, inv);
            MatchDefinition.Put(sb, "shapeSinHalf", ShapeSinHalf, inv);
            MatchDefinition.Put(sb, "shapeCosHalf", ShapeCosHalf, inv);
            MatchDefinition.Put(sb, "shapeHalfWidth", ShapeHalfWidth, inv);
            MatchDefinition.Put(sb, "knockbackDistance", KnockbackDistance, inv);
            MatchDefinition.Put(sb, "knockbackDuration", KnockbackDuration, inv);
            MatchDefinition.Put(sb, "sleepOnHitSec", SleepOnHitSec, inv);
            MatchDefinition.Put(sb, "knockupOnHitSec", KnockupOnHitSec, inv);
            MatchDefinition.Put(sb, "knockupVisualHeight", KnockupVisualHeight, inv);
            MatchDefinition.Put(sb, "bombProjectileDef", BombProjectileDefIndex, inv);
            MatchDefinition.Put(sb, "bombDamage", BombDamage, inv);
            MatchDefinition.Put(sb, "bombAoeTileRange", BombAoeTileRange, inv);
            MatchDefinition.Put(sb, "bombAoeTargetCap", BombAoeTargetCap, inv);
            MatchDefinition.Put(sb, "bombTravelSec", BombTravelSeconds, inv);
            MatchDefinition.Put(sb, "bombFuseSec", BombFuseSeconds, inv);
            MatchDefinition.Put(sb, "bombArcHeight", BombArcHeight, inv);
            MatchDefinition.Put(sb, "summonPatrolDef", SummonPatrolDefIndex, inv);
            MatchDefinition.Put(sb, "bossImmune", BossImmune ? 1 : 0, inv);
            int outputs = Outputs != null ? Outputs.Length : 0;
            for (int i = 0; i < outputs; i++)
            {
                var o = Outputs[i];
                MatchDefinition.Put(sb, "out" + i.ToString(inv),
                    ((int)o.Kind).ToString(inv) + ","
                    + o.Magnitude.ToString("R", inv) + ","
                    + o.Duration.ToString("R", inv) + ","
                    + o.Stat.ToString(inv) + ","
                    + o.Op.ToString(inv) + ","
                    + o.StackKind.ToString(inv) + ","
                    + o.StackMaxStack.ToString(inv));
            }
            int patterns = PatternDefIndices != null ? PatternDefIndices.Length : 0;
            for (int i = 0; i < patterns; i++)
                MatchDefinition.Put(sb, "pattern" + i.ToString(inv), PatternDefIndices[i], inv);
            // unit 7a — 비면 한 줄도 안 쓴다(저작을 안 건드린 판의 해시 무변).
            int mods = Mods != null ? Mods.Length : 0;
            for (int i = 0; i < mods; i++) Mods[i].Canonicalize(sb, inv, "mod" + i.ToString(inv));
        }
    }

    // battle-core-rebuild unit 6a2 — **한 발이 나를 수 있는 세기의 상한**(사용자 결정 ②).
    //
    // 스택 종류의 최대 중첩(`StackRuleDef.MaxStack`)과 **다른 축**이다: 저쪽은 「피해자에게
    // 몇 개까지 쌓이나」이고 이쪽은 「한 발에 얼마까지 실리나」다. 카드 넷이 같은 불 부여를
    // 걸면 합이 4가 되는데, 그 합을 막는 것이 여기이고 쌓인 뒤의 상한은 저쪽이다.
    //
    // ⚠ **값이 없으면 「상한 없음」이 아니라 부여 거절**이다(제약 6). 저작 없는 무한 부여가
    // 조용히 성립하지 않게 하는 것이 이 표의 존재 이유다.
    public struct ImbueCapDef
    {
        /// <summary>`Effects.ImbueKind` 의 int 값.</summary>
        public int Kind;

        /// <summary>`ApplyStat` = `StatKind` · `ApplyStack` = `StackKind` · `Cc` = `CcRequestKind`.</summary>
        public int Target;

        /// <summary>`ApplyStat` 전용 — `CombineOp` 의 int 값. 나머지 종류는 0.</summary>
        public int Op;

        /// <summary>한 발이 나르는 크기의 상한. 저작은 **양수**이고 빌더가 검증한다.</summary>
        public float Cap;

        internal void Canonicalize(StringBuilder sb, CultureInfo inv)
        {
            MatchDefinition.Put(sb, "kind", Kind, inv);
            MatchDefinition.Put(sb, "target", Target, inv);
            MatchDefinition.Put(sb, "op", Op, inv);
            MatchDefinition.Put(sb, "cap", Cap, inv);
        }
    }
}
