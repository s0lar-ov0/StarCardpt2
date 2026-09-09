using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StarCard.Core;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>一局结束的结算面板。挂在场景里的 GameOver 上。</summary>
    public class GameOverView : MonoBehaviour
    {
        [Header("显示 / 隐藏")]
        [Tooltip("画外坐标面板。留空则退化成 SetActive 开关")]
        public OffscreenPanel panel;

        [Header("控件（场景物体）")]
        [Tooltip("四方归位-星海归宁 / 时辰已尽")]
        public TMP_Text titleText;

        [Tooltip("总分 / 归位方位 / 回合数 / 卡池余牌")]
        public TMP_Text bodyText;

        [Tooltip("再来一局")]
        public Button againButton;

        [Header("标题文案")]
        public string winTitle = "四方归位-星海归宁";
        public string loseTitle = "时辰已尽";

        [Tooltip("开局时是否隐藏")]
        public bool hideOnStart = true;

        private DriftGameManager _game;

        public void Init(DriftGameManager game, Action onRestart)
        {
            _game = game;

            if (panel == null) panel = GetComponent<OffscreenPanel>();

            if (againButton != null)
                againButton.onClick.AddListener(() =>
                {
                    SetVisible(false);
                    onRestart?.Invoke();
                });

            if (hideOnStart) SetVisible(false);
            _game.GameOverEvent += Show;
        }

        private void OnDestroy()
        {
            if (_game != null) _game.GameOverEvent -= Show;
        }

        private void SetVisible(bool on)
        {
            if (panel != null) panel.SetShown(on);
            else gameObject.SetActive(on);
        }

        private void Show()
        {
            SetVisible(true);
            if (titleText != null) titleText.text = _game.IsWin ? winTitle : loseTitle;
            if (bodyText == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"总分：{_game.Score}");
            sb.AppendLine($"归位方位：{_game.Homecomed.Count} / 4");
            sb.Append("    ");
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
                sb.Append($"{DirectionBlessing.GetBeastName(dir)}{(_game.IsHomecomed(dir) ? "成" : "未")}  ");
            sb.AppendLine();
            sb.AppendLine($"经历回合：{_game.TurnIndex}");
            sb.AppendLine($"卡池余牌：{_game.Pool.Count}");
            bodyText.text = sb.ToString();
        }
    }
}
