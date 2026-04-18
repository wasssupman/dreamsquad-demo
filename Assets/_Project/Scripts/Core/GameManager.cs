using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Logging;
using Wassup.UI;

namespace Wassup.Core
{
    public enum GamePhase { None, Briefing, Draft, Placement, Battle, Result }

    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private BattleLogger logger;
        [SerializeField] private BattleBridge battleBridge;
        [SerializeField] private DraftController draftController;
        [SerializeField] private TimelineBriefingView timelineBriefing;
        [SerializeField] private CostRuntime costRuntime;
        [SerializeField] private CostConfig costConfig;
        [SerializeField] private SkillLoadoutController skillLoadout;
        public BattleLogger Logger => logger;
        public DraftController DraftController => draftController;
        public CostRuntime CostRuntime => costRuntime;
        public CostConfig CostConfig => costConfig;
        public SkillLoadoutController SkillLoadout => skillLoadout;
        public bool IsAiming { get; set; }
        public DefenderUnitData SelectedDefender { get; set; }

        public GamePhase CurrentPhase { get; private set; } = GamePhase.None;
        public event System.Action<GamePhase> PhaseChanged;

        // Fired whenever a UI layer wants all *other* aim-style selections to
        // cancel (e.g. picking a defender should cancel any active skill aim,
        // picking a skill slot should clear the pending defender selection).
        // Subscribers clear their own local aim state; publisher does not know
        // who listens. Keeps input modes mutually exclusive — last click wins.
        public event System.Action AimCanceled;
        public void RaiseAimCanceled() => AimCanceled?.Invoke();

        public void SetPhase(GamePhase phase)
        {
            if (CurrentPhase == phase) return;
            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (logger == null)
            {
                Debug.LogWarning("[GameManager] BattleLogger reference missing. Attempting GetComponentInChildren.");
                logger = GetComponentInChildren<BattleLogger>();
            }
            if (costRuntime == null) costRuntime = GetComponentInChildren<CostRuntime>();
            if (costRuntime != null && costConfig != null)
            {
                costRuntime.Configure(costConfig.startingCost, costConfig.maxCost, costConfig.regenPerSec);
            }
            if (skillLoadout == null) skillLoadout = GetComponentInChildren<SkillLoadoutController>();
            if (skillLoadout == null)
            {
                skillLoadout = gameObject.AddComponent<SkillLoadoutController>();
                Debug.LogWarning("[GameManager] SkillLoadoutController missing; added fallback component on GameManager.", this);
            }
        }

        private void OnEnable()
        {
            if (logger != null) logger.StartSession();
        }

        // Start runs after all Awake/OnEnable of peers, so DraftView has subscribed
        // to DraftController events before we emit DraftStarted.
        private void Start()
        {
            if (timelineBriefing != null && draftController != null)
            {
                SetPhase(GamePhase.Briefing);
                timelineBriefing.BriefingConfirmed = OnBriefingConfirmed;
                timelineBriefing.Show();
            }
            else if (draftController != null)
            {
                SetPhase(GamePhase.Draft);
                draftController.BeginDraft();
            }
            else if (battleBridge != null)
            {
                Debug.LogWarning("[GameManager] draftController unset; starting battle with inspector defenderPool fallback.");
                SetPhase(GamePhase.Battle);
                battleBridge.StartBattle();
            }
        }

        private void OnBriefingConfirmed()
        {
            SetPhase(GamePhase.Draft);
            draftController.BeginDraft();
        }

        private void OnDisable()
        {
            if (battleBridge != null) battleBridge.StopBattle();
            if (logger != null) logger.EndSession();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
