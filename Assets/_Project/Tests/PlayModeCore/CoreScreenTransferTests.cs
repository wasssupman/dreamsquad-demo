using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Cards;
using Wassup.BattleCoreUnity.Hud;
using Wassup.BattleCoreUnity.View;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 8a — **옛 화면에만 있던 것이 새 씬에 섰는가.**
    //
    // 완료 기준의 PlayMode 칸을 한 줄씩 옮긴다: 당김 → receipt → 웨이브 도착 · 보너스 → 포탈 열림/닫힘 ·
    // 보스 스폰 → 배너 1회 · 결과 뒤 HUD 비활성 · 스테이지 프랍 틸트 = 저작 값 · 브리핑 웨이브 수 = 코어 플랜.
    // + 재측정이 더한 행: 퇴근 비행 · 전투 중에만 BGM · 기믹 리빌이 판의 시간을 붙들었다 돌려준다 · 손패 유체 배경.
    //
    // 규칙은 코어의 것이라 여기서 다시 묻지 않는다 — **화면이 코어의 값을 옮겨 적는가**만 본다.
    public sealed class CoreScreenTransferTests
    {
        private MatchModeData _mode;

        [TearDown]
        public void TearDown()
        {
            MatchEntryContext.Consume();
            if (_mode != null)
            {
                if (_mode.costConfig != null) Object.DestroyImmediate(_mode.costConfig);
                Object.DestroyImmediate(_mode);
            }
            _mode = null;
        }

        // ── 당김 알약 ────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 당김_알약을_누르면_커맨드가_가고_다음_웨이브가_온다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            var dock = Object.FindAnyObjectByType<CoreNextWaveDock>();
            Assert.IsNotNull(dock, "당김 알약이 새 씬에 없다");

            Assert.IsFalse(dock.PillShown, "배치 중에는 알약이 없다(옛 도크 = 전투 페이즈 표시물)");
            driver.Apply(Command.FinishPlacement());
            yield return WaitUntil(() => driver.Match.Waves.WaveReached >= 1, 3f);
            yield return null;   // 도크 Update 한 번

            var waves = driver.Match.Waves;
            Assume.That(!waves.AuthoredPlan, "저작 플랜 판은 알약이 없다 — 이 씬의 기본 판은 생성 웨이브여야 한다");
            Assert.IsTrue(dock.PillShown, "전투 중·생성 웨이브 판인데 알약이 없다");

            int before = waves.WaveReached;
            int left = waves.PullsLeft;
            Assume.That(left > 0 && before < waves.WaveCount, "당길 여지가 없는 판");
            dock.OnPillClicked();
            Assert.IsTrue(dock.LastReceipt.Accepted, $"당김이 거절됐다: {dock.LastReceipt.Reason}");
            Assert.AreEqual(before + 1, waves.WaveReached, "누르면 다음 웨이브가 예약된다");
            Assert.AreEqual(left - 1, waves.PullsLeft, "상한은 코어가 센다");

            // 상한을 다 쓰면 코어가 거절하고, 알약은 그 사유를 receipt 로 받는다(판정 0).
            for (int i = 0; i < 16 && waves.PullsLeft > 0 && waves.WaveReached < waves.WaveCount; i++)
                dock.OnPillClicked();
            if (waves.WaveReached < waves.WaveCount)
            {
                dock.OnPillClicked();
                Assert.IsFalse(dock.LastReceipt.Accepted);
                Assert.AreEqual(RejectReason.PullCapReached, dock.LastReceipt.Reason);
            }
        }

        // ── 보너스 → 포탈 ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 보너스를_당기면_포탈이_판의_시계로_열리고_닫힌다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            var dock = Object.FindAnyObjectByType<CoreNextWaveDock>();
            var portal = Object.FindAnyObjectByType<CoreBonusPortalPresenter>();
            Assert.IsNotNull(dock);
            Assert.IsNotNull(portal, "보너스 포탈 뷰가 새 씬에 없다");
            Assume.That(driver.Match.Map.Snapshot.BonusSpawns.Length > 0, "이 스테이지는 보너스 포탈 칸이 없다");

            // 판을 짧게 다시 건다 — 임계 1 · 짧은 타임라인. 포탈 **뷰 타이밍**(linger)은 저작 SO 그대로.
            var def = driver.Definition;
            def.Bonus.KillThreshold = 1;
            def.Bonus.MaxStressToOffer = 100f;
            def.Bonus.EnemyCount = 2;
            def.Bonus.PortalAppearDelaySec = 0.2f;
            def.Bonus.FirstSpawnDelaySec = 0.1f;
            def.Bonus.SpawnIntervalSec = 0.05f;
            Assume.That(def.Bonus.Enabled, "보너스 저작이 없는 판");
            driver.Begin(def);
            driver.Apply(Command.FinishPlacement());
            yield return WaitUntil(() => HasEnemy(driver), 4f);
            KillAllEnemies(driver);
            yield return WaitUntil(() => driver.Match.Waves.BonusOffered, 2f);
            Assert.IsTrue(driver.Match.Waves.BonusOffered, "일반 처치 1 로 보너스가 제안되지 않았다");
            yield return null;
            Assert.IsTrue(dock.BonusShown, "코어가 제안했는데 보너스 알약이 안 떴다");

            float pullAt = driver.Match.Clock.BattleTime;
            dock.OnBonusClicked();
            Assert.IsTrue(dock.LastReceipt.Accepted, $"보너스 당김 거절: {dock.LastReceipt.Reason}");

            var entries = Wassup.BattleCore.Wave.BonusWaveSchedule.Build(driver.Match.Map.Snapshot.BonusSpawns.Length,
                def.Bonus.EnemyCount, def.Bonus.FirstSpawnAtSec, def.Bonus.SpawnIntervalSec);
            float linger = driver.BonusAuthoring != null ? driver.BonusAuthoring.portalLingerSec : 0f;
            Assert.AreEqual(pullAt + def.Bonus.PortalAppearDelaySec, portal.OpenAtSec, 1e-4f, "열림 = 당김 + 등장 지연");
            Assert.AreEqual(pullAt + entries[entries.Length - 1].SpawnAtSec + linger, portal.CloseAtSec, 1e-4f,
                "닫힘 = 마지막 스폰 + linger");

            Assert.AreEqual(0, portal.OpenCount, "당긴 순간에는 아직 안 열렸다");
            yield return WaitUntil(() => portal.OpenCount > 0, 3f);
            Assert.GreaterOrEqual(driver.Match.Clock.BattleTime, portal.OpenAtSec - 1e-3f, "판의 시계보다 먼저 열렸다");
            Assert.AreEqual(driver.Match.Map.Snapshot.BonusSpawns.Length, portal.OpenCount, "포탈 칸마다 하나");

            float closeAt = portal.CloseAtSec;
            yield return WaitUntil(() => portal.OpenCount == 0, linger + 4f);
            Assert.AreEqual(0, portal.OpenCount, "linger 가 지났는데 포탈이 남았다");
            Assert.GreaterOrEqual(driver.Match.Clock.BattleTime, closeAt - 1e-3f, "판의 시계보다 먼저 닫혔다");
        }

        // ── 보스 경보 ────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 보스가_태어나면_경보가_한_번_뜬다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            var warning = Object.FindAnyObjectByType<CoreBossWarning>();
            Assert.IsNotNull(warning, "보스 경보가 새 씬에 없다");

            int boss = -1, normal = -1;
            var enemies = driver.EnemyAssets;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == null) continue;
                if (enemies[i].tier == EnemyTier.Boss) { if (boss < 0) boss = i; }
                else if (normal < 0) normal = i;
            }
            Assume.That(boss >= 0, "이 판의 적 표에 보스가 없다");
            driver.Apply(Command.FinishPlacement());

            int shown = warning.ShownCount;
            if (normal >= 0)
            {
                Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemyInLane(normal, 0)).Accepted);
                Assert.AreEqual(shown, warning.ShownCount, "보스가 아닌 적에 경보가 떴다");
            }
            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemyInLane(boss, 0)).Accepted);
            Assert.AreEqual(shown + 1, warning.ShownCount, "보스 스폰 → 배너 1회");
            Assert.IsTrue(warning.Showing);
            yield return null;
        }

        // ── 결과 뒤 HUD ──────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 결과_화면이_뜨면_전투_HUD_가_숨는다()
        {
            _mode = ShortMode();
            MatchEntryContext.Set(ModeSelection.ForTest(_mode));
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            var gate = Object.FindAnyObjectByType<CoreHudGate>();
            var presenter = Object.FindAnyObjectByType<CoreMatchOutcomePresenter>();
            Assert.IsNotNull(gate, "HUD 게이트가 새 씬에 없다");
            Assert.IsFalse(gate.Hidden, "판 중에 HUD 가 숨었다");

            for (int i = 0; i < 900 && !presenter.ResultShown; i++) yield return null;
            Assert.IsTrue(presenter.ResultShown, "짧은 판이 결과까지 안 갔다");
            yield return null;
            Assert.IsTrue(gate.Hidden, "결과 화면 뒤로 HUD 가 남았다(옛 씬은 Result 페이즈에서 숨겼다)");

            var tray = Object.FindAnyObjectByType<CoreDefenderTray>();
            var canvas = tray != null ? tray.GetComponentInParent<Canvas>(true) : null;
            if (canvas != null) Assert.IsFalse(canvas.enabled, "트레이·점수판이 있는 HUD 캔버스가 켜져 있다");
        }

        // ── 페이즈 먹이 · BGM ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator BGM_은_전투_중에만_울린다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            var feed = Object.FindAnyObjectByType<CorePhaseFeed>();
            Assert.IsNotNull(feed);
            Assert.IsNotNull(SoundManager.Instance, "새 씬에 SoundManager 가 없다");
            yield return null;
            Assert.AreEqual(GamePhase.Placement, feed.LastPhase);
            Assert.IsFalse(SoundManager.Instance.BgmPlaying, "배치 중에 BGM 이 울린다");

            driver.Apply(Command.FinishPlacement());
            yield return null; yield return null;
            Assert.AreEqual(GamePhase.Battle, feed.LastPhase);
            Assert.IsTrue(SoundManager.Instance.BgmPlaying, "전투가 시작됐는데 BGM 이 안 울린다(클립 배선 포함)");
        }

        // ── 브리핑 ───────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 메뉴_브리핑은_코어_플랜의_웨이브를_그린다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            var menu = Object.FindAnyObjectByType<CoreMenuPopup>();
            Assert.IsNotNull(menu);
            yield return null;   // 메뉴가 첫 Update 에 자기 화면을 세운다

            Button open = null;
            foreach (var b in menu.GetComponentsInChildren<Button>(true)) if (b.name == "MenuButton") open = b;
            Assert.IsNotNull(open, "메뉴 버튼이 없다");
            open.onClick.Invoke();
            Assert.IsTrue(menu.IsOpen);
            Assert.IsNotNull(menu.Strip, "브리핑 스트립이 없다");
            Assert.AreEqual(driver.Match.Waves.WaveCount, menu.BriefedWaveCount, "브리핑 웨이브 수 = 코어 플랜 웨이브 수");

            // 어댑터가 적을 되찾는 번호는 드라이버 목록과 같다.
            var plan = CoreBriefingPlan.From(driver.Match.Waves, driver.EnemyAssets);
            if (plan.waves != null && plan.waves.Count > 0)
            {
                var w0 = driver.Match.Waves.WaveAt(0);
                Assert.AreEqual(w0.Groups.Length, plan.waves[0].groups.Count);
                Assert.AreSame(driver.EnemyAssets[w0.Groups[0].EnemyIndex], plan.waves[0].groups[0].unit);
            }
            Assert.AreEqual(0f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), "메뉴가 판을 멈추지 않았다");
        }

        // ── 브리지 static 미러 ───────────────────────────────────────────────

        // ⚠ 오늘 새 씬의 스테이지(`MapStage_Duel`)에는 프랍·블롭이 **0** 이다 — 그래서 판 위가 아니라
        // **프랍 프리팹 전수**를 본다(37개 · 블롭 36개). 맵 풀이 들어오면(8b) 이 프리팹들이 새 씬 판에 선다.
        [UnityTest]
        public IEnumerator 프랍_프리팹의_틸트와_블롭은_외형_SO_에서_온다()
        {
#if UNITY_EDITOR
            var view = UnityEditor.AssetDatabase.LoadAssetAtPath<Wassup.Data.BattleView.CharacterViewConfig>(
                "Assets/_Project/Data/BattleView/CharacterViewConfig.asset");
            var blob = UnityEditor.AssetDatabase.LoadAssetAtPath<Wassup.Data.BattleView.BlobShadowConfig>(
                "Assets/_Project/Data/BattleView/BlobShadowConfig.asset");
            Assert.IsNotNull(view);
            Assert.IsNotNull(blob);
            Assert.AreNotEqual(0f, view.PropDistanceTiltFactor, "저작 값이 0 이면 이 테스트는 아무것도 증언하지 않는다");

            int billboards = 0;
            GameObject sample = null;
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Props" }))
            {
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                foreach (var pb in go.GetComponentsInChildren<Wassup.Presentation.PropBillboard>(true))
                {
                    billboards++;
                    Assert.AreEqual(view.PropDistanceTiltFactor, pb.DistanceTiltFactor, 1e-6f,
                        $"프랍 '{go.name}' 의 거리 틸트가 저작 값이 아니다(브리지 static 미러 = 새 씬에서 0)");
                }
                if (sample == null && go.GetComponentInChildren<Wassup.Presentation.BlobShadow>(true) != null) sample = go;
            }
            Assert.Greater(billboards, 0, "프랍 프리팹이 없다");
            Assert.IsNotNull(sample, "블롭을 든 프랍 프리팹이 없다");

            // 브리지가 없는 씬에서 **Awake 가 읽는 색**이 SO 색이다(옛 씬 브리지 색 = 같은 값).
            var inst = Object.Instantiate(sample);
            yield return null;
            var sr = inst.GetComponentInChildren<Wassup.Presentation.BlobShadow>(true).GetComponent<SpriteRenderer>();
            Assert.AreEqual(blob.Color, sr.color, "스테이지 블롭 색이 외형 SO 가 아니다(코드 기본값)");
            Assert.AreSame(blob.Sprite, sr.sprite);
            Object.Destroy(inst);
#else
            yield break;
#endif
        }

        // ── 퇴근 비행 ────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 퇴근하면_유닛이_사라지지_않고_뽑혀_날아간다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            var flight = Object.FindAnyObjectByType<CoreRetireFlightPresenter>();
            var pool = Object.FindAnyObjectByType<CoreUnitViewPool>();
            Assert.IsNotNull(flight, "퇴근 비행이 새 씬에 없다");

            driver.Apply(Command.FinishPlacement());
            var id = PlaceAny(driver);
            Assert.IsTrue(id.IsEntity, "놓을 자리가 없다");
            yield return WaitUntil(() => driver.Find(id) != null && !driver.Find(id).Deploying, 5f);
            Assert.IsTrue(pool.TryGet(id, out var view));

            var receipt = driver.Apply(Command.Retire(id));
            Assume.That(receipt.Accepted, $"퇴근 거절: {receipt.Reason}");
            Assert.IsNull(driver.Find(id), "퇴근했는데 코어에 남았다");
            Assert.IsFalse(pool.TryGet(id, out _), "뷰가 풀에 남았다 — 떼어 넘기지 않았다");
            Assert.AreEqual(1, flight.InFlightCount, "퇴근 연출이 시작되지 않았다");
            Assert.IsTrue(view != null, "뷰가 연출 전에 파괴됐다");

            yield return WaitUntil(() => flight.InFlightCount == 0, 5f);
            Assert.AreEqual(0, flight.InFlightCount, "퇴근 연출이 끝나지 않았다");
            yield return null;   // `Destroy` 는 프레임 끝에 적용된다
            Assert.IsTrue(view == null, "연출이 끝났는데 뷰가 남았다");
        }

        // ── 기믹 리빌 ────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 기믹_리빌은_판의_시간을_붙들었다가_돌려준다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            var reveal = Object.FindAnyObjectByType<CoreGimmickReveal>();
            Assert.IsNotNull(reveal, "기믹 리빌이 새 씬에 없다");
            GimmickData gimmick = null;
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:GimmickData"))
            {
                gimmick = UnityEditor.AssetDatabase.LoadAssetAtPath<GimmickData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (gimmick != null) break;
            }
#endif
            Assume.That(gimmick != null, "기믹 저작이 없다");

            // 기믹 없는 판은 붙들지 않는다(오늘 라이브 모드는 기믹 꺼짐 — 판 시작에 사건이 없다).
            Assert.IsFalse(reveal.Holding);
            reveal.BeginIntro(null);
            Assert.IsFalse(reveal.Holding, "기믹 없는 판을 붙들었다");

            reveal.BeginIntro(gimmick);
            Assert.IsTrue(reveal.Holding, "리빌이 판의 시간을 세우지 않았다(GimmickRevealConfig 배선 포함)");
            Assert.AreEqual(0f, TimeManager.Instance.ScaleOf(TimeDomain.Battle));
            yield return WaitUntil(() => !reveal.Holding, 6f);
            Assert.IsFalse(reveal.Holding, "리빌이 끝나지 않았다");
            Assert.AreEqual(1f, TimeManager.Instance.ScaleOf(TimeDomain.Battle), 1e-4f, "리스를 반납하지 않았다 — 판이 멈춘 채다");
        }

        // ── 손패 유체 배경 ───────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator 손패가_닫혀_있으면_유체_배경은_꺼져_있다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            var backdrop = Object.FindAnyObjectByType<CoreHandFluidBackdrop>();
            var hand = Object.FindAnyObjectByType<CoreHandView>();
            Assert.IsNotNull(backdrop, "손패 유체 배경이 새 씬에 없다");
            Assert.IsNotNull(hand);
            yield return null;
            Assert.AreEqual(hand.State == CoreHandView.HandState.Hand, backdrop.Open,
                "배경의 게이트가 손패 상태를 따르지 않는다");
            var sim = backdrop.GetComponent<Wassup.Presentation.FluidPaintSim>();
            if (sim != null && !backdrop.Open) Assert.IsFalse(sim.enabled, "닫힌 손패인데 유체 sim 이 돈다");
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        private static IEnumerator WaitUntil(System.Func<bool> cond, float seconds)
        {
            for (float t = 0f; !cond() && t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        private static bool HasEnemy(BattleDriver driver)
        {
            var units = driver.Match.World.Units;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Faction == Wassup.Battle.Units.Faction.EnemyUnit && !units[i].Dead) return true;
            return false;
        }

        // 테스트 전용 — 피해 받은 편지함에 넣어 **처치 사건**을 내게 한다(EditMode `MatchWaveTests` 와 같은 손).
        private static void KillAllEnemies(BattleDriver driver)
        {
            var units = driver.Match.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Faction != Wassup.Battle.Units.Faction.EnemyUnit || u.Dead) continue;
                u.Inbox.Damage.Add(new DamageEntry { Amount = 99999f, Source = SimEntityId.Match });
            }
        }

        private static SimEntityId PlaceAny(BattleDriver driver)
        {
            var placement = driver.Match.Placement;
            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (!placement.InRoster(i)) continue;
                for (int y = 0; y < driver.GridSize.y; y++)
                for (int x = 0; x < driver.GridSize.x; x++)
                {
                    if (placement.Judge(i, new int2(x, y)) != RejectReason.None) continue;
                    if (!driver.Apply(Command.PlaceDefender(i, new int2(x, y))).Accepted) continue;
                    var units = driver.Match.World.Units;
                    for (int k = units.Count - 1; k >= 0; k--)
                        if (units[k].Kind == UnitKind.Defender) return units[k].Id;
                }
            }
            return SimEntityId.None;
        }

        // 배치 페이즈가 **아예 없는** 2초 모드(`CoreMatchOutcomeTests.ShortMode` 와 같은 모양).
        private static MatchModeData ShortMode()
        {
            var m = ScriptableObject.CreateInstance<MatchModeData>();
            m.modeId = "test_hud_gate";
            m.goalKind = GoalKind.KillScoreTimed;
            m.clockKind = ClockKind.FixedLimit;
            m.durationSec = 2f;
            m.submitUnlockSec = 0f;
            m.allowSubmit = false;
            m.submitsReport = false;
            m.waveSourceKind = WaveSourceKind.GeneratedFromDeck;
            m.placementPhaseEnabled = false;
            m.autoStartCountdownSec = 0f;
            m.gimmickEnabled = false;
            m.costConfig = ScriptableObject.CreateInstance<CostConfig>();
            return m;
        }
    }
}
