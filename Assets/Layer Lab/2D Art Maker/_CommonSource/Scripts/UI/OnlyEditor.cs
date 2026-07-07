using UnityEngine;

namespace LayerLab.ArtMaker
{
    /// <summary>
    /// 에디터 환경에서만 오브젝트를 활성화하는 컴포넌트
    /// Component that keeps the object active only in the Editor environment
    /// </summary>
    public class OnlyEditor : MonoBehaviour
    {
        private void Start()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.OSXEditor)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
