using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Skills;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7b — **카드 한 장이 숙주에 무엇을 싣는가**(← 옛 `BattleBridge.ApplyDreamcatcherCardToUnit` ·
    // `ApplyDreamcatcherCard` · `ApplyBountyMark` 의 판정 몫 + `DreamcatcherAttachEval.WouldApply`).
    //
    // 둘로 나뉜다:
    //   ① `Plan` — **판정**. 이 카드의 규칙 줄 중 이 숙주에서 도는 것을 고른다(host 종속은 `Applicability` 한 곳).
    //      부착 커밋과 조준 preflight 가 **같은 함수**를 부른다. 쓰기가 없다.
    //   ② `FireOnAttach` — 붙인 뒤 **부착 즉시** 발동할 것을 `Immediate` seam 에 줄 세운다(트리거 없음 3장 +
    //      Squad 카드의 판 위 전원 전개). 드레인은 이 커맨드의 콜스택(`CommandPhase`)이 한다.
    //
    // 손패 담당자(`HandDeck`)는 ① → 등록부 부착 → ② 순서로 부르기만 한다 — 효과를 모른다.
    public static class CardBindings
    {
        /// <summary>
        /// 그 카드가 그 숙주에 붙으면 무엇이 붙나. 반환 = 거절 사유(`None` = 붙는다 — 한 줄이라도 돈다).
        /// 옛 규약: 이중 상태만 **카드 전체** 거절이고, 나머지 host 사유는 그 줄만 건너뛴다(전량 무효일 때만 카드 거절).
        /// </summary>
        public static RejectReason Plan(MatchDefinition def, int cardIndex, Unit host,
                                        List<int> rows, List<int> squadRows, List<Combat.AttackModDef> mods)
        {
            rows?.Clear(); squadRows?.Clear(); mods?.Clear();
            if (cardIndex < 0 || cardIndex >= def.Cards.Length) return RejectReason.CardNotInHand;
            ref var card = ref def.Cards[cardIndex];
            if (card.Kind != CardKind.Attach) return RejectReason.WrongCardKind;
            if (host == null) return RejectReason.NoSuchEntity;

            // 표식 — 적을 겨누는 유일한 카드(D14 — 부착 상한 밖). 적당 하나(이중 배율 방지 — 옛 preflight).
            if (card.TargetsEnemies)
            {
                if (host.Kind != UnitKind.Enemy || host.Dead) return RejectReason.NotAnEnemy;
                if (IsMarked(host)) return RejectReason.DuplicateState;
                var marks = card.Bindings;
                for (int i = 0; marks != null && i < marks.Length; i++)
                    if (InTable(def, marks[i]) && def.Bindings[marks[i]].Payload == TriggerPayload.BountyMark)
                        rows?.Add(marks[i]);
                return rows != null && rows.Count > 0 ? RejectReason.None : RejectReason.NoContribution;
            }

            // 방어유닛 부착(Unit · Squad 공통). 옛 계약 2 — 슬롯은 방어유닛에만 산다(순찰 소환물 제외).
            if (host.Kind != UnitKind.Defender || host.Dead) return RejectReason.NotADefender;
            if (host.DefIndex < 0 || host.DefIndex >= def.Units.Length) return RejectReason.InvalidUnit;
            // Squad 에서 부착 제한은 **수혜 축과 별개인 수명 앵커 제한**이다(옛 unit 10).
            if (!Applicability.MeetsRequirement(in card.Requirement, in def.Units[host.DefIndex]))
                return RejectReason.AttachRequirementUnmet;

            var profile = HostProfile.Of(host, def);
            var list = card.Bindings;
            // 이중 상태는 어떤 쓰기보다 먼저 **카드 전체**를 거절한다(부분 적용 금지).
            for (int i = 0; list != null && i < list.Length; i++)
                if (InTable(def, list[i]) && Applicability.Evaluate(in def.Bindings[list[i]], in profile) == RejectReason.DuplicateState)
                    return RejectReason.DuplicateState;

            var first = RejectReason.None;
            for (int i = 0; list != null && i < list.Length; i++)
            {
                if (!InTable(def, list[i])) continue;
                var r = Applicability.Evaluate(in def.Bindings[list[i]], in profile);
                if (r == RejectReason.None) rows?.Add(list[i]);
                else if (first == RejectReason.None) first = r;
            }
            var squad = card.SquadBindings;
            for (int i = 0; squad != null && i < squad.Length; i++)
                if (InTable(def, squad[i])) squadRows?.Add(squad[i]);
            var am = card.AttackMods;
            for (int i = 0; am != null && i < am.Length; i++)
            {
                var r = Applicability.EvaluateAttackMod(in am[i], in profile);
                if (r == RejectReason.None) mods?.Add(am[i]);
                else if (first == RejectReason.None) first = r;
            }

            // 「인수인계」 선언은 판 위에서 일어나는 일이 아니다(퇴근 회수 규칙) — 규칙 0 줄이어도 카드는 일한다.
            bool contributes = (rows != null && rows.Count > 0) || (squadRows != null && squadRows.Count > 0)
                               || (mods != null && mods.Count > 0) || card.DeclaresRetireRecall;
            if (contributes) return RejectReason.None;
            return first != RejectReason.None ? first : RejectReason.NoContribution;
        }

        /// <summary>
        /// 붙인 뒤 부착 즉시 발동할 것을 `Immediate` seam 에 줄 세운다. ⚠ 순서가 계약이다: 트리거 없음 줄(부착 순) →
        /// Squad 줄마다 판 위 유닛(`SimEntityId` 오름차순 = 월드 목록 순). 배치 중인 유닛은 건너뛴다 — 활성화되면
        /// 그 줄이 배치 사건으로 상속시키므로, 여기서도 걸면 **두 번** 붙는다(칸이 규칙마다 다르다).
        /// </summary>
        public static void FireOnAttach(TriggerDispatcher dispatcher, BattleWorld world, MatchDefinition def,
                                        CardAttachment att, Unit host)
        {
            if (dispatcher == null || att == null) return;
            for (int i = 0; i < att.Bindings.Count; i++)
            {
                var b = att.Bindings[i];
                if (b.Def.Trigger != TriggerKind.None) continue;
                TriggerEvent e;
                if (b.Def.Payload == TriggerPayload.BountyMark)
                {
                    // 표식은 **플레이어가 건다** — 시전자가 없다(옛 `Caster = Entity.Null`). 대상 = 그 적.
                    e = new TriggerEvent
                    {
                        Seam = Seam.Immediate, Kind = TriggerKind.None,
                        Subject = SimEntityId.Match, SubjectFaction = Faction.DefenderUnit,
                        Target = host.Id, TargetHp = host.Health, TargetMaxHp = host.MaxHealth,
                        HasSite = true, Site = host.Position, SiteBody = host.HitRadius,
                    };
                }
                else e = TriggerDispatcher.SubjectOf(host, Seam.Immediate, TriggerKind.None);
                dispatcher.RaiseFor(b, in e);
            }
            var units = world.Units;
            for (int s = 0; s < att.SquadBindings.Count; s++)
            {
                var b = att.SquadBindings[s];
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u.Kind != UnitKind.Defender || u.Dead || u.Deploying) continue;
                    if (!TriggerDispatcher.SubjectPasses(in b.Def, u, def)) continue;
                    var e = TriggerDispatcher.SubjectOf(u, Seam.Immediate, TriggerKind.OnPlace);
                    dispatcher.RaiseFor(b, in e);
                }
            }
        }

        /// <summary>액티브 시전의 사건 — 시전자 없음(판) · 칸 조준.</summary>
        public static TriggerEvent CastEvent(int2 cellA, int2 cellB, bool hasCellB)
            => new TriggerEvent
            {
                Seam = Seam.Immediate, Kind = TriggerKind.None,
                Subject = SimEntityId.Match, SubjectFaction = Faction.DefenderUnit,
                Target = SimEntityId.None,
                HasCellAim = true, CellA = cellA, CellB = cellB, HasCellB = hasCellB,
            };

        /// <summary>이 적에 표식이 이미 붙었나 — 카드 표식 규칙이 붙어 있는가(옛 `_bountyMarked` 등록부의 후계).</summary>
        public static bool IsMarked(Unit enemy)
        {
            if (enemy == null) return false;
            var list = enemy.Bindings;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Def.Payload == TriggerPayload.BountyMark && list[i].Def.Origin == BindingOrigin.Card) return true;
            return false;
        }

        private static bool InTable(MatchDefinition def, int row) => row >= 0 && row < def.Bindings.Length;
    }
}
