using System;
using System.Globalization;
using System.Text;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-K/1 — **레거시 키 트레이스 emitter 의 토대.**
    ///
    /// 18-A/3 이 `LegacyTraceKeyContractTests` 로 계약을 박제했고 여기가 그 구현이다.
    ///
    /// ## 왜 자기 타입명을 찍으면 안 되나
    ///
    /// 구 상태 해시 포매터는 필드 이름만 쓰는 게 아니라 **타입 `FullName` 을 박는다** —
    /// 라인 키에도, 버퍼 라인에도, **중첩 값마다** 박는다. 신 sim 이 `Wassup.Sim.SimVec3` 를
    /// 찍으면 키가 통째로 달라지고, A/B parity 의 exact 축(상태 해시)이 **구조적으로** 불일치한다.
    /// 그것을 unit 20 에서 발견하면 되돌릴 반경이 7,000줄이다.
    ///
    /// ⇒ 이 클래스는 **구 sim 의 문자열을 그대로 출력한다.** 리플렉션을 쓰지 않는다 —
    /// 신 타입에 리플렉션을 걸면 신 이름이 나오고, 구 타입은 여기서 참조할 수 없다.
    /// 드리프트는 `SimLegacyTraceContractTests` 가 **구 타입에서 유도한 진실**과 대조해 막는다.
    ///
    /// ## 값 렌더 규칙 (구 `FormatLegacyValue` 그대로)
    ///
    /// <list type="bullet">
    /// <item>`float`/`double` → `"R"` · `InvariantCulture`</item>
    /// <item>`bool` → `true`/`false`</item>
    /// <item>enum → **정수값**(`Convert.ToInt64`)</item>
    /// <item>그 외 primitive → `Convert.ToString(InvariantCulture)`</item>
    /// <item>엔티티 참조 → `sim:N`(`Null` 은 `sim:-1` — ⚠ 아래 참조)</item>
    /// <item>중첩 값 → `FullName{이름=값,…}` — 이름 **ordinal 오름차순**</item>
    /// </list>
    ///
    /// ⚠ **`Null` 엔티티는 `sim:-1` 이다.** 구 `ResolveLegacyTraceEntity` 가 `Entity.Null` 에
    /// `-1` 을 돌려주고 포매터가 `"sim:" + -1` 을 만든다. `SimEntityId.ToString()` 은 `sim:null`
    /// 을 주므로 **그것을 쓰면 안 된다** — 이 클래스가 따로 렌더하는 이유다.
    /// (`LegacyTraceKeyContractTests` 는 `sim:7`/`sim:null` 을 박제하지만 그건 `ToString` 의
    /// 계약이고, 트레이스가 쓰는 것은 `ResolveLegacyTraceEntity` 경로다.)
    /// </summary>
    public static class SimLegacyTrace
    {
        // ── 구 타입 FullName 표 (하드코딩 — 리플렉션 금지) ──────────────────────
        // 신 sim 이 `Unity.*` 를 버려도 **키는 승계**한다. 이 문자열이 곧 상태 해시의 키다.

        public const string KeyLocalTransform = "Unity.Transforms.LocalTransform";
        public const string KeyHealth = "Wassup.Battle.Units.Health";
        public const string KeyFactionTag = "Wassup.Battle.Units.FactionTag";
        public const string KeyKillScore = "Wassup.Battle.Units.KillScore";
        public const string KeyDefenderTile = "Wassup.Battle.Units.DefenderTile";
        public const string KeyPathFollowState = "Wassup.Battle.Movement.PathFollowState";
        public const string KeyAttackState = "Wassup.Battle.Combat.AttackState";
        public const string KeyModifierStats = "Wassup.Battle.Effects.ModifierStats";
        public const string KeyProjectileState = "Wassup.Battle.Combat.Projectile.ProjectileState";
        public const string KeyBombLauncherState = "Wassup.Battle.Combat.BombLauncherState";
        public const string KeyPickupSpawnState = "Wassup.Battle.Effects.PickupSpawnState";

        public const string KeyPatternSlot = "Wassup.Battle.Combat.Projectile.Emission.PatternSlot";
        public const string KeyCcEffect = "Wassup.Battle.Effects.CcEffect";
        public const string KeyDotEffect = "Wassup.Battle.Effects.DotEffect";
        public const string KeyStatModifierSlot = "Wassup.Battle.Effects.StatModifierSlot";
        public const string KeyStackModifierSlot = "Wassup.Battle.Effects.StackModifierSlot";
        public const string KeyThreatEntry = "Wassup.Battle.Combat.ThreatEntry";
        public const string KeyShieldSlot = "Wassup.Battle.Units.ShieldSlot";
        public const string KeyIncomingDamage = "Wassup.Battle.Units.IncomingDamage";
        public const string KeyIncomingHeal = "Wassup.Battle.Units.IncomingHeal";
        public const string KeyIncomingShield = "Wassup.Battle.Units.IncomingShield";

        /// 중첩 값 타입 — 구 sim 이 엔진 타입을 그대로 실었다.
        public const string KeyFloat3 = "Unity.Mathematics.float3";
        public const string KeyFloat2 = "Unity.Mathematics.float2";
        public const string KeyInt2 = "Unity.Mathematics.int2";
        public const string KeyRandom = "Unity.Mathematics.Random";
        public const string KeyModifierHeader = "Wassup.Battle.Effects.ModifierHeader";

        /// <summary>
        /// ⚠ **`LocalTransform.Rotation` 은 신 sim 에 없다** — sim 코드가 회전을 한 번도 쓰지 않아
        /// (`quaternion`·`float4x4` 사용 0) `SimTransform` 이 `Position`·`Scale` 만 옮겼다.
        ///
        /// **18-K 의 결정: 비교기가 양쪽에서 이 필드를 떼어낸다.** 골든 파일과 기록기는 건드리지
        /// 않는다(동결 유지) — 비교 직전 정규화 단계가 `Rotation=` 을 지운다.
        ///
        /// 근거: 회전은 스폰 시점 이후 **불변이고 뷰가 소유**한다. 포함하면 sim 이 만들지도
        /// 소비하지도 않는 값 때문에 거짓 불일치만 난다. 반대로 `Scale` 은 **떼면 안 된다** —
        /// #24(피격 플래시)가 실제로 그 값을 움직인다(18-J/1 살베지 판정).
        /// </summary>
        public const string ExcludedField = "Rotation";

        // ── 값 렌더 ──────────────────────────────────────────────────────────────

        public static string Float(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        public static string Double(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        public static string Bool(bool v) => v ? "true" : "false";
        public static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);
        public static string UInt(uint v) => v.ToString(CultureInfo.InvariantCulture);
        public static string Byte(byte v) => v.ToString(CultureInfo.InvariantCulture);
        public static string UShort(ushort v) => v.ToString(CultureInfo.InvariantCulture);

        /// enum 은 **정수값**으로 나간다(구 `Convert.ToInt64`).
        public static string Enum<T>(T v) where T : struct, IConvertible
            => System.Convert.ToInt64(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// 엔티티 참조 — **핸들이 아니라 <see cref="SimEntityId.SpawnOrdinal"/>**(구 0-base 순번)이다.
        ///
        /// ⚠ 18-K/1 은 여기서 `Value` 를 썼다. **틀렸다** — 구 `_simEntityIdCounter` 는 0 부터 세고
        /// 신 핸들은 1 부터 발급한다(0 = `Null` 예약). 기록기를 다시 읽고 나서야 드러났다(18-K/2).
        ///
        /// `Null` 이 `sim:-1` 인 것도 특수 분기가 아니라 같은 축의 결과다 — `0 - 1 = -1` 이 구
        /// `ResolveLegacyTraceEntity` 의 `Entity.Null` 반환값과 정확히 같다.
        /// (`SimEntityId.ToString()` 의 `sim:null` 은 별개 계약이다.)
        /// </summary>
        public static string Entity(SimEntityId e) => "sim:" + Int(e.SpawnOrdinal);

        public static string Vec3(SimVec3 v)
            => KeyFloat3 + "{x=" + Float(v.x) + ",y=" + Float(v.y) + ",z=" + Float(v.z) + "}";

        public static string Vec2(SimVec2 v)
            => KeyFloat2 + "{x=" + Float(v.x) + ",y=" + Float(v.y) + "}";

        public static string Int2(SimInt2 v)
            => KeyInt2 + "{x=" + Int(v.x) + ",y=" + Int(v.y) + "}";

        public static string Random(SimRandom r) => KeyRandom + "{state=" + UInt(r.state) + "}";

        // ── 라인 조립 ────────────────────────────────────────────────────────────

        /// `name=value\n`.
        public static void Line(StringBuilder sb, string name, string value)
            => sb.Append(name).Append('=').Append(value).Append('\n');

        /// <summary>
        /// 버퍼 라인 — `FullName[N]=v0;v1;…\n`. ⚠ 원소 구분자는 `;` 이고 **길이가 키에 들어간다**.
        /// </summary>
        public static void BufferLine(StringBuilder sb, string key, int count, Func<int, string> render)
        {
            sb.Append(key).Append('[').Append(count).Append("]=");
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(render(i));
            }
            sb.Append('\n');
        }

        /// <summary>
        /// 엔티티 블록의 여는/닫는 줄. 구 포매터는 `entity+N` … `entity-N` 으로 감싸고,
        /// **`SimEntityId` 오름차순**으로 순회한다.
        /// </summary>
        public static void EntityOpen(StringBuilder sb, int simId) => sb.Append("entity+").Append(simId).Append('\n');
        public static void EntityClose(StringBuilder sb, int simId) => sb.Append("entity-").Append(simId).Append('\n');

        /// <summary>
        /// 비교 직전 정규화 — 양쪽에서 <see cref="ExcludedField"/> 를 뗀다.
        ///
        /// ⚠ **골든 파일을 고치지 않는다.** 기록기와 코퍼스는 동결이고, 비교기만 이 함수를 통과시킨다.
        /// 중첩 값 안의 `Rotation=…` 은 `,` 또는 `}` 로 끝나고, 라인 형태의 `Rotation=` 은 줄 단위다 —
        /// 구 트레이스에서 `Rotation` 은 `LocalTransform` 의 **중첩 필드로만** 나타난다.
        /// </summary>
        public static string StripExcludedFields(string canonical)
        {
            if (string.IsNullOrEmpty(canonical)) return canonical;

            var sb = new StringBuilder(canonical.Length);
            int i = 0;
            while (i < canonical.Length)
            {
                int hit = canonical.IndexOf(ExcludedField + "=", i, StringComparison.Ordinal);
                if (hit < 0) { sb.Append(canonical, i, canonical.Length - i); break; }

                // 필드 경계인지 확인 — 바로 앞이 `{` 또는 `,` 여야 한다(부분 일치 방지).
                if (hit == 0 || (canonical[hit - 1] != '{' && canonical[hit - 1] != ','))
                {
                    sb.Append(canonical, i, hit + ExcludedField.Length + 1 - i);
                    i = hit + ExcludedField.Length + 1;
                    continue;
                }

                // 값의 끝 = 중첩 깊이 0 에서 만나는 `,` 또는 `}`.
                int j = hit + ExcludedField.Length + 1;
                int depth = 0;
                while (j < canonical.Length)
                {
                    char c = canonical[j];
                    if (c == '{') depth++;
                    else if (c == '}') { if (depth == 0) break; depth--; }
                    else if (c == ',' && depth == 0) break;
                    else if (c == '\n' && depth == 0) break;
                    j++;
                }

                // `{Rotation=…,` → 앞의 `{` 를 남기고 필드와 뒤따르는 `,` 를 지운다.
                // `,Rotation=…}` → 앞의 `,` 를 지운다.
                int keepUntil = hit;
                if (j < canonical.Length && canonical[j] == ',') j++;              // 뒤 구분자 흡수
                else if (canonical[hit - 1] == ',') keepUntil = hit - 1;           // 앞 구분자 흡수

                sb.Append(canonical, i, keepUntil - i);
                i = j;
            }
            return sb.ToString();
        }
    }
}
