using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // skill-layer-foundation unit 3/5 — 포트의 **페이크**.
    //
    // 이게 서면 스킬 하나의 동작을 **ECS 월드 없이** 단위 테스트할 수 있다. 오늘
    // 자장가의 skip-rank 선별은 `BossPeriodicTriggerSystem` 733줄 한복판에 있어
    // 테스트가 아예 불가능했다 — bare world 를 세우고 보스를 스폰하고 임계까지 체력을
    // 깎아야 한 줄을 검증할 수 있었다.
    //
    // 페이크가 sim 재구현이 되지 않는 이유: 무거운 판단(밀집·착지·선별)이 전부
    // **순수 코어**(`SkillMath`)라 페이크는 딕셔너리 저장소만 들면 된다.
    public sealed class TestSkillContext : ISkillContext
    {
        public sealed class Unit
        {
            public float3 Position;
            public Faction Faction;
            // 몸 반지름(타일). 어댑터의 `HitRadius` 에 해당 — 광역·사거리 술어가 민코프스키로 더한다.
            // 기본 0 = 점. 몸 걸침을 시험하는 테스트만 켠다.
            public float BodyRadius;
            public float Health = 100f, MaxHealth = 100f;
            public float AttackRange, AttackTargetCount;
            // unit 5b — 「얼마나 다쳤나」(HP+실드합 ÷ 최대HP). 1 = 멀쩡함.
            public float EffectiveHpRatio = 1f;
            public bool HasHealth = true;
            public byte TraversalLayers;
            // 공격 층 마스크(=이 유닛이 때릴 수 있는 층). `TraversalLayers`(다니는 층)와
            // **다른 축**이다 — 겸직시키면 「지상 유닛은 지상만 때린다」가 우연히 성립해
            // 게이트 결함이 안 보인다.
            public byte AttackTraversalLayers = 0xFF;
            public bool Dead, Pending, InUltimateLeap;
            // 부재를 표현할 수 있어야 한다 — `Position()` 은 없는 자리를 0 으로 접기 때문에,
            // 「위치를 모르면 조준도 못 한다」 규칙을 이 축 없이는 시험할 수 없다.
            public bool HasPosition = true;
            // 조준. 없으면 최근접 폴백으로 흐른다.
            public bool HasFacing;
            // 가디언 표식 — 도발 캐스터 자격.
            public bool HasAggroCapacity;
            // `RequireDamageable` 축. 기본 true — 대부분의 후보는 때릴 수 있다.
            public bool CanReceiveDamage = true;
            public bool IsPathFollowing = true;
            public float2 Facing;
            // 실드 축 — 기본 true 다. 실제 유닛은 대부분 버퍼를 갖고, false 가 기본이면
            // 테스트가 조용히 vacuous 해진다(모든 대상이 건너뛰어진다).
            public bool HasShield = true;
            public readonly System.Collections.Generic.Dictionary<int, float> ShieldFromSource
                = new System.Collections.Generic.Dictionary<int, float>();
        }

        // 인터페이스 멤버다(unit 3f) — 필드로 두면 구현으로 안 쳐준다.
        public float TileSize { get; set; } = 1f;
        public readonly Dictionary<int, Unit> Units = new Dictionary<int, Unit>();
        public readonly List<SimIntent> SimIntents = new List<SimIntent>();
        public readonly List<MetaIntent> MetaIntents = new List<MetaIntent>();

        public SkillEntityId Add(int id, float3 pos, Faction faction, System.Action<Unit> tweak = null)
        {
            var u = new Unit { Position = pos, Faction = faction };
            tweak?.Invoke(u);
            Units[id] = u;
            return new SkillEntityId(id);
        }

        private Unit Get(SkillEntityId id) => Units.TryGetValue(id.Value, out var u) ? u : null;

        public float3 Position(SkillEntityId id) => Get(id)?.Position ?? float3.zero;
        public int2 CellOf(SkillEntityId id) => CellOfPosition(Position(id));
        public int2 CellOfPosition(float3 w) => new int2((int)math.floor(w.x / TileSize), (int)math.floor(w.z / TileSize));
        public float3 CellCenter(int2 c) => new float3((c.x + 0.5f) * TileSize, 0f, (c.y + 0.5f) * TileSize);
        public bool TryFacing(SkillEntityId id, out float2 dirXZ)
        {
            var u = Get(id);
            dirXZ = u != null && u.HasFacing ? u.Facing : default;
            return u != null && u.HasFacing;
        }

        public Faction FactionOf(SkillEntityId id) => Get(id)?.Faction ?? Faction.None;
        public float Health(SkillEntityId id) => Get(id)?.Health ?? 0f;
        public float MaxHealth(SkillEntityId id) => Get(id)?.MaxHealth ?? 0f;
        public byte TraversalLayers(SkillEntityId id) => Get(id)?.TraversalLayers ?? (byte)0;
        public float ShieldValueFrom(SkillEntityId t, SkillEntityId s)
        {
            var u = Get(t);
            return u != null && u.ShieldFromSource.TryGetValue(s.Value, out var v) ? v : 0f;
        }

        public float Stat(SkillEntityId id, UnitStat stat)
        {
            var u = Get(id);
            if (u == null) return 0f;
            switch (stat)
            {
                case UnitStat.AttackRange: return u.AttackRange;
                case UnitStat.AttackTargetCount: return u.AttackTargetCount;
                case UnitStat.TargetTraversalLayers: return u.TraversalLayers;
                case UnitStat.EffectiveHpRatio: return u.EffectiveHpRatio;
                default: return 0f;
            }
        }

        public bool Has(SkillEntityId id, UnitPredicate pred)
        {
            var u = Get(id);
            if (u == null) return false;
            switch (pred)
            {
                case UnitPredicate.Alive: return !u.Dead;
                case UnitPredicate.PendingDeployment: return u.Pending;
                case UnitPredicate.HasShieldBuffer: return u.HasShield;
                case UnitPredicate.InUltimateLeap: return u.InUltimateLeap;
                case UnitPredicate.HasPosition: return u.HasPosition;
                case UnitPredicate.HasAggroCapacity: return u.HasAggroCapacity;
                case UnitPredicate.CanReceiveDamage: return u.CanReceiveDamage;
                case UnitPredicate.IsPathFollowing: return u.IsPathFollowing;
                // ⚠ **미구현은 어댑터와 같은 모양으로 던진다**(리뷰 M2).
                // `false` 로 조용히 답하면 페이크가 어댑터보다 **관대**해져서,
                // 어댑터가 `NotSupportedException` 을 던지는 술어를 쓰는 concrete 가
                // EditMode 에선 초록인데 라이브에선 그 발동만 통째로 버려진다
                // (디스패처가 예외를 삼키고 로그만 남긴다). 페이크가 관대한 것이
                // 이 레이어에서 가장 비싼 실패 유형이다.
                default: throw new System.NotSupportedException(
                    $"TestSkillContext: Has({pred}) 미구현 — 어댑터도 함께 채워라");
            }
        }

        public int Opponents(CasterRef caster, float3 center, int tileRange,
                             CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
            => Collect(FactionRelation.OpponentUnitsOf(caster.Faction), caster, center, tileRange, filter, metric, into);

        public int Allies(CasterRef caster, float3 center, int tileRange,
                          CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
            => Collect(FactionRelation.AllyUnitsOf(caster.Faction), caster, center, tileRange, filter, metric, into);

        private int Collect(Faction wanted, CasterRef caster, float3 center, int tileRange,
                            CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
        {
            if (wanted == Faction.None) return 0;
            int n = 0;
            // 결정론 — 딕셔너리 순서에 기대지 않도록 id 오름차순으로 훑는다.
            var ids = new List<int>(Units.Keys);
            ids.Sort();
            foreach (int id in ids)
            {
                if (n >= into.Length) break;
                var u = Units[id];
                if ((u.Faction & wanted) == 0) continue;
                if ((filter & CandidateFilter.ExcludeSelf) != 0 && id == caster.Unit.Value) continue;
                if ((filter & CandidateFilter.ExcludeDead) != 0 && u.Dead) continue;
                if ((filter & CandidateFilter.ExcludePendingDeployment) != 0 && u.Pending) continue;
                // 어댑터가 거르는 축은 페이크도 거른다(리뷰 M2 — 양방향 대칭).
                if ((filter & CandidateFilter.RequireDamageable) != 0 && !u.CanReceiveDamage) continue;
                if ((filter & CandidateFilter.RequireHealth) != 0 && !u.HasHealth) continue;
                if ((filter & CandidateFilter.ExcludeInUltimateLeap) != 0 && u.InUltimateLeap) continue;
                // ⚠ 어댑터가 거르는 것을 이 페이크도 거른다. 한쪽만 걸면 도메인 테스트가
                // 초록인데 라이브에서 «못 때리는 층» 이 총구를 가져간다.
                if ((filter & CandidateFilter.MatchTraversalLayers) != 0)
                {
                    var host = Get(caster.Unit);
                    byte hostLayers = host?.AttackTraversalLayers ?? (byte)0;
                    if (!Wassup.Data.PlacementLayers.CanTarget(hostLayers, u.TraversalLayers)) continue;
                }

                // 판정 본체는 어댑터(`EcsSkillContext.Collect`)와 **같은 함수**를 부른다 — 페이크가
                // 자를 재구현하면 도메인 테스트가 초록인데 라이브가 다른 갈림이 생긴다(attach-range-preview 0a).
                // 입력은 타일 단위(`/ TileSize`) — `TileSize ≠ 1` 픽스처에서도 반경이 타일로 읽힌다.
                float dxT = (u.Position.x - center.x) / TileSize, dzT = (u.Position.z - center.z) / TileSize;
                bool inRange = metric == RangeMetric.Euclidean
                    ? SkillMath.InBodyReach(dxT, dzT, tileRange, 0f, u.BodyRadius)
                    : SkillMath.InBodyReach(dxT, dzT, tileRange, SkillMath.CellHalfWidthTiles, u.BodyRadius);
                if (inRange) into[n++] = new SkillEntityId(id);
            }
            return n;
        }

        // ⚠ **실제로 센다.** 예전엔 `false` 를 돌려주는 스텁이었고, 그 탓에 도약 계열
        // concrete 가 첫 줄에서 빠져나가 **토대 spec 의 검증 질문에 증인이 없었다**
        // (「방어유닛이 이 스킬을 쓰면 상대 진영 밀집 셀로 간다」). 페이크가 막고 있으면
        // 그 문장은 구조적으로만 참이고 아무도 확인하지 않는다.
        //
        // 진영은 **호출자 상대**로 고른다 — 그게 이 질문의 전부다. 동률은 셀 좌표
        // 오름차순으로 끊는다(결정론).
        public bool TryDensestOpponentCluster(CasterRef c, int r, out int2 cell, out int count)
        {
            cell = default; count = 0;
            var wanted = FactionRelation.OpponentUnitsOf(c.Faction);
            if (wanted == Faction.None) return false;

            var tally = new Dictionary<int2, int>();
            foreach (var kv in Units)
            {
                var u = kv.Value;
                if (u.Dead || (u.Faction & wanted) == 0) continue;
                var uc = CellOfPosition(u.Position);
                tally.TryGetValue(uc, out var n);
                tally[uc] = n + 1;
            }
            if (tally.Count == 0) return false;

            bool found = false;
            foreach (var kv in tally)
            {
                // 반경은 «그 셀 주변 r 칸» 의 합으로 세는 게 라이브 규칙이지만, 이 페이크는
                // 셀 단위 밀집만 본다 — 검증 질문이 묻는 것은 «어느 진영인가» 이지
                // 밀집 산식이 아니다(산식은 어댑터가 소유한다).
                bool better = !found || kv.Value > count
                    || (kv.Value == count && (kv.Key.y < cell.y
                        || (kv.Key.y == cell.y && kv.Key.x < cell.x)));
                if (!better) continue;
                cell = kv.Key; count = kv.Value; found = true;
            }
            return found;
        }

        // 원하는 칸이 곧 착지 칸이다 — 이 페이크엔 막힌 칸이 없다.
        public bool TryLandingCellNear(int2 d, int maxRing, out int2 cell) { cell = d; return true; }

        // 발사 명세. 테스트가 답을 직접 정한다 — 도메인은 결론만 쓰기 때문에
        // 페이크가 템플릿을 흉내낼 이유가 없다.
        public readonly Dictionary<int, PatternAimNeed> PatternAim = new Dictionary<int, PatternAimNeed>();
        public PatternAimNeed AimNeedOfPattern(SkillEntityId host, int patternIndex)
            => PatternAim.TryGetValue(patternIndex, out var v) ? v : PatternAimNeed.Missing;

        public void Emit(in SimIntent intent) => SimIntents.Add(intent);
        public void Emit(in MetaIntent intent) => MetaIntents.Add(intent);
    }
}
