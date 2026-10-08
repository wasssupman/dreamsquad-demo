using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Effects
{
    // battle-core-rebuild unit 6a2 — **그 시전자가 쏘는 모든 탄이 나를 싣는다.**
    //
    // 사용자 결정 2026-09-24 ①. 옛 전투는 착탄 출력(스탯·스택 부여 — 킨들러 화염,
    // 난도질꾼 출혈)을 **평타 팔 안**에서만 주입해 포물선탄·카드탄·배치 스킬탄은 원천
    // 배제했다(`dreamcatcher-attack-mod-bounce` README 가 그 배제를 계약으로 적었다).
    // 여기 있는 것은 그 배제를 푸는 **한 벌의 슬롯**이고, 접는 자리는 관문 하나
    // (`TickProjectilePhase.SpawnRequested`)뿐이다.
    //
    // ⚠ **어휘를 늘리지 않는다**(사용자 결정 ③). 「화염 부여」는 `ApplyStack(Fire)` 이고
    // 「출혈 부여」는 `ApplyStack(Bleed)` 다 — 새 enum 이 없다. 이 파일이 새로 여는 것은
    // **적용 범위**(모든 탄)와 **겹침 규칙**(합·상한)뿐이다.
    //
    // ⚠ 생산자(카드·스킬)는 unit 7 이다. 여기서는 슬롯·부여·회수·상한만 선다.

    /// <summary>
    /// 부여가 무엇을 얹나. **`AttackOutputDef`·`CcRequestKind` 의 부분집합이지 새 어휘가
    /// 아니다** — 피해·회복은 부여 대상이 아니라 키가 없다(`None`).
    /// ⚠ append-only(정의표 저작과 골든이 이 번호를 읽는다).
    /// </summary>
    public enum ImbueKind : byte
    {
        None = 0,
        ApplyStat = 1,
        ApplyStack = 2,
        /// <summary>군중 제어. 적용은 `Projectile.OnHitCc` 를 통해 `RequestCc` 로 간다.</summary>
        Cc = 3,
    }

    /// <summary>
    /// 슬롯을 가르는 **「무엇을·어디에」**. 출처는 여기 없다 — 키와 짝이 되어 슬롯을 만든다
    /// (`ImbueSlot.Source`). 그래야 회수가 **출처 단위**로 가능하다(카드 한 장만 떼기).
    ///
    /// ⚠ `ApplyStat` 만 두 축(`Stat`, `Op`)이다. `Op` 를 접으면 「가산 공속 부여」와
    /// 「곱셈 공속 부여」가 한 칸을 다퉈, 합산이 뜻을 잃는다(6a 의 `SlotTag` 가 판별자를
    /// 접으면 안 되는 것과 같은 이유다).
    /// </summary>
    public readonly struct ImbueKey : IEquatable<ImbueKey>
    {
        public readonly ImbueKind Kind;

        /// <summary>`ApplyStat` = `StatKind` · `ApplyStack` = `StackKind` · `Cc` = `CcRequestKind`.</summary>
        public readonly byte Target;

        /// <summary>`ApplyStat` = `CombineOp`. 나머지는 0.</summary>
        public readonly byte Op;

        public ImbueKey(ImbueKind kind, byte target, byte op = 0)
        {
            Kind = kind;
            Target = target;
            Op = op;
        }

        public static ImbueKey Stat(StatKind stat, CombineOp op)
            => new ImbueKey(ImbueKind.ApplyStat, (byte)stat, (byte)op);

        public static ImbueKey Stack(StackKind kind)
            => new ImbueKey(ImbueKind.ApplyStack, (byte)kind);

        public static ImbueKey Cc(CcRequestKind kind)
            => new ImbueKey(ImbueKind.Cc, (byte)kind);

        public bool IsNone => Kind == ImbueKind.None;

        /// <summary>
        /// 저작 착탄 출력 한 줄의 키. **피해·회복은 키가 없다**(`None`) — 부여로 겹칠 축이
        /// 아니고, 피해는 발사 시점에 `ProjectileRequest.Damage` 로 이미 접혔다.
        /// </summary>
        public static ImbueKey OfOutput(in AttackOutputDef o)
        {
            switch (o.Kind)
            {
                case AttackOutputKind.ApplyStat:
                    return new ImbueKey(ImbueKind.ApplyStat, (byte)o.Stat, (byte)o.Op);
                case AttackOutputKind.ApplyStack:
                    return new ImbueKey(ImbueKind.ApplyStack, (byte)o.StackKind);
                default:
                    return default;
            }
        }

        /// <summary>
        /// 사건 한 줄의 `i` 칸에 싣는 십진 포장(`DotElementMap.PackArg` 선례). 눈이
        /// `20100` 을 「스택 부여·불」로 바로 읽는다.
        /// </summary>
        public int Pack() => (int)Kind * 10000 + Target * 100 + Op;

        public static ImbueKey Unpack(int packed)
            => new ImbueKey((ImbueKind)(packed / 10000), (byte)(packed / 100 % 100), (byte)(packed % 100));

        public bool Equals(ImbueKey other)
            => Kind == other.Kind && Target == other.Target && Op == other.Op;

        public override bool Equals(object obj) => obj is ImbueKey o && Equals(o);

        public override int GetHashCode() => ((int)Kind << 16) | (Target << 8) | Op;

        public static bool operator ==(ImbueKey a, ImbueKey b) => a.Equals(b);

        public static bool operator !=(ImbueKey a, ImbueKey b) => !a.Equals(b);
    }

    public struct ImbueSlot
    {
        /// <summary>건 쪽(카드 인스턴스·스킬 호스트). **회수의 축**이다.</summary>
        public SimEntityId Source;
        public ImbueKey Key;

        /// <summary>같은 (출처, 키)가 다시 오면 **더한다**(사용자 결정 ②). 상한은 관문이 건다.</summary>
        public float Magnitude;

        /// <summary>한 발이 나르는 지속(초). 겹치면 **긴 쪽**이다(6a `ModifierSet` 과 같은 규율).</summary>
        public float Seconds;
    }

    /// <summary>
    /// 한 시전자에게 걸린 **탄 부여 슬롯들**. 6a 의 `ModifierSet` 과 같은 규율이다:
    /// 부여는 큐가 아니라 함수이고, **회수는 슬롯 삭제**다(항등값 재발행이 아니다).
    ///
    /// ⚠ **수명이 없다.** 슬롯은 시간으로 사라지지 않고 회수로만 사라진다 — 부여는
    /// 「이 유닛의 공격이 어떤 성질을 갖는가」이지 그 유닛에게 걸린 효과가 아니기 때문이다.
    /// (`Seconds` 는 슬롯의 수명이 아니라 **탄이 맞은 놈에게 거는 효과의 길이**다.)
    /// </summary>
    public sealed class ProjectileImbueSet
    {
        private readonly List<ImbueSlot> _slots = new List<ImbueSlot>(2);

        /// <summary>삽입 순서. 순회는 이 순서이고 출처 id 를 키로 쓰지 않는다(결정론).</summary>
        public IReadOnlyList<ImbueSlot> Slots => _slots;

        public int Count => _slots.Count;

        public bool Any => _slots.Count > 0;

        /// <summary>
        /// 한 슬롯을 걸거나 더한다. 반환 = **새 슬롯이 생겼나**.
        /// 같은 (출처, 키)면 크기는 **합**, 지속은 **긴 쪽**이다.
        /// </summary>
        public bool Grant(SimEntityId source, in ImbueKey key, float magnitude, float seconds)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Source != source || _slots[i].Key != key) continue;
                var slot = _slots[i];
                slot.Magnitude += magnitude;
                slot.Seconds = math.max(slot.Seconds, seconds);
                _slots[i] = slot;
                return false;
            }

            _slots.Add(new ImbueSlot
            {
                Source = source,
                Key = key,
                Magnitude = magnitude,
                Seconds = seconds,
            });
            return true;
        }

        /// <summary>
        /// 그 출처가 건 것을 **통째로 지운다**. 반환 = 지운 수.
        /// 지운 슬롯을 `removed` 에 삽입 순서대로 담는다 — 회수 사건의 순서가 순회 순서에
        /// 매이지 않게 하는 것이 그 값이다(6a `ModifierSet.Expire` 와 같은 규율).
        /// </summary>
        public int Revoke(SimEntityId source, List<ImbueSlot> removed = null)
        {
            int n = 0;
            for (int i = 0; i < _slots.Count;)
            {
                if (_slots[i].Source == source)
                {
                    removed?.Add(_slots[i]);
                    _slots.RemoveAt(i);
                    n++;
                }
                else i++;
            }
            return n;
        }

        /// <summary>그 (출처, 키) 하나만 지운다. 반환 = 지웠나.</summary>
        public bool RevokeKey(SimEntityId source, in ImbueKey key)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Source != source || _slots[i].Key != key) continue;
                _slots.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>그 키에 걸린 것의 **합**(출처를 가리지 않는다). 상한은 관문이 건다.</summary>
        public float SumOf(in ImbueKey key)
        {
            float sum = 0f;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Key == key) sum += _slots[i].Magnitude;
            return sum;
        }

        /// <summary>그 키의 지속 — **긴 쪽**이다.</summary>
        public float SecondsOf(in ImbueKey key)
        {
            float best = 0f;
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Key == key && _slots[i].Seconds > best) best = _slots[i].Seconds;
            return best;
        }

        public void Reset() => _slots.Clear();
    }

    /// <summary>
    /// 부여의 **관문 함수들**. 6a 의 `EffectApply` 와 같은 규율이다 — 슬롯을 고치는 것은
    /// 순수 자료구조(`ProjectileImbueSet`)이고, 사건을 내고 상한을 확인하는 것이 여기다.
    ///
    /// ⚠ **상한이 없는 키는 부여 자체를 거절한다**(제약 6). 「정의표에 값이 없다」가
    /// 「상한이 없다」로 읽히면 그 순간 저작 없는 무한 부여가 조용히 성립한다.
    /// </summary>
    public static class ImbueGate
    {
        /// <summary>
        /// 정의표에서 그 키의 상한을 찾는다. 반환 = 찾았나.
        /// 줄은 **키 하나에 하나**이고, 빌더가 중복·비정상 값을 걸러 loud 하게 거절한다.
        /// </summary>
        public static bool TryCapOf(ImbueCapDef[] caps, in ImbueKey key, out float cap)
        {
            if (caps != null)
                for (int i = 0; i < caps.Length; i++)
                {
                    ref var c = ref caps[i];
                    if (c.Kind != (int)key.Kind || c.Target != key.Target) continue;
                    // `Op` 는 `ApplyStat` 만 쓴다 — 나머지 종류는 저작에서도 0 이다.
                    if (key.Kind == ImbueKind.ApplyStat && c.Op != key.Op) continue;
                    cap = c.Cap;
                    return true;
                }
            cap = 0f;
            return false;
        }

        /// <summary>
        /// 시전자에게 부여를 건다. 반환 = **걸렸나**(상한 저작이 없으면 거절).
        /// 사건의 `Amount` 는 **한 발이 실제로 나를 값**이다(합을 상한으로 접은 것).
        /// </summary>
        public static bool Grant(TickContext ctx, Unit owner, SimEntityId source,
                                 in ImbueKey key, float magnitude, float seconds)
        {
            if (owner == null || owner.Dead || key.IsNone) return false;
            if (!TryCapOf(ctx.Def.ImbueCaps, in key, out float cap))
            {
                // C4 — **조용한 무동작 금지.** 저작이 없으면 부여가 안 걸린다는 사실을 말한다.
                ctx.Warn("[Imbue] 부여 상한 저작이 없다 — 이 키는 걸리지 않는다(키 "
                         + key.Pack().ToString(System.Globalization.CultureInfo.InvariantCulture) + ").");
                return false;
            }

            owner.Imbue = owner.Imbue ?? ctx.World.Parts.RentImbue();
            owner.Imbue.Grant(source, in key, magnitude, seconds);
            ctx.Bus.Publish(CoreEvent.ImbueChanged(ctx.Tick, owner, source, key,
                                                   math.min(cap, owner.Imbue.SumOf(in key))));
            return true;
        }

        /// <summary>그 출처가 건 것을 통째로 회수한다. 반환 = 지운 수. 키마다 사건 1건.</summary>
        public static int Revoke(TickContext ctx, Unit owner, SimEntityId source, List<ImbueSlot> scratch)
        {
            if (owner?.Imbue == null) return 0;
            scratch.Clear();
            int n = owner.Imbue.Revoke(source, scratch);
            for (int i = 0; i < scratch.Count; i++)
            {
                var key = scratch[i].Key;
                float sum = owner.Imbue.SumOf(in key);
                if (TryCapOf(ctx.Def.ImbueCaps, in key, out float cap)) sum = math.min(cap, sum);
                ctx.Bus.Publish(CoreEvent.ImbueChanged(ctx.Tick, owner, source, key, sum));
            }
            return n;
        }

        /// <summary>
        /// 부여 한 칸을 **착탄 출력 한 줄**로 편다. `Cc` 는 여기 오지 않는다 —
        /// 그쪽은 출력 표가 아니라 `Projectile.OnHitCc` 로 간다(`RequestCc` 가 문이다).
        /// </summary>
        public static AttackOutputDef ToOutput(in ImbueKey key, float magnitude, float seconds)
        {
            switch (key.Kind)
            {
                case ImbueKind.ApplyStat:
                    return new AttackOutputDef
                    {
                        Kind = AttackOutputKind.ApplyStat,
                        Stat = key.Target,
                        Op = key.Op,
                        Magnitude = magnitude,
                        Duration = seconds,
                    };
                case ImbueKind.ApplyStack:
                    return new AttackOutputDef
                    {
                        Kind = AttackOutputKind.ApplyStack,
                        StackKind = key.Target,
                        Magnitude = magnitude,
                        Duration = seconds,
                    };
                default:
                    return default;
            }
        }
    }
}
