using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core.Session;
using Wassup.Data;
using Wassup.Sim.Match;

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
        // gift-phase unit 1 — 선물 이벤트 가중치/무의식 수량. 없으면 Lucid 고정 폴백.
        [SerializeField] private GiftConfig giftConfig;

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

        // gift-phase unit 1 — Gift 페이즈에서 덱 조합이 끝나 GiftPhaseView 가 읽을 수
        // 있게 됐음을 알린다(연출 시작 트리거).
        public event System.Action GiftDeckReady;

        public GiftKind GiftKind => _giftKind;
        public IReadOnlyList<DreamcatcherCard> GiftBaseCards => _giftBaseCards;
        public IReadOnlyList<DreamcatcherCard> GiftAddedCards => _giftAddedCards;
        // 확정 12장 순서 = 실제 사이클 큐 초기 순서(연출 착지 대상). handSize=TotalCount 로
        // 전체 큐를 순서대로 반환. 부착 0 인 Gift 시점엔 12장 전부.
        public List<DreamcatcherCycleDeck.Entry> GiftFinalOrder() =>
            _deck != null ? _deck.Hand(_deck.TotalCount) : new List<DreamcatcherCycleDeck.Entry>();

        // unit 16-G — 게이지 **상태와 산식**은 `MatchGaugeRules` 가 소유한다. 이 프로퍼티는
        // 읽기 표면으로 남는다(소비자 diff 0). 뷰 신호 3종은 여기 그대로 — 프레젠테이션이다.
        private readonly MatchGaugeRules _gauge = new MatchGaugeRules();
        public int Gauge => _gauge.Current;
        public int GaugeMax => config != null ? config.gaugeMax : 100;
        public int HandSize => config != null ? config.handSize : 5;
        // dreamcatcher-orb-dock unit 1 — 항아리 독이 코스트 눈금·ready 임계를 데이터에서
        // 파생하기 위해 config 를 읽는다(뷰는 읽기 전용; DreamcatcherHandView.Config 미러).
        public AwakeningConfig Config => config;
        // subconscious-curse-expansion unit 3 — 표식 드롭 픽 반경(타일, SO 노브).
        public float EnemyPickRadiusTiles => config != null ? config.enemyPickRadiusTiles : 1.5f;

        private DreamcatcherCycleDeck _deck;
        // gift-phase unit 1 — 선물 페이즈에서 확정한 조합 캐시. 배치 진입 시 _deck 재사용
        // (이중 셔플 방지: DreamcatcherCycleDeck 무변경, 연출은 GiftFinalOrder 로 순서 읽음).
        private GiftKind _giftKind = GiftKind.Lucid;
        private List<DreamcatcherCard> _giftBaseCards = new List<DreamcatcherCard>();
        private List<DreamcatcherCard> _giftAddedCards = new List<DreamcatcherCard>();
        private List<DreamcatcherCard> _lastComposedCards = new List<DreamcatcherCard>();
        // gift-phase (review M1) — 이번 배치 진입에서 Gift 가 덱을 구성했는지. 배치 진입 시
        // 소비한다. false 면 매 진입마다 새로 구성(원래 "배치 진입마다 재구성" 불변식) →
        // gift 우회 폴백 경로에서 재시작 시 stale/소비된 _deck 재사용을 막는다.
        private bool _giftDeckComposed;
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
                bridge.EnemyGone -= OnEnemyGone;
            }
        }

        // gift-phase unit 1 — Gift 진입 시 덱을 조합·구성(단일 셔플, _deck 생성),
        // Placement 진입 시 그 _deck 을 재사용하며 게이지/부착만 리셋. Gift 우회 경로면
        // Placement 에서 폴백 구성(기존 동작). 구독은 OnEnable/OnDisable 대칭 유지.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Gift) { BuildGiftDeck(); return; }
            if (phase != GamePhase.Placement) return;

            // Gift 페이즈가 이번 진입에서 _deck 을 구성했으면 그대로 재사용(이중 셔플 방지,
            // 연출 순서 == 인게임 순서). 아니면(Gift 우회) 매 진입마다 새로 구성한다.
            // 플래그는 진입마다 소비 — 다음 진입은 다시 Gift 구성 or 폴백을 강제한다.
            if (!_giftDeckComposed) BuildFallbackDeck();
            _giftDeckComposed = false;
            _attachedTo.Clear();
            AttachmentsChanged?.Invoke();
            _gauge.Reset(config != null ? config.gaugeStart : 0, config != null ? config.gaugeMax : 0);
            GaugeChanged?.Invoke(Gauge);
            HandChanged?.Invoke(HandChangeReason.Reset);
            LogDeck(_lastComposedCards);
        }

        // Gift 진입 시 선물 이벤트를 결정하고 확정 12장 덱을 구성한다. _deck 을 1회
        // 생성(단일 Fisher-Yates)하고 배치 진입 시 재사용. 게이지/부착 리셋은 배치에서
        // — Gift 동안 인게임 핸드는 아직 등장하지 않는다.
        private void BuildGiftDeck()
        {
            int seed = GameManager.Instance != null ? GameManager.Instance.MatchSeed : 0;
            _giftBaseCards = ResolveAttachDeck();
            _giftKind = giftConfig != null
                ? GiftDeckComposer.PickKind(seed, giftConfig.lucidWeight, giftConfig.rimWeight)
                : GiftKind.Lucid;

            _giftAddedCards = new List<DreamcatcherCard>();
            if (_giftKind == GiftKind.Lucid) AppendActiveCards(_giftAddedCards);
            else _giftAddedCards.AddRange(ResolveRimGift(seed));

            _lastComposedCards = new List<DreamcatcherCard>(_giftBaseCards);
            _lastComposedCards.AddRange(_giftAddedCards);
            _deck = new DreamcatcherCycleDeck(_lastComposedCards, seed);
            _giftDeckComposed = true; // 배치 진입이 이 _deck 을 재사용하도록(review M1)
            GiftDeckReady?.Invoke();
        }

        // Gift 우회(직접 배치 진입) 안전 폴백 — 기존 동작 그대로(저장10 + 롤 Active).
        private void BuildFallbackDeck()
        {
            _giftKind = GiftKind.Lucid;
            _giftBaseCards = ResolveAttachDeck();
            _giftAddedCards = new List<DreamcatcherCard>();
            AppendActiveCards(_giftAddedCards);
            _lastComposedCards = new List<DreamcatcherCard>(_giftBaseCards);
            _lastComposedCards.AddRange(_giftAddedCards);
            int seed = GameManager.Instance != null ? GameManager.Instance.MatchSeed : 0;
            _deck = new DreamcatcherCycleDeck(_lastComposedCards, seed);
        }

        // 림의 선물: 카탈로그의 무의식(Subconscious) 카드에서 시드로 N장. 풀 부족분은
        // non-Active·non-Subconscious 카드에서 임의 폴백(안전장치, unit 2 저작 후 미발동).
        // 숨김(visible == 0) 카드는 풀/폴백 양쪽에서 제외(card-visibility unit 4).
        private List<DreamcatcherCard> ResolveRimGift(int seed)
        {
            var pool = new List<DreamcatcherCard>();
            var fallback = new List<DreamcatcherCard>();
            if (cardCatalog != null && cardCatalog.cards != null)
                foreach (var c in cardCatalog.cards)
                {
                    if (c == null) continue;
                    if (c.visible == 0) continue; // 숨김 카드는 선물 풀에서도 제외 (card-visibility unit 4)
                    if (c.category == CardCategory.Subconscious) pool.Add(c);
                    else if (c.type != CardType.Active) fallback.Add(c);
                }
            int count = giftConfig != null ? giftConfig.rimGiftCount : 2;
            return GiftDeckComposer.PickRim(pool, fallback, count, seed);
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

            // Card recovery: every entry hosted by the dead defender rejoins the
            // queue at the back (death order = recovery order). Squad entries
            // (handle>0) also revoke their squad-wide effect (unit 9).
            if (_deck == null || _attachedTo.Count == 0) return;
            _recoverScratch.Clear();
            foreach (var kv in _attachedTo)
                if (kv.Value.host == entity) _recoverScratch.Add(kv.Key);
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
        private void OnEnemyGone(Entity entity)
        {
            if (_deck == null || _attachedTo.Count == 0) return;
            _recoverScratch.Clear();
            foreach (var kv in _attachedTo)
                if (kv.Value.host == entity) _recoverScratch.Add(kv.Key);
            if (_recoverScratch.Count == 0) return;
            foreach (var entryId in _recoverScratch)
            {
                _attachedTo.Remove(entryId);
                _deck.Recover(entryId);
            }
            HandChanged?.Invoke(HandChangeReason.Recovered);
            AttachmentsChanged?.Invoke();
        }

        private void GainAwakening(int reward, Vector3 sourceWorldPos, ISpineUnitVisualData killedVisual)
        {
            if (reward <= 0) return;
            // unit 16-G — 클램프·넘침 산식은 규칙이 결정하고, 여기서는 그 값을 뷰로 흘린다.
            bool moved = _gauge.TryGain(reward, out int applied, out int overflowed);
            if (overflowed > 0) AwakeningOverflowed?.Invoke(overflowed);
            if (!moved) return;
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
            // unit 16 — 주석대로 **손패 안**을 실제로 확인한다. `TryGetCard` 는 부착분도 통과시켜서
            // 이 게이트가 자기 계약("in hand")을 어기고 있었다.
            if (!_deck.IsInHand(entryId, HandSize)) return false;
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
        /// <summary>
        /// battle-sim-extraction unit 16-E — **거절 사유를 밖으로 낸다.**
        /// 그 전에는 네 `Commit*` 이 전부 `bool` 이라 30여 사유가 어댑터에서 `Card_NotInHand`
        /// 하나로 접혔고(실제로 손패와 무관한 거절까지 그렇게 보고됐다), UI 는 그것을 preflight 로
        /// 다시 계산했다. 이제 receipt 가 진짜 사유를 싣는다.
        /// </summary>
        public bool CommitAttach(int entryId, Entity host, out CommandReject reject)
        {
            reject = CommitAttachReason(entryId, host);
            return reject == CommandReject.None;
        }

        public bool CommitAttach(int entryId, Entity host) => CommitAttach(entryId, host, out _);

        private CommandReject CommitAttachReason(int entryId, Entity host)
        {
            // unit 16-C — 손패·종류·게이지 + **유출 선불 가능성 + 부착 캡**을 한 판정으로 본다.
            // subconscious-curse-expansion unit 1 (몽마의 계약): 지불 가능성(잔여 − cost ≥ 1)은
            // apply **전**에 확인하고 실제 지불은 apply 성공 후에만 한다(실패한 부착이 지불하는 일
            // 없음 — contract 9). 지불은 비가역: host 사망 revoke 는 hosted 버프만 회수한다.
            var reason = JudgeAttach(entryId, host, out var card);
            if (reason != CommandReject.None)
            {
                if (reason == CommandReject.Card_LeakAllowanceTooLow)
                    Debug.Log($"[DreamcatcherHandController] '{card?.id}' rejected — 잔여 유출 허용치 부족(지불 시 즉시 패배 금지).");
                else if (reason == CommandReject.Card_AttachCapReached)
                    Debug.Log($"[DreamcatcherHandController] '{card?.id}' rejected — host at attach cap.");
                return reason;
            }
            int handle = bridge.ApplyDreamcatcherCard(host, card);
            if (handle < 0)
            {
                // unit 16-D — **적용성은 위에서 이미 확인했다.** 여기 오면 preflight 와 apply 가
                // 갈린 것이다(`WouldDreamcatcherCardApply` 주석의 "★ 동기화" 부채가 실현된 상태).
                // 조용히 거절하면 그 드리프트가 "가끔 안 붙네" 로 묻힌다.
                Debug.LogError($"[DreamcatcherHandController] '{card.id}' — preflight 는 통과했는데 " +
                               "apply 가 기여 0 을 반환했다. 적용성 판정과 적용 경로가 어긋났다.");
                return CommandReject.Card_NoEffect;
            }
            if (card.leakAllowanceCost > 0 && !bridge.TryPayLeakAllowance(card.leakAllowanceCost))
            {
                // 게이트 통과 직후라 단일 스레드 흐름에서 실패할 수 없는 경로 — 방어적
                // 처리: 이미 성립한 부착을 회수하고 커밋 전체를 거절(무차감·카드 잔류).
                if (handle > 0) bridge.RevokeDreamcatcherEffects(handle);
                Debug.LogWarning($"[DreamcatcherHandController] '{card.id}' — 지불 단계 실패, 커밋 롤백.");
                return CommandReject.Session_InternalError;
            }
            return AttachAndSpend(entryId, card, host, handle);
        }

        /// <summary>
        /// battle-sim-extraction unit 16-D — **부착의 검증 전량.** 손패·종류·게이지·유출·캡
        /// (`MatchCardRules`) + **적용성**(`WouldDreamcatcherCardApply`)까지 여기서 닫힌다.
        ///
        /// 적용성이 검증으로 올라온 것이 이 unit 의 요점이다. 그 전에는 "이 카드가 이 host 에
        /// 기여하는가" 를 **적용해 보고** `handle &lt; 0` 으로 알았고, 그래서 검증과 적용 사이에
        /// 부분 상태가 생길 수 있는 구조였다. 이제 부작용 전에 판정이 끝난다.
        /// </summary>
        private CommandReject JudgeAttach(int entryId, Entity host, out DreamcatcherCard card)
        {
            var reason = Judge(entryId, wantActive: false, host, applyCapAndLeak: true, out card);
            if (reason != CommandReject.None) return reason;
            // 적용성 판정의 정본은 Bridge 다(ECS 능력 조회가 필요 — `ProjectileRef` 유무 등).
            // 순수 부분은 `DreamcatcherAttachEval` 이 갖고 EditMode 로 검증된다.
            if (!bridge.WouldDreamcatcherCardApply(host, card)) return CommandReject.Card_NoEffect;
            return CommandReject.None;
        }

        /// <summary>
        /// unit 16-D — 뷰 3곳이 각자 조합하던 **대상-종속 2조건**(부착 캡 + 적용성)을 한 이름으로.
        /// 드래그 타깃 유효성 · 타깃 수집 · 손패 딤 처리가 같은 술어를 보게 된다.
        ///
        /// 카드-종속 조건(손패 멤버십·게이지)은 <see cref="CanUse"/> 가 이미 따로 본다 —
        /// **여기서 합치지 않는다.** 합치면 뷰의 기존 판정이 넓어져 행동이 바뀐다(동결 규율).
        /// 커밋의 전량 판정은 <see cref="JudgeAttach"/> 이고, 이 술어는 그것의 부분집합이다.
        /// </summary>
        public bool CanAttachTo(Entity host, DreamcatcherCard card)
            => card != null && bridge != null
               && CountAttachedTo(host) < MaxAttachPerUnit
               && bridge.WouldDreamcatcherCardApply(host, card);

        // Shared attach tail: out-of-pool, host registry, spend, notify.
        private CommandReject AttachAndSpend(int entryId, DreamcatcherCard card, Entity host, int handle)
        {
            if (!_deck.UseUnit(entryId, HandSize))
            {
                // unit 16 — 검증이 손패 멤버십을 보게 된 뒤로 **도달 불가**다. 그래도 조용히
                // false 를 돌려주면 부분 커밋(효과 적용 + 유출 비가역 차감은 끝났는데 손패·게이지는
                // 그대로, 회수 핸들 미등록)이 다시 숨는다. 여기 오면 불변식이 깨진 것이다.
                Debug.LogError($"[DreamcatcherHandController] '{card.id}' — 검증 통과 후 손패 이탈. " +
                               "효과는 이미 적용됐고 유출 허용치는 비가역 차감됐다(핸들 미등록).");
                return CommandReject.Session_InternalError;
            }
            _attachedTo[entryId] = (host, handle);
            AttachmentsChanged?.Invoke();
            Spend(card);
            HandChanged?.Invoke(HandChangeReason.Used);
            return CommandReject.None;
        }

        // (unit 16-C — 부착 캡 판정은 `MatchCardRules.Check` 로 이관됐다. 공유 캡 계약은 그대로:
        //  Unit + Squad 부착이 함께 세어진다 — unit 9. 집계는 `CountAttachedTo` 가 소유한다.)

        // subconscious-curse-expansion unit 2 (살찌운 제물) — 적 표식 커밋. Unit 부착과
        // 같은 수명주기(UseUnit 풀 이탈 + _attachedTo 등록 + spend)를 적 host 로 재사용.
        // 부착 캡은 **의도적 미적용**(unit 16-C: `applyCapAndLeak: false`) — 표식 상한은 bridge 의 이중 표식 preflight
        // (적당 1개)가 강제하고, 부착 캡은 defender 슬롯 개념이다(spec critic m4).
        // BountyMark 카드가 실수로 CommitAttach(defender 경로)에 유입돼도 bake 의
        // trigger=None 가드가 무차감 거절한다 — 정식 라우팅은 unit 3 드래그 판별.
        public bool CommitMarkEnemy(int entryId, Entity enemy, out CommandReject reject)
        {
            reject = Judge(entryId, wantActive: false, default, applyCapAndLeak: false, out var card);
            if (reject != CommandReject.None) return false;
            int handle = bridge.ApplyBountyMark(enemy, card);
            // 이미 표식된 적 · 적이 아닌 대상이 여기로 접힌다 — 가르려면 Bridge 가 사유를
            // 돌려줘야 한다(16-D+F).
            if (handle < 0) { reject = CommandReject.Card_NoEffect; return false; }
            reject = AttachAndSpend(entryId, card, enemy, handle);
            return reject == CommandReject.None;
        }

        public bool CommitMarkEnemy(int entryId, Entity enemy) => CommitMarkEnemy(entryId, enemy, out _);

        // active-dreamcatcher-tile-aim unit 0 — Active 의 단일 커밋(포탈만 별도). 아군 버프
        // (공격폭증·속사)도 여기로 온다 — 구 CommitActiveDefender 은퇴.
        public bool CommitActiveTile(int entryId, Vector2Int cell, out CommandReject reject)
        {
            reject = JudgeActive(entryId, out var card);
            if (reject != CommandReject.None) return false;
            // 캐스트 실패(쿨다운·부적합 타일 등)가 여기로 접힌다 — 가르려면 `CastSkillAtTile` 이
            // 사유를 돌려줘야 한다(16-D+F).
            if (!bridge.CastSkillAtTile(card.skill, cell, out _)) { reject = CommandReject.Card_NoEffect; return false; }
            SpendAndRecycle(entryId, card);
            return true;
        }

        public bool CommitActiveTile(int entryId, Vector2Int cell) => CommitActiveTile(entryId, cell, out _);

        public bool CommitActivePortal(int entryId, Vector2Int entryTile, Vector2Int exitTile,
                                       out CommandReject reject)
        {
            reject = JudgeActive(entryId, out var card);
            if (reject != CommandReject.None) return false;
            if (!bridge.CastPortal(card.skill, entryTile, exitTile, out _)) { reject = CommandReject.Card_NoEffect; return false; }
            SpendAndRecycle(entryId, card);
            return true;
        }

        public bool CommitActivePortal(int entryId, Vector2Int entryTile, Vector2Int exitTile)
            => CommitActivePortal(entryId, entryTile, exitTile, out _);

        // ── internals ────────────────────────────────────────────────────────

        /// <summary>
        /// battle-sim-extraction unit 16 — **손패 멤버십을 여기서 본다.**
        ///
        /// 그 전에는 `TryGetCard`(큐 **또는** 부착) 로만 판정하고 커밋 단계의
        /// `UseUnit`/`UseAndRecycle` 이 `IndexInHand`(큐 **앞 N칸**)를 요구했다. 두 조건이 달라서
        /// **이미 부착된 entryId·손패 밖 entryId 가 검증을 통과한 뒤 커밋에서 실패**했고, 그
        /// 시점엔 효과 적용(ECS 쓰기)과 유출 허용치 **비가역 차감**이 이미 끝나 있었다 — 손패도
        /// 게이지도 그대로인 채 회수 핸들이 등록되지 못해 영영 revoke 불가였다.
        /// `AttachAndSpend` 의 `// guarded by TryGetUsable` 주석은 사실이 아니었다.
        /// </summary>
        /// <summary>
        /// battle-sim-extraction unit 16-C — **판정은 `MatchCardRules.Check` 가 한다.** 여기 남은
        /// 것은 규칙이 알 수 없는 것뿐이다: 덱 조회 · SO 필드 읽기 · 부착 등록부 집계.
        ///
        /// 그 전에는 같은 3~4조건이 세 함수에 복제돼 있었고 유출·캡은 `CommitAttach` 본문에
        /// 따로 있어서, 거절 사유가 전부 `bool false` 하나로 접혔다.
        /// </summary>
        /// <param name="wantActive">true = Active 경로, false = 부착 경로(Squad|Unit 둘 다 허용)</param>
        /// <param name="host">부착 캡을 볼 때만 쓰인다. 그 외에는 무시.</param>
        /// <param name="applyCapAndLeak">
        /// 부착 캡 + 유출 선불 게이트를 적용할지. **`CommitAttach` 만 true** 다 — 적 표식
        /// (`CommitMarkEnemy`)은 캡이 defender 슬롯 개념이라 의도적 미적용이고(spec critic m4),
        /// 유출 선불도 적출 전부터 보지 않았다. 행동 보존을 위해 그대로 둔다.
        /// </param>
        private CommandReject Judge(int entryId, bool wantActive, Entity host,
                                    bool applyCapAndLeak, out DreamcatcherCard card)
        {
            card = null;
            if (_deck == null || bridge == null) return CommandReject.Card_NotInHand;

            bool exists = _deck.TryGetCard(entryId, out card) && card != null;
            bool typeOk = exists && (wantActive ? card.type == CardType.Active
                                                : card.type != CardType.Active);
            return MatchCardRules.Check(new MatchCardRules.CommitInputs
            {
                CardExists     = exists,
                // `TryGetCard`(큐 **또는** 부착)와 `IsInHand`(큐 **앞 N칸**)는 다른 조건이다 —
                // 이 둘이 어긋나서 부분 커밋 구멍이 났었다(`2d4fab98`).
                InHand         = exists && _deck.IsInHand(entryId, HandSize),
                TypeMatches    = typeOk,
                SkillWired     = !typeOk || !wantActive || card.skill != null,
                Gauge          = Gauge,
                Cost           = exists ? CostOf(card) : 0,
                LeakRemaining  = applyCapAndLeak ? bridge.RemainingLeakAllowance() : 0,
                LeakCost       = applyCapAndLeak && exists ? card.leakAllowanceCost : 0,
                AttachedToHost = applyCapAndLeak ? CountAttachedTo(host) : 0,
                AttachCap      = applyCapAndLeak ? MaxAttachPerUnit : 0,
            });
        }

        // dreamcatcher-taxonomy-cleanup unit 1 — attach gate for both host-attached
        // kinds (Squad|Unit). Active is rejected (it uses the skill-cast paths).
        private bool TryGetUsableAttach(int entryId, out DreamcatcherCard card)
            => Judge(entryId, wantActive: false, default, applyCapAndLeak: false, out card)
               == CommandReject.None;

        private CommandReject JudgeActive(int entryId, out DreamcatcherCard card)
        {
            var reason = Judge(entryId, wantActive: true, default, applyCapAndLeak: false, out card);
            if (reason == CommandReject.Session_InternalError)
            {
                Debug.LogWarning($"[DreamcatcherHandController] Active card '{card?.id}' has no skill — config error.");
            }
            return reason;
        }

        private void SpendAndRecycle(int entryId, DreamcatcherCard card)
        {
            // unit 16 — 반환값을 무시하면 **스킬은 나갔고 쿨다운도 물렸는데 카드는 제자리, 게이지만
            // 차감**되는 비대칭이 조용히 생긴다. 검증이 손패 멤버십을 보게 된 뒤로 도달 불가다.
            if (!_deck.UseAndRecycle(entryId, HandSize))
                Debug.LogError($"[DreamcatcherHandController] '{card.id}' — 검증 통과 후 손패 이탈. " +
                               "스킬은 이미 발동됐다(카드 잔류).");
            Spend(card);
            HandChanged?.Invoke(HandChangeReason.Used);
        }

        private void Spend(DreamcatcherCard card)
        {
            _gauge.Spend(CostOf(card));
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
            // tournament-deck-info unit 1 — 선물을 뺀 "고른 덱"도 같이 기록한다.
            // _giftBaseCards 가 ResolveAttachDeck() 결과 = 저장 덱(또는 기본 덱)이다.
            var baseIds = new List<string>(_giftBaseCards.Count);
            foreach (var card in _giftBaseCards)
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
