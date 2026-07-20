using UnityEditor;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Editor
{
    // sprite-character-preview unit 1 — 맵에 올려놓고 상태를 바꿔가며 눈으로 확인하는 어포던스.
    // 이게 없으면 상태를 전환할 방법이 없어서 프리팹이 사실상 Idle 확인용에 그친다.
    [CustomEditor(typeof(FlipbookCharacterView))]
    public class FlipbookCharacterViewEditor : UnityEditor.Editor
    {
        private static readonly FlipbookCharacterState[] States =
        {
            FlipbookCharacterState.Idle,
            FlipbookCharacterState.Attack,
            FlipbookCharacterState.Death,
            FlipbookCharacterState.Deploy,
            FlipbookCharacterState.Drag,
        };

        // 재생 상태를 라이브로 보여주려면 Play 중 계속 다시 그려야 한다.
        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("상태 전환 (확인용)", EditorStyles.boldLabel);

            if (!EditorApplication.isPlaying)
            {
                // 에디트 모드에서 누르면 SpriteFlipbookPlayer.Play(data) 가 직렬화 필드에 써서
                // 컴포넌트를 dirty 하게 만든다(재생기의 기존 동작). 확인용 도구가 씬/프리팹을
                // 조용히 오염시켜선 안 되므로 아예 막는다.
                EditorGUILayout.HelpBox("Play 중에만 전환할 수 있다.", MessageType.Info);
                return;
            }

            var view = (FlipbookCharacterView)target;

            // 원샷이 갇혔는지(README 함정)를 버튼을 눌러보는 것만으로 알 수 있어야 한다.
            EditorGUILayout.LabelField("현재", $"{view.Current}   {(view.IsPlaying ? "재생 중" : "정지")}");

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var state in States)
                {
                    if (GUILayout.Button(state.ToString()))
                        view.Play(state);
                }
            }

            if (view.Current == FlipbookCharacterState.Death)
                EditorGUILayout.HelpBox(
                    "Death 는 마지막 프레임을 유지한다(파괴하지 않는다) — 다른 상태를 눌러 되돌린다.",
                    MessageType.None);
        }
    }
}
