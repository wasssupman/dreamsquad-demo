using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Wassup.Core
{
    // battle-sim-extraction M0 unit 3 — 한 판의 «조건» 을 불변 텍스트로 물질화하고 해시한다.
    //
    // 왜: 골든(unit 4)이 갈렸을 때 첫 질문은 언제나 **「코드가 바뀐 건가, 값이 바뀐 건가」**다.
    // 이 프로젝트에서 값은 조용히 바뀐다 — 로비 진입마다 시트 임포터가 SO 를 덮는다
    // (`docs/reference` 의 시트↔SO 드리프트 함정). 그래서 조건 전체를 한 문자열로 접고
    // 해시를 골든 헤더에 동봉한다. 해시가 다르면 그건 회귀가 아니라 **드리프트**다.
    //
    // 담는 것 / 안 담는 것: 「게임 결과에 영향을 주는가」가 기준이다. 숫자·불리언·열거형·
    // 문자열과 **데이터 SO 참조**(이름으로)는 담고, **아트 참조**(Sprite/Material/Prefab/
    // Texture/AudioClip/Shader/…)는 담지 않는다. 아트를 담으면 스킨 교체가 「조건이 바뀌었다」로
    // 읽혀 판독 장치가 거짓말을 한다.
    //
    // ⚠ 필드는 **이름순**으로 접는다. 리플렉션이 돌려주는 선언 순서는 런타임 계약이 아니라
    // 관례일 뿐이라, 정렬해 두어야 「같은 값인데 해시가 다르다」가 원천적으로 불가능해진다.
    public readonly struct MatchConfigSnapshot
    {
        public readonly string text;   // canonical 직렬화 결과(사람이 읽을 수 있는 형태)
        public readonly string hash;   // SHA-256 hex 16자 — 로그·헤더에 싣기 위한 길이

        public MatchConfigSnapshot(string text, string hash)
        {
            this.text = text;
            this.hash = hash;
        }

        public bool IsEmpty => string.IsNullOrEmpty(hash);
    }

    // canonical 텍스트 작성기. 포맷 규칙은 셋뿐이다:
    //   · 줄 단위 `key=value`, 섹션은 `[name]`
    //   · 문화권 불변(InvariantCulture), 부동소수는 "R"(왕복 손실 없는 최단 표기)
    //   · null 은 `~` (빈 문자열과 구분 — 「참조가 없다」와 「이름이 빈 문자열」은 다른 조건이다)
    public sealed class MatchConfigWriter
    {
        private readonly StringBuilder _sb = new StringBuilder(4096);

        public void Section(string name) => _sb.Append('[').Append(name).Append("]\n");

        public void Put(string key, string value)
            => _sb.Append(key).Append('=').Append(value ?? "~").Append('\n');

        public void Put(string key, int value) => Put(key, value.ToString(CultureInfo.InvariantCulture));
        public void Put(string key, bool value) => Put(key, value ? "1" : "0");

        public void Put(string key, float value)
            => Put(key, value.ToString("R", CultureInfo.InvariantCulture));

        // 데이터 SO 한 장을 통째로 접는다. 필드를 손으로 나열하지 않는 이유: 나열은
        // 반드시 낡고, 낡은 목록은 「스탯 하나를 바꿨는데 해시가 그대로」라는 **조용한
        // 실패**를 만든다. 완료 기준이 정확히 그것을 금지한다.
        public void PutAsset(string key, UnityEngine.Object asset)
        {
            if (asset == null) { Put(key, (string)null); return; }
            Put(key, asset.name);
            Describe(key, asset, depth: 0);
        }

        public string Text => _sb.ToString();

        public MatchConfigSnapshot Build()
        {
            string t = Text;
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(t));
            var hex = new StringBuilder(16);
            for (int i = 0; i < 8; i++) hex.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            return new MatchConfigSnapshot(t, hex.ToString());
        }

        // ── 리플렉션 접기 ──────────────────────────────────────────────────────

        private const int MaxDepth = 4;

        // 아트 참조. 이들 타입(과 그 파생)은 **이름조차 담지 않는다** — 값이 아니라 그림이다.
        private static readonly Type[] ArtTypes =
        {
            typeof(Sprite), typeof(Texture), typeof(Material), typeof(Shader),
            typeof(GameObject), typeof(Component), typeof(AudioClip), typeof(Font),
            typeof(AnimationClip), typeof(RuntimeAnimatorController),
        };

        private void Describe(string prefix, object obj, int depth)
        {
            if (obj == null || depth > MaxDepth) return;
            var type = obj.GetType();

            var fields = new List<FieldInfo>();
            for (var t = type;
                 t != null && t != typeof(object) && t != typeof(ValueType)
                 && t != typeof(ScriptableObject) && t != typeof(MonoBehaviour) && t != typeof(Component);
                 t = t.BaseType)
                fields.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
            // 이름순 — 선언 순서에 기대지 않는다(위 ⚠).
            fields.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            foreach (var f in fields)
            {
                if (f.IsNotSerialized) continue;
                if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                object v;
                try { v = f.GetValue(obj); } catch { continue; }
                Emit($"{prefix}.{f.Name}", f.FieldType, v, depth);
            }
        }

        private void Emit(string key, Type declared, object v, int depth)
        {
            // ⚠ 아트 판정이 **null 검사보다 앞**이다. 뒤에 두면 «아트가 비어 있을 때만 줄이
            // 생기고 채우면 사라지는» 비대칭이 되어, 머티리얼 하나를 꽂는 것만으로 해시가
            // 바뀐다 — 정확히 이 규칙이 막으려던 거짓 신호다(테스트가 그 형태로 잡았다).
            if (typeof(UnityEngine.Object).IsAssignableFrom(declared) && IsArt(declared)) return;

            if (v == null) { Put(key, (string)null); return; }

            if (typeof(UnityEngine.Object).IsAssignableFrom(declared))
            {
                var uo = v as UnityEngine.Object;
                Put(key, uo == null ? null : uo.name);             // 참조는 **이름까지만** —
                return;                                            // 파고들면 SO 그래프에서 순환한다
            }

            switch (v)
            {
                case bool b: Put(key, b); return;
                case float f: Put(key, f); return;
                case double d: Put(key, ((float)d)); return;
                case string s: Put(key, s); return;
            }
            if (declared.IsEnum) { Put(key, Convert.ToInt32(v)); return; }
            // ⚠ 나머지 primitive 는 **int 로 좁히지 않는다.** `Convert.ToInt32` 는 uint/long 의
            // 큰 값에서 OverflowException 을 던지는데, 이 수집은 `StartBattle` 안에서 돌아
            // 그 예외가 곧 「판이 시작되지 않는 것」이 된다. 문자열로 그대로 적으면 값도
            // 안 잃고 던질 일도 없다.
            if (declared.IsPrimitive) { Put(key, Convert.ToString(v, CultureInfo.InvariantCulture)); return; }

            if (v is IList list)
            {
                Put(key + ".n", list.Count);
                var elem = declared.IsArray ? declared.GetElementType()
                         : (declared.IsGenericType ? declared.GetGenericArguments()[0] : typeof(object));
                for (int i = 0; i < list.Count; i++)
                    Emit($"{key}[{i}]", list[i] != null ? list[i].GetType() : elem, list[i], depth + 1);
                return;
            }

            if (declared.IsValueType || declared.IsClass) Describe(key, v, depth + 1);
        }

        private static bool IsArt(Type t)
        {
            for (int i = 0; i < ArtTypes.Length; i++)
                if (ArtTypes[i].IsAssignableFrom(t)) return true;
            return false;
        }
    }
}
