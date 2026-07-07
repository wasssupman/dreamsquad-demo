using UnityEditor;
using UnityEngine;

namespace LayerLab.ArtMaker
{
    /// <summary>
    /// PartsManager 커스텀 인스펙터
    /// Custom inspector for PartsManager
    /// </summary>
    [CustomEditor(typeof(PartsManager))]
    public class PartsManagerEditor : Editor
    {
        /// <summary>
        /// 인스펙터 GUI 렌더링
        /// Render inspector GUI
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var script = (PartsManager)target;
            if (GUILayout.Button("SetSkinData")) script.SetSkin();
        }
    }
}