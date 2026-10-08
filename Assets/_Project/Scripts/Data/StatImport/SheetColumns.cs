using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Somnia.Battle.Data.StatImport
{
    /// <summary>
    /// skill-data-table unit 9 — **시트 열 이름 = DTO 의 JSON 이름**(전 탭 스네이크 · `[JsonProperty]`). C# 필드 이름은 SO 와 1:1 로 두고
    /// (`UnitStatFieldMapper` 가 C# 이름으로 짝짓는다) 시트 · 로그 · 헤더 시드처럼 **사람이 보는 열 이름**은 여기서 JSON 이름으로 푼다 —
    /// `nameof(필드)` 를 헤더로 쓰면 스네이크 뒤 옛 카멜 열이 되살아난다.
    /// </summary>
    public static class SheetColumns
    {
        /// <summary>그 DTO 필드의 시트 열 이름(`[JsonProperty]` 이름 · 없으면 필드 이름).</summary>
        public static string NameOf(Type dto, string field)
        {
            var attr = dto.GetField(field, BindingFlags.Public | BindingFlags.Instance)?.GetCustomAttribute<JsonPropertyAttribute>();
            return attr?.PropertyName ?? field;
        }

        /// <summary>그 행 타입의 열 이름 전부 — **export 가 쓰는 순서**(직렬화기 계약 순서 = 시트 헤더 줄).</summary>
        public static string[] Of(Type rowType)
        {
            var contract = (JsonObjectContract)JsonSerializer.CreateDefault().ContractResolver.ResolveContract(rowType);
            return contract.Properties.Where(p => !p.Ignored).Select(p => p.PropertyName).ToArray();
        }
    }
}
