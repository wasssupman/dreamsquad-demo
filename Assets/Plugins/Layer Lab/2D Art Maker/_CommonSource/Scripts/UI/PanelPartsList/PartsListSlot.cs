using UnityEngine;
using UnityEngine.UI;

namespace LayerLab.ArtMaker
{
    public class PartsListSlot : MonoBehaviour
    {
        public int SlotIndex { get; private set; }
        
        [SerializeField] private Image imageItem;
        private PanelPartsList _panelPartsList;

        /// <summary>
        /// 아이템 색 변경
        /// Change item color
        /// </summary>
        /// <param name="color">색상 / Color</param>
        public void ChangeColor(Color color)
        {
            imageItem.color = color;
        }

        /// <summary>
        /// 프리셋 슬롯 설정
        /// Set preset slot
        /// </summary>
        /// <param name="panelPartsList">파츠 리스트 패널 / Parts list panel</param>
        /// <param name="sprite">스프라이트 / Sprite</param>
        /// <param name="index">인덱스 / Index</param>
        public void SetSlot(PanelPartsList panelPartsList, Sprite sprite, int index)
        {
            SlotIndex = index;
            _panelPartsList = panelPartsList;
            imageItem.sprite = sprite;
            imageItem.SetNativeSize();
        }

        /// <summary>
        /// 프리셋 슬롯 선택
        /// Select preset slot
        /// </summary>
        public void SelectSlot()
        {
            AudioManager.Instance.PlaySound(SoundList.ButtonDefault);
            _panelPartsList.SelectSlot(this);
        }
    }
}