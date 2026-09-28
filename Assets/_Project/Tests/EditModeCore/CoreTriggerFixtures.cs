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
    /// <summary>
    /// skill-data-table 1b — 고정구의 **규칙 한 줄 + 그 효과 줄**(표에 넣기 전의 짝). 정의표에서는 둘이 갈라진다 — 규칙 줄은
    /// `MatchDefinition.Bindings`, 효과 줄은 `MatchDefinition.Effects`(`Add` 가 넣고 번호를 잇는다).
    /// </summary>
    public struct RuleRow
    {
        public BindingDef Rule;
        public EffectDef Effect;
    }

    public static class CoreTriggerFixtures
    {
        /// <summary>라우팅 표로 실행자를 고른 규칙 한 줄(센티널 -1 로 시작).</summary>
        public static RuleRow Rule(TriggerKind trigger, EffectKind payload)
        {
            var d = BindingDef.Default();
            d.Trigger = trigger;
            d.Skill = SkillRouting.Resolve(trigger, payload);
            d.Label = trigger + "×" + payload;
            var e = EffectDef.Default();
            e.Kind = payload;
            return new RuleRow { Rule = d, Effect = e };
        }

        /// <summary>실행자를 직접 주는 규칙(탐침 스킬).</summary>
        public static RuleRow Probe(TriggerKind trigger, ISkill skill, Seam _ = Seam.Periodic)
        {
            var d = BindingDef.Default();
            d.Trigger = trigger;
            d.Skill = skill;
            d.Label = "probe×" + trigger;
            return new RuleRow { Rule = d, Effect = EffectDef.Default() };
        }

        /// <summary>규칙 줄을 표에 더한다 — 효과 줄은 효과 표로(id = `fixture.{규칙 줄 번호}`). 반환 = 규칙 줄 번호들.</summary>
        public static int[] Add(MatchDefinition def, params RuleRow[] rows)
        {
            var list = new List<BindingDef>(def.Bindings);
            var effects = new List<EffectDef>(def.Effects);
            var idx = new int[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                idx[i] = list.Count;
                var b = rows[i].Rule;
                var e = rows[i].Effect;
                e.Id = "fixture." + list.Count;
                b.EffectIndex = EffectDef.Intern(effects, in e);
                list.Add(b);
            }
            def.Bindings = list.ToArray();
            def.Effects = effects.ToArray();
            return idx;
        }

        /// <summary>
        /// 런타임 부착(정의표 줄 없음 · `DefIndex = -1`) — 효과 줄은 그 판 정의표의 효과 표에 넣는다(인스턴스가 붙는 순간
        /// `EffectOf` 로 해석한다). 효과 없는 탐침(`Probe`)도 같은 길이다. 효과 표는 해시 밖이라 판의 해시는 안 바뀐다.
        /// </summary>
        public static Binding AttachRuntime(BattleMatch m, Unit owner, RuleRow row, int tick = 0)
        {
            var def = m.Definition;
            var effects = new List<EffectDef>(def.Effects);
            var e = row.Effect;
            e.Id = "runtime." + effects.Count;
            row.Rule.EffectIndex = EffectDef.Intern(effects, in e);
            def.Effects = effects.ToArray();
            return m.Bindings.Attach(owner, in row.Rule, -1, tick);
        }

        public static void GiveUnit(MatchDefinition def, int unitIndex, params RuleRow[] rows)
        {
            var idx = Add(def, rows);
            def.Units[unitIndex].Bindings = Concat(def.Units[unitIndex].Bindings, idx);
            def.ConfigHash = def.ComputeConfigHash();
        }

        public static void GiveEnemy(MatchDefinition def, int enemyIndex, params RuleRow[] rows)
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
