using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;

namespace Wassup.BattleCore.Trigger
{
    // battle-core-rebuild unit 7b — **사직서 임계 → 운석 barrage**(← 옛 `BattleBridge.DrainMeteorBarrageRequests`).
    //
    // 임계 도달은 6b2 가 사건(`ResignationThreshold` — 임계마다 1건, 한 틱 여러 건이 사양)으로 낸다. 이 담당자는 그 사건의
    // **소비자**다: 이동(Walk) 칸 중 겹치지 않게 `MeteorCount` 곳을 골라 하늘 낙하 × 칸 광역 탄을 **순차 예고**로 떨어뜨린다
    // (적 피해 · 착탄 간격 = 예고 + k × 시차). 판정(어느 칸)과 발사가 전부 여기고, 쓰기는 스킬 경로와 같은 표면
    // (`IntentApplier` — 하늘 낙하 × 칸 광역 · 원점 몸 0 = 자리형)을 지난다.
    //
    // 매니저가 아니다: 사직서 개체·장수는 `BattleWorld`, 임계 판정은 `TickProjectilePhase`, 이 파일은 **운석 한 가지 규칙**만 든다.
    //
    // 결정론 — 칸 난수는 `RngStreams.Meteor` 하나(6b2 가 계열을 나눈 이유: 픽업 호출 횟수가 바뀌어도 운석 자리가 안 밀린다).
    // ⚠ 사건은 틱 끝 배달이라 탄은 **다음 틱**에 선다 — 옛 경로도 브리지 드레인이 다음 프레임에 쐈다(같은 한 틱).
    public sealed class ResignationBarrage
    {
        /// <summary>한 발의 빈 칸 재시도 상한(옛 값 — 밸런스가 아니라 겹침 회피의 탐색 예산).</summary>
        private const int PickAttempts = 8;

        private readonly BattleWorld _world;
        private readonly MatchDefinition _def;
        private readonly Map.MapRuntime _map;
        private readonly GimmickHost _gimmick;
        private readonly RngStreams _rng;
        private readonly IntentApplier _intents;

        private readonly List<int2> _walk = new List<int2>(64);
        private readonly HashSet<int2> _chosen = new HashSet<int2>();
        private bool _walkBuilt;

        public System.Action<string> Report;

        public ResignationBarrage(EventBus bus, BattleWorld world, MatchDefinition def, Map.MapRuntime map,
                                  GimmickHost gimmick, RngStreams rng, IntentApplier intents)
        {
            _world = world;
            _def = def;
            _map = map;
            _gimmick = gimmick;
            _rng = rng;
            _intents = intents;
            bus.Subscribe(CoreEventKind.ResignationThreshold, EventOrder.Hand, OnThreshold);
        }

        private void OnThreshold(CoreEvent e)
        {
            if (_gimmick == null || !_gimmick.TryActive(GimmickKind.ClockOut, out var g)) return;
            ref var co = ref g.ClockOut;
            if (co.MeteorProjectileDefIndex < 0 || co.MeteorProjectileDefIndex >= _def.Projectiles.Length)
            {
                Report?.Invoke("[Barrage] 퇴근 기믹의 운석 탄이 지정되지 않았다 — 운석 barrage 를 떨어뜨리지 않는다.");
                return;
            }
            BuildWalk();
            if (_walk.Count == 0) return;

            int shots = math.min(e.Arg, _walk.Count);
            _chosen.Clear();
            int landed = 0;
            _intents.Begin(null, Faction.DefenderUnit, null);   // 시전자 없음 — 진영은 플레이어(적 피해, 옛 `targetFaction = Enemy`)
            try
            {
                for (int s = 0; s < shots; s++)
                {
                    bool found = false;
                    int2 cell = default;
                    for (int attempt = 0; attempt < PickAttempts; attempt++)
                    {
                        var c = _walk[_rng.Meteor.NextInt(0, _walk.Count)];
                        if (_chosen.Contains(c)) continue;
                        cell = c; found = true; break;
                    }
                    if (!found) continue;
                    _chosen.Add(cell);
                    _intents.Apply(new SimIntent
                    {
                        Kind = SimIntentKind.SpawnProjectile,
                        Source = SkillEntityId.None,
                        Target = SkillEntityId.None,                 // 칸을 때린다
                        Position = _map.CenterOf(cell),
                        Amount = co.MeteorDamage,
                        TileRange = co.MeteorTileRange,
                        DataIndex = co.MeteorProjectileDefIndex,
                        Duration = co.MeteorWarningSec + landed * co.MeteorStaggerSec,   // 순차 착탄
                        Telegraph = false,                           // 옛 barrage 는 예고 표식 반경을 안 실었다
                        TargetTraversalLayers = 0,
                    });
                    landed++;
                }
            }
            finally { _intents.End(); }
        }

        // 이동 칸 목록 — 맵은 판 수명 동안 칸 종류가 안 바뀐다(장애물은 칸 종류가 아니다). 한 번 모은다.
        private void BuildWalk()
        {
            if (_walkBuilt) return;
            _walkBuilt = true;
            var snap = _map.Snapshot;
            for (int i = 0; i < snap.Tiles.Length; i++)
                if (snap.Tiles[i] == Map.MapTile.Walk) _walk.Add(new int2(i % snap.Width, i / snap.Width));
        }
    }
}
