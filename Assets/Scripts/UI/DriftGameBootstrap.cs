using UnityEngine;
using UnityEngine.EventSystems;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 把场景里搭好的各个 View 和 DriftGameManager 接起来，然后开局。
    /// 挂在场景里的 DriftGame 上（和 DriftGameManager 同一个物体）。
    ///
    /// **它不再生成任何 UI** —— 全部界面都是场景物体，下面这些字段拖引用即可。
    /// Init 的调用顺序在这里是写死的（有依赖，见 Wire()）。
    /// </summary>
    [RequireComponent(typeof(DriftGameManager))]
    public class DriftGameBootstrap : MonoBehaviour
    {
        [Header("各个界面（拖场景里的物体）")]
        public BoardView board;
        public HandView hand;
        public HudView hud;
        public MeteorFieldView meteorField;
        public RewardPickerView rewardPicker;
        public HelpPanelView helpPanel;
        public GameOverView gameOver;

        [Header("开局")]
        [Tooltip("Start 时自动开一局。关掉的话要自己调 StartGame()")]
        public bool autoStart = true;

        [Header("快捷键")]
        [Tooltip("结束定位 / 结束回合")] public KeyCode endPhaseKey = KeyCode.Space;
        [Tooltip("开关阵型图鉴")] public KeyCode helpKey = KeyCode.H;
        [Tooltip("重开一局")] public KeyCode restartKey = KeyCode.R;
        [Tooltip("关掉就不响应快捷键")] public bool enableHotkeys = true;

        private DriftGameManager _game;

        private void Awake()
        {
            _game = GetComponent<DriftGameManager>();
            EnsureEventSystem();
            Wire();
        }

        private void Start()
        {
            if (autoStart) _game.StartGame();
        }

        /// <summary>
        /// 按依赖顺序 Init。这个顺序不能随便改：
        ///   HandView 要先于 BoardView（棋盘要读手牌的选中状态）；
        ///   HudView 最后（它的 Init 里会立刻 Refresh 一次，要求前面的都已就绪）。
        /// </summary>
        private void Wire()
        {
            if (hand == null || board == null)
            {
                Debug.LogError("[DriftGameBootstrap] board / hand 没拖，游戏没法开始。", this);
                return;
            }

            hand.Init(_game);
            board.Init(_game, hand);

            if (meteorField != null) meteorField.Init(_game);
            else Debug.LogWarning("[DriftGameBootstrap] meteorField 没拖，流星定位阶段会是空屏。", this);

            if (rewardPicker != null) rewardPicker.Init(_game);
            else Debug.LogWarning("[DriftGameBootstrap] rewardPicker 没拖，定位收获会没法取舍。", this);

            if (helpPanel != null) helpPanel.Init();
            if (gameOver != null) gameOver.Init(_game, Restart);

            if (hud != null) hud.Init(_game, Restart, ToggleHelp);
            else Debug.LogWarning("[DriftGameBootstrap] hud 没拖，顶栏和按钮都不会工作。", this);
        }

        public void StartGame() => _game.StartGame();

        public void Restart()
        {
            if (hand != null) hand.ClearSelection();
            if (board != null) board.ClearSelection();
            _game.StartGame();
        }

        public void ToggleHelp()
        {
            if (helpPanel != null) helpPanel.Toggle();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            Debug.LogWarning("[DriftGameBootstrap] 场景里没有 EventSystem，已自动补一个。建议手动加到场景里。");
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void Update()
        {
            if (!enableHotkeys) return;

            if (Input.GetKeyDown(endPhaseKey))
            {
                if (_game.Phase == DriftPhase.Meteor) _game.EndMeteorPhase();
                else if (_game.Phase == DriftPhase.Board) _game.EndTurnByPlayer();
                else if (_game.Phase == DriftPhase.TurnReview) _game.FinishTurnReview();
            }
            if (Input.GetKeyDown(helpKey)) ToggleHelp();
            if (Input.GetKeyDown(restartKey)) Restart();
        }
    }
}
