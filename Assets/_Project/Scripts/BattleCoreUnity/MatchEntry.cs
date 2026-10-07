using System.Collections.Generic;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    /// <summary>이 판이 어느 문으로 들어왔나. 규칙이 갈리는 축이 이것 하나다(G5 · G13).</summary>
    public enum MatchEntryKind : byte
    {
        /// <summary>입력 없는 진입(에디터 메뉴 · 테스트 하네스). 드라이버 저작 편성·덱이 쓰인다.</summary>
        EditorDirect = 0,
        /// <summary>바깥(somnia App)이 편성을 **값으로** 넘긴 판(G5·G7). 기본 편성의 덱은 이 입력에 양보한다.</summary>
        Squad = 1,
        // 2 는 비워 둔다 — 첫 판 안내 진입이었고 사용자 결정 ④(2026-09-25)로 제거됐다.
        /// <summary>테스트 모드 플랜(G13). 에디터 「Test this plan」· 테스트 하네스.</summary>
        TestMode = 3,
    }

    /// <summary>`MatchEntry.Resolve` 의 결과 — **값뿐이다**(판정·상태 0). 드라이버가 정의표 입력으로 옮긴다.</summary>
    public sealed class MatchEntryPlan
    {
        public MatchEntryKind Kind;
        public int Seed;
        /// <summary>null = 드라이버 저작 편성(에디터 직접 진입 폴백).</summary>
        public DefenderUnitData[] Defenders;
        /// <summary>null = 드라이버 저작 돌. 스탯 돌·코스트 돌이 섞여 있다 — 가르는 것은 빌더다(G9·G10).</summary>
        public DreamstoneData[] Stones;
        /// <summary>테스트 모드 플랜. 없으면 null.</summary>
        public WavePlanAsset ForcedPlan;
        /// <summary>반입 기록용 **원시 id**(못 찾는 id 도 id 로 — G23).</summary>
        public readonly List<string> UnitIds = new List<string>();
        public readonly List<string> StoneIds = new List<string>();

        /// <summary>바깥에서 편성을 받은 판인가. 참이면 기본 편성(`DefaultLoadout`)의 덱은 입력의 덱에 **양보**한다.</summary>
        public bool FromOutside => Kind != MatchEntryKind.EditorDirect;
    }

    // battle-core-rebuild unit 8b — **판에 들어가는 문의 해석.** 옛 `GameManager.Start`(G3·G5·G7·G9·G10·G13)와
    // 반입 기록(G22·G23)의 후계다. 여기서 하는 일은 «바깥이 넘긴 값을 정의표 입력 값으로 푼다» 하나뿐이고,
    // 판정도 상태도 없다.
    //
    // demo-diet unit 0 — 입력이 **값**(`MatchEntryInput`)이 됐다. 옛 코드는 프로필 SO 를 직접 읽고(「이번 세션에 읽은
    // 프로필인가」로 로비 판을 판별) 테스트 모드 static 을 1회 소비했다. 그 둘은 아웃게임의 사정이라 전투 밖으로 나갔고,
    // 「어느 문인가」는 입력이 **선언**한다(`MatchEntryInput.Kind`). 덱 스냅샷 직렬화(`TournamentDeckInfo`)도 제출 측 몫이라
    // 여기 없다 — 기록용 원시 id 만 `MatchEntryPlan` 에 남긴다.
    public static class MatchEntry
    {
        /// <summary>판에 서는 편성 칸 수(옛 `SquadDraw.FieldCount`). 입력이 이보다 길면 앞에서 자른다.</summary>
        public const int FieldCount = 7;

        /// <summary>
        /// 입력을 푼다. `selectionSeed` ≠ 0 이면 그 값(하네스·재현), 아니면 G3 — 고정 노브 ≠ 0 이면 그 값, 아니면 새 난수.
        /// `input` 이 null 이면 에디터 직접 진입(드라이버 저작 그대로).
        /// </summary>
        public static MatchEntryPlan Resolve(MatchEntryInput input, DefenderCatalog defenderCatalog,
                                             DreamstoneCatalog stoneCatalog, int fixedSeed, int selectionSeed)
        {
            var plan = new MatchEntryPlan
            {
                Seed = selectionSeed != 0 ? selectionSeed
                     : fixedSeed != 0 ? fixedSeed
                     : MatchSeed.GenerateRandom(),
            };

            if (input == null)
            {
                plan.Kind = MatchEntryKind.EditorDirect;
                return plan;
            }

            plan.Kind = input.Kind;
            plan.ForcedPlan = input.PlanOverride;

            // G7 — 받은 그대로(랜덤 채움 없음). 못 찾는 id 는 그 슬롯만 빠진다. 전부 못 찾으면 옛 게임은 뽑기로
            // 떨어졌는데(G8) 뽑기는 은퇴했다(계약 9) — 드라이버 저작으로 짓고 크게 알린다.
            var defenders = input.Defenders ?? ResolveUnits(input.UnitIds, defenderCatalog);
            if (defenders != null && defenders.Length == 0)
            {
                if (input.Kind == MatchEntryKind.Squad)
                    Debug.LogWarning("[MatchEntry] 편성이 유닛 0 으로 풀렸다 — 드라이버 저작 편성으로 짓는다(뽑기 폴백 은퇴 · 계약 9).");
                defenders = null;
            }
            plan.Defenders = defenders;

            // 돌은 편성 소속이다 — 유닛이 저작 폴백으로 떨어져도 입력의 돌은 반입한다(옛 `StartTestModeMatch` 주석).
            // id 가 없으면 **빈 배열**이지 null 이 아니다: 바깥에서 들어온 판은 드라이버 저작 돌을 상속하지 않는다.
            plan.Stones = input.Stones ?? ResolveStones(input.StoneIds, stoneCatalog);
            CopyIds(input, plan);
            return plan;
        }

        /// <summary>
        /// 옛 `SquadDraw.Resolve` — 빈 칸 제거 · 중복 제거 · 순서 유지 · `FieldCount` 상한. 난수·셔플 없음(저장 편성은
        /// 판마다 같아야 한다 — 사용자 결정). null 입력 = 빈 목록.
        /// </summary>
        public static List<string> ResolveUnitIds(IReadOnlyList<string> unitIds)
        {
            var result = new List<string>();
            if (unitIds == null) return result;
            var seen = new HashSet<string>();
            for (int i = 0; i < unitIds.Count; i++)
            {
                var id = unitIds[i];
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                result.Add(id);
                if (result.Count >= FieldCount) break;
            }
            return result;
        }

        /// <summary>id → 카탈로그. 입력이 없으면(null) null, 있으면 찾은 것만(빈 배열 가능).</summary>
        public static DefenderUnitData[] ResolveUnits(IReadOnlyList<string> unitIds, DefenderCatalog catalog)
        {
            if (unitIds == null || catalog == null) return null;
            var ids = ResolveUnitIds(unitIds);
            var units = new List<DefenderUnitData>(ids.Count);
            foreach (var id in ids)
            {
                var u = catalog.ById(id);
                if (u != null) units.Add(u);
            }
            return units.ToArray();
        }

        /// <summary>
        /// 옛 `ResolveEquippedStones` + `ResolveCostRateMultiplier` 의 입력 몫 — 장착 돌 **전부**(스탯·코스트).
        /// 가르는 것(스탯 → 규칙 줄 · 코스트 → 재생 배율)은 `CardDefinitionBuilder` 한 곳이다. 못 찾는 id 는 건너뛴다.
        /// </summary>
        public static DreamstoneData[] ResolveStones(IReadOnlyList<string> stoneIds, DreamstoneCatalog catalog)
        {
            var stones = new List<DreamstoneData>();
            if (stoneIds == null || catalog == null) return stones.ToArray();
            for (int i = 0; i < stoneIds.Count; i++)
            {
                var id = stoneIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                var stone = catalog.ById(id);
                if (stone != null) stones.Add(stone);
                else Debug.LogWarning($"[MatchEntry] 드림스톤 '{id}' 를 카탈로그에서 찾지 못했다 — 효과는 없고 기록에는 id 로 남는다(G23).");
            }
            return stones.ToArray();
        }

        // G22·G23 — 기록은 **원시 id** 다. 유닛은 빈 칸만 뺀 입력 id(없으면 직접 넘긴 에셋의 id), 돌은 못 찾는 id 도 id 로.
        private static void CopyIds(MatchEntryInput input, MatchEntryPlan plan)
        {
            if (input.UnitIds != null)
            {
                for (int i = 0; i < input.UnitIds.Count; i++)
                    if (!string.IsNullOrEmpty(input.UnitIds[i])) plan.UnitIds.Add(input.UnitIds[i]);
            }
            else if (input.Defenders != null)
            {
                for (int i = 0; i < input.Defenders.Length; i++)
                    if (input.Defenders[i] != null) plan.UnitIds.Add(input.Defenders[i].id);
            }
            if (input.StoneIds != null)
            {
                for (int i = 0; i < input.StoneIds.Count; i++)
                    if (!string.IsNullOrEmpty(input.StoneIds[i])) plan.StoneIds.Add(input.StoneIds[i]);
            }
            else if (input.Stones != null)
            {
                for (int i = 0; i < input.Stones.Length; i++)
                    if (input.Stones[i] != null) plan.StoneIds.Add(input.Stones[i].id);
            }
        }
    }
}
