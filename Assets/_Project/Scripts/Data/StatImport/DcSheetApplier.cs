using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Wassup.Data.StatImport
{
    // dreamcatcher-sheet-sync unit 2 — the dreamcatcher-tab apply core. Same
    // id-match partial-update philosophy as UnitStatApplier, plus the two array
    // semantics from 0_json_schema_contract.md:
    //  - sheet-SoT tabs (DcCardEffects/DcAttackMods, pure scalars): a cardId that
    //    appears in the tab gets its array REBUILT from its rows (slot-ordered);
    //    absent cards keep their arrays; length changes are reported.
    //  - Unity-SoT tab (DcMechanics, holds a projectile asset ref): value overlay
    //    onto the existing slot only; structure and asset refs never change.
    public static class DcSheetApplier
    {
        public static string Apply(DcSheetPayload payload,
            Dictionary<string, DreamcatcherCard> cardsById,
            Dictionary<string, SkillData> skillsById,
            Dictionary<string, ScriptableObject> configsById,
            Action<ScriptableObject> onApplied,
            StringBuilder log)
        {
            var c = new Counters();

            ApplyFlat(payload?.cards, "dc-card", dto => dto.id, cardsById, onApplied, log, c, null);
            ApplyFlat(payload?.skills, "dc-skill", dto => dto.id, skillsById, onApplied, log, c, null);
            ApplyFlat(payload?.configs, "dc-config", dto => dto.id, configsById, onApplied, log, c, null);

            RebuildEffects(payload?.cardEffects, cardsById, onApplied, log, c);
            RebuildAttackMods(payload?.attackMods, cardsById, onApplied, log, c);
            OverlayMechanics(payload?.mechanics, cardsById, onApplied, log, c);

            log.Insert(0, $"Matched {c.matched}, unmatched {c.unmatched}, fields applied {c.fieldsApplied}, arrays rebuilt {c.rebuilt}, skipped {c.skipped}.\n");
            return log.ToString();
        }

        private class Counters
        {
            public int matched, unmatched, fieldsApplied, rebuilt, skipped;
        }

        // -------- flat tabs (cards / skills / configs) --------

        private static void ApplyFlat<TDto, TSo>(TDto[] rows, string label,
            Func<TDto, string> idOf, Dictionary<string, TSo> byId,
            Action<ScriptableObject> onApplied, StringBuilder log, Counters c,
            Action<TSo> postApply) where TSo : ScriptableObject
        {
            if (rows == null) return;
            var seen = new HashSet<string>();
            foreach (var dto in rows)
            {
                string id = idOf(dto);
                if (!seen.Add(id ?? ""))
                { c.skipped++; log.AppendLine($"[{label}] duplicate row for id='{id}' — skipped."); continue; }
                if (string.IsNullOrEmpty(id) || !byId.TryGetValue(id, out var so))
                { c.unmatched++; log.AppendLine($"[{label}] no match for id='{id}'"); continue; }
                c.fieldsApplied += UnitStatFieldMapper.ApplyNonNullFields(dto, so);
                postApply?.Invoke(so);
                onApplied?.Invoke(so);
                c.matched++;
            }
        }

        // -------- sheet-SoT tabs: group rows per card, rebuild the array --------

        // Groups child rows by cardId with per-card validation: null/duplicate
        // slots poison the whole card (never guess an order), unknown cardIds are
        // reported once. Returns slot-sorted rows per matched card.
        private static Dictionary<DreamcatcherCard, List<TDto>> GroupByCard<TDto>(TDto[] rows,
            string label, Func<TDto, string> cardIdOf, Func<TDto, int?> slotOf,
            Dictionary<string, DreamcatcherCard> cardsById, StringBuilder log, Counters c)
        {
            var byCard = new Dictionary<DreamcatcherCard, List<TDto>>();
            var poisoned = new HashSet<DreamcatcherCard>();
            foreach (var dto in rows)
            {
                string cardId = cardIdOf(dto);
                if (string.IsNullOrEmpty(cardId) || !cardsById.TryGetValue(cardId, out var so))
                { c.unmatched++; log.AppendLine($"[{label}] no match for cardId='{cardId}'"); continue; }
                // review H1 — negative slots must poison like null ones: they are
                // not "new" (isNew checks >= old.Length) and would index old[-1].
                int? slot = slotOf(dto);
                if (slot == null || slot < 0)
                {
                    c.skipped++; poisoned.Add(so);
                    log.AppendLine($"[{label}] '{cardId}' row without valid slot ({(slot == null ? "blank" : slot.ToString())}) — card skipped.");
                    continue;
                }
                if (!byCard.TryGetValue(so, out var list)) byCard[so] = list = new List<TDto>();
                list.Add(dto);
            }
            foreach (var (so, list) in byCard.ToArray())
            {
                bool dupSlot = list.GroupBy(d => slotOf(d).Value).Any(g => g.Count() > 1);
                if (dupSlot)
                {
                    c.skipped += list.Count; poisoned.Add(so);
                    log.AppendLine($"[{label}] '{so.id}' has duplicate slots — card skipped.");
                }
                list.Sort((a, b) => slotOf(a).Value.CompareTo(slotOf(b).Value));
            }
            foreach (var so in poisoned) byCard.Remove(so);
            return byCard;
        }

        private static void RebuildEffects(DcCardEffectDto[] rows,
            Dictionary<string, DreamcatcherCard> cardsById,
            Action<ScriptableObject> onApplied, StringBuilder log, Counters c)
        {
            if (rows == null) return;
            foreach (var (so, list) in GroupByCard(rows, "dc-effects",
                         d => d.cardId, d => d.slot, cardsById, log, c))
            {
                var old = so.effects ?? Array.Empty<CardEffect>();
                var next = new CardEffect[list.Count];
                bool valid = true;
                for (int i = 0; i < list.Count; i++)
                {
                    var dto = list[i];
                    bool isNew = dto.slot.Value >= old.Length;
                    if (isNew && dto.kind == null)
                    {
                        log.AppendLine($"[dc-effects] '{so.id}' new slot {dto.slot} missing kind — card skipped.");
                        c.skipped += list.Count; valid = false; break;
                    }
                    var e = isNew ? default : old[dto.slot.Value];
                    if (dto.kind != null) { e.kind = dto.kind.Value; c.fieldsApplied++; }
                    if (dto.percent != null) { e.percent = dto.percent.Value; c.fieldsApplied++; }
                    next[i] = e;
                }
                if (!valid) continue;
                if (old.Length != next.Length)
                    log.AppendLine($"[dc-effects] '{so.id}' effects {old.Length}→{next.Length}.");
                so.effects = next;
                c.rebuilt++; c.matched++;
                onApplied?.Invoke(so);
            }
        }

        private static void RebuildAttackMods(DcAttackModDto[] rows,
            Dictionary<string, DreamcatcherCard> cardsById,
            Action<ScriptableObject> onApplied, StringBuilder log, Counters c)
        {
            if (rows == null) return;
            foreach (var (so, list) in GroupByCard(rows, "dc-attackmods",
                         d => d.cardId, d => d.slot, cardsById, log, c))
            {
                var old = so.attackMods ?? Array.Empty<DcAttackModSpec>();
                var next = new DcAttackModSpec[list.Count];
                bool valid = true;
                for (int i = 0; i < list.Count; i++)
                {
                    var dto = list[i];
                    bool isNew = dto.slot.Value >= old.Length;
                    if (isNew && dto.kind == null)
                    {
                        log.AppendLine($"[dc-attackmods] '{so.id}' new slot {dto.slot} missing kind — card skipped.");
                        c.skipped += list.Count; valid = false; break;
                    }
                    var m = isNew ? default : old[dto.slot.Value];
                    if (dto.kind != null) { m.kind = dto.kind.Value; c.fieldsApplied++; }
                    if (dto.count != null) { m.count = dto.count.Value; c.fieldsApplied++; }
                    if (dto.tileRange != null) { m.tileRange = dto.tileRange.Value; c.fieldsApplied++; }
                    if (dto.damageMul != null) { m.damageMul = dto.damageMul.Value; c.fieldsApplied++; }
                    next[i] = m;
                }
                if (!valid) continue;
                if (old.Length != next.Length)
                    log.AppendLine($"[dc-attackmods] '{so.id}' attackMods {old.Length}→{next.Length}.");
                so.attackMods = next;
                c.rebuilt++; c.matched++;
                onApplied?.Invoke(so);
            }
        }

        // -------- Unity-SoT tab: overlay values onto existing mechanics slots --------

        private static void OverlayMechanics(DcMechanicDto[] rows,
            Dictionary<string, DreamcatcherCard> cardsById,
            Action<ScriptableObject> onApplied, StringBuilder log, Counters c)
        {
            if (rows == null) return;
            var seen = new HashSet<string>();
            foreach (var dto in rows)
            {
                if (string.IsNullOrEmpty(dto.cardId) || !cardsById.TryGetValue(dto.cardId, out var so))
                { c.unmatched++; log.AppendLine($"[dc-mechanics] no match for cardId='{dto.cardId}'"); continue; }
                if (dto.slot == null)
                { c.skipped++; log.AppendLine($"[dc-mechanics] '{dto.cardId}' row without slot — skipped."); continue; }
                if (!seen.Add($"{dto.cardId}:{dto.slot}"))
                { c.skipped++; log.AppendLine($"[dc-mechanics] duplicate row for '{dto.cardId}' slot {dto.slot} — skipped."); continue; }
                var arr = so.mechanics;
                if (arr == null || dto.slot.Value < 0 || dto.slot.Value >= arr.Length)
                {
                    c.skipped++;
                    log.AppendLine($"[dc-mechanics] '{dto.cardId}' slot {dto.slot} out of range (have {arr?.Length ?? 0}) — structure changes stay in Unity.");
                    continue;
                }

                var m = arr[dto.slot.Value];
                if (dto.triggerKind != null) { m.trigger.kind = dto.triggerKind.Value; c.fieldsApplied++; }
                if (dto.triggerPeriod != null) { m.trigger.period = dto.triggerPeriod.Value; c.fieldsApplied++; }
                if (dto.payloadKind != null) { m.payload.kind = dto.payloadKind.Value; c.fieldsApplied++; }
                if (dto.magnitude != null) { m.payload.magnitude = dto.magnitude.Value; c.fieldsApplied++; }
                if (dto.tileRange != null) { m.payload.tileRange = dto.tileRange.Value; c.fieldsApplied++; }
                if (dto.duration != null) { m.payload.duration = dto.duration.Value; c.fieldsApplied++; }
                arr[dto.slot.Value] = m; // payload.projectile untouched by design

                if (m.payload.kind == DcPayloadKind.ProjectileToTarget && m.payload.projectile == null)
                    log.AppendLine($"[dc-mechanics] '{dto.cardId}' slot {dto.slot} is ProjectileToTarget but projectile is unassigned — assign it in Unity.");

                c.matched++;
                onApplied?.Invoke(so);
            }
        }
    }
}
