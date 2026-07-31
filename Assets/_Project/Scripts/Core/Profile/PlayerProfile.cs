using System;
using System.Collections.Generic;

namespace Wassup.Core
{
    // outgame-scene-and-flow Unit 0 — persisted player profile, serialized to
    // JSON by ProfileStore. Unit/card references use stable string ids
    // (DefenderUnitData.id), never asset GUIDs or list indices.
    [Serializable]
    public class PlayerProfile
    {
        public int schemaVersion = 1;

        // page-local-presets unit 0 — 프리셋 상한. 목록 팝업 스크롤 예산에서 나온 UI 용량
        // 한계라 const 다 — SquadPreset.SlotCount 와 같은 급. 재화/과금 knob("프리셋 슬롯
        // 확장 판매")이 되면 SO 로 이관한다.
        public const int MaxPresets = 30;

        // first-session-tutorial unit 0 — independent, versioned onboarding
        // progress. Additive JSON fields intentionally default to 0 for profiles
        // written before the tutorial existed; schemaVersion stays unchanged.
        public int firstBattleTutorialVersion;
        // unit 17 — 이 필드는 이제 **드래그 부착 안내**(항아리로 연 손패)만 뜻한다. 이름을
        // 좁히지 않는 이유는 JSON 호환이다 — 바꾸면 기존 프로필의 진행이 0 으로 읽혀
        // 튜토리얼이 되살아난다. 의미는 TutorialProgress 의 API 이름이 나른다.
        public int awakeningHintVersion;
        // unit 17 — 탭 즉발 부착 안내(선택으로 연 손패). 위와 **독립**이다: 두 안내는 서로
        // 다른 조작을 가르치므로 하나를 봤다고 다른 하나가 필요 없어지지 않는다.
        public int awakeningTapAttachHintVersion;
        // first-session-tutorial unit 6 — gift-phase walkthrough, shown once on
        // the first battle where the gift presentation is visible (core done).
        public int giftTutorialVersion;
        // outgame-tutorial unit 0 — blocking lobby onboarding. A runs on the first
        // lobby reveal, B once the in-game core tutorial is done and the player is
        // back in the lobby. Same additive-field rule as the three above.
        public int lobbyIntroVersion;
        public int lobbyLoadoutHintVersion;
        // outgame-tutorial unit 6 — chapter C: the first time the player closes a
        // panel and the lobby comes back, it asks them to actually drag a lobby
        // character. Additive like the rest — 0 means pending.
        public int lobbyKeyringHintVersion;

        // Units are not profile-owned — all catalog units are always available
        // (the squad page lists the catalog directly). No ownedUnitIds by design.

        // page-local-presets unit 0 — 이 두 리스트가 곧 페이지별 프리셋 저장소다(각 ≤
        // MaxPresets). **JSON 필드명은 의도적으로 옛 이름을 유지한다** — `squads`/
        // `dreamcatcherDecks` 를 `squadPresets`/`dreamcatcherPresets` 로 바꾸면 기존
        // 프로필을 읽지 못해 마이그레이션이 필요해지고, 얻는 것은 정돈뿐이다. 타입명만
        // 프리셋 어휘로 바꿨다. 엔트리가 1개뿐이던 시절엔 "현재 편성" 하나를 뜻했다.
        public List<SquadPreset> squads = new List<SquadPreset>();
        public List<DreamcatcherPreset> dreamcatcherDecks = new List<DreamcatcherPreset>();

        // 확정(committed) 프리셋 = 전투에 반입되는 것. 페이지에서 [선택]으로만 바뀐다.
        // 필드명은 위와 같은 이유로 유지(selectedSquadId/selectedDeckId).
        // null/empty = 확정 없음 → BattleScene 은 기존 draft 폴백을 쓴다.
        public string selectedSquadId;
        public string selectedDeckId;

        // page-local-presets unit 0 — 확정 프리셋을 돌려준다(없으면 null).
        //
        // **반환값은 리스트 안의 살아있는 객체다** — 투영/복제가 아니다. 따라서 이걸 통해
        // 쓰면 프로필이 실제로 바뀐다. 그걸 막는 타입 장치는 두지 않았다(읽기전용 래퍼는
        // 구현체 1개짜리 추상이라 제약 8 위반). 규율로 지킨다:
        //   **프리셋 리스트에 쓰는 것은 페이지 컨트롤러의 저장 경로와 ProfileStore 시드뿐이다.**
        // 페이지 편집은 분리된 작업본에서 하고 [저장]에서만 여기에 반영한다.
        public SquadPreset CommittedSquad()
        {
            if (string.IsNullOrEmpty(selectedSquadId) || squads == null) return null;
            for (int i = 0; i < squads.Count; i++)
                if (squads[i] != null && squads[i].id == selectedSquadId) return squads[i];
            return null;
        }

        // dreamcatcher-deck-builder Unit 0 — 확정 덱 프리셋. CommittedSquad 와 같은 규약.
        public DreamcatcherPreset CommittedDeck()
        {
            if (string.IsNullOrEmpty(selectedDeckId) || dreamcatcherDecks == null) return null;
            for (int i = 0; i < dreamcatcherDecks.Count; i++)
                if (dreamcatcherDecks[i] != null && dreamcatcherDecks[i].id == selectedDeckId) return dreamcatcherDecks[i];
            return null;
        }

        // page-local-presets unit 0 — 프리셋 리스트의 불변식을 한곳에서 보장한다.
        // **호출 지점은 3곳뿐: 로드(ProfileStore) · 프리셋 생성 · 프리셋 삭제.**
        // 내용 편집(유닛/스톤/카드 토글)에서는 부르지 않는다 — 작업본을 건드릴 뿐이고
        // 매 탭마다 리스트를 훑을 이유가 없다.
        public void NormalizePresets()
        {
            if (squads == null) squads = new List<SquadPreset>();
            if (dreamcatcherDecks == null) dreamcatcherDecks = new List<DreamcatcherPreset>();

            squads.RemoveAll(p => p == null);
            dreamcatcherDecks.RemoveAll(p => p == null);

            for (int i = 0; i < squads.Count; i++) squads[i].NormalizeSlots();
            for (int i = 0; i < dreamcatcherDecks.Count; i++) dreamcatcherDecks[i].NormalizeCards();

            if (squads.Count > MaxPresets) squads.RemoveRange(MaxPresets, squads.Count - MaxPresets);
            if (dreamcatcherDecks.Count > MaxPresets)
                dreamcatcherDecks.RemoveRange(MaxPresets, dreamcatcherDecks.Count - MaxPresets);

            // 확정 포인터가 실존 엔트리를 가리키지 않으면 첫 엔트리로 교정한다. 리스트가
            // 비어 있으면 빈 문자열 — "확정 없음"이 유효한 상태다(신규 프로필 시드 직전).
            if (CommittedSquad() == null)
                selectedSquadId = squads.Count > 0 ? squads[0].id : "";
            if (CommittedDeck() == null)
                selectedDeckId = dreamcatcherDecks.Count > 0 ? dreamcatcherDecks[0].id : "";
        }
    }

    // squad-loadout Unit 0 — a 7-slot squad. Slots hold DefenderUnitData.id;
    // empty slot = "" (kept non-null for stable JSON).
    //
    // page-local-presets unit 0 — 구 `SquadSave` 에서 개명. 이 타입이 곧 **스쿼드 페이지의
    // 프리셋 1개**다: 유닛 7 + 스톤 4 를 한 객체가 들고 [저장] 한 번이 둘을 함께 기록한다.
    // 스톤을 별도 `DreamstonePreset` 리스트로 떼지 않은 이유는, 1:1 로 묶인 병렬 리스트가
    // 짝 어긋남(고아 스톤셋/짝 없는 스쿼드)이라는 없어도 될 실패 모드를 만들고 그걸
    // 수리하는 코드를 요구하기 때문이다.
    [Serializable]
    public class SquadPreset
    {
        public const int SlotCount = 7;

        // dreamstone-loadout Unit 1 — 4 stone slots, 프리셋 소속(계정 소속이 아니다).
        // Holds DreamstoneData.id (Wassup.Data), empty slot = "". Duplicate ids across
        // slots are allowed by design (e.g. 4x the same Unique attack stone).
        public const int StoneSlotCount = 4;

        public string id;
        public string name = "스쿼드 1";
        public List<string> unitIds = new List<string>();
        public List<string> stoneIds = new List<string>();

        public bool IsEmpty()
        {
            if (unitIds == null) return true;
            for (int i = 0; i < unitIds.Count; i++)
                if (!string.IsNullOrEmpty(unitIds[i])) return false;
            return true;
        }

        public int FilledCount()
        {
            if (unitIds == null) return 0;
            int n = 0;
            for (int i = 0; i < unitIds.Count; i++)
                if (!string.IsNullOrEmpty(unitIds[i])) n++;
            return n;
        }

        // Pad/trim to exactly SlotCount with "" for empty slots.
        public void NormalizeSlots()
        {
            if (unitIds == null) unitIds = new List<string>();
            for (int i = 0; i < unitIds.Count; i++)
                if (unitIds[i] == null) unitIds[i] = "";
            while (unitIds.Count < SlotCount) unitIds.Add("");
            if (unitIds.Count > SlotCount) unitIds.RemoveRange(SlotCount, unitIds.Count - SlotCount);

            // dreamstone-loadout Unit 1 — same pad/trim, independent slot count.
            if (stoneIds == null) stoneIds = new List<string>();
            for (int i = 0; i < stoneIds.Count; i++)
                if (stoneIds[i] == null) stoneIds[i] = "";
            while (stoneIds.Count < StoneSlotCount) stoneIds.Add("");
            if (stoneIds.Count > StoneSlotCount) stoneIds.RemoveRange(StoneSlotCount, stoneIds.Count - StoneSlotCount);
        }

        // dreamstone-loadout Unit 1 — assign (or clear, id="") a single stone slot by
        // index. No "first empty slot" search: the caller always targets an explicit
        // slot. Duplicate ids are not rejected — dedup would break the "4x Unique"
        // scenario the stat-cap contract relies on.
        public bool SetStoneSlot(int index, string id)
        {
            if (stoneIds == null) stoneIds = new List<string>();
            if (index < 0 || index >= StoneSlotCount) return false;
            NormalizeSlots();
            stoneIds[index] = id ?? "";
            return true;
        }
    }

    // dreamcatcher-deck-builder Unit 0 — a saved dreamcatcher deck. cardIds holds
    // DreamcatcherCard.id; deck-rule validity (exactly N, per-type caps) is checked
    // by DeckRules at the start gate, not enforced here.
    //
    // page-local-presets unit 0 — 구 `DeckSave` 에서 개명. 드림캐쳐 페이지의 프리셋 1개.
    // 스쿼드와 달리 고정 칸이 없어 가변 길이다(빈 프리셋 = 빈 리스트).
    [Serializable]
    public class DreamcatcherPreset
    {
        public string id;
        public string name = "덱 1";
        public List<string> cardIds = new List<string>();

        public int Count() => cardIds != null ? cardIds.Count : 0;

        // 가변 길이라 패딩은 없다. null 리스트/null 항목만 걷어낸다 —
        // JsonUtility 가 부분 JSON 에서 컬렉션 항목을 null 로 남길 수 있다.
        public void NormalizeCards()
        {
            if (cardIds == null) { cardIds = new List<string>(); return; }
            cardIds.RemoveAll(string.IsNullOrEmpty);
        }
    }
}
