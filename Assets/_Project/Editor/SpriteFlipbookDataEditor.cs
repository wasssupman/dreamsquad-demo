using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Editor
{
    // sprite-flipbook-player unit 3 — 통 시트에서 프레임 배열을 채우는 오소링 유틸.
    // 슬라이스 자체는 Unity 임포터(Sprite Mode = Multiple)가 하고, 여기는 나온 서브스프라이트를
    // 올바른 순서로 SO 에 주입하기만 한다. 런타임에는 아무 것도 추가되지 않는다.
    //
    // unit 5 — 그 앞 단계(격자 자르기)까지 흡수했다. PNG 한 장 + 가로 N × 세로 M 만 주면
    // 임포터 슬라이스부터 frames 주입까지 버튼 하나로 끝난다. 슬라이스 **시점**은 그대로
    // 임포트 시점이다(런타임 Sprite.Create 없음) — 바뀐 것은 격자를 누가 입력하느냐뿐이다.
    [CustomEditor(typeof(SpriteFlipbookData))]
    public class SpriteFlipbookDataEditor : UnityEditor.Editor
    {
        // unit 1 의 private 직렬화 필드명. 바꾸면 이 유틸이 조용히 깨지므로 계약으로 취급한다.
        private const string FramesProperty = "frames";

        private Texture2D _sheet;
        private int _columns = 4;
        private int _rows = 4;

        // 격자 미리보기용 원본 해상도 캐시. 프로바이더 초기화는 매 repaint 하기엔 무거워서
        // 시트가 바뀔 때만 다시 잰다. 캐시는 미리보기 라벨 전용이고, 실제 자르기는 항상 다시 잰다.
        private Texture2D _probedSheet;
        private int _probedWidth;
        private int _probedHeight;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("통 시트에서 채우기", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "아직 안 자른 PNG 는 아래 가로/세로 칸 수를 넣고 '자르고 채우기' — 임포터 슬라이스부터 frames 주입까지 한 번에 한다.\n" +
                "이미 Sprite Mode = Multiple 로 잘라 둔 시트는 '프레임 채우기' 만 누르면 서브스프라이트를 이름 끝 숫자 순으로 채운다.\n" +
                "컷 모드(개별 스프라이트)는 위 frames 배열에 직접 할당하면 된다.",
                MessageType.None);

            var sheet = (Texture2D)EditorGUILayout.ObjectField("시트 텍스처", _sheet, typeof(Texture2D), false);
            if (sheet != _sheet)
            {
                _sheet = sheet;
                _probedSheet = null;
            }

            DrawGridSlice();

            using (new EditorGUI.DisabledScope(_sheet == null))
            {
                if (GUILayout.Button("프레임 채우기 (이미 잘린 시트)"))
                    FillFromSheet((SpriteFlipbookData)target, _sheet);
            }
        }

        private void DrawGridSlice()
        {
            _columns = Mathf.Max(1, EditorGUILayout.IntField("가로 칸 수 (N)", _columns));
            _rows = Mathf.Max(1, EditorGUILayout.IntField("세로 칸 수 (M)", _rows));

            // 나누어떨어지는지를 누르기 **전에** 보여준다 — 실패 조건이 곧 이 유닛의 유일한 규칙이라
            // 다이얼로그로만 알리면 N·M 을 감으로 더듬게 된다.
            if (_sheet != null)
            {
                ProbeSourceSize();
                if (_probedWidth > 0 && FlipbookSheetGrid.TryCellSize(_probedWidth, _probedHeight,
                        _columns, _rows, out int cellWidth, out int cellHeight))
                {
                    EditorGUILayout.LabelField(" ",
                        $"{_probedWidth}×{_probedHeight} → 셀 {cellWidth}×{cellHeight} · {_columns * _rows} 프레임",
                        EditorStyles.miniLabel);
                }
                else if (_probedWidth > 0)
                {
                    EditorGUILayout.LabelField(" ",
                        $"{_probedWidth}×{_probedHeight} 를 {_columns}×{_rows} 로 나눌 수 없다",
                        EditorStyles.miniLabel);
                }
            }

            using (new EditorGUI.DisabledScope(_sheet == null))
            {
                if (GUILayout.Button($"{_columns}×{_rows} 로 자르고 채우기"))
                    SliceAndFill((SpriteFlipbookData)target, _sheet, _columns, _rows);
            }
        }

        private void ProbeSourceSize()
        {
            if (_probedSheet == _sheet) return;

            _probedSheet = _sheet;
            _probedWidth = 0;
            _probedHeight = 0;

            if (!TryGetImporter(_sheet, out _, out TextureImporter importer, false)) return;
            var provider = OpenDataProvider(importer);
            if (provider == null) return;
            TryGetSourceSize(provider, out _probedWidth, out _probedHeight);
        }

        // unit 5 — 격자 자르기 + 이어서 unit 3 의 주입. 중간에 끊기면 임포터만 바뀌고 frames 는
        // 옛 프레임을 가리킨 채 남으므로, 실패는 전부 **임포터를 건드리기 전에** 판정한다.
        private static void SliceAndFill(SpriteFlipbookData data, Texture2D sheet, int columns, int rows)
        {
            if (!TryGetImporter(sheet, out string path, out TextureImporter importer)) return;

            // 읽기용 프로바이더. 검증이 끝나기 전에는 임포터를 건드리지 않는다.
            var probe = OpenDataProvider(importer);
            if (probe == null)
            {
                Fail("스프라이트 데이터 프로바이더를 열 수 없다 — com.unity.2d.sprite 패키지를 확인할 것.");
                return;
            }

            // 임포트된 텍스처 크기(Texture2D.width)를 쓰면 안 된다. maxTextureSize 로 축소 임포트된
            // 시트에서는 rect 가 전부 그 비율만큼 작아지는데, 슬라이스 좌표계는 원본 기준이다.
            // 폴백하지 않고 중단한다 — 조용히 어긋난 격자가 프레임으로 굳는 것보다 낫다.
            if (!TryGetSourceSize(probe, out int textureWidth, out int textureHeight))
            {
                Fail($"'{sheet.name}' 의 원본 해상도를 읽을 수 없다.");
                return;
            }

            if (!FlipbookSheetGrid.TryCellSize(textureWidth, textureHeight, columns, rows,
                    out int cellWidth, out int cellHeight))
            {
                Fail($"'{sheet.name}' 은 {textureWidth}×{textureHeight} 라 {columns}×{rows} 로 나누어떨어지지 않는다.\n" +
                     "나머지를 내림으로 삼키면 마지막 열·행에 잘린 띠가 남아 프레임에 섞인다.");
                return;
            }

            // 이름이 같은 기존 rect 의 GUID 를 물려준다. 새로 뽑으면 그 서브스프라이트를 참조하던
            // 다른 frames 배열·프리팹이 전부 Missing 이 된다(다시 자를 때마다 조용히 끊긴다).
            var inherited = new Dictionary<string, GUID>();
            foreach (var existing in probe.GetSpriteRects())
                inherited[existing.name] = existing.spriteID;

            int count = FlipbookSheetGrid.CellCount(columns, rows);
            var rects = new SpriteRect[count];
            for (int i = 0; i < count; i++)
            {
                var cell = FlipbookSheetGrid.CellRect(i, columns, rows, cellWidth, cellHeight);
                // Unity 슬라이서와 같은 이름 규칙 — FlipbookFrameOrder 의 숫자 접미사 정렬이 그대로 먹는다.
                string frameName = $"{sheet.name}_{i}";
                var rect = new SpriteRect
                {
                    name = frameName,
                    rect = new Rect(cell.x, cell.y, cell.width, cell.height),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = SpriteAlignment.Center,
                    border = Vector4.zero,
                };
                if (inherited.TryGetValue(frameName, out GUID id)) rect.spriteID = id;
                rects[i] = rect;
            }

            // 여기서부터가 쓰기 구간이다. 모드를 먼저 확정하고 프로바이더를 **다시** 연다 —
            // 프로바이더는 Init 시점의 직렬화 상태를 들고 있어서, 연 뒤에 바꾼 모드가 Apply 에
            // 되덮일 여지가 있다. 그러면 임포터는 Single 로 남고 frames 만 옛 프레임을 가리킨다.
            // Texture Type 도 같이 확정한다. 이 프로젝트는 3D/URP 라 새로 넣은 PNG 가 Default 로
            // 임포트되는데, 그 상태에서는 Multiple 로 바꿔도 서브스프라이트가 아예 생성되지 않는다
            // (rect 는 들어가는데 에셋이 안 나와 "왜 프레임이 0개냐"로 보인다).
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;

            var writer = OpenDataProvider(importer);
            if (writer == null)
            {
                Fail("스프라이트 데이터 프로바이더를 다시 열 수 없다.");
                return;
            }

            writer.SetSpriteRects(rects);
            writer.Apply();
            EditorUtility.SetDirty(importer);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            FillFromSheet(data, sheet);
        }

        private static void FillFromSheet(SpriteFlipbookData data, Texture2D sheet)
        {
            if (!TryGetImporter(sheet, out string path, out TextureImporter importer)) return;

            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                Fail($"'{sheet.name}' 의 Sprite Mode 가 {importer.spriteImportMode} 다.\n" +
                     "위 '자르고 채우기' 로 격자를 잘라 넣거나, Sprite Editor 에서 슬라이스한 뒤 다시 시도할 것.");
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

        private static bool TryGetImporter(Texture2D sheet, out string path, out TextureImporter importer,
            bool report = true)
        {
            path = null;
            importer = null;
            if (sheet == null) return false;

            path = AssetDatabase.GetAssetPath(sheet);
            if (string.IsNullOrEmpty(path))
            {
                if (report) Fail("시트 텍스처가 프로젝트 에셋이 아니다.");
                return false;
            }

            importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                if (report) Fail($"'{path}' 의 TextureImporter 를 읽을 수 없다.");
                return false;
            }

            return true;
        }

        private static ISpriteEditorDataProvider OpenDataProvider(TextureImporter importer)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();

            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider?.InitSpriteEditorDataProvider();
            return provider;
        }

        private static bool TryGetSourceSize(ISpriteEditorDataProvider provider, out int width, out int height)
        {
            width = 0;
            height = 0;

            var textureProvider = provider.GetDataProvider<ITextureDataProvider>();
            if (textureProvider == null) return false;

            textureProvider.GetTextureActualWidthAndHeight(out width, out height);
            return width > 0 && height > 0;
        }

        private static void Fail(string message)
        {
            EditorUtility.DisplayDialog("시트 자르기 실패", message, "확인");
        }
    }
}
