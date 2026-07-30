using System.Collections.Generic;

namespace Wassup.Core
{
    // page-local-presets unit 0 — 신규 프리셋 id 발급. 순수 함수 하나라 파일을 따로 둔다
    // (EditMode 테스트 대상, 제약 10).
    //
    // 왜 "리스트 개수 + 1" 이 아닌가: 프리셋 3개(1,2,3) 중 2번을 지우면 개수는 2가 되고
    // 다음 발급이 "preset_3" 이라 살아있는 3번과 충돌한다. 기존 접미 **최대값 + 1** 이라야
    // 삭제 후 재생성에서도 안전하다.
    public static class PresetIds
    {
        // prefix 뒤에 숫자를 붙여 유일한 id 를 만든다. 기존 id 중 `{prefix}{정수}` 꼴의
        // 접미 최대값 + 1 을 쓰고, 그런 id 가 없으면 1 부터 시작한다.
        //
        // 레거시 id(`squad_1`/`deck_1`)도 그대로 입력으로 들어온다 — prefix 가 "squad_"
        // 이면 접미 1 로 세어지고, prefix 가 다르면 무시된다. 어느 쪽이든 충돌하지 않는
        // 값이 나온다.
        public static string NextId(IReadOnlyList<string> existingIds, string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) prefix = "preset_";

            int max = 0;
            if (existingIds != null)
            {
                for (int i = 0; i < existingIds.Count; i++)
                {
                    var id = existingIds[i];
                    if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix)) continue;
                    var suffix = id.Substring(prefix.Length);
                    // 정수 접미만 센다. "squad_1a" 같은 건 후보가 아니므로 건너뛴다 —
                    // 세었다면 파싱 실패를 0 으로 삼켜 충돌 위험이 생긴다.
                    if (int.TryParse(suffix, out int n) && n > max) max = n;
                }
            }
            return prefix + (max + 1);
        }
    }
}
