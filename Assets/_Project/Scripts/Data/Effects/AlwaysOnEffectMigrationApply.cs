using System;
using System.Collections.Generic;
using UnityEngine;
using Wassup.BattleCore.Trigger;

namespace Wassup.Data
{
    /// <summary>
    /// skill-data-table unit 8 — 상시 효과 이전 계획(`AlwaysOnEffectMigration` — 순수)을 **SO 에 입히는 쪽**. 에디터 메뉴는 만든 효과 에셋으로,
    /// 테스트는 메모리 사본으로 같은 함수를 부른다(적용 전 증명 — 굽기 스냅샷 · 문안 동치). 에디터 API 는 쓰지 않는다(에셋 생성 · 저장은 메뉴 몫).
    /// 이 파일은 이전 과도기 전용이다 — 옛 칸이 걷히면(단계 B) 같이 지운다.
    /// </summary>
    public static class AlwaysOnEffectMigrationApply
    {
        /// <summary>카드 SO → 계획 입력(plain 값).</summary>
        public static AlwaysOnEffectMigration.CardInput InputOf(DreamcatcherCard c, string source)
        {
            var auraIds = new List<string>();
            var auraFilters = new List<CardTargetAxis>();
            if (c.bindings != null)
                foreach (var b in c.bindings)
                    if (b.effect != null && b.effect.values.kind == EffectKind.PlacementAura && !auraIds.Contains(b.effect.id))
                    {
                        auraIds.Add(b.effect.id);
                        auraFilters.Add(b.effect.values.allyFilter);
                    }
            return new AlwaysOnEffectMigration.CardInput
            {
                Id = c.id,
                Source = source,
                Type = c.type,
                Axis = c.axis,
                Hosts = c.hostKinds,
                Effects = c.effects,
                AttackMods = c.attackMods,
                BindingCount = c.bindings != null ? c.bindings.Length : 0,
                HasAlwaysOnRows = c.HasAlwaysOnRows(),
                PlacementAuraEffectIds = auraIds.ToArray(),
                PlacementAuraFilters = auraFilters.ToArray(),
            };
        }

        /// <summary>계획의 효과 한 줄 → 효과 SO(메모리 · 에셋 아님). 메뉴는 이것을 에셋으로 만든다.</summary>
        public static EffectData Materialize(AlwaysOnEffectMigration.Row r)
        {
            var e = ScriptableObject.CreateInstance<EffectData>();
            e.name = "Effect_" + r.EffectId;
            e.id = r.EffectId;
            e.values = r.Values;
            return e;
        }

        /// <summary>
        /// 그 카드의 계획 줄을 소유 줄 **뒤에** 붙인다(트리거 `None` · 게이트 없음 · fire_cap 0). 새 칸만 쓴다 — 옛 칸(`effects` · `attackMods`)은
        /// 그대로(단계 B 가 지운다 · 과도기 굽기는 상시 효과 줄이 있으면 옛 칸을 안 읽는다). slot 이 계획과 어긋나면 아무것도 안 쓰고 false.
        /// </summary>
        public static bool AppendRows(DreamcatcherCard card, IEnumerable<AlwaysOnEffectMigration.Row> rows,
                                      Func<AlwaysOnEffectMigration.Row, EffectData> effectFor)
        {
            var list = new List<BindingSpec>(card.bindings ?? Array.Empty<BindingSpec>());
            var add = new List<AlwaysOnEffectMigration.Row>();
            foreach (var r in rows) if (r.CardId == card.id) add.Add(r);
            for (int i = 0; i < add.Count; i++)
                if (add[i].Slot != list.Count + i)
                {
                    Debug.LogError($"[AlwaysOnEffectMigration] 카드 '{card.id}': 계획 slot {add[i].Slot} ≠ 지금 자리 {list.Count + i} — 계획 뒤에 카드가 바뀌었다. 쓰지 않는다.");
                    return false;
                }
            foreach (var r in add)
                list.Add(new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.None }, fireCap = 0, effect = effectFor(r) });
            card.bindings = list.ToArray();
            return true;
        }
    }
}
