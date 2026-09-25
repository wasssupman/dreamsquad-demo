using System.Collections.Generic;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    /// <summary>이 판이 어느 문으로 들어왔나. 규칙이 갈리는 축이 이것 하나다(G5 · G11 · G13).</summary>
    public enum MatchEntryKind : byte
    {
        /// <summary>로비를 거치지 않은 진입(에디터 메뉴 · 테스트 하네스). 드라이버 저작 편성·덱이 쓰인다.</summary>
        EditorDirect = 0,
        /// <summary>로비에서 저장 편성으로(G5·G7).</summary>
        Squad = 1,
        /// <summary>로비에서 — 계정 첫 판 온보딩(G11·G12).</summary>
        Onboarding = 2,
        /// <summary>테스트 모드 플랜(G13). 로비 패널·에디터 「Test this plan」.</summary>
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
        /// <summary>① 테스트 모드 플랜 · ② 온보딩 플랜. 없으면 null.</summary>
        public WavePlanAsset ForcedPlan;
        /// <summary>온보딩 첫 손패(덱에 실제로 든 것만 고정된다).</summary>
        public DreamcatcherCard[] FirstHand;
        public bool BonusPullSuppressed;
        /// <summary>반입 기록용 **원시 id**(못 찾는 id 도 id 로 — G23).</summary>
        public readonly List<string> UnitIds = new List<string>();
        public readonly List<string> StoneIds = new List<string>();

        /// <summary>로비 문으로 들어왔나. 참이면 개발용 덱 덮어쓰기는 프로필 덱에 **양보**한다.</summary>
        public bool FromLobby => Kind != MatchEntryKind.EditorDirect;
    }

    // battle-core-rebuild unit 8b — **판에 들어가는 문의 해석.** 옛 `GameManager.Start`(G3·G5·G7·G9·G10·G11·G12·G13)와
    // 반입 기록(G22·G23)의 후계다. 여기서 하는 일은 «로비가 남긴 것을 정의표 입력 값으로 푼다» 하나뿐이고,
    // 판정도 상태도 없다 — 테스트 모드 문맥을 **한 번 소비**하는 것(G13)이 유일한 부수 효과다.
    //
    // ⚠ **「로비에서 왔나」의 판별 = 이번 세션에 읽은 프로필인가**(`PlayerProfileSO.IsLoadedThisSession`). 옛 코드가
    // 온보딩·「한 판 해봤다」 기록에 이미 쓰던 가드다. 옛 편성 반입은 이 가드 없이 SO 의 메모리 사본을 읽었는데,
    // 그러면 에디터에서 새 씬을 직접 열어도 개발자의 저장 편성이 끼어든다 — 그래서 에디터 직접 진입은 드라이버 저작이다
    // (8b 「구현」 G5 행: 「드라이버 `_defenders` 는 에디터 직접 진입 폴백으로만」).
    public static class MatchEntry
    {
        public struct Sources
        {
            public PlayerProfileSO Profile;
            public DefenderCatalog DefenderCatalog;
            public DreamstoneCatalog StoneCatalog;
            public WavePlanAsset OnboardingPlan;
            public FirstRunTutorialConfig OnboardingConfig;
            /// <summary>G3 의 고정 노브. 0 = 판마다 새 난수.</summary>
            public int FixedSeed;
        }

        /// <summary>테스트 모드 문맥(G13) — 값으로 넘긴다. `Consume` 이 `TestModeContext` 에서 채운다.</summary>
        public struct TestCarry
        {
            public bool Active;
            public WavePlanAsset Plan;
            public DefenderUnitData[] Preset;
        }

        /// <summary>테스트 모드 문맥을 읽고 **지운다**(1회 소비 — 옛 `StartTestModeMatch` 첫 줄).</summary>
        public static TestCarry ConsumeTestMode()
        {
            var carry = new TestCarry
            {
                Active = TestModeContext.Active,
                Plan = TestModeContext.Plan,
                Preset = TestModeContext.DefenderPreset,
            };
            TestModeContext.Clear();
            return carry;
        }

        /// <summary>
        /// 입력을 푼다. `selectionSeed` ≠ 0 이면 그 값(하네스·재현), 아니면 G3 — 고정 노브 ≠ 0 이면 그 값, 아니면 새 난수.
        /// </summary>
        public static MatchEntryPlan Resolve(in Sources src, in TestCarry test, int selectionSeed)
        {
            var plan = new MatchEntryPlan
            {
                Seed = selectionSeed != 0 ? selectionSeed
                     : src.FixedSeed != 0 ? src.FixedSeed
                     : MatchSeed.GenerateRandom(),
            };

            bool lobby = src.Profile != null && src.Profile.IsLoadedThisSession && src.Profile.profile != null;
            var squad = lobby ? src.Profile.profile.CommittedSquad() : null;

            // ① 테스트 모드가 먼저다(G5 — 테스트 모드 > 저장 편성). 편성 = 저장 편성, 비면 프리셋(G13).
            if (test.Active)
            {
                plan.Kind = MatchEntryKind.TestMode;
                plan.ForcedPlan = test.Plan;
                var units = ResolveSquadUnits(squad, src.DefenderCatalog);
                plan.Defenders = units != null && units.Length > 0 ? units
                               : test.Preset != null && test.Preset.Length > 0 ? test.Preset
                               : null;
                // 돌은 편성 소속이라 유닛이 프리셋으로 떨어져도 저장 편성의 돌을 반입한다(옛 `StartTestModeMatch` 주석).
                plan.Stones = ResolveStones(squad, src.StoneCatalog);
                CopyIds(squad, plan);
                return plan;
            }

            if (squad == null || squad.IsEmpty())
            {
                plan.Kind = MatchEntryKind.EditorDirect;
                return plan;
            }

            // G7 — 저장된 그대로(랜덤 채움 없음). 못 찾는 id 는 그 슬롯만 빠진다. 전부 못 찾으면 옛 게임은 뽑기로
            // 떨어졌는데(G8) 뽑기는 은퇴했다(계약 9) — 드라이버 저작으로 짓고 크게 알린다.
            var resolved = ResolveSquadUnits(squad, src.DefenderCatalog);
            if (resolved == null || resolved.Length == 0)
            {
                Debug.LogWarning("[MatchEntry] 저장 편성이 유닛 0 으로 풀렸다 — 드라이버 저작 편성으로 짓는다(뽑기 폴백 은퇴 · 계약 9).");
                resolved = null;
            }
            plan.Defenders = resolved;
            plan.Stones = ResolveStones(squad, src.StoneCatalog);
            CopyIds(squad, plan);

            // G11·G12 — 온보딩 판 = 저작 웨이브 + 첫 손패 고정 + 보너스 억제. 억제는 **조건 밖에서 무조건** 값이 정해진다
            // (정의표가 판마다 새로 지어지므로 이 대입이 곧 그 규칙이다).
            bool onboarding = FirstRunTutorialConfig.ShouldRun(src.Profile.profile);
            plan.BonusPullSuppressed = onboarding;
            if (onboarding)
            {
                plan.Kind = MatchEntryKind.Onboarding;
                plan.ForcedPlan = src.OnboardingPlan;
                plan.FirstHand = src.OnboardingConfig != null ? src.OnboardingConfig.firstHandCards : null;
            }
            else plan.Kind = MatchEntryKind.Squad;
            return plan;
        }

        /// <summary>옛 `ResolveSquadDefenders` — `SquadDraw.Resolve` → 카탈로그. 편성 없음 = null.</summary>
        public static DefenderUnitData[] ResolveSquadUnits(SquadPreset squad, DefenderCatalog catalog)
        {
            if (squad == null || squad.IsEmpty() || catalog == null) return null;
            var ids = SquadDraw.Resolve(squad.unitIds);
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
        public static DreamstoneData[] ResolveStones(SquadPreset squad, DreamstoneCatalog catalog)
        {
            var stones = new List<DreamstoneData>();
            if (squad == null || squad.stoneIds == null || catalog == null) return stones.ToArray();
            foreach (var id in squad.stoneIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                var stone = catalog.ById(id);
                if (stone != null) stones.Add(stone);
                else Debug.LogWarning($"[MatchEntry] 드림스톤 '{id}' 를 카탈로그에서 찾지 못했다 — 효과는 없고 기록에는 id 로 남는다(G23).");
            }
            return stones.ToArray();
        }

        // G22·G23 — 기록은 **원시 id** 다. 유닛은 옛 `LogSquadCarryIn`(빈 칸만 뺀 `squad.unitIds`), 돌은 옛
        // `LogDreamstoneCarryIn`(못 찾는 id 도 id 로).
        private static void CopyIds(SquadPreset squad, MatchEntryPlan plan)
        {
            if (squad == null) return;
            if (squad.unitIds != null)
                foreach (var id in squad.unitIds) if (!string.IsNullOrEmpty(id)) plan.UnitIds.Add(id);
            if (squad.stoneIds != null)
                foreach (var id in squad.stoneIds) if (!string.IsNullOrEmpty(id)) plan.StoneIds.Add(id);
        }

        /// <summary>
        /// 덱 스냅샷 문자열(`TournamentDeckInfo.Serialize` 직접 — 로거를 새 씬에 들이지 않는다, 결정 ⑷).
        /// 카드는 **고른 덱만**(판마다 굴린 액티브 제외 — 옛 `BattleLogger.DeckInfoJson` 의 `baseDeckCardIds`).
        /// </summary>
        public static string DeckInfoJson(MatchEntryPlan plan, IEnumerable<string> baseCardIds)
            => Wassup.Core.Api.TournamentDeckInfo.Serialize(
                   plan != null ? plan.UnitIds : null, plan != null ? plan.StoneIds : null, baseCardIds);
    }
}
