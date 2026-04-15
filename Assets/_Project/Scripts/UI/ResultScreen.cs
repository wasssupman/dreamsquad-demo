using TMPro;
using UnityEngine;

namespace Wassup.UI
{
    // Manages the battle result overlay (DEFEAT / VICTORY). Disabled on Awake.
    public class ResultScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI resultLabel;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void ShowDefeat()
        {
            resultLabel.text = "DEFEAT";
            gameObject.SetActive(true);
        }

        public void ShowVictory()
        {
            resultLabel.text = "VICTORY";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
