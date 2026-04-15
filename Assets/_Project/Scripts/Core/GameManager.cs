using UnityEngine;
using Wassup.Bridge;
using Wassup.Logging;

namespace Wassup.Core
{
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private BattleLogger logger;
        [SerializeField] private BattleBridge battleBridge;
        [SerializeField] private DraftController draftController;
        public BattleLogger Logger => logger;
        public DraftController DraftController => draftController;

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
        }

        private void OnEnable()
        {
            if (logger != null) logger.StartSession();
        }

        // Start runs after all Awake/OnEnable of peers, so DraftView has subscribed
        // to DraftController events before we emit DraftStarted.
        private void Start()
        {
            if (draftController != null)
            {
                draftController.BeginDraft();
            }
            else if (battleBridge != null)
            {
                Debug.LogWarning("[GameManager] draftController unset; starting battle with inspector defenderPool fallback.");
                battleBridge.StartBattle();
            }
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
