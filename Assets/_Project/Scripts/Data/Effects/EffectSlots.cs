using System;
using System.Collections.Generic;
using Wassup.BattleCore.Trigger;
using Wassup.Data.Authoring;

namespace Wassup.Data
{
    /// <summary>효과 값의 뜻 이름 칸 하나(`EffectValues` 의 스칼라 칸 · 「칸 없음」 포함).</summary>
    public enum EffectSlot : byte
    {
        None,
        Damage,
        Shield,
        Percent,
        Mul,
        Count,
        RadiusTiles,
        RangeTiles,
        DurationSec,
        FlightSec,
        StackCap,
        Speed,
        DensityRadiusTiles,
        LandingRingTiles,
    }

    /// <summary>
    /// skill-data-table unit 8 — 효과 줄의 **시트 열**(`tables.md` §2 `Skills` — `EffectValues` 의 칸 + 효과 에셋의 참조 셋). 종류마다
    /// 어느 열을 쓰나는 `EffectSlots.UsedColumns` 한 표다. 이름 = `EffectValues` 칸 이름의 첫 글자 대문자(참조는 `…Id`).
    /// </summary>
    [Flags]
    public enum EffectColumns : uint
    {
        None = 0,
        Damage = 1u << 0,
        Shield = 1u << 1,
        Percent = 1u << 2,
        Mul = 1u << 3,
        Count = 1u << 4,
        RadiusTiles = 1u << 5,
        RangeTiles = 1u << 6,
        DurationSec = 1u << 7,
        FlightSec = 1u << 8,
        TickSec = 1u << 9,
        StackCap = 1u << 10,
        Speed = 1u << 11,
        ConeHalfDeg = 1u << 12,
        DensityRadiusTiles = 1u << 13,
        LandingRingTiles = 1u << 14,
        CcKind = 1u << 15,
        StackKind = 1u << 16,
        BuffStat = 1u << 17,
        ShieldFilter = 1u << 18,
        IncludesSelf = 1u << 19,
        Telegraph = 1u << 20,
        AllyFilter = 1u << 21,
        MagnitudeMode = 1u << 22,
        BasisStat = 1u << 23,
        Ratio = 1u << 24,
        ProjectileId = 1u << 25,
        PatternId = 1u << 26,
        HazardId = 1u << 27,
    }

    /// <summary>
    /// 옛 저작의 스칼라 칸(`DcPayloadSpec` 의 값 칸만 — 참조 · 뷰 칸 제외). 엔진 타입이 없어 이전 변환 · 헤드리스 dry-run 이 그대로 쓴다.
    /// CC · 스택은 **옛 번호**(`DcCcKind` · `DcStackKind` 의 정수)다.
    /// </summary>
    public struct LegacyPayload
    {
        public EffectKind Kind;
        public float Magnitude;
        public int TileRange;
        public float Duration;
        public float SlamDamage;
        public int SlamTileRange;
        public float TickIntervalSec;
        public int OrbitCount;
        public float ConeHalfAngleDeg;
        public int DcCcKind;
        public int DcStackKind;
        public CardBuffKind BuffStat;
        public bool Telegraph;
    }

    /// <summary>
    /// skill-data-table unit 4 — **옛 겸직 칸 ↔ 뜻 이름 칸 표 하나**(`tables.md` §3). 굽기는 `ToLegacy` 로 효과 값을 코어 효과 줄의
    /// 겸직 칸(`EffectDef.Magnitude` · `TileRange` · `Duration`)에 싣고, 이전 스크립트는 `FromLegacy` 로 옛 저작을 효과 값에 옮긴다 —
    /// **두 방향이 같은 표(`Of`)를 지나** 이전 전후 굽기가 값으로 같다(라이브 전량 왕복 = dry-run 이 확인).
    ///
    /// 순수 함수(제약 10) — 엔진 · SO 를 모른다.
    /// </summary>
    public static class EffectSlots
    {
        /// <summary>
        /// 그 종류가 옛 겸직 칸 셋(magnitude · tileRange · duration)을 **어느 뜻 칸으로** 읽나. 표에 들지 않는 종류(센티넬 · 이관 ·
        /// 죽은 값 · 분열)는 false.
        /// </summary>
        public static bool Of(EffectKind kind, out EffectSlot m, out EffectSlot t, out EffectSlot d)
        {
            m = t = d = EffectSlot.None;
            switch (kind)
            {
                case EffectKind.ProjectileToTarget: m = EffectSlot.Damage; t = EffectSlot.RangeTiles; d = EffectSlot.FlightSec; return true;
                case EffectKind.SelfTileAoe: m = EffectSlot.Damage; t = EffectSlot.RadiusTiles; d = EffectSlot.FlightSec; return true;
                case EffectKind.NextAttackDoubleFire: return true;
                case EffectKind.SelfBuffLethal: m = EffectSlot.Percent; d = EffectSlot.DurationSec; return true;
                case EffectKind.SelfBlink: m = EffectSlot.DensityRadiusTiles; t = EffectSlot.LandingRingTiles; return true;
                case EffectKind.PlacementAura: m = EffectSlot.Percent; d = EffectSlot.DurationSec; return true;
                case EffectKind.AllyMoveSpeedAura: m = EffectSlot.Percent; t = EffectSlot.RadiusTiles; d = EffectSlot.DurationSec; return true;
                case EffectKind.ApplyCcToTarget: m = EffectSlot.Speed; d = EffectSlot.DurationSec; return true;
                case EffectKind.ApplyStackToTarget: m = EffectSlot.Count; t = EffectSlot.StackCap; d = EffectSlot.DurationSec; return true;
                case EffectKind.SelfStatBuff: m = EffectSlot.Percent; t = EffectSlot.StackCap; d = EffectSlot.DurationSec; return true;
                case EffectKind.HeavyStrike: m = EffectSlot.Mul; return true;
                case EffectKind.DreamCocoon: m = EffectSlot.Percent; d = EffectSlot.DurationSec; return true;
                case EffectKind.BountyMark: m = EffectSlot.Mul; t = EffectSlot.Percent; return true;
                case EffectKind.AreaSleep: m = EffectSlot.Count; t = EffectSlot.RadiusTiles; d = EffectSlot.DurationSec; return true;
                case EffectKind.EmitProjectilePattern: t = EffectSlot.RangeTiles; return true;
                case EffectKind.UltimateLeap: m = EffectSlot.DensityRadiusTiles; t = EffectSlot.LandingRingTiles; d = EffectSlot.FlightSec; return true;
                case EffectKind.GrantShield: m = EffectSlot.Shield; t = EffectSlot.RadiusTiles; return true;
                case EffectKind.AreaBreath: m = EffectSlot.Damage; t = EffectSlot.RangeTiles; return true;
                case EffectKind.SelfOrbitProjectile: m = EffectSlot.Damage; t = EffectSlot.RadiusTiles; d = EffectSlot.DurationSec; return true;
                case EffectKind.AreaTaunt: t = EffectSlot.RadiusTiles; d = EffectSlot.DurationSec; return true;
                case EffectKind.SpawnHazard: return true;
                case EffectKind.RecallAttachedToFront: return true;
                case EffectKind.AllyStatAura:
                case EffectKind.OpponentStatAura: m = EffectSlot.Percent; t = EffectSlot.RadiusTiles; d = EffectSlot.DurationSec; return true;
                case EffectKind.GainCost: m = EffectSlot.Count; return true;
                case EffectKind.ReduceSkillCooldown: m = EffectSlot.DurationSec; return true;
                case EffectKind.AreaApplyStack: m = EffectSlot.Count; t = EffectSlot.RadiusTiles; d = EffectSlot.DurationSec; return true;
                case EffectKind.AreaCc:
                case EffectKind.AreaDot: m = EffectSlot.Damage; t = EffectSlot.RadiusTiles; d = EffectSlot.DurationSec; return true;
                case EffectKind.ActiveMeteor: m = EffectSlot.Damage; t = EffectSlot.RadiusTiles; d = EffectSlot.FlightSec; return true;
                case EffectKind.ActiveSlowField:
                case EffectKind.ActivePowerSurge:
                case EffectKind.ActiveRapidFire: m = EffectSlot.Mul; t = EffectSlot.RadiusTiles; d = EffectSlot.DurationSec; return true;
                case EffectKind.ActiveTornado: m = EffectSlot.Speed; t = EffectSlot.RadiusTiles; d = EffectSlot.DurationSec; return true;
                case EffectKind.ActivePortal: d = EffectSlot.DurationSec; return true;
                // skill-data-table unit 8 — 상시 효과 4. 옛 겸직 칸 모양이 없던 종류(카드 자식 값이었다) — 보기용 첫 칸만 준다.
                case EffectKind.FactionStatBuff: m = EffectSlot.Percent; return true;
                case EffectKind.ProjectileBounce: m = EffectSlot.Mul; t = EffectSlot.RangeTiles; return true;
                case EffectKind.FrontmostTarget:
                case EffectKind.DamageVsSleeping: m = EffectSlot.Mul; return true;
                default: return false;   // None · AreaBarrage · SelfWarmupBuff · SplitOnDeath
            }
        }

        /// <summary>
        /// skill-data-table unit 8 — **종류별 사용 칸 표**(정본 = `tables.md` §3 의 새 칸 · 참조 + §9 의 비율 칸). 표에 드는 종류면 true 와
        /// 그 종류가 읽는 칸 전부(시트 `Skills` 탭의 열), 들지 않는 종류(센티넬 · 이관 · 죽은 값 · 분열 — `Of` 와 같은 넷)는 false.
        /// unit 9 export 가 「그 종류가 쓰는 칸만」 쓰는 데 쓴다(오라 줄의 `cc_kind` 같은 잡음 0). `kind` · `deprecated` · `effect_id` 는 모든 줄의 칸이라 여기 없다.
        /// </summary>
        public static bool UsedColumns(EffectKind kind, out EffectColumns cols)
        {
            const EffectColumns RatioCols = EffectColumns.MagnitudeMode | EffectColumns.BasisStat | EffectColumns.Ratio;
            switch (kind)
            {
                case EffectKind.ProjectileToTarget:
                    cols = EffectColumns.Damage | EffectColumns.RangeTiles | EffectColumns.FlightSec | EffectColumns.Telegraph | EffectColumns.ProjectileId; break;
                case EffectKind.SelfTileAoe:
                    cols = EffectColumns.Damage | EffectColumns.RadiusTiles | EffectColumns.FlightSec | EffectColumns.ProjectileId; break;
                case EffectKind.NextAttackDoubleFire:
                case EffectKind.RecallAttachedToFront:
                    cols = EffectColumns.None; break;
                case EffectKind.SelfBuffLethal:
                    cols = EffectColumns.Percent | EffectColumns.DurationSec; break;
                case EffectKind.SelfBlink:
                    cols = EffectColumns.DensityRadiusTiles | EffectColumns.LandingRingTiles | EffectColumns.Damage | EffectColumns.RadiusTiles | EffectColumns.ProjectileId; break;
                case EffectKind.PlacementAura:
                    cols = EffectColumns.Percent | EffectColumns.DurationSec | EffectColumns.AllyFilter; break;
                case EffectKind.AllyMoveSpeedAura:
                    cols = EffectColumns.Percent | EffectColumns.RadiusTiles | EffectColumns.DurationSec | EffectColumns.ProjectileId; break;
                case EffectKind.ApplyCcToTarget:
                    cols = EffectColumns.CcKind | EffectColumns.DurationSec | EffectColumns.Speed; break;
                case EffectKind.ApplyStackToTarget:
                    cols = EffectColumns.StackKind | EffectColumns.Count | EffectColumns.DurationSec | EffectColumns.StackCap; break;
                case EffectKind.SelfStatBuff:
                    cols = EffectColumns.BuffStat | EffectColumns.Percent | EffectColumns.DurationSec | EffectColumns.StackCap; break;
                case EffectKind.HeavyStrike:
                case EffectKind.FrontmostTarget:
                case EffectKind.DamageVsSleeping:
                    cols = EffectColumns.Mul; break;
                case EffectKind.DreamCocoon:
                    cols = EffectColumns.Percent | EffectColumns.DurationSec | EffectColumns.BuffStat; break;
                case EffectKind.BountyMark:
                    cols = EffectColumns.Mul | EffectColumns.Percent; break;
                case EffectKind.AreaSleep:
                    cols = EffectColumns.Count | EffectColumns.RadiusTiles | EffectColumns.DurationSec | EffectColumns.ProjectileId; break;
                case EffectKind.EmitProjectilePattern:
                    cols = EffectColumns.Damage | EffectColumns.RangeTiles | EffectColumns.PatternId; break;
                case EffectKind.UltimateLeap:
                    cols = EffectColumns.DensityRadiusTiles | EffectColumns.LandingRingTiles | EffectColumns.FlightSec | EffectColumns.Damage
                         | EffectColumns.RadiusTiles | EffectColumns.ProjectileId; break;
                case EffectKind.GrantShield:
                    cols = EffectColumns.Shield | EffectColumns.RadiusTiles | EffectColumns.Count | EffectColumns.ShieldFilter | EffectColumns.IncludesSelf; break;
                case EffectKind.AreaBreath:
                    cols = EffectColumns.Damage | EffectColumns.RangeTiles | EffectColumns.ConeHalfDeg; break;
                case EffectKind.SelfOrbitProjectile:
                    cols = EffectColumns.Damage | EffectColumns.RadiusTiles | EffectColumns.DurationSec | EffectColumns.Count | EffectColumns.ProjectileId; break;
                case EffectKind.AreaTaunt:
                    cols = EffectColumns.RadiusTiles | EffectColumns.DurationSec; break;
                case EffectKind.SpawnHazard:
                    cols = EffectColumns.Damage | EffectColumns.HazardId; break;
                case EffectKind.AllyStatAura:
                case EffectKind.OpponentStatAura:
                    cols = EffectColumns.BuffStat | EffectColumns.Percent | EffectColumns.RadiusTiles | EffectColumns.DurationSec; break;
                case EffectKind.GainCost:
                    cols = EffectColumns.Count; break;
                case EffectKind.ReduceSkillCooldown:
                    cols = EffectColumns.DurationSec; break;
                case EffectKind.AreaApplyStack:
                    cols = EffectColumns.StackKind | EffectColumns.Count | EffectColumns.RadiusTiles | EffectColumns.DurationSec; break;
                case EffectKind.AreaCc:
                    cols = EffectColumns.Damage | EffectColumns.RadiusTiles | EffectColumns.DurationSec | EffectColumns.CcKind; break;
                case EffectKind.AreaDot:
                    cols = EffectColumns.Damage | EffectColumns.RadiusTiles | EffectColumns.DurationSec | EffectColumns.TickSec; break;
                case EffectKind.ActiveMeteor:
                    cols = EffectColumns.Damage | EffectColumns.RadiusTiles | EffectColumns.FlightSec | EffectColumns.ProjectileId; break;
                case EffectKind.ActiveSlowField:
                case EffectKind.ActivePowerSurge:
                case EffectKind.ActiveRapidFire:
                    cols = EffectColumns.Mul | EffectColumns.RadiusTiles | EffectColumns.DurationSec; break;
                case EffectKind.ActiveTornado:
                    cols = EffectColumns.Speed | EffectColumns.RadiusTiles | EffectColumns.DurationSec; break;
                case EffectKind.ActivePortal:
                    cols = EffectColumns.DurationSec; break;
                case EffectKind.FactionStatBuff:
                    cols = EffectColumns.BuffStat | EffectColumns.Percent | EffectColumns.AllyFilter; break;
                case EffectKind.ProjectileBounce:
                    cols = EffectColumns.Count | EffectColumns.RangeTiles | EffectColumns.Mul; break;
                default:
                    cols = EffectColumns.None;
                    return false;   // None · AreaBarrage · SelfWarmupBuff · SplitOnDeath
            }
            // §9 — 비율 칸이 있는 종류만 수치 방식 칸을 쓴다(나머지는 비율형이 거절된다 — 칸이 뜻이 없다).
            if (EffectMagnitude.AcceptsRatio(kind)) cols |= RatioCols;
            return true;
        }

        /// <summary>착지 슬램이 있는 도약 둘 — 옛 `slamDamage` · `slamTileRange` 가 `damage` · `radiusTiles` 로 온다.</summary>
        public static bool IsLeap(EffectKind kind) => kind == EffectKind.SelfBlink || kind == EffectKind.UltimateLeap;

        public static float Get(in EffectValues v, EffectSlot s)
        {
            switch (s)
            {
                case EffectSlot.Damage: return v.damage;
                case EffectSlot.Shield: return v.shield;
                case EffectSlot.Percent: return v.percent;
                case EffectSlot.Mul: return v.mul;
                case EffectSlot.Count: return v.count;
                case EffectSlot.RadiusTiles: return v.radiusTiles;
                case EffectSlot.RangeTiles: return v.rangeTiles;
                case EffectSlot.DurationSec: return v.durationSec;
                case EffectSlot.FlightSec: return v.flightSec;
                case EffectSlot.StackCap: return v.stackCap;
                case EffectSlot.Speed: return v.speed;
                case EffectSlot.DensityRadiusTiles: return v.densityRadiusTiles;
                case EffectSlot.LandingRingTiles: return v.landingRingTiles;
                default: return 0f;
            }
        }

        /// <summary>뜻 칸에 쓴다. 정수 칸인데 값이 정수가 아니면 false(잘라서 쓴다 — 부르는 쪽이 손실로 적는다).</summary>
        public static bool Set(ref EffectValues v, EffectSlot s, float x)
        {
            int n = (int)x;
            bool exact = n == x;
            switch (s)
            {
                case EffectSlot.Damage: v.damage = x; return true;
                case EffectSlot.Shield: v.shield = x; return true;
                case EffectSlot.Percent: v.percent = x; return true;
                case EffectSlot.Mul: v.mul = x; return true;
                case EffectSlot.Count: v.count = n; return exact;
                case EffectSlot.RadiusTiles: v.radiusTiles = n; return exact;
                case EffectSlot.RangeTiles: v.rangeTiles = n; return exact;
                case EffectSlot.DurationSec: v.durationSec = x; return true;
                case EffectSlot.FlightSec: v.flightSec = x; return true;
                case EffectSlot.StackCap: v.stackCap = n; return exact;
                case EffectSlot.Speed: v.speed = x; return true;
                case EffectSlot.DensityRadiusTiles: v.densityRadiusTiles = n; return exact;
                case EffectSlot.LandingRingTiles: v.landingRingTiles = n; return exact;
                default: return x == 0f;
            }
        }

        /// <summary>
        /// 효과 값 → 옛 겸직 칸(굽기 방향). 코어 효과 줄은 아직 겸직 칸(`EffectDef.Magnitude` · `TileRange` · `Duration`)을 읽는다.
        /// 도약의 슬램 · 궤도 개수 · 틱 · 반각 · 예고 · 스탯 선택자도 옛 이름 칸으로 싣는다. CC · 스택은 옮기지 않는다(굽기가 새 번호를 직접 쓴다).
        /// </summary>
        public static LegacyPayload ToLegacy(in EffectValues v)
        {
            var p = new LegacyPayload { Kind = v.kind, BuffStat = v.buffStat, Telegraph = v.telegraph,
                                        TickIntervalSec = v.tickSec, ConeHalfAngleDeg = v.coneHalfDeg };
            if (Of(v.kind, out var m, out var t, out var d))
            {
                p.Magnitude = Get(in v, m);
                p.TileRange = (int)Get(in v, t);
                p.Duration = Get(in v, d);
            }
            if (IsLeap(v.kind)) { p.SlamDamage = v.damage; p.SlamTileRange = v.radiusTiles; }
            if (v.kind == EffectKind.SelfOrbitProjectile) p.OrbitCount = v.count;
            return p;
        }

        /// <summary>
        /// 옛 저작 → 효과 값(이전 방향). `movedDamage` = 옛 저작이 **효과 밖**에 두던 피해(U10 — 발사 명세 `damage` · 길막 폭발 ·
        /// 장판 DoT)로, 부르는 쪽이 굽기와 같은 산식으로 구해 넘긴다. 손실 · 모호는 `notes` 에 한 줄씩 적는다(dry-run 표의 깃발).
        /// `cardOwner` = 카드 저작인가(옛 카드 버프는 % · 유닛 저작은 배율 그대로 — 인코딩이 소유자마다 달랐다).
        /// </summary>
        public static EffectValues FromLegacy(in LegacyPayload p, bool cardOwner, float movedDamage, List<string> notes)
        {
            var v = new EffectValues
            {
                kind = p.Kind,
                buffStat = p.BuffStat,
                telegraph = p.Telegraph,
                tickSec = p.TickIntervalSec,
                coneHalfDeg = p.ConeHalfAngleDeg,
                ccKind = CcFromLegacy(p.DcCcKind),
                stackKind = StackFromLegacy(p.DcStackKind),
            };
            if (!Of(p.Kind, out var m, out var t, out var d))
            {
                notes?.Add($"종류 {p.Kind} 는 효과 표에 들지 않는다 — 옮기지 않는다");
                return v;
            }
            float magnitude = p.Magnitude;
            if (p.Kind == EffectKind.SelfStatBuff && !cardOwner)
            {
                // 옛 유닛 · 적 저작의 자기 버프는 배율 그대로였다(카드만 % → 배율). 효과 값은 소유자 무관하게 % 다.
                magnitude = (p.Magnitude - 1f) * 100f;
                notes?.Add($"SelfStatBuff 유닛·적 저작 배율 {p.Magnitude} → {magnitude}% (소유자 인코딩 통일 · 부동소수 오차 가능)");
            }
            Put(ref v, m, magnitude, "magnitude", notes);
            Put(ref v, t, p.TileRange, "tileRange", notes);
            Put(ref v, d, p.Duration, "duration", notes);

            if (IsLeap(p.Kind))
            {
                v.damage = p.SlamDamage;
                v.radiusTiles = p.SlamTileRange;
            }
            else
            {
                if (p.SlamDamage != 0f) notes?.Add($"slamDamage {p.SlamDamage} 는 도약 전용 — 옛 굽기도 무시했다(버림)");
                if (p.SlamTileRange != 0) notes?.Add($"slamTileRange {p.SlamTileRange} 는 도약 전용 — 옛 굽기는 해시에 실었다(버림 · 해시 변화)");
            }
            if (p.Kind == EffectKind.SelfOrbitProjectile) v.count = p.OrbitCount;
            else if (p.OrbitCount != 0) notes?.Add($"orbitCount {p.OrbitCount} 는 궤도 화염구 전용 — 옛 굽기도 무시했다(버림)");

            if (p.Kind == EffectKind.EmitProjectilePattern || p.Kind == EffectKind.SpawnHazard) v.damage = movedDamage;
            else if (movedDamage != 0f) notes?.Add($"옮길 피해 {movedDamage} 를 받을 칸이 {p.Kind} 에 없다(버림)");
            return v;
        }

        private static void Put(ref EffectValues v, EffectSlot s, float x, string legacyName, List<string> notes)
        {
            if (s == EffectSlot.None)
            {
                if (x != 0f) notes?.Add($"{v.kind} 는 옛 {legacyName} 칸을 안 읽는다 — 값 {x} 버림(옛 굽기는 해시에 실었다 · 해시 변화)");
                return;
            }
            if (!Set(ref v, s, x)) notes?.Add($"옛 {legacyName} {x} → {s} 정수 칸 — 소수부 버림(손실)");
        }

        /// <summary>
        /// 액티브(옛 `SkillData`) → 효과 값. `radiusTiles` = 옛 굽기의 `SkillMath.RangeToTiles(range)`(부르는 쪽이 같은 함수로 구한다).
        /// 메테오의 지속은 **낙하 예고**(`warningSec`)다 — 옛 굽기도 `durationSec` 를 버렸다.
        /// </summary>
        public static EffectValues FromActive(EffectKind activeKind, int radiusTiles, float magnitude, float durationSec, float warningSec,
                                              List<string> notes)
        {
            var v = new EffectValues { kind = activeKind };
            if (!Of(activeKind, out var m, out var t, out var d) || activeKind < EffectKind.ActiveMeteor)
            {
                notes?.Add($"액티브 종류 {activeKind} 가 표에 없다");
                return v;
            }
            float dur = activeKind == EffectKind.ActiveMeteor ? (warningSec > 0f ? warningSec : 0f) : durationSec;
            Put(ref v, m, magnitude, "magnitude", notes);
            Put(ref v, t, radiusTiles, "range", notes);
            Put(ref v, d, dur, activeKind == EffectKind.ActiveMeteor ? "warningSec" : "durationSec", notes);
            return v;
        }

        /// <summary>
        /// 실드 캐스트 능력(옛 `ShieldCastAbility` — 코드가 굽던 넷째 저장처) → 효과 값. U14 — 반경 = **오늘 유닛 사거리 파생값을 고정값으로**
        /// (`radiusTiles` · 부르는 쪽이 `SkillMath.RangeToTiles(attackRange)`). 대상 수는 옛 굽기처럼 최소 1 · 자기 포함.
        /// CC · 스택 칸은 **0(비움)** — 옛 굽기가 이 줄에는 선택자를 안 썼다(효과 줄 값 동치).
        /// </summary>
        public static EffectValues FromShieldCast(float amount, int targetCount, ShieldTargetFilter filter, int radiusTiles)
            => new EffectValues
            {
                kind = EffectKind.GrantShield,
                shield = amount,
                radiusTiles = radiusTiles,
                count = Math.Max(1, targetCount),
                shieldFilter = filter,
                includesSelf = true,
            };

        /// <summary>스킬 어휘 CC → 옛 `DcCcKind`(문안 · 진단의 옛 칸 보기). 옛 저작이 못 고르던 값(둔화 · DoT)은 기절(옛 기본).</summary>
        public static DcCcKind CcToLegacy(CcKind kind)
        {
            switch (kind)
            {
                case CcKind.Impulse: return DcCcKind.Impulse;
                case CcKind.Sleep: return DcCcKind.Sleep;
                default: return DcCcKind.Stun;
            }
        }

        /// <summary>스킬 어휘 스택 → 옛 `DcStackKind`. 옛 저작이 못 고르던 값(None · Fatigue)은 출혈(옛 번역 기본).</summary>
        public static DcStackKind StackToLegacy(StackKind kind)
        {
            switch (kind)
            {
                case StackKind.Fire: return DcStackKind.Fire;
                case StackKind.Ice: return DcStackKind.Ice;
                case StackKind.Poison: return DcStackKind.Poison;
                default: return DcStackKind.Bleed;
            }
        }

        /// <summary>옛 `DcCcKind { Stun, Impulse, Sleep }` 정수 → 스킬 어휘(`Stun 0→3 · Impulse 1→1 · Sleep 2→4`). 모르는 값 = 기절(옛 번역의 기본).</summary>
        public static CcKind CcFromLegacy(int dcCcKind)
        {
            switch (dcCcKind)
            {
                case 1: return CcKind.Impulse;
                case 2: return CcKind.Sleep;
                default: return CcKind.Stun;
            }
        }

        /// <summary>옛 `DcStackKind { Fire, Ice, Bleed, Poison }` 정수 → 스킬 어휘(None 이 0 이라 하나씩 밀린다). 모르는 값 = 출혈(옛 번역의 기본).</summary>
        public static StackKind StackFromLegacy(int dcStackKind)
        {
            switch (dcStackKind)
            {
                case 0: return StackKind.Fire;
                case 1: return StackKind.Ice;
                case 3: return StackKind.Poison;
                default: return StackKind.Bleed;
            }
        }
    }
}
