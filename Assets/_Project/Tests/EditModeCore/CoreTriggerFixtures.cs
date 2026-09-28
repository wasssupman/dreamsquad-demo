using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7a — 규칙(바인딩) 테스트의 공용 고정구.
    //
    // ⚠ 여기 수치는 **게임 값이 아니라 픽스처**다(다른 고정구와 같은 규율).
    public static class CoreTriggerFixtures
    {
        /// <summary>라우팅 표로 실행자를 고른 규칙 한 줄(센티널 -1 로 시작).</summary>
        public static BindingDef Rule(TriggerKind trigger, EffectKind payload)
        {
            var d = BindingDef.Default();
            d.Trigger = trigger;
            d.Payload = payload;
            d.Skill = SkillRouting.Resolve(trigger, payload);
            d.Label = trigger + "×" + payload;
            return d;
        }

        /// <summary>실행자를 직접 주는 규칙(탐침 스킬).</summary>
        public static BindingDef Probe(TriggerKind trigger, ISkill skill, Seam _ = Seam.Periodic)
        {
            var d = BindingDef.Default();
            d.Trigger = trigger;
            d.Skill = skill;
            d.Label = "probe×" + trigger;
            return d;
        }

        public static int[] Add(MatchDefinition def, params BindingDef[] rows)
        {
            var list = new List<BindingDef>(def.Bindings);
            var idx = new int[rows.Length];
            for (int i = 0; i < rows.Length; i++) { idx[i] = list.Count; list.Add(rows[i]); }
            def.Bindings = list.ToArray();
            return idx;
        }

        public static void GiveUnit(MatchDefinition def, int unitIndex, params BindingDef[] rows)
        {
            var idx = Add(def, rows);
            def.Units[unitIndex].Bindings = Concat(def.Units[unitIndex].Bindings, idx);
            def.ConfigHash = def.ComputeConfigHash();
        }

        public static void GiveEnemy(MatchDefinition def, int enemyIndex, params BindingDef[] rows)
        {
            var idx = Add(def, rows);
            def.Enemies[enemyIndex].Bindings = Concat(def.Enemies[enemyIndex].Bindings, idx);
            def.ConfigHash = def.ComputeConfigHash();
        }

        private static int[] Concat(int[] a, int[] b)
        {
            var list = new List<int>(a ?? Array.Empty<int>());
            list.AddRange(b);
            return list.ToArray();
        }

        /// <summary>자리를 때리는 폭발용 탄(하늘 낙하 × 칸 광역). 반환 = 줄 번호.</summary>
        public static int AddBlastProjectile(MatchDefinition def)
        {
            var p = ProjectileDef.Default();
            p.Id = "fixture_blast";
            p.Movement = (int)MovementKind.SkyFall;
            p.Payload = (int)PayloadKind.TileAoe;
            var list = new List<ProjectileDef>(def.Projectiles) { p };
            def.Projectiles = list.ToArray();
            return list.Count - 1;
        }

        /// <summary>
        /// 탐침 스킬 — 실행될 때마다 부른다. 규칙 레이어의 **순서·세대·깊이**를 재는 도구다(실제 concrete 가 아니다).
        /// </summary>
        public sealed class ProbeSkill : ISkill
        {
            public int SkillId => 900;
            public Action<CasterRef, SkillTarget, SkillParams, ISkillContext> OnExecute;
            public int Count;

            public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
            {
                Count++;
                OnExecute?.Invoke(caster, target, p, ctx);
            }
        }

        public static TriggerEvent EventAt(Seam seam, Unit subject)
            => new TriggerEvent
            {
                Seam = seam,
                Kind = TriggerKind.None,
                Subject = subject.Id,
                SubjectFaction = subject.Faction,
                SubjectPos = subject.Position,
                SubjectBody = subject.HitRadius,
                Target = SimEntityId.None,
            };

        public static Unit SpawnDefender(BattleMatch m, int2 cell)
        {
            m.Apply(Command.DebugSpawnDefender(0, cell));
            var units = m.World.Units;
            return units[units.Count - 1];
        }

        public static Unit SpawnEnemy(BattleMatch m, int2 cell)
        {
            m.Apply(Command.DebugSpawnEnemy(0, cell));
            var units = m.World.Units;
            return units[units.Count - 1];
        }
    }
}
