using UnityEditor;
using Somnia.Battle.Core;

namespace Somnia.Battle.Editor
{
    // somnia-battle-rename unit 2 — `MapStageGizmoUtil.Label` 의 글자 그리기(`Handles.Label`)를 에디터 어셈블리에서 꽂는다.
    // Runtime 코드에 `UnityEditor.` 토큰을 두지 않기 위해서다(somnia governance `check_runtime_no_unityeditor` 는 `#if` 를 안 본다).
    // 도메인 리로드마다 다시 꽂힌다(static 은 리로드에 비워진다).
    internal static class MapStageGizmoLabels
    {
        [InitializeOnLoadMethod]
        private static void Install() => MapStageGizmoUtil.LabelDrawer = (world, text) => Handles.Label(world, text);
    }
}
