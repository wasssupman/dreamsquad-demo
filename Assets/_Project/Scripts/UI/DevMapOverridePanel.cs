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

            // map-painter-tool unit 5 — 풀 뒤에 dev 슬롯(devEntries, 시드 선택 미포함)을 이어붙인다.
            // 슬롯 [0..count-1]=풀, [count..count+dev-1]=dev 맵. OFF 는 별도(OFF 버튼).
            // endless-mode-removal unit 0 — 마지막의 ENDLESS 슬롯이 빠져 total == steppable 이다.
            int total = count + (pool != null ? pool.DevCount : 0);
            if (total <= 0) return;
            int cur = DevMapOverride.HasIndex ? DevMapOverride.Index : (dir > 0 ? -1 : total);
            DevMapOverride.Index = ((cur + dir) % total + total) % total;
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
            if (!DevMapOverride.HasIndex) { label.text = "MAP?"; return; }

            int i = DevMapOverride.Index;
            int count = pool != null ? pool.Count : 0;
            if (pool != null && i >= count && i < count + pool.DevCount)
            {
                // dev 슬롯 — 풀 본편과 구분되는 라벨(시드 선택에 안 잡히는 맵임을 표시).
                int d = i - count;
                var devDoc = pool.GetDev(d).document;
                label.text = $"D{d}:{(devDoc != null ? devDoc.name.Replace("MapDocument_", "") : "?")}";
                return;
            }
            string name = (pool != null && i >= 0 && i < count && pool.Get(i).document != null)
                ? pool.Get(i).document.name.Replace("MapDocument_", "")
                : "?";
            label.text = $"{i}:{name}";
        }
    }
}
