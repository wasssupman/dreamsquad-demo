using UnityEngine;

namespace LayerLab.ArtMaker
{
    /// <summary>
    /// 화면 비율에 따라 오브젝트 크기를 자동 조정하는 컴포넌트
    /// Component that automatically scales the object based on screen aspect ratio
    /// </summary>
    public class ObjectScaler : MonoBehaviour
    {
        private void Start()
        {
            var screenRatio = (float)Screen.width / Screen.height;
            const float targetRatio = 1920f / 1080f;
            var scale = screenRatio / targetRatio;
            transform.localScale = scale >= 1f ? new Vector3(scale, scale, 1f) : new Vector3(1f, 1f, 1f);
        }
    }
}
