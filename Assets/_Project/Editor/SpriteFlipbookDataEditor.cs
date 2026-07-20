using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Editor
{
    // sprite-flipbook-player unit 3 — 통 시트에서 프레임 배열을 채우는 오소링 유틸.
    // 슬라이스 자체는 Unity 임포터(Sprite Mode = Multiple)가 하고, 여기는 나온 서브스프라이트를
    // 올바른 순서로 SO 에 주입하기만 한다. 런타임에는 아무 것도 추가되지 않는다.
    [CustomEditor(typeof(SpriteFlipbookData))]
    public class SpriteFlipbookDataEditor : UnityEditor.Editor
    {
        // unit 1 의 private 직렬화 필드명. 바꾸면 이 유틸이 조용히 깨지므로 계약으로 취급한다.
        private const string FramesProperty = "frames";

        private Texture2D _sheet;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("통 시트에서 채우기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Sprite Mode = Multiple 로 슬라이스된 시트를 지정하면 서브스프라이트를 이름 끝 숫자 순으로 frames 에 채운다.\n" +
                "컷 모드(개별 스프라이트)는 위 frames 배열에 직접 할당하면 된다.",
                MessageType.None);

            _sheet = (Texture2D)EditorGUILayout.ObjectField("시트 텍스처", _sheet, typeof(Texture2D), false);

            using (new EditorGUI.DisabledScope(_sheet == null))
            {
                if (GUILayout.Button("프레임 채우기"))
                    FillFromSheet((SpriteFlipbookData)target, _sheet);
            }
        }

        private static void FillFromSheet(SpriteFlipbookData data, Texture2D sheet)
        {
            string path = AssetDatabase.GetAssetPath(sheet);
            if (string.IsNullOrEmpty(path))
            {
                Fail("시트 텍스처가 프로젝트 에셋이 아니다.");
                return;
            }

            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                Fail($"'{path}' 의 TextureImporter 를 읽을 수 없다.");
                return;
            }

            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                Fail($"'{sheet.name}' 의 Sprite Mode 가 {importer.spriteImportMode} 다.\n" +
                     "Multiple 로 바꾸고 Sprite Editor 에서 슬라이스한 뒤 다시 시도할 것.");
                return;
            }

            var sprites = new List<Sprite>();
            // 서브스프라이트만 수집한다 — LoadAllAssetsAtPath 는 텍스처 본체까지 섞여 들어온다.
            foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (asset is Sprite sprite)
                    sprites.Add(sprite);

            if (sprites.Count == 0)
            {
                Fail($"'{sheet.name}' 에 슬라이스된 서브스프라이트가 없다. Sprite Editor 에서 먼저 자를 것.");
                return;
            }

            // 사전순으로 두면 _1, _10, _11, _2 … 가 되어 프레임 순서가 조용히 뒤섞인다.
            // 컴파일도 통과하고 경고도 없이 애니메이션만 이상해지는 종류라 순수 함수로 빼서 테스트한다.
            sprites.Sort((a, b) => FlipbookFrameOrder.Compare(a.name, b.name));

            var so = new SerializedObject(data);
            var frames = so.FindProperty(FramesProperty);
            if (frames == null)
            {
                Fail($"SpriteFlipbookData 에 '{FramesProperty}' 필드가 없다 — 필드명이 바뀌었는지 확인할 것.");
                return;
            }

            frames.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++)
                frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(data);
            // SaveAssets() 를 쓰면 이 버튼 하나가 인스펙터에서 편집 중이던 **무관한 dirty 에셋 전부**를
            // 디스크로 밀어낸다(씬 저장이 미저장 WIP 를 베이크하는 것과 같은 계열의 사고).
            AssetDatabase.SaveAssetIfDirty(data);

            Debug.Log($"SpriteFlipbookData '{data.name}': '{sheet.name}' 에서 {sprites.Count} 프레임을 채웠다 " +
                      $"({sprites[0].name} … {sprites[sprites.Count - 1].name}).", data);
        }

        private static void Fail(string message)
        {
            EditorUtility.DisplayDialog("프레임 채우기 실패", message, "확인");
        }
    }
}
