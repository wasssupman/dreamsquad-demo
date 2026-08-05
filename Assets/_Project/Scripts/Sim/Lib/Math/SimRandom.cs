namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-A — sim 이 소유하는 난수. `Unity.Mathematics.Random` 의
    /// **비트 동일 재현**이다(xorshift32).
    ///
    /// 비트 동일성이 선택이 아닌 이유: 시드 파생 스트림(`meteorRng` 등)이 **골든 상태 해시에
    /// 실린다**(`BattleBridge.LegacyTrace.cs:246` — `AppendStateLine(sb, "meteorRng", state)`).
    /// 한 draw 라도 어긋나면 그 뒤 모든 확률 판정이 갈린다.
    ///
    /// 옮길 때 놓치기 쉬운 것 3개 — 원본에서 그대로 가져왔다:
    /// - <see cref="NextState"/> 는 **변이 전** 값을 반환한다(변이 후가 아니다).
    /// - 생성자가 `state = seed` 뒤 <see cref="NextState"/> 를 **한 번 버린다**.
    /// - <see cref="NextUInt"/> 는 `NextState() - 1u` 다(그냥 상태가 아니다).
    ///
    /// 게이트: `SimMathParityTests` 가 같은 시드에서 N draw 를 비트 대조한다.
    /// </summary>
    public struct SimRandom
    {
        public uint state;

        public SimRandom(uint seed)
        {
            state = seed;
            NextState();
        }

        /// 변이 **전** 상태를 반환하고 xorshift 를 적용한다.
        public uint NextState()
        {
            uint t = state;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return t;
        }

        public bool NextBool() => (NextState() & 1) == 1;

        public uint NextUInt() => NextState() - 1u;

        public int NextInt() => (int)NextState() ^ -2147483648;

        /// `[min, max)`. 곱셈 후 상위 32비트 — 나머지 연산이 아니다(분포·비트가 달라진다).
        public int NextInt(int min, int max)
        {
            uint range = (uint)(max - min);
            return (int)(NextState() * (ulong)range >> 32) + min;
        }

        /// `[0, 1)`. 지수부를 1.0 으로 고정하고 가수부 23비트 중 상위를 채운 뒤 1을 뺀다.
        public float NextFloat()
        {
            uint bits = 0x3f800000 | (NextState() >> 9);
            return System.BitConverter.Int32BitsToSingle(unchecked((int)bits)) - 1.0f;
        }

        public float NextFloat(float min, float max) => NextFloat() * (max - min) + min;

        /// <summary>
        /// 구 `Unity.Mathematics.Random.CreateFromIndex` 의 비트 동일 재현 — 18-H/4(발사 패턴
        /// 트리거 스냅샷)가 처음 요구했다.
        ///
        /// ⚠ **인덱스를 시드로 그냥 쓰지 않는다.** `WangHash(index + 62)` 로 흩뿌린 뒤 시드로
        /// 삼는다 — 연속한 인덱스가 연속한 스트림이 되는 것을 막는 자리다. `+ 62` 와 해시 상수
        /// 넷 전부 원본 그대로여야 한 draw 도 어긋나지 않는다.
        ///
        /// ⚠ 원본은 `index == uint.MaxValue` 를 던진다(해시 결과가 0 이 되어 xorshift 가 죽는
        /// 값). 여기서도 같은 자리에서 거절한다 — 조용히 0 스트림을 돌리면 그 판이 통째로
        /// 결정론을 잃는다.
        /// </summary>
        public static SimRandom CreateFromIndex(uint index)
        {
            if (index == uint.MaxValue)
                throw new System.ArgumentException(
                    "index must not be uint.MaxValue — 해시가 0 이 되어 난수열이 죽는다.", nameof(index));
            return new SimRandom(WangHash(index + 62u));
        }

        private static uint WangHash(uint n)
        {
            n = (n ^ 61u) ^ (n >> 16);
            n *= 9u;
            n = n ^ (n >> 4);
            n *= 0x27d4eb2du;
            n = n ^ (n >> 15);
            return n;
        }
    }
}
