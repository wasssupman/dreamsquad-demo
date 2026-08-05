using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Core;
using Wassup.Core.Session;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Sim.Match;

namespace Wassup.Bridge
{
    // battle-sim-extraction unit 12 — 구 ECS sim 위에 얹는 IMatchSession 구현체.
    //
    // 목적은 **스왑 반경 축소**다(ADR D4): 소비자가 이 계약만 보게 만든 뒤(unit 13),
    // 신 sim 은 구현체 교체 1곳으로 붙는다. 그래서 이 어댑터는 규칙을 옮기지 않고
    // **기존 Bridge 공개면 호출로 번역만** 한다 — 규칙 적출은 units 14~16 의 몫이다.
    //
    // unit 12 범위 경계:
    //  · 커맨드 7종 번역 + receipt 발급(멱등·순번) — 구현됨
    //  · 읽기 모델 — Bridge 가 이미 공개한 값만. 점수/유출/스트레스·통화는 Supported* = false 로
    //    표시하고 unit 14·15 가 채운다(0 을 조용히 흘리면 HUD 가 0 을 그린다)
    //  · 이벤트 스트림 — **드레인 배선은 unit 13**. 지금 큐를 소비하면 Bridge 직독 소비자와
    //    같은 큐를 다툰다(중복 소비). 여기서는 빈 목록을 돌려주고 계약면만 성립시킨다
    //  · MonoBehaviour 가 아니다 — 수명은 생성자에 넘긴 Bridge 가 소유한다
    public sealed class LegacyMatchSessionAdapter : IMatchSession
    {
        private readonly BattleBridge _bridge;
        private DreamcatcherHandController _hand;
        private readonly Dictionary<uint, CommandReceipt> _receipts = new(); // 멱등: seq → receipt
        private uint _nextExpectedSeq;
        private int _eventSeq;   // unit 13-B — 매치 전역 단조 이벤트 순번
        private int _orderInTick;
        private int _lastTick = -1;
        private bool _disposed;
        // TimeLease 는 struct 다 — MenuPopup 관용구(struct 필드 + 보유 플래그)를 따른다.
        private TimeLease _pauseLease;
        private bool _paused;

        public event Action<MatchOutcome> MatchEnded;

        public LegacyMatchSessionAdapter(BattleBridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public bool IsActive => !_disposed && _bridge != null;

        // ── 읽기 모델 ────────────────────────────────────────────────────────────
        public MatchReadModel ReadModel
        {
            get
            {
                // 다른 조회 API(`TryGetSpawnAlertForecast`·`TryGetPlacementCooldown`)와 같은
                // 가드를 둔다. 뷰는 `MatchSession.IsActive` 로 이미 걸러지지만, 가드가 없는
                // 표면 하나가 남아 있으면 그것을 그냥 부르는 호출자가 생긴다(리뷰 minor 8).
                if (_disposed) return default;
                var phase = ResolvePhase();
                var gm = GameManager.Instance;
                // unit 13-A3 — 코스트·쿨타임은 **번역으로 채운다**(사용자 결정: "지금 번역").
                // 여기서 값을 만들지 않고 Mono 런타임을 미러링할 뿐이며, 소유권을 sim 으로 옮기는
                // 것은 unit 15 다. 런타임이 없는 구간(씬 로드 전·아웃게임)은 미지원으로 신고한다 —
                // 0 을 조용히 흘리면 HUD 가 코스트 0 을 그린다.
                var cost = gm != null ? gm.CostRuntime : null;
                var cooldown = gm != null ? gm.CooldownRuntime : null;
                // unit 16-G — 게이지는 이제 `MatchGaugeRules` 가 소유하고 컨트롤러가 그것을 든다.
                // 컨트롤러가 없는 구간(씬 로드 전·아웃게임)은 **미지원으로 신고**한다 — 0 을 조용히
                // 흘리면 HUD 가 게이지 0 을 그린다(코스트와 같은 처분).
                var hand = ResolveHand();
                return new MatchReadModel(
                    tick: _bridge.HarnessTick,
                    battleClock: _bridge.BattleClock,
                    phase: phase,
                    timerRemaining: _bridge.TimerRemaining,
                    nextWaveAvailable: _bridge.NextWaveAvailable,
                    nextWaveHasNext: _bridge.NextWaveHasNext,
                    nextWaveNumber: _bridge.NextWaveNumber,
                    nextWaveClearReady: _bridge.NextWaveClearReady,
                    // unit 14 — 규칙이 `MatchOutcomeRules` 로 이사해 **실제 값**을 서빙한다.
                    // 뷰의 독립 누적은 이 값으로 대체된다(두 산식이 갈리면 화면 검산이 깨진다).
                    supportedScore: true,
                    scoreKill: _bridge.OutcomeScoreKill,
                    goals: _bridge.OutcomeGoals,
                    effectiveLeakLimit: _bridge.OutcomeEffectiveLeakLimit,
                    stressAccrued: _bridge.OutcomeStressAccrued,
                    stressLimit: _bridge.OutcomeStressLimit,
                    supportedCost: cost != null,
                    costCurrent: cost != null ? cost.Current : 0f,
                    costMax: cost != null ? cost.Max : 0f,
                    costCurrentInt: cost != null ? cost.CurrentInt : 0,
                    supportedGauge: hand != null,
                    gaugeCurrent: hand != null ? hand.Gauge : 0,
                    gaugeMax: hand != null ? hand.GaugeMax : 0,
                    anyPlacementCooldown: cooldown != null && cooldown.AnyActive);
            }
        }

        private MatchPhase ResolvePhase()
        {
            var gm = GameManager.Instance;
            if (gm == null) return _bridge.BattleRunning ? MatchPhase.Battle : MatchPhase.None;
            switch (gm.CurrentPhase)
            {
                case GamePhase.Battle: return MatchPhase.Battle;
                case GamePhase.Result:
                case GamePhase.Tally: return MatchPhase.Ended;
                // Draft/Gift/Gimmick/Placement 는 세션 관점에서 전부 배치 이전·배치 구간이다
                // (연출 페이즈는 프레젠테이션 소유 — 청사진 ① §1). 8→3 축소는 명시적 행동 차이이며
                // 비교기가 폴딩표를 갖는다(청사진 ① §9).
                case GamePhase.None: return MatchPhase.None;
                default: return MatchPhase.Placement;
            }
        }

        // ── 커맨드 ───────────────────────────────────────────────────────────────
        public CommandReceipt SendCommand(in MatchCommand command)
        {
            if (_disposed)
                return CommandReceipt.Rejected(command.ClientSeq, CommandReject.Session_PhaseClosed);

            // 멱등: 같은 seq 재전송은 재실행 없이 같은 receipt.
            if (_receipts.TryGetValue(command.ClientSeq, out var known)) return known;

            // 순번 갭 — 전송 채널이 순서를 보장한다는 전제(청사진 ① §3). 인프로세스라 갭은
            // 곧 호출자 버그이므로 보류 없이 거절한다. 비순서 채널(M3)에서는 세션이 재정렬
            // 버퍼를 소유하도록 이 지점을 바꾼다.
            //
            // **반드시 시끄러워야 한다**(리뷰 #1): 이 분기는 `_nextExpectedSeq` 를 전진시키지
            // 않으므로 한 번 어긋나면 재수렴이 없다. 그런데 호출부는 receipt 를 보지 않아, 로그가
            // 없으면 "웨이브 버튼·정지·배치가 콘솔 깨끗한 채로 영구히 죽는" 증상만 남는다.
            // 정상 경로(`MatchSession.Send`)는 `NextClientSeq()` 를 써서 이 분기에 오지 않는다 —
            // 여기 오면 누군가 순번을 직접 만들어 `SendCommand` 를 부른 것이다.
            if (command.ClientSeq != _nextExpectedSeq)
            {
                Debug.LogError($"[MatchSession] 커맨드 순번 갭: got={command.ClientSeq} " +
                               $"expected={_nextExpectedSeq} ({command.Kind}) — 호출자가 순번을 " +
                               $"직접 만들었다. MatchSession.Send 를 쓸 것. 이후 커맨드는 전부 거절된다.");
                return Remember(command.ClientSeq,
                    CommandReceipt.Rejected(command.ClientSeq, CommandReject.Session_SeqGap));
            }

            int axis = OrderResetAxis;
            if (axis != _lastTick) { _lastTick = axis; _orderInTick = 0; }
            int tick = CurrentTick;

            CommandReceipt receipt = command.Kind switch
            {
                CommandKind.DeployDefender   => Deploy(command, tick),
                CommandKind.SetDeployFacing  => SetFacing(command, tick),
                CommandKind.RelocateDefender => Relocate(command, tick),
                CommandKind.PlayCard         => PlayCard(command, tick),
                CommandKind.ForceNextWave    => ForceNextWave(command, tick),
                CommandKind.FinishPlacement  => FinishPlacement(command, tick),
                CommandKind.SetPaused        => SetPaused(command, tick),
                _ => CommandReceipt.Rejected(command.ClientSeq, CommandReject.Session_UnknownVerb),
            };

            // 실행 중에 이 세션이 파기됐으면(예: FinishPlacement → StartBattle → BeginPlacement 가
            // 새 세션을 무장하고 이 어댑터를 Dispose 한 경로 — 리뷰 #2) 기록을 남기지 않는다.
            // `Dispose` 가 이미 비운 `_receipts` 에 다시 쓰거나 죽은 세션의 기대값을 전진시키면
            // 살아 있는 새 세션과 어긋난다.
            if (_disposed) return receipt;

            _nextExpectedSeq = command.ClientSeq + 1;
            if (receipt.Accepted) _orderInTick++;
            return Remember(command.ClientSeq, receipt);
        }

        // 세션이 다음에 기대하는 순번. 호출자 쪽 카운터를 없애 어긋남을 구조적으로 제거한다.
        public uint NextClientSeq() => _nextExpectedSeq;

        // ── tick 축 (리뷰 #6) ────────────────────────────────────────────────────
        //
        // `_harnessTick` 은 **하네스 스테퍼만** 증가시킨다(`StepOneTick`). 라이브 판에서는 매치
        // 내내 0 이다. 그래서 라이브의 tick 을 0 으로 신고하면 "tick-스탬프드 읽기 모델"·
        // "수락 tick"·"tick 내 순서"가 전부 거짓이 되고, 골든은 하네스로 녹음되므로 **byte diff
        // 로는 절대 잡히지 않는다**. unit 19(커맨드로그)·unit 20(A/B parity)이 이 필드 위에
        // 세워지기 전에 부재를 명시한다: 라이브는 **-1 = 모른다**.
        //
        // 진짜 tick 을 라이브에도 주는 것은 시계 정책의 몫이다(unit 19) — 여기서
        // `_battleClock / fixedDt` 로 지어내면 하네스와 라이브가 서로 다른 두 시계를 갖게 된다.
        private int CurrentTick => TestModeContext.HarnessActive ? _bridge.HarnessTick : -1;

        // `_orderInTick` 을 되돌릴 축. tick 이 -1 로 고정이면 리셋이 영원히 안 걸려 순서가 매치
        // 전체로 단조 증가한다("같은 tick 내 순서 0부터"라는 계약과 어긋남). 라이브에서는
        // 프레임이 그 역할을 한다 — 한 프레임에 들어온 커맨드들이 한 묶음이라는 의미는 유지된다.
        private int OrderResetAxis
            => TestModeContext.HarnessActive ? _bridge.HarnessTick : Time.frameCount;

        private CommandReceipt Remember(uint seq, CommandReceipt receipt)
        {
            _receipts[seq] = receipt;
            return receipt;
        }

        private CommandReceipt Ok(in MatchCommand c, int tick, int subjectSimId = -1) =>
            CommandReceipt.Ok(c.ClientSeq, tick, _orderInTick, subjectSimId);

        private CommandReceipt Deploy(in MatchCommand c, int tick)
        {
            if (!TryResolveUnitDef(c.UnitDefId, out var unitData))
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Place_InvalidUnit);

            // 사전 판정으로 사유를 얻는다 — 커밋 API 는 bool 만 돌려주므로 사유 손실을 막는다.
            if (!_bridge.CanPlaceDefenderAt(c.Cell.X, c.Cell.Y, unitData, out var reason))
                return CommandReceipt.Rejected(c.ClientSeq, MapPlacement(reason));

            if (!_bridge.TryBeginDefenderDeployment(c.Cell.X, c.Cell.Y, unitData, out var deployed))
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_InternalError);

            // unit 13-C2 — 만든 개체의 id 를 receipt 에 실어야 뷰가 후속 사건을 이어갈 수 있다
            // (방향 지정 유닛의 활성화, 드롭 하마 뷰 비행). 엔진 타입은 넘기지 않는다.
            return Ok(c, tick, SubjectSimIdOf(deployed));
        }

        private int SubjectSimIdOf(Entity entity)
            => _bridge.TryGetSimId(entity, out int simId) ? simId : -1;

        private CommandReceipt SetFacing(in MatchCommand c, int tick)
        {
            if (!_bridge.TryResolveSimEntity(c.TargetSimId, out var entity))
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_UnknownEntity);
            if (!_bridge.TryGetDefenderCell(entity, out var cell))
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_UnknownEntity);

            // ⚠ unit 12 는 기존 시맨틱을 그대로 번역한다 — **현재는 이 호출이 활성화까지 수행**한다.
            // 활성화 주체를 Deploy 의 activationTick 예약으로 옮기고 이 커맨드를 방향 힌트로
            // 격하하는 것은 unit 15(배치 규칙 적출)의 몫이다. 그때 Session_TooLate 가 의미를 갖는다.
            _bridge.ActivateDeployedDefender(cell, entity, new Vector2Int(c.Facing.X, c.Facing.Y));
            return Ok(c, tick, c.TargetSimId);
        }

        private CommandReceipt Relocate(in MatchCommand c, int tick)
        {
            var from = new Vector2Int(c.Cell2.X, c.Cell2.Y);
            var to = new Vector2Int(c.Cell.X, c.Cell.Y);
            if (!_bridge.TryBeginDefenderRelocation(from, to, out var moved, out var reason))
                return CommandReceipt.Rejected(c.ClientSeq, MapPlacement(reason));
            // 재배치는 생성이 아니지만 **움직인 개체**를 실어 뷰가 후속 활성화를 이어간다.
            return Ok(c, tick, SubjectSimIdOf(moved));
        }

        private CommandReceipt PlayCard(in MatchCommand c, int tick)
        {
            var hand = ResolveHand();
            if (hand == null)
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_InternalError);

            bool ok;
            CommandReject reject;
            switch (c.Variant)
            {
                case CardVariant.Attach:
                    if (!_bridge.TryResolveSimEntity(c.TargetSimId, out var host))
                        return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_UnknownEntity);
                    ok = hand.CommitAttach(c.CardHandle, host, out reject);
                    break;
                case CardVariant.MarkEnemy:
                    if (!_bridge.TryResolveSimEntity(c.TargetSimId, out var enemy))
                        return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_UnknownEntity);
                    ok = hand.CommitMarkEnemy(c.CardHandle, enemy, out reject);
                    break;
                case CardVariant.ActiveTile:
                    ok = hand.CommitActiveTile(c.CardHandle, new Vector2Int(c.Cell.X, c.Cell.Y), out reject);
                    break;
                case CardVariant.ActivePortal:
                    if (c.Cell.X == c.Cell2.X && c.Cell.Y == c.Cell2.Y)
                        return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Card_PortalSameCell);
                    ok = hand.CommitActivePortal(c.CardHandle,
                        new Vector2Int(c.Cell.X, c.Cell.Y), new Vector2Int(c.Cell2.X, c.Cell2.Y), out reject);
                    break;
                default:
                    return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_UnknownVerb);
            }

            // unit 16-E — **사유 손실 지점이 사라졌다.** 그 전에는 `Commit*` 이 bool 만 돌려줘
            // 모든 거절이 `Card_NotInHand` 로 보고됐다(손패와 무관한 거절까지). 이제 검증 사유는
            // `MatchCardRules` 가 결정한 그대로 실리고, 적용 단계 거절은 `Card_NoEffect` 다.
            // 남은 과제: `Card_NoEffect` 를 세부 사유로 가르려면 Bridge 의 apply 경로가 사유를
            // 돌려줘야 한다(16-D+F 묶음). UI preflight 미러 소거도 그때.
            return ok ? Ok(c, tick) : CommandReceipt.Rejected(c.ClientSeq, reject);
        }

        private CommandReceipt ForceNextWave(in MatchCommand c, int tick)
        {
            if (!_bridge.BattleRunning)
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Wave_NotRunning);
            if (!_bridge.NextWaveHasNext)
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Wave_NoWaveLeft);
            // 비멱등 — _waveTimeShift 가 누적 재기준되므로 연타 순서가 결과를 바꾼다(기존 계약 유지).
            _bridge.ForceNextWave();
            return Ok(c, tick);
        }

        private CommandReceipt FinishPlacement(in MatchCommand c, int tick)
        {
            if (_bridge.BattleRunning)
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_PhaseClosed);
            // 배치 카운트다운을 sim 이 소유하게 만드는 것은 unit 14 — 지금은 기존 진입점 그대로다.
            _bridge.StartBattle();
            return Ok(c, tick);
        }

        private CommandReceipt SetPaused(in MatchCommand c, int tick)
        {
            // 유일하게 커맨드 자격이 있는 시간 제어(청사진 ① §2). UI 제스처 슬로모 5종은
            // 커맨드가 아니며 처분은 unit 19.
            if (c.Flag)
            {
                if (!_paused && TimeManager.Instance != null)
                {
                    _pauseLease = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);
                    _paused = true;
                }
            }
            else if (_paused)
            {
                _pauseLease.Dispose();
                _paused = false;
            }
            return Ok(c, tick);
        }

        // ── 이벤트 ───────────────────────────────────────────────────────────────
        // unit 13 이 드레인 소유권을 여기로 옮긴다. 그 전에는 Bridge 직독 소비자가 큐의 주인이므로
        // 빈 목록을 돌려준다(중복 소비 금지). 계약면은 이미 성립해 소비자가 미리 붙을 수 있다.
        // unit 13-B — 발행 지점. Bridge 가 뷰 메서드를 직접 부르던 자리에서 이것을 부르면
        // 방향이 뒤집힌다(뷰가 구독). `EventSeq` 는 매치 전역 단조다(청사진 ① §4).
        internal void Emit(SessionEventKind kind, int subjectSimId = -1, float amount = 0f)
        {
            if (_disposed) return;
            // `this` 를 넘겨 라우터가 `Current` 인지 확인한다 — 죽은/곁의 세션이 뷰에 흘리지 못한다.
            MatchSession.Publish(this, new SessionEvent(
                _eventSeq++, CurrentTick, kind, subjectSimId, amount: amount));
        }

        // 여전히 빈 목록이다 — **의도적**이다. 지금 여기에 누적하면 소비자가 없어 무한히 자란다
        // (fan-out 은 `MatchSession.Publish` 가 이미 했다). 누적·드레인의 소유는 기록기가 생기는
        // 시점(unit 19 커맨드로그/AMR)에 함께 온다.
        //
        // `List` 대신 `Array.Empty` 를 돌려주는 이유: `IReadOnlyList` 로 감싼 `List` 는 호출자가
        // `List<T>` 로 되돌려 **수정할 수 있다**. A2 가 예보 배열에서 막은 것과 같은 구멍이다.
        public IReadOnlyList<SessionEvent> DrainEvents() => Array.Empty<SessionEvent>();

        // unit 13-A2 — Bridge 는 내부 캐시 배열 참조를 넘기지만 여기서 span 으로 좁혀 **쓰기 경로를
        // 끊는다**. 복사가 아니므로 할당 0이고, 유효 범위는 호출 프레임뿐이라는 계약이 그 대가다.
        public bool TryGetSpawnAlertForecast(out ReadOnlySpan<float> laneFirstSpawnSec)
        {
            if (_disposed || !_bridge.TryGetSpawnAlertForecast(out _, out float[] lanes) || lanes == null)
            {
                laneFirstSpawnSec = default;
                return false;
            }
            laneFirstSpawnSec = lanes;
            return true;
        }

        // unit 13-A3 — id→정의 해석을 구현체가 소유한다(계약은 문자열 키만 안다). 해석기는
        // Deploy 커맨드가 쓰는 것과 **같은 함수**라 뷰와 커맨드가 같은 카탈로그를 본다.
        public bool TryGetPlacementCooldown(string unitDefId, out float remaining, out float fraction)
        {
            remaining = 0f;
            fraction = 0f;
            if (_disposed) return false;
            var runtime = GameManager.Instance != null ? GameManager.Instance.CooldownRuntime : null;
            if (runtime == null || !TryResolveUnitDef(unitDefId, out var unitData)) return false;

            remaining = runtime.RemainingFor(unitData);
            if (remaining <= 0f) return false;
            fraction = runtime.Fraction(unitData);
            return true;
        }

        internal void RaiseMatchEnded(MatchOutcome outcome) => MatchEnded?.Invoke(outcome);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // 정지 lease 누수 방지 — 남기면 그 판이 영구 정지한다(unit 2 의 lease 계약과 같은 함정).
            if (_paused) { _pauseLease.Dispose(); _paused = false; }
            _receipts.Clear();
            MatchEnded = null;
        }

        // ── 번역 헬퍼 ────────────────────────────────────────────────────────────
        private bool TryResolveUnitDef(string unitDefId, out DefenderUnitData unitData)
        {
            unitData = null;
            if (string.IsNullOrEmpty(unitDefId)) return false;
            var pool = _bridge.DefenderPool;
            if (pool == null) return false;
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && pool[i].id == unitDefId) { unitData = pool[i]; return true; }
            }
            return false;
        }

        private DreamcatcherHandController ResolveHand()
        {
            if (_hand != null) return _hand;
            // Bridge 가 쓰는 것과 같은 관용구(BattleBridge.cs:1321) — 씬 배선 신설을 피한다.
            _hand = UnityEngine.Object.FindAnyObjectByType<DreamcatcherHandController>();
            return _hand;
        }

        // 기존 enum 을 값 손실 없이 통합 축으로 옮긴다(청사진 ① §3).
        private static CommandReject MapPlacement(PlacementRejectReason reason) => reason switch
        {
            PlacementRejectReason.None => CommandReject.None,
            PlacementRejectReason.NotRunningOrPlacementClosed => CommandReject.Place_NotRunningOrPlacementClosed,
            PlacementRejectReason.MissingMap => CommandReject.Place_MissingMap,
            PlacementRejectReason.OutOfBounds => CommandReject.Place_OutOfBounds,
            PlacementRejectReason.NotBuildable => CommandReject.Place_NotBuildable,
            PlacementRejectReason.Occupied => CommandReject.Place_Occupied,
            PlacementRejectReason.InvalidUnit => CommandReject.Place_InvalidUnit,
            PlacementRejectReason.NotInPickedPool => CommandReject.Place_NotInPickedPool,
            PlacementRejectReason.InsufficientCost => CommandReject.Place_InsufficientCost,
            PlacementRejectReason.NoDefenderAtSource => CommandReject.Relocate_NoDefenderAtSource,
            PlacementRejectReason.SourceBusy => CommandReject.Relocate_SourceBusy,
            PlacementRejectReason.SameCell => CommandReject.Relocate_SameCell,
            // unit 15 — 배치 쿨타임. 재배치는 이 사유를 내지 않는다(같은 유닛을 옮기는 것이라
            // 배치 쿨타임 대상이 아니다) — 그래서 Place_ 계열로 접는다.
            PlacementRejectReason.OnCooldown => CommandReject.Place_OnCooldown,
            _ => CommandReject.Session_InternalError,
        };
    }
}
