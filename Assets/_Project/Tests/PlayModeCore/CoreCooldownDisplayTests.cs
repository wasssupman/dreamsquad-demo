using System.Collections;
using NUnit.Framework;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.BattleCoreUnity.Hud;

namespace Somnia.Battle.Tests.PlayMode.Core
{
    // battle-core-rebuild 5b 수정 — **사망·퇴근 뒤 재배치 대기가 보이는가.**
    //
    // 사용자 플레이 2차의 문장: **「유닛 선택 UI 의 사망/퇴근 쿨타임은 아직 미구현?」**.
    //
    // 둘을 따로 묻는다:
    //   ① 코어가 실제로 대기를 도는가(`CooldownFraction` > 0) — 값은 결함 1 수정으로 실렸다
    //   ② 화면이 그것을 **말하는가** — 어두운 덮개만으로는 「얼마나 남았나」를 못 읽는다.
    //      옛 트레이(`defender-placement-cooldown` 2)는 덮개 **+ 남은 초**였다.
    public sealed class CoreCooldownDisplayTests
    {
        [UnityTest]
        public IEnumerator 퇴근하면_그_종류의_재배치_대기가_돈다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");
            driver.Apply(Command.FinishPlacement());

            var placement = driver.Match.Placement;
            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor),
                "로스터의 어떤 유닛도 이 판 어디에도 놓을 수 없다");

            float retireCd = driver.Definition.Units[defIndex].EffectiveRetireCooldown;
            Assert.Greater(retireCd, 0f,
                "퇴근 대기가 0 이다 — 저작이 정의표에 안 실렸다(결함 1 과 같은 종류)");

            Assert.IsTrue(driver.Apply(Command.PlaceDefender(defIndex, anchor)).Accepted);
            var id = LastDefender(driver);
            Assert.IsTrue(driver.Apply(Command.Retire(id)).Accepted, "퇴근이 거절됐다");

            Assert.Greater(placement.CooldownRemaining(defIndex), 0f,
                "퇴근했는데 재배치 대기가 안 걸렸다");
            Assert.Greater(placement.CooldownFraction(defIndex), 0f,
                "남은 비율이 0 이다 — 아이콘 레이디얼이 아무것도 못 그린다");
            Assert.AreEqual(RejectReason.OnCooldown, placement.SlotBlock(defIndex),
                "대기 중인데 슬롯이 「쓸 수 있다」고 답한다");
        }

        [UnityTest]
        public IEnumerator 트레이는_남은_초를_숫자로_보여_준다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);
            var tray = Object.FindAnyObjectByType<CoreDefenderTray>();
            Assert.IsNotNull(tray, "트레이가 씬에 없다");
            driver.Apply(Command.FinishPlacement());
            yield return null;

            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor));
            Assert.Greater(driver.Definition.Units[defIndex].EffectiveRetireCooldown, 0f);

            // 대기가 걸리기 전에는 숫자가 **없어야** 한다 — 평소에 0 이 떠 있으면 노이즈다.
            yield return null;
            Assert.IsFalse(TryReadCooldownText(tray, defIndex, out _),
                "대기가 없는데 남은 초가 떠 있다");

            Assert.IsTrue(driver.Apply(Command.PlaceDefender(defIndex, anchor)).Accepted);
            Assert.IsTrue(driver.Apply(Command.Retire(LastDefender(driver))).Accepted);
            yield return null;                      // 트레이가 도색하는 프레임

            Assert.IsTrue(TryReadCooldownText(tray, defIndex, out string shown),
                "재배치 대기 중인데 남은 초가 안 보인다 — 어두운 덮개만으로는 얼마나 남았는지 못 읽는다");

            int want = Mathf.CeilToInt(driver.Match.Placement.CooldownRemaining(defIndex));
            Assert.AreEqual(want.ToString(), shown,
                "보여 주는 숫자가 코어의 남은 초와 다르다 — 화면이 자기 셈을 갖고 있다");
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        // 트레이가 「어느 칸을 칠했나」를 내주는 창구를 따로 두지 않으려고 계층에서 찾는다.
        private static bool TryReadCooldownText(CoreDefenderTray tray, int defIndex, out string text)
        {
            text = null;
            var labels = tray.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].gameObject.name != "CooldownText") continue;
                var slotRoot = labels[i].transform.parent;
                while (slotRoot != null && !slotRoot.name.StartsWith("Slot_")) slotRoot = slotRoot.parent;
                if (slotRoot == null || slotRoot.name != "Slot_" + defIndex) continue;
                if (!labels[i].enabled || string.IsNullOrEmpty(labels[i].text)) return false;
                text = labels[i].text;
                return true;
            }
            return false;
        }

        private static SimEntityId LastDefender(BattleDriver driver)
        {
            var units = driver.Match.World.Units;
            for (int i = units.Count - 1; i >= 0; i--)
                if (units[i].Kind == UnitKind.Defender) return units[i].Id;
            Assert.Fail("판에 방어유닛이 없다");
            return SimEntityId.None;
        }

        private static bool TryFindPlaceable(BattleDriver driver, out int defIndex, out int2 anchor)
        {
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (!placement.InRoster(i)) continue;
                for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    var c = new int2(x, y);
                    if (placement.Judge(i, c) != RejectReason.None) continue;
                    defIndex = i;
                    anchor = c;
                    return true;
                }
            }
            defIndex = -1;
            anchor = default;
            return false;
        }
    }
}
