using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Core
{
    // dreamcatcher-awakening-hand unit 4 — match controller for the awakening
    // currency + CR-style cycling hand. Replaces the retired 3-choose-1 flow
    // (구 DreamcatcherController 는 dreamcatcher-bridge-partial-cleanup unit 1 에서 삭제).
    //
    // Owns: awakening gauge (Mono state), the 12-entry cycle deck (attach deck 10
    // + common Active cards from the per-match SkillLoadoutController roll), and
    // the entryId↔entity attach registry. Views (units 5~8) subscribe to
    // GaugeChanged/HandChanged and call the Commit* APIs at pending-commit time —
    // pending/cancel UX is entirely the view's job (spec contract 9).
    //
    // NO pause and NO slomo here: realtime is the contract (7); the slomo lease
    // belongs to the hand view (unit 6).
    public class DreamcatcherHandController : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private AwakeningConfig config;
        // Attach-deck resolve: validated saved deck via catalog only. 기본(fallback)
        // 덱은 제거됨 (사용자 결정 2026-07-15) — 저장 덱이 없으면 부착 덱은 비어 있다.
        [SerializeField] private PlayerProfileSO profileSO;
        [SerializeField] private DreamcatcherCardCatalog cardCatalog;
        // Active(common) wiring: the existing per-match skill roll stays the
        // source of truth (seed + logging untouched); each rolled SkillData is
        // translated to its wrapping Active card via this serialized list.
        [SerializeField] private SkillLoadoutController skillLoadout;
        [SerializeField] private DreamcatcherCard[] activeCards;

        public enum HandChangeReason { Reset, Used, Recovered }

        public event System.Action<int> GaugeChanged;
        // dreamcatcher-orb-dock unit 4 — 게이지가 상한이라 획득분이 소멸(넘침)할 때 손실량을
        // 알린다. 뷰가 오버플로우 경고 연출을 구동(손실 회피 신호). Mono 전용.
        public event System.Action<int> AwakeningOverflowed;
        // dreamcatcher-orb-dock unit 3 — 실제 적용된 획득량 + 사망 view-space 위치. 항아리 뷰가
        // 킬 위치에서 피규어를 날려보내는 흡수 비행을 구동한다. Mono 전용(bridge→controller→view).
        public event System.Action<int, Vector3, ISpineUnitVisualData> AwakeningGainedAt;
        public event System.Action<HandChangeReason> HandChanged;
        // unit-dreamcatcher-icons unit 0 — fires only when the attach registry
        // actually changes (attach / death recovery / placement reset).
        // HandChanged is a superset: Active use fires Used without an attach change.
        public event System.Action AttachmentsChanged;

        public int Gauge { get; private set; }
        public int GaugeMax => config != null ? config.gaugeMax : 100;
        public int HandSize => config != null ? config.handSize : 5;
        // dreamcatcher-orb-dock unit 1 — 항아리 독이 코스트 눈금·ready 임계를 데이터에서
        // 파생하기 위해 config 를 읽는다(뷰는 읽기 전용; DreamcatcherHandView.Config 미러).
        public AwakeningConfig Config => config;
        // subconscious-curse-expansion unit 3 — 표식 드롭 픽 반경(타일, SO 노브).
        public float EnemyPickRadiusTiles => config != null ? config.enemyPickRadiusTiles : 1.5f;

        private DreamcatcherCycleDeck _deck;
        // gift-phase-removal unit 1 — 저장 덱(선물을 뺀 "고른 덱"). LogDeck 이 토너먼트
        // 리포트의 baseIds 로 읽는 유일한 출처다.
        private List<DreamcatcherCard> _baseCards = new List<DreamcatcherCard>();
        private List<DreamcatcherCard> _lastComposedCards = new List<DreamcatcherCard>();
        // entryId → (host defender, revocation handle). handle 0 = 무회수(엔티티 부착
        // Unit 카드: 슬롯이 엔티티와 함께 소멸, revoke 대상 없음). handle>0 = host 사망 시
        // revoke(Squad hosted 버프 unit 9 · placement-aura 오라). placement-aura unit 2 —
        // 예전 -1 관례는 폐기(ApplyDreamcatcherCardToUnit 이 int 규약으로 0/>0 반환).
        // Reverse scan on death is O(attached).
        private readonly Dictionary<int, (Entity host, int handle)> _attachedTo =
            new Dictionary<int, (Entity, int)>();
        private readonly List<int> _recoverScratch = new List<int>();
        private readonly List<int> _attachReadScratch = new List<int>();

        private void OnEnable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
            if (bridge != null)
            {
                bridge.EnemyKilledAwakening += OnEnemyKilledAwakening;
                bridge.DefenderDied += OnDefenderDied;
                bridge.DefenderRetired += OnDefenderRetired; // 퇴근 — 회수만(각성 없음)
                bridge.EnemyGone += OnEnemyGone; // 살찌운 제물 — 표식 소멸(처치/유출) 회수
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            if (bridge != null)
            {
                bridge.EnemyKilledAwakening -= OnEnemyKilledAwakening;
                bridge.DefenderDied -= OnDefenderDied;
                bridge.DefenderRetired -= OnDefenderRetired;
                bridge.EnemyGone -= OnEnemyGone;
            }
        }

        // gift-phase-removal unit 1 — 덱 구성은 **배치 진입 단일 경로**다. 선물 페이즈가
        // 있던 시절엔 Gift 에서 미리 만들어 캐시하고 여기서 재사용했지만(연출 순서 ==
        // 인게임 순서를 맞추려고), 그 연출이 사라지면서 캐시의 이유도 함께 사라졌다.
        // 매 배치 진입마다 새로 구성한다 — gift-phase 이전의 원래 불변식.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Placement) return;

            BuildDeck();
            _attachedTo.Clear();
            AttachmentsChanged?.Invoke();
            Gauge = config != null ? Mathf.Clamp(config.gaugeStart, 0, config.gaugeMax) : 0;
            GaugeChanged?.Invoke(Gauge);
            HandChanged?.Invoke(HandChangeReason.Reset);
            LogDeck(_lastComposedCards);
        }

        // 저장 덱 10 + 공용 Active 2 = 12장. 시드는 매치 시드 하나이고 셔플은
        // DreamcatcherCycleDeck 생성자의 단일 Fisher-Yates 가 전담한다(여기서 섞지 않는다).
        //
        // gift-phase-removal unit 1 — 루시드/림 분기는 폐지됐다. 추가 2장의 유일한 출처는
        // 이 매치의 스킬 롤(SkillLoadoutController)이며, 무의식 카드는 저장 덱에 직접
        // 넣는 일반 카드가 됐다(unit 0).
        private void BuildDeck()
        {
            _baseCards = ResolveAttachDeck();
            _lastComposedCards = new List<DreamcatcherCard>(_baseCards);
            AppendActiveCards(_lastComposedCards);
            int pinned = PinTutorialFirstHand(_lastComposedCards);
            int seed = GameManager.Instance != null ? GameManager.Instance.MatchSeed : 0;
            _deck = new DreamcatcherCycleDeck(_lastComposedCards, seed, pinned);
        }

        // first-run-tutorial — 온보딩 판의 «첫 손패». GameManager 가 판 시작 전에 넣어두고
        // (웨이브 플랜 주입과 같은 자리 · 같은 술어), 여기서는 그 카드들을 앞으로 끌어온다.
        //
        // **덱에 실제로 든 카드만 옮긴다** — 없는 카드를 끼워 넣으면 온보딩이 저장 덱을
        // 조작하는 셈이 되고(계약 4: 편성은 프로필 기본값 그대로), 부착 후 사이클에도
        // 정체불명 항목이 남는다. 저작 목록이 덱과 어긋나면 그만큼만 고정된다.
        private List<DreamcatcherCard> _tutorialFirstHand;

        public void SetTutorialFirstHand(IReadOnlyList<DreamcatcherCard> cards)
        {
            _tutorialFirstHand = (cards == null || cards.Count == 0)
                ? null : new List<DreamcatcherCard>(cards);
        }

        private int PinTutorialFirstHand(List<DreamcatcherCard> composed)
        {
            if (_tutorialFirstHand == null || composed == null) return 0;
            int pinned = 0;
            for (int i = 0; i < _tutorialFirstHand.Count; i++)
            {
                var want = _tutorialFirstHand[i];
                if (want == null) continue;
                int at = composed.IndexOf(want);
                if (at < pinned) continue;   // 덱에 없거나(-1) 이미 고정 구간에 있다
                composed.RemoveAt(at);
                composed.Insert(pinned, want);
                pinned++;
            }
            if (pinned < _tutorialFirstHand.Count)
                Debug.LogWarning($"[DreamcatcherHand] 온보딩 첫 손패 {_tutorialFirstHand.Count}장 중 " +
                                 $"{pinned}장만 덱에 있어 그만큼만 고정했다.", this);
            return pinned;
        }


        // Saved deck (validated, catalog-resolved) → serialized fallback.
        private List<DreamcatcherCard> ResolveAttachDeck()
        {
            var result = new List<DreamcatcherCard>();
            var save = (profileSO != null && profileSO.profile != null) ? profileSO.profile.CommittedDeck() : null;
            if (save != null && cardCatalog != null && DeckRules.Validate(save.cardIds, cardCatalog, out _))
            {
                foreach (var id in save.cardIds)
                {
                    var card = cardCatalog.ById(id);
                    if (card != null) result.Add(card);
                }
                if (result.Count > 0) return result;
            }
            // 기본(fallback) 덱 제거 (사용자 결정 2026-07-15): 저장/선택 덱이 없으면 부착 덱은
            // 빈 목록. 기본 덱이 모든 배치 유닛을 상시 버프하던 미의도 동작 차단(→ 강화 오라 오작동
            // 근본 원인). 각성 손패는 롤된 Active 카드만으로 구성된다.
            return result;
        }

        // Common Active cards from the existing per-match roll. Fewer than the
        // rolled count (missing mapping / empty pool) → warn and proceed with
        // what exists; the hand/cycle logic is size-agnostic (critic M2).
        private void AppendActiveCards(List<DreamcatcherCard> cards)
        {
            if (skillLoadout == null || skillLoadout.Picked == null) return;
            foreach (var skill in skillLoadout.Picked)
            {
                if (skill == null) continue;
                var card = FindActiveCard(skill);
                if (card != null) cards.Add(card);
                else Debug.LogWarning($"[DreamcatcherHandController] No Active card wraps skill '{skill.id}' — skipped (queue runs short).");
            }
        }

        private DreamcatcherCard FindActiveCard(SkillData skill)
        {
            if (activeCards == null) return null;
            foreach (var card in activeCards)
                if (card != null && card.type == CardType.Active && card.skill == skill)
                    return card;
            return null;
        }

        // ── Gauge ────────────────────────────────────────────────────────────

        private void OnEnemyKilledAwakening(int reward, Vector3 sourceWorldPos, ISpineUnitVisualData killedVisual)
            => GainAwakening(reward, sourceWorldPos, killedVisual);

        private void OnDefenderDied(Entity entity, DefenderUnitData data, Vector3 sourceWorldPos)
        {
            // unit 6 — 죽은 유닛(디펜더도 ISpineUnitVisualData) 스킨을 피규어 소스로 전달.
            GainAwakening(data != null ? data.awakeningReward : 0, sourceWorldPos, data);
            RecoverCardsHostedBy(entity);
        }

        // defender-clock-out unit 2 — 퇴근은 **회수만** 한다. 각성 지급이 빠지는 것이
        // 사망과의 유일한 차이다: 각성은 처치/사망의 보상이지 퇴장의 보상이 아니고,
        // 주면 배치→퇴근 반복이 게이지 파밍이 된다.
        private void OnDefenderRetired(Entity entity, DefenderUnitData _, Vector3 __)
            => RecoverCardsHostedBy(entity);

        // host 에 얹혀 있던 항목을 전부 큐 뒤로 돌려보낸다(퇴장 순서 = 회수 순서).
        // 호출처 3개: 방어유닛 사망 · 방어유닛 퇴근 · 적 소멸.
        //
        // ⚠ 통합 전 두 판본은 **완전히 같지 않았다** — 사망 쪽에만 `handle > 0` 이면
        // 스쿼드 전역 효과를 회수하는 3줄이 있었다(unit 9). 적 소멸 판본은 "표식은 handle 0
        // 이라 revoke 가 없다"고 주석으로 단언한다. 통합하면 적 경로가 그 분기를 물려받으므로
        // 확인했다: 적 부착의 유일한 writer 인 `ApplyBountyMark` 는 **성공 시 0 을 반환**하고
        // 나머지 경로는 전부 -1(부착 자체가 없음)이다. 따라서 적에게는 분기가 절대 안 탄다.
        private void RecoverCardsHostedBy(Entity host)
        {
            if (_deck == null || _attachedTo.Count == 0) return;
            _recoverScratch.Clear();
            foreach (var kv in _attachedTo)
                if (kv.Value.host == host) _recoverScratch.Add(kv.Key);
            if (_recoverScratch.Count == 0) return;
            foreach (var entryId in _recoverScratch)
            {
                int handle = _attachedTo[entryId].handle;
                if (handle > 0 && bridge != null)
                    bridge.RevokeDreamcatcherEffects(handle);
                _attachedTo.Remove(entryId);
                _deck.Recover(entryId);
            }
            HandChanged?.Invoke(HandChangeReason.Recovered);
            AttachmentsChanged?.Invoke(); // 위 early-return 들을 지나면 회수가 1건 이상
        }

        // subconscious-curse-expansion unit 2 — 표식 악몽 소멸(처치/유출) 회수.
        // OnDefenderDied 의 회수 절반과 대칭(각성 지급 없음 — 처치 보상은 배율된
        // EnemyKilledAwakening 가, 유출은 무보상이 각각 자연 처리). 표식은 handle 0
        // (무회수) 이라 revoke 호출도 없다 — 큐 복귀만.
        private void OnEnemyGone(Entity entity) => RecoverCardsHostedBy(entity);

        private void GainAwakening(int reward, Vector3 sourceWorldPos, ISpineUnitVisualData killedVisual)
        {
            if (reward <= 0) return;
            int next = Mathf.Min(Gauge + reward, GaugeMax); // overflow is lost
            int applied = next - Gauge;
            if (applied < reward) // 일부(또는 전부)가 상한에 막혀 소멸 → 넘침 경고
                AwakeningOverflowed?.Invoke(reward - applied);
            if (next == Gauge) return;
            Gauge = next;
            GaugeChanged?.Invoke(Gauge);
            // unit 3 — 흡수 비행: 실제 적용된 획득량 + 사망 view-space 위치를 뷰로 흘려
            // 킬 위치에서 피규어가 날아오게 한다(입자=피규어).
            // unit 6 — 죽은 유닛 스킨을 함께 실어 피규어를 그 스킨으로 렌더.
            AwakeningGainedAt?.Invoke(applied, sourceWorldPos, killedVisual);
        }

        // ── Hand / use API (views call Commit* at pending-commit time) ───────

        public List<DreamcatcherCycleDeck.Entry> Hand() =>
            _deck != null ? _deck.Hand(HandSize) : new List<DreamcatcherCycleDeck.Entry>();

        public int CostOf(DreamcatcherCard card) =>
            (config != null && card != null) ? config.CostFor(card.type) : int.MaxValue;

        // Drag-start / dim gate: in hand + affordable. Per-target attach caps are
        // re-checked in CommitAttach.
        public bool CanUse(int entryId)
        {
            if (_deck == null || !_deck.TryGetCard(entryId, out var card)) return false;
            return Gauge >= CostOf(card);
        }

        // dreamcatcher-taxonomy-cleanup unit 1 — single attach commit for both
        // host-attached kinds. Squad (axis-set buff anchored at host) and Unit
        // (host-only mechanics) share the whole lifecycle: cap check, apply,
        // out-of-pool, host↔handle registry, spend, host-death revoke/recycle.
        // The bridge dispatcher picks the apply machine by CardType — the caller
        // no longer forks. Active cards use the Commit*Active* skill paths.
        //
        // Apply first: a failed attach (entity gone, non-defender, contributed
        // nothing) must not spend or cycle (contract 9). Handle 규약: <0 실패 /
        // 0 무회수(엔티티 부착형: 슬롯이 엔티티와 함께 소멸) / >0 회수핸들(host 사망 시
        // RevokeDreamcatcherEffects — squad 버프·placement-aura 오라 회수).
        public bool CommitAttach(int entryId, Entity host)
        {
            if (!TryGetUsableAttach(entryId, out var card)) return false;
            // subconscious-curse-expansion unit 1 (몽마의 계약) — 유출 허용치 선불 게이트.
            // 지불 가능성(잔여 − cost ≥ 1)을 apply 전에 확인하고, 실제 지불은 apply 성공
            // 후에만 한다(실패한 부착이 지불하는 일 없음 — contract 9). 지불은 비가역:
            // host 사망 revoke 는 hosted 버프만 회수하고 허용치는 돌아오지 않는다.
            if (card.leakAllowanceCost > 0 &&
                bridge.RemainingLeakAllowance() - card.leakAllowanceCost < 1)
            {
                Debug.Log($"[DreamcatcherHandController] '{card.id}' rejected — 잔여 유출 허용치 부족(지불 시 즉시 패배 금지).");
                return false;
            }
            if (AtAttachCap(host, card)) return false;
            int handle = bridge.ApplyDreamcatcherCard(host, card);
            if (handle < 0) return false; // contributed nothing — no spend
            if (card.leakAllowanceCost > 0 && !bridge.TryPayLeakAllowance(card.leakAllowanceCost))
            {
                // 게이트 통과 직후라 단일 스레드 흐름에서 실패할 수 없는 경로 — 방어적
                // 처리: 이미 성립한 부착을 회수하고 커밋 전체를 거절(무차감·카드 잔류).
                if (handle > 0) bridge.RevokeDreamcatcherEffects(handle);
                Debug.LogWarning($"[DreamcatcherHandController] '{card.id}' — 지불 단계 실패, 커밋 롤백.");
                return false;
            }
            return AttachAndSpend(entryId, card, host, handle);
        }

        // Shared attach tail: out-of-pool, host registry, spend, notify.
        private bool AttachAndSpend(int entryId, DreamcatcherCard card, Entity host, int handle)
        {
            if (!_deck.UseUnit(entryId, HandSize)) return false; // guarded by TryGetUsable
            _attachedTo[entryId] = (host, handle);
            AttachmentsChanged?.Invoke();
            Spend(card);
            HandChanged?.Invoke(HandChangeReason.Used);
            return true;
        }

        // Shared cap (unit 9): Unit + Squad attachments count together.
        private bool AtAttachCap(Entity host, DreamcatcherCard card)
        {
            if (CountAttachedTo(host) < (config != null ? config.maxAttachPerUnit : 3)) return false;
            Debug.Log($"[DreamcatcherHandController] '{card.id}' rejected — host at attach cap.");
            return true;
        }

        // subconscious-curse-expansion unit 2 (살찌운 제물) — 적 표식 커밋. Unit 부착과
        // 같은 수명주기(UseUnit 풀 이탈 + _attachedTo 등록 + spend)를 적 host 로 재사용.
        // AtAttachCap 은 **의도적 미적용** — 표식 상한은 bridge 의 이중 표식 preflight
        // (적당 1개)가 강제하고, 부착 캡은 defender 슬롯 개념이다(spec critic m4).
        // BountyMark 카드가 실수로 CommitAttach(defender 경로)에 유입돼도 bake 의
        // trigger=None 가드가 무차감 거절한다 — 정식 라우팅은 unit 3 드래그 판별.
        public bool CommitMarkEnemy(int entryId, Entity enemy)
        {
            if (!TryGetUsableAttach(entryId, out var card)) return false;
            int handle = bridge.ApplyBountyMark(enemy, card);
            if (handle < 0) return false; // not marked — no spend
            return AttachAndSpend(entryId, card, enemy, handle);
        }

        // active-dreamcatcher-tile-aim unit 0 — Active 의 단일 커밋(포탈만 별도). 아군 버프
        // (공격폭증·속사)도 여기로 온다 — 구 CommitActiveDefender 은퇴.
        public bool CommitActiveTile(int entryId, Vector2Int cell)
        {
            if (!TryGetUsableActive(entryId, out var card)) return false;
            if (!bridge.CastSkillAtTile(card.skill, cell, out _)) return false;
            SpendAndRecycle(entryId, card);
            return true;
        }

        public bool CommitActivePortal(int entryId, Vector2Int entryTile, Vector2Int exitTile)
        {
            if (!TryGetUsableActive(entryId, out var card)) return false;
            if (!bridge.CastPortal(card.skill, entryTile, exitTile, out _)) return false;
            SpendAndRecycle(entryId, card);
            return true;
        }

        // ── internals ────────────────────────────────────────────────────────

        private bool TryGetUsable(int entryId, CardType expected, out DreamcatcherCard card)
        {
            card = null;
            if (_deck == null || bridge == null) return false;
            if (!_deck.TryGetCard(entryId, out card)) return false;
            if (card.type != expected) return false;
            return Gauge >= CostOf(card);
        }

        // dreamcatcher-taxonomy-cleanup unit 1 — attach gate for both host-attached
        // kinds (Squad|Unit). Active is rejected (it uses the skill-cast paths).
        private bool TryGetUsableAttach(int entryId, out DreamcatcherCard card)
        {
            card = null;
            if (_deck == null || bridge == null) return false;
            if (!_deck.TryGetCard(entryId, out card)) return false;
            if (card.type == CardType.Active) return false;
            return Gauge >= CostOf(card);
        }

        private bool TryGetUsableActive(int entryId, out DreamcatcherCard card)
        {
            if (!TryGetUsable(entryId, CardType.Active, out card)) return false;
            if (card.skill == null)
            {
                Debug.LogWarning($"[DreamcatcherHandController] Active card '{card.id}' has no skill — config error.");
                return false;
            }
            return true;
        }

        private void SpendAndRecycle(int entryId, DreamcatcherCard card)
        {
            _deck.UseAndRecycle(entryId, HandSize);
            Spend(card);
            HandChanged?.Invoke(HandChangeReason.Used);
        }

        private void Spend(DreamcatcherCard card)
        {
            Gauge = Mathf.Max(0, Gauge - CostOf(card));
            GaugeChanged?.Invoke(Gauge);
        }

        // unit-dreamcatcher-icons unit 0 — read-only attachment snapshot for
        // presentation (icon strips). Fills the caller's list (no allocation);
        // entryId-ascending order keeps strip order stable across rebuilds
        // (dictionary iteration order is not deterministic after removals).
        public void GetAttachments(List<(Entity host, DreamcatcherCard card)> results)
        {
            results.Clear();
            if (_deck == null || _attachedTo.Count == 0) return;
            _attachReadScratch.Clear();
            foreach (var kv in _attachedTo) _attachReadScratch.Add(kv.Key);
            _attachReadScratch.Sort();
            foreach (var entryId in _attachReadScratch)
                if (_deck.TryGetCard(entryId, out var card))
                    results.Add((_attachedTo[entryId].host, card));
        }

        private int CountAttachedTo(Entity target)
        {
            int count = 0;
            foreach (var kv in _attachedTo)
                if (kv.Value.host == target) count++;
            return count;
        }

        // dreamcatcher-attach-lockon unit 5 — base-ring/리티클/콜아웃 유효성용 공개 조회.
        // 부착수는 드래그 중 불변이라 조준 시작 스냅샷에 쓴다(매프레임 dict 전수 금지).
        public int MaxAttachPerUnit => config != null ? config.maxAttachPerUnit : 3;
        public int AttachCountOf(Entity host) => CountAttachedTo(host);
        public bool CanAttachMore(Entity host) => CountAttachedTo(host) < MaxAttachPerUnit;

        private void LogDeck(List<DreamcatcherCard> cards)
        {
            var logger = GameManager.Instance?.Logger;
            if (logger == null) return;
            var ids = new List<string>(cards.Count);
            foreach (var card in cards)
                if (card != null) ids.Add(card.id);
            // tournament-deck-info unit 1 — 롤된 Active 를 뺀 "고른 덱"도 같이 기록한다.
            // _baseCards 가 ResolveAttachDeck() 결과 = 저장 덱이다.
            var baseIds = new List<string>(_baseCards.Count);
            foreach (var card in _baseCards)
                if (card != null) baseIds.Add(card.id);

            // 머지 해소(2026-07-31): page-local-presets 가 SelectedDeck → CommittedDeck 으로
            // 개명했고 tournament-deck-info 는 개명 전 이름을 썼다. 살아 있는 API 는 CommittedDeck.
            var save = (profileSO != null && profileSO.profile != null) ? profileSO.profile.CommittedDeck() : null;
            logger.SetDreamcatcherDeck(save != null ? save.id : "default",
                save != null ? save.name : "Default+Active", ids, baseIds);

            // tournament-deck-info unit 4 — 카드까지 확정된 덱을 pending 레코드에 갱신한다.
            // 하드킬/이전 세션 마감(ReconcilePending)이 읽는 유일한 출처라, 판 중에 적어두지
            // 않으면 그 경로는 영영 덱 없이 마감된다. 실제 전송은 마감 시점에 일어난다.
            Wassup.Core.Api.TournamentMatchReporter.PersistMatchDeck(logger.DeckInfoJson());
        }
    }
}
