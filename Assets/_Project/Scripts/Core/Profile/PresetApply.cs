using System.Collections.Generic;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Core
{
    // deck-info-preset-apply unit 0 — 히스토리 덱보기(요청)와 페이지 컨트롤러(생성)를 잇는
    // **한 슬롯 예약 채널** + 프리셋 이름 규칙 + "내가 쓸 수 있는 것만 남기는" 적용 필터.
    //
    // 같은 이름의 옛 클래스(WriteToProfile)와는 뜻이 다르다: 옛것은 확정 편성 하나를
    // 즉시 덮어썼고, 이것은 **새 프리셋 + 미저장 작업본**으로 전달한다(적용 = 생성 +
    // 작업본 세팅, 저장이 아니다). 프리셋 리스트에 실제로 쓰는 것은 여전히 페이지
    // 컨트롤러뿐이다(PlayerProfile 의 쓰기 규율).
    //
    // 왜 TournamentDeckInfo.Payload 를 그대로 싣지 않는가: Payload 는 서버 wire 계약
    // (v 버전 게이트 소유)이고 이 채널은 프로필 어휘(plain id 리스트)다. deckInfo 가
    // v2 로 바뀌어도 여기는 무변경이고, 번역 지점은 패널 하나 — 이미 Deserialize 를
    // 부르는 곳이다. Payload 를 실으면 페이지 컨트롤러가 Core.Api 를 새로 참조하게
    // 되고(현재 0건), 미래 소스는 wire 포맷 객체를 지어내야 예약할 수 있게 된다.
    public static class PresetApply
    {
        public enum Target { Squad, Dreamcatcher }

        // 의도적으로 **불활성** — 프로필에 그대로 꽂을 수 없는 plain 데이터다.
        // SquadPreset/DreamcatcherPreset 을 직접 실으면 id/name 반쯤 채운 persisted
        // 객체가 돌아다니고, 실수로 리스트에 직접 Add 하면 생성 경로를 우회한다.
        public class Request
        {
            public Target target;
            public string presetName;
            public List<string> unitIds;    // Squad 만
            public List<string> stoneIds;   // Squad 만
            public List<string> cardIds;    // Dreamcatcher 만
        }

        private static Request _pending;

        public static bool HasPending => _pending != null;

        // 기존 예약을 덮는다. 리스트는 **복제**해서 담는다 — 패널이 넘기는 것은
        // Payload 안의 살아 있는 리스트라, 참조를 공유하면 채널이 남의 객체 수명에
        // 묶인다(페이지 컨트롤러 CopySlots 의 "복제, 참조 공유 금지" 규율과 동일).
        public static void Stage(Request request)
        {
            if (request == null) { _pending = null; return; }
            _pending = new Request
            {
                target = request.target,
                presetName = request.presetName,
                unitIds = Copy(request.unitIds),
                stoneIds = Copy(request.stoneIds),
                cardIds = Copy(request.cardIds),
            };
        }

        // 예약이 있으면 **대상이 맞든 틀리든 지운다** — 맞으면 돌려주고 true. 틀렸을 때
        // 남겨두면 그 예약이 한참 뒤 엉뚱한 진입에서 되살아나 그때의 편집과 충돌한다.
        // 정상 경로는 예약 직후 그 페이지로 이동하므로 첫 진입이 곧 주인이다.
        public static bool TryConsume(Target target, out Request request)
        {
            var pending = _pending;
            _pending = null;
            if (pending == null || pending.target != target)
            {
                request = null;
                return false;
            }
            request = pending;
            return true;
        }

        public static void Clear() => _pending = null;

        // 이 에디터는 도메인 리로드 off(m_EnterPlayModeOptions: 1)라 static 이 Play
        // 세션을 넘어 살아남는다 — 예약만 하고 Play 를 끄면 다음 Play 의 첫 페이지
        // 진입에서 유령 프리셋이 생긴다. 세션 시작마다 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Clear();

        // ---- 이름 규칙 -------------------------------------------------------

        public static string DeckName(string ownerName)
            => string.IsNullOrWhiteSpace(ownerName) ? "불러온 덱" : ownerName.Trim() + "의 덱";

        // id 가 키라 이름 중복 자체는 무해하지만, 드롭다운에 같은 이름 셋이 쌓이면 고를
        // 수 없다. 기존 목록에 없으면 그대로, 있으면 " 2" 부터 빈 번호를 찾는다.
        public static string UniqueName(IReadOnlyList<string> existingNames, string desired)
        {
            if (string.IsNullOrWhiteSpace(desired)) desired = DeckName(null);
            if (!ContainsName(existingNames, desired)) return desired;
            for (int n = 2; ; n++)
            {
                string candidate = desired + " " + n;
                if (!ContainsName(existingNames, candidate)) return candidate;
            }
        }

        private static bool ContainsName(IReadOnlyList<string> names, string candidate)
        {
            if (names == null) return false;
            for (int i = 0; i < names.Count; i++)
                if (names[i] == candidate) return true;
            return false;
        }

        // ---- 적용 필터 -------------------------------------------------------
        //
        // 덱보기의 표시 계약과 **의도적으로 반대**다. 저쪽(DeckInfoDisplay)은 미해석
        // id 를 남긴다 — 남의 덱을 있는 그대로 보는 화면이라 조용히 사라지면 안 된다.
        // 이쪽은 내가 실제로 반입할 편성을 만드는 것이라 버린다 — 유령 id 가 슬롯에
        // 남으면 화면엔 빈칸인데 채울 수 없고, 저장하면 FilledCount 는 7 인데 스폰은
        // 6 이 된다. 판정 기준은 각 페이지에서 손으로 만들 수 있는 편성과 같다.
        //
        // 카탈로그가 null 이면 전량 제외(dropped = 입력 수) — 미배선을 조용한 빈
        // 프리셋으로 위장하지 않는다. 픽업이 dropped 를 안내로 띄운다.

        // 해석 가능 + 중복 제거(첫 등장 유지 — 페이지 ToggleUnit 이 같은 유닛의 두 번째
        // 편성을 막는다) + SlotCount 캡.
        public static List<string> FilterUnits(IReadOnlyList<string> ids, DefenderCatalog catalog, out int dropped)
        {
            var kept = new List<string>();
            var seen = new HashSet<string>();
            int considered = 0;
            if (ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    var id = ids[i];
                    if (string.IsNullOrWhiteSpace(id)) continue;   // 빈 슬롯은 항목이 아니다
                    considered++;
                    if (kept.Count >= SquadPreset.SlotCount) continue;
                    if (catalog == null || catalog.ById(id) == null) continue;
                    if (!seen.Add(id)) continue;
                    kept.Add(id);
                }
            }
            dropped = considered - kept.Count;
            return kept;
        }

        // 해석 가능 + StoneSlotCount 캡. **중복은 유지한다** — 같은 유니크 스톤 4개가
        // 설계상 허용이다(SquadPreset.SetStoneSlot 의 규약).
        public static List<string> FilterStones(IReadOnlyList<string> ids, DreamstoneCatalog catalog, out int dropped)
        {
            var kept = new List<string>();
            int considered = 0;
            if (ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    var id = ids[i];
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    considered++;
                    if (kept.Count >= SquadPreset.StoneSlotCount) continue;
                    if (catalog == null || catalog.ById(id) == null) continue;
                    kept.Add(id);
                }
            }
            dropped = considered - kept.Count;
            return kept;
        }

        // 해석 가능 + 숨김(visible 0) 제외 + 중복 제거 + 덱 상한 + 타입별 상한.
        // 판정 기준을 페이지의 CanAdd 와 일치시켜, 적용 결과가 그 페이지에서 손으로
        // 만들 수 있는 덱과 같아지게 한다. 숨김 카드는 어차피 로그인 prune(DeckPrune)이
        // 떼어내므로 여기서 넣어봐야 다음 로그인에 사라진다.
        //
        // gift-phase-removal unit 0 — Subconscious 제외가 여기 있었다(선물 전용 카드였다).
        // 림의 선물이 폐지되면서 무의식 카드는 일반 덱 카드가 됐고, 그 한 줄은 페이지의
        // BuildPool 과 **짝**이라 반드시 같이 움직인다 — 한쪽만 풀면 프리셋으로만 넣을 수
        // 있거나 그 반대인 카드가 생긴다.
        public static List<string> FilterCards(IReadOnlyList<string> ids, DreamcatcherCardCatalog catalog, out int dropped)
        {
            var kept = new List<string>();
            var seen = new HashSet<string>();
            int considered = 0;
            int deckSize = DeckRules.EffectiveDeckSize(catalog);
            if (ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    var id = ids[i];
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    considered++;
                    if (kept.Count >= deckSize) continue;
                    var card = catalog != null ? catalog.ById(id) : null;
                    if (card == null) continue;
                    if (card.visible == 0) continue;
                    if (seen.Contains(id)) continue;
                    int typeMax = DeckRules.EffectiveMax(catalog, card.type);
                    if (typeMax >= 0 && DeckRules.TypeCount(kept, catalog, card.type) >= typeMax) continue;
                    seen.Add(id);
                    kept.Add(id);
                }
            }
            dropped = considered - kept.Count;
            return kept;
        }

        private static List<string> Copy(List<string> src)
            => src != null ? new List<string>(src) : null;
    }
}
