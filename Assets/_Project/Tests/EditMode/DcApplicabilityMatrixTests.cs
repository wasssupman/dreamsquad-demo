using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-attack-decoupling unit 1 — 전수 행렬. 카탈로그의 **실제 에셋**
    // (전 DreamcatcherCard × 전 DefenderUnitData)을 읽어 판정을 고정한다. 카드나
    // 유닛이 추가되면 자동으로 커버 범위에 들어오므로, "붙는데 무효" 조합이
    // 재생산되면 여기서 먼저 실패한다 — 이 spec 의 검증 질문이 전칭 명제라
    // 일화 몇 건으로는 답이 되지 않는다.
    public class DcApplicabilityMatrixTests
    {
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";
        private const string DefendersRoot = "Assets/_Project/Data/Defenders";

        private static List<T> LoadAll<T>(string root) where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }

        // SO 만으로 host profile 을 근사한다. 런타임 조립(BattleBridge.BuildHostProfile)은
        // 같은 규칙을 ECS 컴포넌트로 판별하며, 그쪽 진실은 Play 검증이 맡는다.
        private static DcHostProfile ProfileOf(DefenderUnitData unit)
        {
            var archetype = DcHostArchetype.Standard;
            var route = DcProjectileRoute.None;

            bool hasBomb = unit.GetAbility<BombThrowAbility>() != null;
            bool hasHazard = unit.GetAbility<HazardCastAbility>() != null;
            if (hasBomb) archetype = DcHostArchetype.BombThrow;
            else if (hasHazard) archetype = DcHostArchetype.HazardCast;
            else if (unit.RequiresFacing) archetype = DcHostArchetype.FacingVolley;

            if (hasBomb) route = DcProjectileRoute.Grenade;
            else if (unit.projectile != null)
            {
                switch (unit.projectile.flightMode)
                {
                    case ProjectileFlightMode.Homing: route = DcProjectileRoute.Homing; break;
                    case ProjectileFlightMode.BallisticToCell: route = DcProjectileRoute.Ballistic; break;
                    case ProjectileFlightMode.Directional: route = DcProjectileRoute.Directional; break;
                }
            }

            bool hasDamageOutput = false;
            if (unit.outputs != null)
                foreach (var o in unit.outputs)
                    if (o.kind == AttackOutputKind.Damage && o.magnitude > 0f) hasDamageOutput = true;

            return new DcHostProfile
            {
                archetype = archetype,
                route = route,
                targetsEnemies = !unit.targetAllies,
                hasDamageOutput = hasDamageOutput,
            };
        }

        // ── 미분류 0: 실제 카탈로그 전 조합이 판정된다 ─────────────────────
        [Test]
        public void EveryCardHostPair_IsClassified()
        {
            var cards = LoadAll<DreamcatcherCard>(CardsRoot);
            var units = LoadAll<DefenderUnitData>(DefendersRoot);
            Assert.Greater(cards.Count, 0, "카드 에셋을 찾지 못했다");
            Assert.Greater(units.Count, 0, "디펜더 에셋을 찾지 못했다");

            foreach (var unit in units)
            {
                var host = ProfileOf(unit);
                foreach (var card in cards)
                {
                    if (card.type != CardType.Unit) continue;
                    if (card.mechanics != null)
                        foreach (var m in card.mechanics)
                            Assert.IsTrue(System.Enum.IsDefined(typeof(DcRejectReason),
                                DcApplicability.EvaluateMechanic(m.payload.kind, m.trigger.kind, host)),
                                $"미분류: {card.id} × {unit.id} ({m.payload.kind})");
                    if (card.attackMods != null)
                        foreach (var am in card.attackMods)
                            Assert.IsTrue(System.Enum.IsDefined(typeof(DcRejectReason),
                                DcApplicability.EvaluateAttackMod(am.kind, host)),
                                $"미분류: {card.id} × {unit.id} ({am.kind})");
                }
            }
        }

        // ── spec unit 1 의 잠금 표를 실제 에셋으로 고정 ────────────────────
        [Test]
        public void LockTable_MatchesSpec()
        {
            var cards = LoadAll<DreamcatcherCard>(CardsRoot);
            var units = LoadAll<DefenderUnitData>(DefendersRoot);
            var log = new StringBuilder();

            foreach (var unit in units)
            {
                var host = ProfileOf(unit);
                foreach (var card in cards)
                {
                    if (card.type != CardType.Unit || card.mechanics == null) continue;
                    foreach (var m in card.mechanics)
                    {
                        var reason = DcApplicability.EvaluateMechanic(m.payload.kind, m.trigger.kind, host);

                        // AttackN 은 사건 지점이 있는 아키타입에서만. unit 3·4 가 이 기대를 뒤집는다.
                        if (m.trigger.kind == DcTriggerKind.AttackN
                            && (host.archetype == DcHostArchetype.BombThrow
                             || host.archetype == DcHostArchetype.HazardCast)
                            && reason != DcRejectReason.NoEventPoint)
                            log.AppendLine($"{card.id} × {unit.id}: AttackN 이 {host.archetype} 에서 열려 있다 ({reason})");

                        // 비수는 아군을 겨누는 host 에 붙으면 안 된다(영구).
                        if (m.payload.kind == DcPayloadKind.ProjectileToTarget
                            && !host.targetsEnemies && reason == DcRejectReason.None)
                            log.AppendLine($"{card.id} × {unit.id}: ally-targeting host 인데 통과했다");
                    }
                }
            }
            Assert.IsEmpty(log.ToString(), "잠금 표 위반:\n" + log);
        }

        // ── unit 2: 비수 폴백 반경이 에셋·bake 에 살아 있는지 ──────────────
        // 시트 DcMechanics 의 tileRange 셀이 명시적 0 이면 로그인 import 가 SO 를
        // 되돌린다(blank 만 keep) → 폴백이 조용히 죽는다. 이 테스트가 그 회귀를 잡는다.
        [Test]
        public void PokeNeedle_HasPositiveFallbackRange()
        {
            var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(
                CardsRoot + "/Card_PokeNeedle.asset");
            Assert.IsNotNull(card, "Card_PokeNeedle.asset 을 찾지 못했다");

            var found = false;
            foreach (var m in card.mechanics)
            {
                if (m.payload.kind != DcPayloadKind.ProjectileToTarget) continue;
                found = true;
                Assert.Greater(m.payload.tileRange, 0,
                    "폴백 탐색 반경이 0 이면 host 타겟이 없는 유닛에서 니들이 영영 안 나간다. "
                    + "시트 DcMechanics/poke_needle 의 tileRange 셀도 함께 확인할 것");
            }
            Assert.IsTrue(found, "ProjectileToTarget 메커닉이 없다");
        }

        // ── 통통구슬: homing 유닛에만 (방향탄 개통은 별도 spec) ────────────
        [Test]
        public void ProjectileBounce_OnlyOnHomingRouteUnits()
        {
            var units = LoadAll<DefenderUnitData>(DefendersRoot);
            var accepted = new List<string>();
            var rejected = new List<string>();

            foreach (var unit in units)
            {
                var host = ProfileOf(unit);
                var reason = DcApplicability.EvaluateAttackMod(DcAttackModKind.ProjectileBounce, host);
                (reason == DcRejectReason.None ? accepted : rejected).Add(unit.id);
                if (reason == DcRejectReason.None)
                    Assert.AreEqual(DcProjectileRoute.Homing, host.route,
                        $"{unit.id}: homing 이 아닌데 bounce 를 통과시켰다");
            }

            Assert.Contains("machine_gunner", rejected, "머신거너는 DirectionalLinear — 별도 spec 전까지 거절");
            Assert.Contains("bomb_man", rejected, "폭탄맨은 GrenadeToCell (ProjectileRef 는 있지만)");
            Assert.Contains("artillery", rejected, "아틸러리는 Ballistic — 기존 계약 4 의 의도된 제외");
            Assert.Greater(accepted.Count, 0, "homing 유닛이 하나도 없을 리 없다");
        }
    }
}
