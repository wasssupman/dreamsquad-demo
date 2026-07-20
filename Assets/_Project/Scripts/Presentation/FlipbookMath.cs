using UnityEngine;

namespace Wassup.Presentation
{
    // sprite-flipbook-player unit 0 — 플립북 프레임 선택 순수 수학 (plain in/out, EditMode 테스트 대상).
    // 시계·렌더러·에셋을 모른다. 오직 (경과시간, fps, 프레임수, 루프) 만 보고 인덱스를 결정하며,
    // 결정된 인덱스를 무엇으로 그릴지는 소비자가 알아서 해석한다. 값 자체는 아키텍처를 모른다.
    public static class FlipbookMath
    {
        // 경과시간 → 프레임 인덱스.
        // 프레임이 없으면 -1 ("그릴 것 없음"). 0 을 돌려주면 소비자가 빈 배열을 인덱싱하려 든다.
        // 원샷은 마지막 프레임에서 정지(hold), 루프는 처음으로 되감는다.
        // fps <= 0 은 "진행 없음" 으로 해석해 첫 프레임을 고정한다(0 나눗셈·무한 진행 방지).
        public static int FrameAt(float elapsed, float fps, int frameCount, bool loop)
        {
            if (frameCount <= 0) return -1;
            // NaN/Infinity 는 float→int 캐스트 동작이 아키텍처마다 갈린다(x64 는 int.MinValue,
            // ARM64 는 saturate — 즉 +Inf 가 int.MaxValue 라 음수 가드를 통과해 버린다).
            // 타겟이 Apple Silicon 에디터 + Android ARM64 라 여기서 명시적으로 막는다.
            // 예외를 던지지 않는 이유: 재생기가 매 프레임 호출하므로 이상치 하나가 로그 폭풍이 된다.
            // elapsed 와 fps 둘 다 검사한다 — fps 만 비유한이어도(인스펙터에 1e39 입력 → +Inf,
            // 또는 에셋 수기 편집으로 NaN) 같은 캐스트 분기가 그대로 재진입한다.
            // SO 의 `fps <= 0` 검증은 NaN·+Inf 를 잡지 못한다(NaN 비교는 전부 false).
            if (!float.IsFinite(elapsed) || !float.IsFinite(fps)) return 0;
            if (fps <= 0f || elapsed <= 0f) return 0;

            int raw = Mathf.FloorToInt(elapsed * fps);
            // elapsed*fps 가 int 범위를 넘으면 여전히 아키텍처별로 튄다 — 음수 쪽만 접어두면 충분하다
            // (양수 오버플로는 loop 이면 어차피 되감기, 원샷이면 마지막 프레임 hold 로 수렴).
            if (raw < 0) return 0;

            return loop ? raw % frameCount : Mathf.Min(raw, frameCount - 1);
        }

        // 원샷 1회 재생 길이(초). fps 가 유효하지 않거나(비유한·0 이하) 프레임 없음 = 0.
        // fps = NaN 을 통과시키면 Duration 이 NaN 이 되고, 아래 IsFinished 의 `elapsed >= NaN` 이
        // 영원히 false 라 원샷이 완주하지 못한다(= 폴링 소비자가 영구 대기).
        public static float Duration(float fps, int frameCount)
        {
            if (!float.IsFinite(fps) || fps <= 0f || frameCount <= 0) return 0f;
            return frameCount / fps;
        }

        // 원샷 완주 여부. 루프는 끝나지 않는다 — 이 규칙을 여기서 단독 소유해 소비자가 재유도하지 않게 한다.
        // fps <= 0 인 원샷은 Duration 이 0 이라 즉시 완료로 판정된다(진행하지 않는 재생의 무한 정지 방지).
        // elapsed 가 비유한이면 완료로 본다 — 재생이 전진할 수 없는데 살아 있으면 소비자가 영구 대기한다.
        public static bool IsFinished(float elapsed, float fps, int frameCount, bool loop)
        {
            if (loop) return false;
            if (!float.IsFinite(elapsed)) return true;
            return elapsed >= Duration(fps, frameCount);
        }
    }
}
