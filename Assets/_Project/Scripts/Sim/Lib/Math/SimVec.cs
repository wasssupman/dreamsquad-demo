using System;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-A — sim 이 소유하는 벡터 타입.
    ///
    /// **왜 `Unity.Mathematics` 를 쓰지 않나** (사용자 결정 2026-08-05): 목적지가 엔진-프리
    /// 라이브러리이므로 sim 의 데이터 타입이 Unity 패키지 타입이면 그 목적이 타입 수준에서
    /// 무너진다. `MatchSessionContract` 의 `SimCell`(= "int2 대용. Unity.Mathematics 를 DTO 에
    /// 들이지 않기 위한 최소 좌표 타입")이 계약 표면에 이미 그은 선을, 내부 12,839줄에도 긋는다.
    ///
    /// **왜 지금인가**: 구 sim 이 살아 있는 units 18~20 이 **유일한 검증 창**이다. A/B parity 가
    /// 이 구현 자체를 검증한다. 스왑 이후에 갈아끼우면 비교할 오라클이 없다.
    ///
    /// ⚠ **비트 동일성이 계약이다.** 연산 순서·`rsqrt` 왕복까지 `Unity.Mathematics` 와 같아야
    /// 사거리·동률 같은 **이산 판정**이 갈리지 않는다. 게이트는 `SimMathParityTests` 이고,
    /// 그 테스트는 두 라이브러리가 공존하는 동안에만 쓸 수 있다.
    /// </summary>
    public readonly struct SimVec3 : IEquatable<SimVec3>
    {
        public readonly float x, y, z;

        public SimVec3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public SimVec3(float v) { x = v; y = v; z = v; }

        public static readonly SimVec3 Zero = default;

        /// 지면 평면 성분. 구 코드의 `.xz` 스위즐 대응(실측 8곳 — 다른 스위즐은 0회).
        public SimVec2 xz => new SimVec2(x, z);

        public static SimVec3 operator +(SimVec3 a, SimVec3 b) => new SimVec3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static SimVec3 operator -(SimVec3 a, SimVec3 b) => new SimVec3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static SimVec3 operator -(SimVec3 a) => new SimVec3(-a.x, -a.y, -a.z);
        // ⚠ 스칼라 곱의 **인자 순서**를 보존한다 — `normalize` 가 `rsqrt(...) * x` 형태라
        //    float 곱은 교환법칙이 성립하지만 컴파일러 폴딩까지 같게 두려면 두 방향 다 둔다.
        public static SimVec3 operator *(SimVec3 a, float s) => new SimVec3(a.x * s, a.y * s, a.z * s);
        public static SimVec3 operator *(float s, SimVec3 a) => new SimVec3(s * a.x, s * a.y, s * a.z);
        public static SimVec3 operator /(SimVec3 a, float s) => new SimVec3(a.x / s, a.y / s, a.z / s);

        public bool Equals(SimVec3 o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object o) => o is SimVec3 v && Equals(v);
        public override int GetHashCode() => (x.GetHashCode() * 397 ^ y.GetHashCode()) * 397 ^ z.GetHashCode();
        public static bool operator ==(SimVec3 a, SimVec3 b) => a.Equals(b);
        public static bool operator !=(SimVec3 a, SimVec3 b) => !a.Equals(b);
        public override string ToString() => $"({x}, {y}, {z})";
    }

    public readonly struct SimVec2 : IEquatable<SimVec2>
    {
        public readonly float x, y;

        public SimVec2(float x, float y) { this.x = x; this.y = y; }

        public static readonly SimVec2 Zero = default;

        public static SimVec2 operator +(SimVec2 a, SimVec2 b) => new SimVec2(a.x + b.x, a.y + b.y);
        public static SimVec2 operator -(SimVec2 a, SimVec2 b) => new SimVec2(a.x - b.x, a.y - b.y);
        public static SimVec2 operator -(SimVec2 a) => new SimVec2(-a.x, -a.y);
        public static SimVec2 operator *(SimVec2 a, float s) => new SimVec2(a.x * s, a.y * s);
        public static SimVec2 operator *(float s, SimVec2 a) => new SimVec2(s * a.x, s * a.y);
        public static SimVec2 operator /(SimVec2 a, float s) => new SimVec2(a.x / s, a.y / s);

        public bool Equals(SimVec2 o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is SimVec2 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() * 397 ^ y.GetHashCode();
        public static bool operator ==(SimVec2 a, SimVec2 b) => a.Equals(b);
        public static bool operator !=(SimVec2 a, SimVec2 b) => !a.Equals(b);
        public override string ToString() => $"({x}, {y})";
    }

    /// <summary>
    /// 셀 좌표. `MatchSessionContract` 의 `SimCell` 과 **의도적으로 별개**다 — 그쪽은 커맨드·이벤트
    /// DTO 의 좌표(직렬화 계약)이고, 이쪽은 sim 내부 연산용이다. 둘을 합치면 DTO 스키마 변경이
    /// 내부 산술에 전파된다.
    /// </summary>
    public readonly struct SimInt2 : IEquatable<SimInt2>
    {
        public readonly int x, y;

        public SimInt2(int x, int y) { this.x = x; this.y = y; }

        public static readonly SimInt2 Zero = default;

        public static SimInt2 operator +(SimInt2 a, SimInt2 b) => new SimInt2(a.x + b.x, a.y + b.y);
        public static SimInt2 operator -(SimInt2 a, SimInt2 b) => new SimInt2(a.x - b.x, a.y - b.y);

        public bool Equals(SimInt2 o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is SimInt2 v && Equals(v);
        public override int GetHashCode() => x * 397 ^ y;
        public static bool operator ==(SimInt2 a, SimInt2 b) => a.Equals(b);
        public static bool operator !=(SimInt2 a, SimInt2 b) => !a.Equals(b);
        public override string ToString() => $"({x}, {y})";
    }
}
