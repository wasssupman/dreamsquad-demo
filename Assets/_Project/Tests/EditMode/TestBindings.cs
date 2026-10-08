using System.Collections.Generic;
using UnityEngine;
using Somnia.Battle.BattleCore.Trigger;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.Data;

namespace Somnia.Battle.Tests.EditMode
{
    /// <summary>
    /// skill-data-table unit 4 — 테스트 픽스처를 **옛 메커닉 모양으로 적어 소유 줄로** 만든다. 이전 뒤 빌더 · 문안 · 진단은 소유 줄
    /// (`bindings` → 효과 에셋)만 읽는다. 픽스처는 읽기 쉬운 옛 모양(magnitude · tileRange · duration)으로 쓰고 여기서 옮긴다 —
    /// 옮기는 규칙은 이전(`2933243c2`)과 같은 표(`EffectSlots.FromLegacy`) + 효과 밖 피해(U10 — 명세 · 길막 · 장판).
    /// 만든 효과 에셋(메모리)은 `made` 에 담는다(부르는 쪽 TearDown 이 지운다 · null 이면 남는다).
    /// </summary>
    public static class TestBindings
    {
        public static BindingSpec From(in DcMechanic m, bool cardOwner = true, List<Object> made = null)
        {
            var p = m.payload;
            var e = ScriptableObject.CreateInstance<EffectData>();
            e.id = "fixture_" + p.kind.ToString().ToLowerInvariant();
            e.values = EffectSlots.FromLegacy(ToLegacyPayload(in p), cardOwner, MovedDamage(in p), null);
            e.projectile = p.projectile;
            e.pattern = p.pattern;
            e.hazard = p.hazard;
            e.auraPrefab = p.auraPrefab;
            e.auraScale = p.auraScale;
            e.stackModifier = p.stackModifier;
            made?.Add(e);
            return new BindingSpec { trigger = m.trigger, fireCap = p.kind == EffectKind.UltimateLeap ? 1 : 0, effect = e };
        }

        public static BindingSpec[] From(DcMechanic[] mechanics, bool cardOwner = true, List<Object> made = null)
        {
            if (mechanics == null) return null;
            var r = new BindingSpec[mechanics.Length];
            for (int i = 0; i < r.Length; i++) r[i] = From(in mechanics[i], cardOwner, made);
            return r;
        }

        /// <summary>카드에 옛 모양 메커닉을 소유 줄로 단다. 숙주 종류 = 표식 효과가 있으면 적만(이전 규칙과 같다) · null = 줄 없음.</summary>
        public static void Attach(DreamcatcherCard card, DcMechanic[] mechanics, List<Object> made = null)
        {
            card.bindings = From(mechanics, true, made);
            card.hostKinds = card.HasBountyMark() ? HostKinds.Enemy : HostKinds.Defender;
        }

        /// <summary>옛 굽기가 효과 밖(명세 · 길막 · 장판 SO)에서 읽던 피해 — 굽기와 같은 산식.</summary>
        private static float MovedDamage(in DcPayloadSpec p)
        {
            if (p.kind == EffectKind.EmitProjectilePattern && p.pattern != null)
            {
                var blocker = p.pattern.barrel != null ? p.pattern.barrel.spawnBlocker : null;
                return blocker != null ? Mathf.Max(0f, blocker.explodeDamage) : p.pattern.damage;
            }
            if (p.kind == EffectKind.SpawnHazard && p.hazard != null)
                return BoardEffectDefinitionBuilder.TryDotDamage(p.hazard, out float dot) ? dot : 0f;
            return 0f;
        }

        private static LegacyPayload ToLegacyPayload(in DcPayloadSpec p) => new LegacyPayload
        {
            Kind = p.kind,
            Magnitude = p.magnitude,
            TileRange = p.tileRange,
            Duration = p.duration,
            SlamDamage = p.slamDamage,
            SlamTileRange = p.slamTileRange,
            TickIntervalSec = p.tickIntervalSec,
            OrbitCount = p.orbitCount,
            ConeHalfAngleDeg = p.coneHalfAngleDeg,
            DcCcKind = (int)p.ccKind,
            DcStackKind = (int)p.stackKind,
            BuffStat = p.buffStat,
            Telegraph = p.telegraph,
        };
    }
}
