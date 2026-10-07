using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    /// <summary>
    /// demo-diet unit 0 — **전투 입구의 값.** 바깥(에디터 런처 · 테스트 하네스 · 훗날 App)이 채우고 드라이버가 읽는다.
    ///
    /// 전투가 바깥에 대해 아는 것은 이 값뿐이다 — 프로필·static·PlayerPrefs 를 모른다(demo-diet README 「공통 원칙」).
    /// 옛 `MatchEntry.Sources`(프로필 SO) · `TestModeContext`(플랜·프리셋) · `DevMapOverride`(맵 인덱스) ·
    /// `TournamentMatchReporter`(서버 맵 시드)가 각자 static 으로 나르던 것을 한 값으로 접었다.
    ///
    /// null 필드 = 「지정 없음」 = 드라이버 저작이 그대로 쓰인다. 에셋(`Defenders`·`Stones`)이 있으면 id 보다 우선한다.
    /// </summary>
    public sealed class MatchEntryInput
    {
        /// <summary>어느 문으로 들어왔나. `Squad` 면 개발용 덱 덮어쓰기(드라이버 `_cards`)가 이 입력의 덱에 양보한다.</summary>
        public MatchEntryKind Kind = MatchEntryKind.EditorDirect;

        /// <summary>편성 유닛 id(원시 — 못 찾는 id 도 기록에는 남는다, G23). 중복 제거·칸 상한은 `MatchEntry` 가 한다.</summary>
        public IReadOnlyList<string> UnitIds;

        /// <summary>장착 드림스톤 id(스탯 돌·코스트 돌 혼재 — 가르는 것은 빌더다, G9·G10).</summary>
        public IReadOnlyList<string> StoneIds;

        /// <summary>고른 덱(부착 카드 id). 검증(`DeckRules.Validate`)에 실패하면 부착 덱은 **비어 있다**(D3 — 기본 덱 폴백 없음).</summary>
        public IReadOnlyList<string> DeckCardIds;

        /// <summary>id 대신 에셋으로 직접(테스트 · 에디터). 있으면 `UnitIds` 는 풀지 않는다.</summary>
        public DefenderUnitData[] Defenders;

        /// <summary>id 대신 에셋으로 직접. 있으면 `StoneIds` 는 풀지 않는다.</summary>
        public DreamstoneData[] Stones;

        /// <summary>저작 플랜 강제(G13 — 옛 `TestModeContext.Plan`). 모드 플랜·엔트리 플랜을 이긴다.</summary>
        public WavePlanAsset PlanOverride;

        /// <summary>맵 풀 인덱스 강제(옛 `DevMapOverride.Index`). -1 = 끔 → 고정 노브 → 서버 시드 → 0번.</summary>
        public int MapIndexOverride = -1;

        /// <summary>서버가 준 맵 시드(옛 `TournamentMatchReporter.TournamentSeed`). `HasMapSeed` 가 거짓이면 무시.</summary>
        public bool HasMapSeed;
        public ulong MapSeed;
    }
}
