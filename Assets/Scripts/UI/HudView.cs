using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StarCard.Core;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 顶栏 + 右侧栏 + 事件横幅 + 三个按钮。挂在场景里的 Hud 上。
    /// 全部控件都是场景物体，Inspector 拖进来即可；拖漏的字段会被跳过（不报错，只是不更新）。
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [Header("顶栏文字")]
        [Tooltip("回合 3/15")] public TMP_Text turnText;
        [Tooltip("阶段 棋盘操作")] public TMP_Text phaseText;
        [Tooltip("行动次数 4")] public TMP_Text actionText;
        [Tooltip("生变倒计时 2/5")] public TMP_Text eventText;
        [Tooltip("卡池 18 | 棋盘 7/14")] public TMP_Text poolText;
        [Tooltip("分数 1240")] public TMP_Text scoreText;

        [Header("四方位进度（顺序：东 北 西 南，对应 青龙 玄武 白虎 朱雀）")]
        [Tooltip("四个 Chip 的底图，会按方位色染色")]
        public Image[] directionChips = new Image[4];

        [Tooltip("四个 Chip 里的文字，显示 青龙 3/7")]
        public TMP_Text[] directionTexts = new TMP_Text[4];

        [Header("右侧栏")]
        [Tooltip("祝福面板（取代原来的星语栏）。由 BlessingPanelView 自己管，Hud 不碰它")]
        public BlessingPanelView blessingPanel;

        [Header("按钮")]
        public Button endTurnButton;
        public Button helpButton;
        public Button restartButton;

        [Header("事件横幅")]
        [Tooltip("横幅根物体，平时 SetActive(false)")] public GameObject banner;
        [Tooltip("横幅里的文字")] public TMP_Text bannerText;
        [Tooltip("横幅停留秒数")] public float bannerDuration = 2.6f;

        [Header("日志")]

        private DriftGameManager _game;
        private float _bannerLife;

        /// <summary>directionChips / directionTexts 的下标顺序，与 Direction 枚举一致。</summary>
        private static readonly Direction[] DirOrder =
        {
            Direction.East, Direction.North, Direction.West, Direction.South
        };

        public void Init(DriftGameManager game, Action onRestart, Action onToggleHelp)
        {
            _game = game;

            if (endTurnButton != null) endTurnButton.onClick.AddListener(() => _game.EndTurnByPlayer());
            if (helpButton != null) helpButton.onClick.AddListener(() => onToggleHelp?.Invoke());
            if (restartButton != null) restartButton.onClick.AddListener(() => onRestart?.Invoke());
            if (banner != null) banner.SetActive(false);

            _game.StateChanged += Refresh;
            _game.RandomEventFired += OnRandomEvent;
            _game.Homecoming += OnHomecoming;
            _game.PhaseChanged += OnPhaseChanged;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_game == null) return;
            _game.StateChanged -= Refresh;
            _game.RandomEventFired -= OnRandomEvent;
            _game.Homecoming -= OnHomecoming;
            _game.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(DriftPhase phase) => Refresh();

        private void OnRandomEvent(RandomEventDef def) => ShowBanner($"【随机事件】{def.Name} — {def.Desc}");

        private void OnHomecoming(Direction dir) =>
            ShowBanner($"【{DirectionBlessing.GetBeastName(dir)}归位】  {DirectionBlessing.GetName(dir)}：{DirectionBlessing.GetDesc(dir)}");

        public void ShowBanner(string content)
        {
            if (banner == null) return;
            if (bannerText != null) bannerText.text = content;
            banner.SetActive(true);
            _bannerLife = bannerDuration;
        }

        private void Update()
        {
            // 回合结算的倒计时每帧在变，而 StateChanged 在停顿期间不会触发，
            // 所以这里单独刷一下那一格文字。
            if (_game != null && _game.Phase == DriftPhase.TurnReview && actionText != null)
                actionText.text = $"下回合 {_game.ReviewTimeLeft:0.0} 秒后";

            if (banner == null || !banner.activeSelf) return;
            _bannerLife -= Time.deltaTime;
            if (_bannerLife <= 0f) banner.SetActive(false);
        }

        public void Refresh()
        {
            if (_game == null || _game.Board == null) return;

            int maxTurns = _game.MaxTurns;   // 含廉贞加成
            if (turnText != null)
                turnText.text = maxTurns > 0 ? $"回合 {_game.TurnIndex}/{maxTurns}" : $"回合 {_game.TurnIndex}";
            if (phaseText != null)
                phaseText.text = "阶段 " + PhaseName(_game.Phase);
            if (actionText != null)
            {
                if (_game.Phase == DriftPhase.Board)
                    actionText.text = $"行动次数 {_game.ActionsLeft}";
                else if (_game.Phase == DriftPhase.TurnReview)
                    actionText.text = $"下回合 {_game.ReviewTimeLeft:0.0} 秒后";
                else
                    actionText.text = $"行动次数 —（+{_game.PendingBonusActions}）";
            }
            if (eventText != null)
                eventText.text = _game.Phase == DriftPhase.Board
                    ? $"生变倒计时 {_game.ActionsUntilEvent}/{_game.EventInterval}"
                    : $"生变间隔 {_game.EventInterval}";
            if (poolText != null)
                poolText.text = $"卡池 {_game.Pool.Count} | 棋盘 {_game.Board.CardCount}/{_game.Config.boardCardLimit}";
            if (scoreText != null)
                scoreText.text = $"分数 {_game.Score}";

            for (int i = 0; i < DirOrder.Length; i++)
            {
                var dir = DirOrder[i];
                bool home = _game.IsHomecomed(dir);
                int best = _game.Links.BestCount.TryGetValue(dir, out var b) ? b : 0;
                var c = MeteorPalette.ColorOfDirection(dir);

                if (directionTexts != null && i < directionTexts.Length && directionTexts[i] != null)
                    directionTexts[i].text = home
                        ? $"{DirectionBlessing.GetBeastName(dir)}\n已归位"
                        : $"{DirectionBlessing.GetBeastName(dir)}\n{best}/7";

                if (directionChips != null && i < directionChips.Length && directionChips[i] != null)
                    directionChips[i].color = home ? new Color(c.r, c.g, c.b, 0.55f) : new Color(c.r, c.g, c.b, 0.14f);
            }

            if (endTurnButton != null) endTurnButton.interactable = _game.Phase == DriftPhase.Board;
        }

        public static string PhaseName(DriftPhase phase) => phase switch
        {
            DriftPhase.Ready => "待开局",
            DriftPhase.Meteor => "流星定位",
            DriftPhase.RewardPick => "收获取舍",
            DriftPhase.Board => "棋盘操作",
            DriftPhase.TurnReview => "回合结算",
            DriftPhase.GameOver => "本局结束",
            _ => "?"
        };
    }
}
