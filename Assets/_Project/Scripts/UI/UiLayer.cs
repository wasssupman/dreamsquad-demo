using UnityEngine;

namespace Wassup.UI
{
    /// <summary>
    /// 런타임에 코드로 생성되는 UI GameObject 트리를 프로젝트 "UI" 레이어로 통일한다.
    /// Scene 뷰에서 UI 레이어를 통째로 잠그면(Layers 드롭다운) 게임오브젝트 선택이 UI에 가로막히지 않는다.
    /// UI는 프리팹이 아니라 각 View 의 BuildCanvas()/슬롯 재생성에서 new GameObject 로 만들어지므로,
    /// 트리를 완성한 직후 이 헬퍼를 호출해 레이어를 일괄 부여한다.
    /// </summary>
    public static class UiLayer
    {
        public const string LayerName = "UI";

        private static int _cached = -2; // -2 = 미조회, -1 = 레이어 없음

        public static int Index
        {
            get
            {
                if (_cached == -2) _cached = LayerMask.NameToLayer(LayerName);
                return _cached;
            }
        }

        /// <summary>root 와 그 하위 모든 GameObject 를 UI 레이어로 설정한다. (비활성 포함)</summary>
        public static void Apply(GameObject root)
        {
            if (root == null) return;
            int layer = Index;
            if (layer < 0) return; // UI 레이어 미정의 시 무시

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = layer;
            }
        }
    }
}
