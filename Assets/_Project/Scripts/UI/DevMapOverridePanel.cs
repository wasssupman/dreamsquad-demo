using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data.MapGrid;

namespace Wassup.UI
{
    // map-play-feel unit 2 — 개발 확인용 맵 강제 스테퍼. 로비 DevOnlyGroup 아래에 두어
    // 개발 빌드/에디터에서만 노출된다(릴리스 APK 무노출). ◀ ▶ 로 풀 인덱스 순환, OFF 로 서버 시드 복귀.
    // 값은 DevMapOverride(PlayerPrefs)에 저장 → 다음 배틀 진입 시 BattleBridge 가 최우선으로 읽는다.
    public class DevMapOverridePanel : MonoBehaviour
    {
        [SerializeField] private MapDocumentPool pool;
        [SerializeField] private TMP_Text label;    // "5:Hook" 또는 "OFF"
        [SerializeField] private Button prevButton;  // ◀
        [SerializeField] private Button nextButton;  // ▶
        [SerializeField] private Button offButton;   // OFF

        private void Awake()
        {
            if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(+1));
            if (offButton != null) offButton.onClick.AddListener(Off);
        }

        private void OnEnable() => Refresh();

        private void Step(int dir)
        {
            int count = pool != null ? pool.Count : 0;
            if (count <= 0) return;

            // endless-mode unit 3 — 스텝 사이클에 ENDLESS 슬롯(마지막 인덱스 다음)을 추가.
            // 슬롯 [0..count-1]=풀 인덱스, 슬롯 count=무한 모드. OFF 는 별도(OFF 버튼).
            int total = count + 1;
            int cur;
            if (DevMapOverride.Endless) cur = count;
            else if (DevMapOverride.HasIndex) cur = DevMapOverride.Index;
            else cur = dir > 0 ? -1 : total;                    // OFF 진입: ▶=0, ◀=ENDLESS
            int next = ((cur + dir) % total + total) % total;

            if (next >= count)                                  // ENDLESS 슬롯
            {
                DevMapOverride.Endless = true;
                DevMapOverride.Index = -1;                      // 인덱스 off (무한이 우선하지만 표시 정합)
            }
            else
            {
                DevMapOverride.Endless = false;
                DevMapOverride.Index = next;
            }
            Refresh();
        }

        private void Off()
        {
            DevMapOverride.Clear();
            Refresh();
        }

        private void Refresh()
        {
            if (label == null) return;
            if (DevMapOverride.Endless) { label.text = "ENDLESS"; return; }
            if (!DevMapOverride.HasIndex) { label.text = "MAP?"; return; }

            int i = DevMapOverride.Index;
            string name = (pool != null && i >= 0 && i < pool.Count && pool.Get(i).document != null)
                ? pool.Get(i).document.name.Replace("MapDocument_", "")
                : "?";
            label.text = $"{i}:{name}";
        }
    }
}
