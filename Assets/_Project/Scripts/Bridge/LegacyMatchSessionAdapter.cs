using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Wassup.Core;
using Wassup.Core.Session;
using Wassup.Core.TimeControl;
using Wassup.Data;

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
        private readonly List<SessionEvent> _emptyEvents = new();
        private uint _nextExpectedSeq;
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
                var phase = ResolvePhase();
                return new MatchReadModel(
                    tick: _bridge.HarnessTick,
                    battleClock: _bridge.BattleClock,
                    phase: phase,
                    timerRemaining: _bridge.TimerRemaining,
                    nextWaveAvailable: _bridge.NextWaveAvailable,
                    nextWaveHasNext: _bridge.NextWaveHasNext,
                    nextWaveNumber: _bridge.NextWaveNumber,
                    nextWaveClearReady: _bridge.NextWaveClearReady,
                    // unit 14 가 채운다 — 현재 Bridge private + 뷰 독립 누적이라 값이 없다.
                    supportedScore: false, scoreKill: 0, goals: 0, effectiveLeakLimit: 0,
                    stressAccrued: 0, stressLimit: 0,
                    // unit 15 가 sim 소유로 옮긴다 — 지금은 Mono 런타임이 권위라 미지원 표기.
                    supportedCurrency: false, costCurrent: 0f, costMax: 0f,
                    gaugeCurrent: 0, gaugeMax: 0);
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
            if (command.ClientSeq != _nextExpectedSeq)
                return Remember(command.ClientSeq,
                    CommandReceipt.Rejected(command.ClientSeq, CommandReject.Session_SeqGap));

            int tick = _bridge.HarnessTick;
            if (tick != _lastTick) { _lastTick = tick; _orderInTick = 0; }

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

            _nextExpectedSeq = command.ClientSeq + 1;
            if (receipt.Accepted) _orderInTick++;
            return Remember(command.ClientSeq, receipt);
        }

        private CommandReceipt Remember(uint seq, CommandReceipt receipt)
        {
            _receipts[seq] = receipt;
            return receipt;
        }

        private CommandReceipt Ok(in MatchCommand c, int tick) =>
            CommandReceipt.Ok(c.ClientSeq, tick, _orderInTick);

        private CommandReceipt Deploy(in MatchCommand c, int tick)
        {
            if (!TryResolveUnitDef(c.UnitDefId, out var unitData))
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Place_InvalidUnit);

            // 사전 판정으로 사유를 얻는다 — 커밋 API 는 bool 만 돌려주므로 사유 손실을 막는다.
            if (!_bridge.CanPlaceDefenderAt(c.Cell.X, c.Cell.Y, unitData, out var reason))
                return CommandReceipt.Rejected(c.ClientSeq, MapPlacement(reason));

            if (!_bridge.TryBeginDefenderDeployment(c.Cell.X, c.Cell.Y, unitData, out _))
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_InternalError);

            return Ok(c, tick);
        }

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
            return Ok(c, tick);
        }

        private CommandReceipt Relocate(in MatchCommand c, int tick)
        {
            var from = new Vector2Int(c.Cell2.X, c.Cell2.Y);
            var to = new Vector2Int(c.Cell.X, c.Cell.Y);
            if (!_bridge.TryBeginDefenderRelocation(from, to, out _, out var reason))
                return CommandReceipt.Rejected(c.ClientSeq, MapPlacement(reason));
            return Ok(c, tick);
        }

        private CommandReceipt PlayCard(in MatchCommand c, int tick)
        {
            var hand = ResolveHand();
            if (hand == null)
                return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_InternalError);

            bool ok;
            switch (c.Variant)
            {
                case CardVariant.Attach:
                    if (!_bridge.TryResolveSimEntity(c.TargetSimId, out var host))
                        return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_UnknownEntity);
                    ok = hand.CommitAttach(c.CardHandle, host);
                    break;
                case CardVariant.MarkEnemy:
                    if (!_bridge.TryResolveSimEntity(c.TargetSimId, out var enemy))
                        return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_UnknownEntity);
                    ok = hand.CommitMarkEnemy(c.CardHandle, enemy);
                    break;
                case CardVariant.ActiveTile:
                    ok = hand.CommitActiveTile(c.CardHandle, new Vector2Int(c.Cell.X, c.Cell.Y));
                    break;
                case CardVariant.ActivePortal:
                    if (c.Cell.X == c.Cell2.X && c.Cell.Y == c.Cell2.Y)
                        return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Card_PortalSameCell);
                    ok = hand.CommitActivePortal(c.CardHandle,
                        new Vector2Int(c.Cell.X, c.Cell.Y), new Vector2Int(c.Cell2.X, c.Cell2.Y));
                    break;
                default:
                    return CommandReceipt.Rejected(c.ClientSeq, CommandReject.Session_UnknownVerb);
            }

            // ⚠ 사유 손실 지점: `Commit*` 이 bool 만 돌려주므로 30여 사유가 하나로 접힌다.
            // 이것을 푸는 것이 unit 16(카드 원자 트랜잭션)의 목표다 — 그때 DcRejectReason 을
            // receipt 에 실어 UI 의 preflight 미러(WouldDreamcatcherCardApply)를 소거한다.
            return ok ? Ok(c, tick)
                      : CommandReceipt.Rejected(c.ClientSeq, CommandReject.Card_NotInHand);
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
        public IReadOnlyList<SessionEvent> DrainEvents() => _emptyEvents;

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
            _ => CommandReject.Session_InternalError,
        };
    }
}
