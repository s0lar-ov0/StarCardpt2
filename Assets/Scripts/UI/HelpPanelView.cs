using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StarCard.UI
{
    /// <summary>阵型图鉴 + 规则速查。挂在场景里的 HelpPanel 上。</summary>
    public class HelpPanelView : MonoBehaviour
    {
        [Header("显示 / 隐藏")]
        [Tooltip("画外坐标面板。留空则退化成 SetActive 开关")]
        public OffscreenPanel panel;

        [Header("四个方位分栏（场景物体，各挂一个 FormationDisplay）")]
        public FormationDisplay[] formations = new FormationDisplay[4];

        [Header("其它控件")]
        [Tooltip("底部规则速查。留空则不填（想自己在场景里写死文案就留空）")]
        public TMP_Text rulesText;

        [Tooltip("关闭按钮")]
        public Button closeButton;

        [Tooltip("开局时是否隐藏")]
        public bool hideOnStart = true;

        public void Init()
        {
            if (formations != null)
                for (int i = 0; i < formations.Length; i++)
                    if (formations[i] != null) formations[i].Build();

            if (panel == null) panel = GetComponent<OffscreenPanel>();
            if (rulesText != null) rulesText.text = RulesText();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (hideOnStart) SetVisible(false);
        }

        public void Open() => SetVisible(true);
        public void Close() => SetVisible(false);
        public void Toggle() => SetVisible(!IsShown);

        private bool IsShown => panel != null ? panel.IsShown : gameObject.activeSelf;

        private void SetVisible(bool on)
        {
            if (panel != null) panel.SetShown(on);
            else gameObject.SetActive(on);
        }

        private static string RulesText() =>
            "· <b>连结</b>：同一方位至少 3 张星宿牌落在本方位阵型模板的格子上（模板可平移，不可旋转），这些牌即进入连结状态。连结的牌在漂移中不会被吹走。\n" +
            "· <b>归位</b>：某方位 7 宿全部填满一个阵型摆放时，该方位归位 —— 这些牌从棋盘与卡池中消失，玩家获得该方位的永久祝福。四方位全部归位即通关。\n" +
            "· <b>行动</b>：放置手牌 / 移动棋盘上的牌 / 交换两张牌，各算一次行动。行动次数用尽即自动结束回合。\n" +
            "· <b>漂移</b>：回合结束时，棋盘上所有非连结的牌随机移动到其他合法空位 —— 也可能因此凑成连结甚至归位。\n" +
            "· <b>随机事件</b>：棋盘操作阶段每（5 减 已归位方位数）次行动触发一次，等概率抽一种棋盘整体变换 —— 左右/上下平移（边缘绕回另一侧）、九宫格顺逆时针旋转（中心不动）、上下/左右对称。\n" +
            "  这些变换<b>对所有牌生效，连结中的牌也会被搬走</b>；变换后重算连结，可能凭空多出连结甚至归位，也可能让原有连结失效。\n" +
            "· <b>流星定位</b>：每回合开头的限时小游戏。大型白色（慢）= 行动次数 +1；中型属性色（中）= 对应属性的卡池星宿牌；小型粉色（快）= 众星祝福（最多 3 个，满了就不再出现）。\n" +
            "· <b>棋盘</b>：4x7 共 28 格，最多同时容纳 14 张牌。被“虚空吞噬”封锁的格子（显示“虚”）在倒计时结束前不可落牌。";
    }
}
