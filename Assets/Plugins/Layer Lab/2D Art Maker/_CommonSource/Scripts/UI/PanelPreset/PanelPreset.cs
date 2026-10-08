using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LayerLab.ArtMaker
{
    public class PanelPreset : MonoBehaviour
    {
        #region Fields and Properties
       
        private List<PresetSlot> _presetSlots = new();
       
        #endregion

        #region Unity Lifecycle
       
        /// <summary>
        /// 초기화
        /// Initialize
        /// </summary>
        public void Init()
        {
            var index = 0;
            _presetSlots = transform.GetComponentsInChildren<PresetSlot>().ToList();
            foreach (var t in _presetSlots)
            {
                t.Init(index);
                index++;
            }
           
        }
       
        #endregion
    }
}