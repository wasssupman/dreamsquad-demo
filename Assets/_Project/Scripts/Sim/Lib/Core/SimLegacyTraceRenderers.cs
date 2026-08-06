using System.Text;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-K/2 — **타입별 렌더러.**
    ///
    /// 규칙은 하나다: **구 타입의 public 필드를 이름 ordinal 오름차순으로** `이름=값` 으로 잇고
    /// `구FullName{…}` 으로 감싼다. 구 포매터가 리플렉션 + `CompareOrdinal` 정렬로 하던 일을
    /// 여기서는 **손으로 고정**한다 — 신 타입에 리플렉션을 걸면 타입 이름이 신 것으로 나오고,
    /// sim 어셈블리는 구 타입을 참조할 수 없기 때문이다(<see cref="SimLegacyTrace"/> 참조).
    ///
    /// 손으로 쓴 것이 맞는지는 `SimLegacyTraceContractTests` 가 **구 타입에 리플렉션을 걸어**
    /// 대조한다. 그 테스트는 양쪽 struct 를 **같은 이름-값 표로 채워** 비교하므로, 필드 하나를
    /// 빠뜨리거나 순서를 틀리면 EditMode 에서 즉시 빨개진다(골든은 Play 14 세션이 걸린다).
    /// </summary>
    public static partial class SimLegacyTrace
    {
        // ── 컴포넌트 10 종 (기록기의 `AppendComponent` 순서) ────────────────────

        /// ⚠ `Rotation` 이 없다 — 비교 직전 <see cref="StripExcludedFields"/> 가 구 쪽에서 뗀다.
        public static string TransformValue(in SimTransform v) => KeyLocalTransform
            + "{Position=" + Vec3(v.Position)
            + ",Scale=" + Float(v.Scale) + "}";

        public static string HealthValue(in Health v) => KeyHealth
            + "{max=" + Float(v.max)
            + ",value=" + Float(v.value) + "}";

        public static string FactionTagValue(in FactionTag v) => KeyFactionTag
            + "{value=" + Enum(v.value) + "}";

        public static string KillScoreValue(in KillScore v) => KeyKillScore
            + "{value=" + Int(v.value) + "}";

        public static string DefenderTileValue(in DefenderTile v) => KeyDefenderTile
            + "{cell=" + Int2(v.cell) + "}";

        public static string PathFollowStateValue(in PathFollowState v) => KeyPathFollowState
            + "{speed=" + Float(v.speed) + "}";

        public static string AttackStateValue(in AttackState v) => KeyAttackState
            + "{attackTargetCount=" + Int(v.attackTargetCount)
            + ",committedDirection=" + Vec2(v.committedDirection)
            + ",cooldownDuration=" + Float(v.cooldownDuration)
            + ",cooldownRemaining=" + Float(v.cooldownRemaining)
            + ",hasCommittedDirection=" + Byte(v.hasCommittedDirection)
            + ",hitDelayRemaining=" + Float(v.hitDelayRemaining)
            + ",hitDelaySec=" + Float(v.hitDelaySec)
            + ",range=" + Float(v.range)
            + ",targetMask=" + Int(v.targetMask) + "}";

        public static string ModifierStatsValue(in ModifierStats v) => KeyModifierStats
            + "{attackSpeedMul=" + Float(v.attackSpeedMul)
            + ",damageMul=" + Float(v.damageMul)
            + ",damageVsCcMul=" + Float(v.damageVsCcMul)
            + ",dmgTakenMul=" + Float(v.dmgTakenMul)
            + ",maxHealthMul=" + Float(v.maxHealthMul)
            + ",moveSpeedMul=" + Float(v.moveSpeedMul)
            + ",regenPerSec=" + Float(v.regenPerSec) + "}";

        public static string ProjectileStateValue(in ProjectileState v) => KeyProjectileState
            + "{aoeTargetCap=" + Int(v.aoeTargetCap)
            + ",arcHeight=" + Float(v.arcHeight)
            + ",bombType=" + Byte(v.bombType)
            + ",bounceDamageMul=" + Float(v.bounceDamageMul)
            + ",bounceRemaining=" + Int(v.bounceRemaining)
            + ",bounceTileRange=" + Int(v.bounceTileRange)
            + ",ccDuration=" + Float(v.ccDuration)
            + ",ccKind=" + Byte(v.ccKind)
            + ",control1=" + Vec3(v.control1)
            + ",control2=" + Vec3(v.control2)
            + ",damage=" + Float(v.damage)
            + ",dataIndex=" + Int(v.dataIndex)
            + ",direction=" + Vec2(v.direction)
            + ",elapsed=" + Float(v.elapsed)
            + ",flightTime=" + Float(v.flightTime)
            + ",fuseSec=" + Float(v.fuseSec)
            + ",heavyDamageMul=" + Float(v.heavyDamageMul)
            + ",hitThreshold=" + Float(v.hitThreshold)
            + ",impact=" + Vec3(v.impact)
            + ",impactReached=" + Bool(v.impactReached)
            + ",impactTileRange=" + Int(v.impactTileRange)
            + ",maxDistance=" + Float(v.maxDistance)
            + ",movement=" + Enum(v.movement)
            + ",onHitEffect=" + Enum(v.onHitEffect)
            + ",origin=" + Vec3(v.origin)
            + ",owner=" + Entity(v.owner)
            + ",payload=" + Enum(v.payload)
            + ",pierceRemaining=" + Int(v.pierceRemaining)
            + ",prevPos=" + Vec3(v.prevPos)
            + ",priorityDamageMul=" + Float(v.priorityDamageMul)
            + ",priorityTarget=" + Entity(v.priorityTarget)
            + ",retargetTileRange=" + Int(v.retargetTileRange)
            + ",speed=" + Float(v.speed)
            + ",splashDamageMul=" + Float(v.splashDamageMul)
            + ",splashRadius=" + Float(v.splashRadius)
            + ",target=" + Entity(v.target)
            + ",targetFaction=" + Enum(v.targetFaction) + "}";

        public static string BombLauncherStateValue(in BombLauncherState v) => KeyBombLauncherState
            + "{aoeTargetCap=" + Int(v.aoeTargetCap)
            + ",aoeTileRange=" + Int(v.aoeTileRange)
            + ",arcHeight=" + Float(v.arcHeight)
            + ",dmgBombDamage=" + Float(v.dmgBombDamage)
            + ",fuseSec=" + Float(v.fuseSec)
            + ",landingTiles=" + Int(v.landingTiles)
            + ",rng=" + Random(v.rng)
            + ",sleepSec=" + Float(v.sleepSec)
            + ",stunSec=" + Float(v.stunSec)
            + ",travelSec=" + Float(v.travelSec) + "}";

        /// ⚠ `candidateCells` 는 **내용이 아니라 타입 문자열**이다 — <see cref="ValueCandidateCellsContainer"/>.
        public static string PickupSpawnStateValue(in PickupSpawnState v) => KeyPickupSpawnState
            + "{candidateCells=" + ValueCandidateCellsContainer
            + ",elapsed=" + Float(v.elapsed)
            + ",rng=" + Random(v.rng) + "}";

        // ── 버퍼 10 종 (기록기의 `AppendBuffer` 순서) ──────────────────────────

        /// ⚠ `shots` 는 **내용이 아니라 타입 문자열**이다 — <see cref="ValueShotsContainer"/>.
        public static string PatternSpecValue(in PatternSpec v) => KeyPatternSpec
            + "{barrelDataIndex=" + Int(v.barrelDataIndex)
            + ",damage=" + Float(v.damage)
            + ",maxAngleDeg=" + Float(v.maxAngleDeg)
            + ",minAngleDeg=" + Float(v.minAngleDeg)
            + ",randomIntervalMaxSec=" + Float(v.randomIntervalMaxSec)
            + ",randomIntervalMinSec=" + Float(v.randomIntervalMinSec)
            + ",randomizeShotsPerTrigger=" + Bool(v.randomizeShotsPerTrigger)
            + ",reselectPerShot=" + Bool(v.reselectPerShot)
            + ",selection=" + Enum(v.selection)
            + ",shots=" + ValueShotsContainer
            + ",telegraphSec=" + Float(v.telegraphSec) + "}";

        public static string ProjectileSpawnRequestValue(in ProjectileSpawnRequest v) => KeyProjectileSpawnRequest
            + "{aoeTargetCap=" + Int(v.aoeTargetCap)
            + ",arcHeight=" + Float(v.arcHeight)
            + ",bombType=" + Byte(v.bombType)
            + ",bounceDamageMul=" + Float(v.bounceDamageMul)
            + ",bounceRemaining=" + Int(v.bounceRemaining)
            + ",bounceTileRange=" + Int(v.bounceTileRange)
            + ",ccDuration=" + Float(v.ccDuration)
            + ",ccKind=" + Byte(v.ccKind)
            + ",damage=" + Float(v.damage)
            + ",dataIndex=" + Int(v.dataIndex)
            + ",direction=" + Vec2(v.direction)
            + ",flightTime=" + Float(v.flightTime)
            + ",fuseSec=" + Float(v.fuseSec)
            + ",heavyDamageMul=" + Float(v.heavyDamageMul)
            + ",hitThreshold=" + Float(v.hitThreshold)
            + ",impact=" + Vec3(v.impact)
            + ",impactTileRange=" + Int(v.impactTileRange)
            + ",maxDistance=" + Float(v.maxDistance)
            + ",movement=" + Enum(v.movement)
            + ",onHitEffect=" + Enum(v.onHitEffect)
            + ",origin=" + Vec3(v.origin)
            + ",owner=" + Entity(v.owner)
            + ",payload=" + Enum(v.payload)
            + ",priorityDamageMul=" + Float(v.priorityDamageMul)
            + ",priorityTarget=" + Entity(v.priorityTarget)
            + ",retargetTileRange=" + Int(v.retargetTileRange)
            + ",speed=" + Float(v.speed)
            + ",splashDamageMul=" + Float(v.splashDamageMul)
            + ",splashRadius=" + Float(v.splashRadius)
            + ",swingIndex=" + Int(v.swingIndex)
            + ",target=" + Entity(v.target)
            + ",targetFaction=" + Enum(v.targetFaction)
            + ",visualScale=" + Float(v.visualScale) + "}";

        public static string PatternSlotValue(in PatternSlot v) => KeyPatternSlot
            + "{fireCountBase=" + Int(v.fireCountBase)
            + ",spec=" + PatternSpecValue(v.spec)
            + ",template=" + ProjectileSpawnRequestValue(v.template) + "}";

        public static string CcEffectValue(in CcEffect v) => KeyCcEffect
            + "{kind=" + Enum(v.kind)
            + ",remainingTime=" + Float(v.remainingTime)
            + ",scalar=" + Float(v.scalar)
            + ",tickInterval=" + Float(v.tickInterval)
            + ",tickTimer=" + Float(v.tickTimer)
            + ",vector=" + Vec3(v.vector) + "}";

        public static string DotEffectValue(in DotEffect v) => KeyDotEffect
            + "{element=" + Enum(v.element)
            + ",origin=" + Enum(v.origin)
            + ",remainingTime=" + Float(v.remainingTime)
            + ",scalar=" + Float(v.scalar)
            + ",tickInterval=" + Float(v.tickInterval)
            + ",tickTimer=" + Float(v.tickTimer) + "}";

        public static string ModifierHeaderValue(in ModifierHeader v) => KeyModifierHeader
            + "{origin=" + Enum(v.origin)
            + ",remaining=" + Float(v.remaining)
            + ",source=" + Entity(v.source)
            + ",stackId=" + UShort(v.stackId) + "}";

        public static string StatModifierSlotValue(in StatModifierSlot v) => KeyStatModifierSlot
            + "{header=" + ModifierHeaderValue(v.header)
            + ",magnitude=" + Float(v.magnitude)
            + ",op=" + Enum(v.op)
            + ",stat=" + Enum(v.stat) + "}";

        public static string StackModifierSlotValue(in StackModifierSlot v) => KeyStackModifierSlot
            + "{header=" + ModifierHeaderValue(v.header)
            + ",kind=" + Enum(v.kind)
            + ",lastTriggeredStack=" + Byte(v.lastTriggeredStack)
            + ",maxStack=" + Byte(v.maxStack)
            + ",stackCount=" + Byte(v.stackCount) + "}";

        public static string ThreatEntryValue(in ThreatEntry v) => KeyThreatEntry
            + "{attacker=" + Entity(v.attacker)
            + ",cumulativeDamage=" + Float(v.cumulativeDamage) + "}";

        public static string ShieldSlotValue(in ShieldSlot v) => KeyShieldSlot
            + "{source=" + Entity(v.source)
            + ",value=" + Float(v.value) + "}";

        public static string IncomingDamageValue(in IncomingDamage v) => KeyIncomingDamage
            + "{amount=" + Float(v.amount)
            + ",source=" + Entity(v.source) + "}";

        public static string IncomingHealValue(in IncomingHeal v) => KeyIncomingHeal
            + "{amount=" + Float(v.amount) + "}";

        public static string IncomingShieldValue(in IncomingShield v) => KeyIncomingShield
            + "{amount=" + Float(v.amount)
            + ",source=" + Entity(v.source) + "}";

        // ── 조립 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 구 `BuildLegacyFinalStateCanonical` 의 3 부. **순서가 계약**이다 —
        /// ① 헤더 12 줄 ② `SimEntityId` 오름차순 엔티티 블록 ③ unkeyed `PickupSpawnState`.
        ///
        /// ⚠ 헤더 값은 아직 sim 밖에 산다(웨이브 일정·판정·`GameManager`). 그래서 이 함수는
        /// 그것을 **받는다** — 채우는 책임은 조립 지점(18-K/3)이고, 여기는 형식만 소유한다.
        /// 그 경계를 흐리면 트레이스가 규칙을 소유하기 시작한다.
        /// </summary>
        public static string BuildStateCanonical(SimWorld world, in SimLegacyTraceHeader header)
        {
            var sb = new StringBuilder(32768);
            AppendHeader(sb, in header);
            AppendEntities(sb, world);
            AppendUnkeyedPickupSpawnState(sb, world);
            return sb.ToString();
        }

        public static void AppendHeader(StringBuilder sb, in SimLegacyTraceHeader h)
        {
            // ⚠ `battleClock` 은 double 이고 `cost` 는 float 다 — 구 기록기의 이원화를 그대로 옮긴다
            //   (`RecordTick` 은 cost 를 int 로 쓰는데 해시는 float 다. 통일은 스냅샷 스키마의 몫).
            Line(sb, "battleClock", Double(h.battleClock));
            Line(sb, "nextWaveIndex", Int(h.nextWaveIndex));
            Line(sb, "pendingSpawns", Int(h.pendingSpawns));
            Line(sb, "goals", Int(h.goals));
            Line(sb, "leakPenalty", Int(h.leakPenalty));
            Line(sb, "killScore", Int(h.killScore));
            Line(sb, "running", Bool(h.running));
            Line(sb, "phase", Int(h.phase));
            Line(sb, "timerRemaining", Float(h.timerRemaining));
            Line(sb, "cost", Float(h.cost));
            Line(sb, "simEntityIdCounter", Int(h.simEntityIdCounter));
            Line(sb, "meteorRng", UInt(h.meteorRngState));
        }

        /// <summary>
        /// 엔티티 블록. **추적 엔티티만**(<see cref="SimEntityId.IsInternal"/> 제외) — 구 sim 이
        /// `SimEntityId` 컴포넌트를 가진 엔티티만 쿼리한 것과 같은 집합이다.
        ///
        /// 추적 id 는 생성 순으로 오르므로 <see cref="SimWorld.Entities"/>(생성 순)를 거르면
        /// **이미 오름차순**이다 — 구가 하던 정렬이 여기서는 불필요하다(그 사실 자체가
        /// `SimWorldIdSpaceTests` 의 단정 대상이다).
        ///
        /// ⚠ 부재는 라인을 내지 않고, **빈 버퍼는 낸다**(`[0]=`). 둘은 다른 상태다.
        /// </summary>
        public static void AppendEntities(StringBuilder sb, SimWorld world)
        {
            foreach (SimEntityId e in world.Entities())
            {
                if (e.IsInternal) continue;
                int id = e.SpawnOrdinal;
                EntityOpen(sb, id);

                if (world.Has<AttackUnitTag>(e)) sb.Append("tag=attacker\n");
                if (world.Has<DefenderUnitTag>(e)) sb.Append("tag=defender\n");
                if (world.Has<BossTag>(e)) sb.Append("tag=boss\n");
                if (world.Has<PendingDeployment>(e)) sb.Append("tag=pendingDeployment\n");

                if (world.TryGet(e, out SimTransform xf)) Line(sb, KeyLocalTransform, TransformValue(in xf));
                if (world.TryGet(e, out Health hp)) Line(sb, KeyHealth, HealthValue(in hp));
                if (world.TryGet(e, out FactionTag ft)) Line(sb, KeyFactionTag, FactionTagValue(in ft));
                if (world.TryGet(e, out KillScore ks)) Line(sb, KeyKillScore, KillScoreValue(in ks));
                if (world.TryGet(e, out DefenderTile dt)) Line(sb, KeyDefenderTile, DefenderTileValue(in dt));
                if (world.TryGet(e, out PathFollowState pf)) Line(sb, KeyPathFollowState, PathFollowStateValue(in pf));
                if (world.TryGet(e, out AttackState at)) Line(sb, KeyAttackState, AttackStateValue(in at));
                if (world.TryGet(e, out ModifierStats ms)) Line(sb, KeyModifierStats, ModifierStatsValue(in ms));
                if (world.TryGet(e, out ProjectileState ps)) Line(sb, KeyProjectileState, ProjectileStateValue(in ps));
                if (world.TryGet(e, out BombLauncherState bl)) Line(sb, KeyBombLauncherState, BombLauncherStateValue(in bl));

                AppendBuffer<PatternSlot>(sb, world, e, KeyPatternSlot, PatternSlotValue);
                AppendBuffer<CcEffect>(sb, world, e, KeyCcEffect, CcEffectValue);
                AppendBuffer<DotEffect>(sb, world, e, KeyDotEffect, DotEffectValue);
                AppendBuffer<StatModifierSlot>(sb, world, e, KeyStatModifierSlot, StatModifierSlotValue);
                AppendBuffer<StackModifierSlot>(sb, world, e, KeyStackModifierSlot, StackModifierSlotValue);
                AppendBuffer<ThreatEntry>(sb, world, e, KeyThreatEntry, ThreatEntryValue);
                AppendBuffer<ShieldSlot>(sb, world, e, KeyShieldSlot, ShieldSlotValue);
                AppendBuffer<IncomingDamage>(sb, world, e, KeyIncomingDamage, IncomingDamageValue);
                AppendBuffer<IncomingHeal>(sb, world, e, KeyIncomingHeal, IncomingHealValue);
                AppendBuffer<IncomingShield>(sb, world, e, KeyIncomingShield, IncomingShieldValue);

                EntityClose(sb, id);
            }
        }

        private delegate string Render<T>(in T value);

        private static void AppendBuffer<T>(StringBuilder sb, SimWorld world, SimEntityId e,
                                            string key, Render<T> render) where T : struct
        {
            var buf = world.GetBuffer<T>(e);
            if (buf == null) return;                       // 부재 — 라인 없음
            sb.Append(key).Append('[').Append(buf.Count).Append("]=");
            for (int i = 0; i < buf.Count; i++)
            {
                if (i > 0) sb.Append(';');
                T item = buf[i];
                sb.Append(render(in item));
            }
            sb.Append('\n');
        }

        /// <summary>
        /// `PickupSpawnState` 는 **id 없는 엔티티**에 산다(스포너 싱글턴). 구 기록기가
        /// `SimEntityId` 없는 것만 모아 **문자열 ordinal 정렬** 후 인덱스로 키를 만들었다 —
        /// 엔티티 신원이 없으니 값 자체가 정렬 축인 것이다.
        /// </summary>
        public static void AppendUnkeyedPickupSpawnState(StringBuilder sb, SimWorld world)
        {
            var values = new System.Collections.Generic.List<string>();
            foreach (SimEntityId e in world.With<PickupSpawnState>())
            {
                if (!e.IsInternal) continue;               // 구 = `SimEntityId` 미보유분만
                var v = world.Get<PickupSpawnState>(e);
                values.Add(PickupSpawnStateValue(in v));
            }
            values.Sort(System.StringComparer.Ordinal);
            for (int i = 0; i < values.Count; i++)
                Line(sb, "unkeyed." + KeyPickupSpawnState + "." + Int(i), values[i]);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-K/2 — 상태 해시 **헤더 12 줄**의 입력.
    ///
    /// 아직 sim 이 소유하지 않는 값들이다 — 웨이브 일정·판정·코스트는 18-K/3(P0/P13 흡수)와
    /// 18-L(Bridge 규칙 축출)이 옮긴다. 그때까지 조립 지점이 채워 넣는다.
    /// </summary>
    public struct SimLegacyTraceHeader
    {
        public double battleClock;
        public int nextWaveIndex;
        public int pendingSpawns;
        public int goals;
        public int leakPenalty;
        public int killScore;
        public bool running;
        public int phase;
        public float timerRemaining;
        public float cost;
        /// 구 `_simEntityIdCounter` — <see cref="SimWorld.SpawnedCount"/> 가 그 대응물이다.
        public int simEntityIdCounter;
        public uint meteorRngState;
    }
}
