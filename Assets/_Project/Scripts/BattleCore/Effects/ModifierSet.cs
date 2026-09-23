using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Wassup.BattleCore.Effects
{
    // battle-core-rebuild unit 6a — 한 개체에 걸린 **스탯 슬롯들**.
    //
    // ⚠ **부여는 큐가 아니라 관문 함수다**(구현 1). 옛 3채널(`StatModifierApplyEvents` 외)은
    // Entities 산물이라 안 옮긴다 — 생산자가 `unit.Modifiers.Apply(...)` 를 그 자리에서 부른다.
    // 그렇다고 「즉시 반영」이 되는 것은 아니다: **반영 시점은 단계 위치가 계승한다**(F29).
    // 생산자 단계가 소비자 단계보다 뒤면 그 효과는 여전히 다음 틱에 든다.
    //
    // ⚠ **회수는 슬롯 삭제다**(F27·F28·F33). 옛 회수는 「항등값 재발행」이라 ⑴ 강제 고정에
    // 항등이 없고 ⑵ 상한을 실으면 조용히 실패하며 ⑶ 효과 타일은 회수가 없어 개체당 1회로
    // 봉인됐다. `Revoke` 한 함수가 셋을 한꺼번에 푼다.

    /// <summary>
    /// 슬롯을 가르는 **4축**. 옛 `(source, stat, op, stackId)` 의 후계이고, 넷째가
    /// 전역 번호판에서 `SlotTag`(종류, 판별자)로 바뀌었다(F26 · 구현 3).
    /// </summary>
    public readonly struct ModifierKey : IEquatable<ModifierKey>
    {
        public readonly SimEntityId Source;
        public readonly StatKind Stat;
        public readonly CombineOp Op;
        public readonly SlotTag Tag;

        public ModifierKey(SimEntityId source, StatKind stat, CombineOp op, SlotTag tag)
        {
            Source = source;
            Stat = stat;
            Op = op;
            Tag = tag;
        }

        /// <summary>일반 칸(옛 `stackId = 0`).</summary>
        public static ModifierKey Of(SimEntityId source, StatKind stat, CombineOp op)
            => new ModifierKey(source, stat, op, SlotTag.Default);

        public bool Equals(ModifierKey other)
            => Source == other.Source && Stat == other.Stat && Op == other.Op && Tag == other.Tag;

        public override bool Equals(object obj) => obj is ModifierKey o && Equals(o);

        public override int GetHashCode()
            => (((Source.Value * 397) ^ ((int)Stat << 4)) ^ ((int)Op << 2)) ^ Tag.GetHashCode();
    }

    public struct ModifierSlot
    {
        public ModifierKey Key;
        public float Magnitude;

        /// <summary>남은 시간(초). `float.PositiveInfinity` = 영구(만료 경로를 자연 통과).</summary>
        public float Remaining;

        /// <summary>꼬리표. **병합 키가 아니다** — 오라 판정·로그가 읽는다.</summary>
        public ModifierOrigin Origin;
    }

    public sealed class ModifierSet
    {
        private const int StatCount = 7;

        private readonly List<ModifierSlot> _slots = new List<ModifierSlot>(4);

        // 접기 스크래치 — 틱 중 할당 0. 개체마다 한 벌이고 `Fold` 안에서만 쓴다.
        private readonly float[] _add = new float[StatCount];
        private readonly float[] _mul = new float[StatCount];
        private readonly float[] _over = new float[StatCount];
        private readonly bool[] _hasOver = new bool[StatCount];

        private EffectiveStats _cached = EffectiveStats.Identity;

        /// <summary>
        /// 슬롯이 바뀐 뒤 아직 안 접혔나. **만료도 이것을 켠다**(F21) — 안 켜서 만료가
        /// 영원히 안 돌고 모디파이어가 무한 지속된 이력이 있다.
        /// </summary>
        private bool _dirty;

        public IReadOnlyList<ModifierSlot> Slots => _slots;

        public int Count => _slots.Count;

        public bool Any => _slots.Count > 0;

        /// <summary>
        /// 접힌 값. **읽는 자리에서 늦게 접는다**(구현 5) — 슬롯이 바뀐 뒤 처음 읽을 때
        /// 한 번만 재계산한다. 읽어 두고 나중에 쓰지 말 것(그 사이의 만료가 안 보인다).
        /// </summary>
        public EffectiveStats Effective
        {
            get
            {
                if (_dirty) Fold();
                return _cached;
            }
        }

        /// <summary>
        /// 한 슬롯을 걸거나 갱신한다. 반환 = **새 슬롯이 생겼나**.
        ///
        /// 갱신 규칙: 남은 시간은 긴 쪽, 크기는 덮어쓰기. `cap > 0` 이면 크기가 덮어쓰기가
        /// 아니라 **누적**이 된다(`min(cap, 기존 + 새 값)`) — 광란(공격마다 공속이 쌓임)이
        /// 서는 축이다.
        ///
        /// ⚠ **상한은 크기만 막고 남은 시간은 안 막는다.** 둘을 같이 막으면 최대 중첩에
        /// 도달한 «가장 뜨거운 지점»에서 버프가 스스로 꺼진다.
        /// ⚠ 상한은 **신규 슬롯 경로에도** 건다(F4). 옛 클램프는 갱신 경로에만 있어 신규
        /// 슬롯 2경로로 샜다 — 「상한은 언제나 1회분 이상」이라는 전제가 깨지면 그대로 드러난다.
        /// </summary>
        public bool Apply(in ModifierKey key, float magnitude, float seconds,
                          float cap = 0f, ModifierOrigin origin = ModifierOrigin.Unspecified)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].Key.Equals(key)) continue;

                var slot = _slots[i];
                slot.Remaining = math.max(slot.Remaining, seconds);
                slot.Magnitude = cap > 0f ? math.min(cap, slot.Magnitude + magnitude) : magnitude;
                slot.Origin = origin;
                _slots[i] = slot;
                _dirty = true;
                return false;
            }

            _slots.Add(new ModifierSlot
            {
                Key = key,
                Magnitude = cap > 0f ? math.min(cap, magnitude) : magnitude,
                Remaining = seconds,
                Origin = origin,
            });
            _dirty = true;
            return true;
        }

        /// <summary>그 슬롯을 **지운다**. 반환 = 지웠나.</summary>
        public bool Revoke(in ModifierKey key)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].Key.Equals(key)) continue;
                _slots.RemoveAt(i);
                _dirty = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 그 칸(종류 + 판별자)의 슬롯을 전부 지운다. 반환 = 지운 수.
        /// 효과 타일·카드처럼 「그 하나가 준 것을 통째로 거둔다」가 회수의 모양인 소비자용이다.
        /// </summary>
        public int RevokeTag(SlotKind kind, int discriminator, List<ModifierSlot> removed = null)
        {
            int n = 0;
            for (int i = 0; i < _slots.Count;)
            {
                var tag = _slots[i].Key.Tag;
                if (tag.Kind == kind && tag.Discriminator == discriminator)
                {
                    removed?.Add(_slots[i]);
                    _slots.RemoveAt(i);
                    n++;
                }
                else i++;
            }
            if (n > 0) _dirty = true;
            return n;
        }

        /// <summary>
        /// 남은 시간을 깎고 만료된 슬롯을 지운다. 반환 = 지운 수.
        /// 지운 것을 `removed` 에 **삽입 순서 오름차순**으로 담는다 — 회수 사건의 순서가
        /// 순회 순서에 매이지 않게 하는 것이 그 값이다.
        /// </summary>
        public int Expire(float dt, List<ModifierSlot> removed = null)
        {
            if (_slots.Count == 0) return 0;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                slot.Remaining -= dt;   // +∞ 는 그대로 +∞ 다
                _slots[i] = slot;
            }

            int n = 0;
            for (int i = 0; i < _slots.Count;)
            {
                if (_slots[i].Remaining <= 0f)
                {
                    removed?.Add(_slots[i]);
                    _slots.RemoveAt(i);
                    n++;
                }
                else i++;
            }

            // F21 — **만료도 dirty 를 켠다.**
            if (n > 0) _dirty = true;
            return n;
        }

        public void Reset()
        {
            _slots.Clear();
            _cached = EffectiveStats.Identity;
            _dirty = false;
        }

        private void Fold()
        {
            for (int s = 0; s < StatCount; s++)
            {
                _add[s] = 0f;
                _mul[s] = 1f;
                _over[s] = 0f;
                _hasOver[s] = false;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                int s = (int)slot.Key.Stat;
                if (s < 0 || s >= StatCount) continue;
                switch (slot.Key.Op)
                {
                    case CombineOp.Multiplicative: _mul[s] *= slot.Magnitude; break;
                    case CombineOp.Additive: _add[s] += slot.Magnitude; break;
                    default:
                        // 강제 고정끼리는 **큰 쪽**이 이긴다 — 승자를 순회 순서에 맡기지 않는다(F23).
                        _over[s] = _hasOver[s] ? math.max(_over[s], slot.Magnitude) : slot.Magnitude;
                        _hasOver[s] = true;
                        break;
                }
            }

            _cached.DamageMul = Clamped(StatKind.DamageMul);
            _cached.AttackSpeedMul = Clamped(StatKind.AttackSpeedMul);
            _cached.DmgTakenMul = Clamped(StatKind.DmgTakenMul);
            _cached.MoveSpeedMul = Clamped(StatKind.MoveSpeedMul);
            _cached.DamageVsCcMul = Clamped(StatKind.DamageVsCcMul);
            _cached.MaxHealthMul = Clamped(StatKind.MaxHealthMul);

            // 재생은 **자원 값**(기본 0)이라 배율 클램프 밖이고 `max(0, ·)` 만 건다.
            // ⚠ 결합식이 `(0 + Σadd) × Πmul` 이라 **곱셈 슬롯만 있으면 0** 이다(F5) —
            // 사실상 더하기 전용 스탯이고, 그 성질을 그대로 옮긴다.
            int r = (int)StatKind.RegenPerSec;
            _cached.RegenPerSec = math.max(0f, _hasOver[r] ? _over[r] : (0f + _add[r]) * _mul[r]);

            _dirty = false;
        }

        private float Clamped(StatKind stat)
        {
            int s = (int)stat;
            ModifierMath.BoundsOf(stat, out float floor, out float ceil);
            return ModifierMath.CombineMul(_hasOver[s], _over[s], _add[s], _mul[s], floor, ceil);
        }
    }
}
