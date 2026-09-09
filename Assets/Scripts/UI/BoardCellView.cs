using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 棋盘上的一个空位。挂在场景里 Board/Cells/Cell_r_c 上。
    /// 它自己不管游戏逻辑，只负责显示底色和「虚」字，点击转交给 BoardView。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BoardCellView : MonoBehaviour
    {
        [Header("坐标（Cell_行_列，从 0 开始）")]
        public int row;
        public int col;

        [Header("子控件")]
        [Tooltip("格子底图。留空则自动取自己身上的 Image")]
        public Image background;

        [Tooltip("封锁时显示 虚 + 剩余回合。留空则不显示")]
        public TMP_Text label;

        [Tooltip("点击按钮。留空则自动取自己身上的 Button")]
        public Button button;

        [Header("配色")]
        public Color normalColor = new Color(1f, 1f, 1f, 0.06f);
        public Color blockedColor = new Color(0.55f, 0.15f, 0.45f, 0.35f);

        [Tooltip("阵型提示的透明度（颜色取该方位色）")]
        [Range(0f, 1f)]
        public float hintAlpha = 0.22f;

        public GridPos Pos => new GridPos(row, col);
        public RectTransform Rect => (RectTransform)transform;

        private System.Action<GridPos> _onClick;

        private void Awake()
        {
            if (background == null) background = GetComponent<Image>();
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => _onClick?.Invoke(Pos));
        }

        public void SetClickHandler(System.Action<GridPos> onClick) => _onClick = onClick;

        public void ShowNormal()
        {
            if (background != null) background.color = normalColor;
            if (label != null) label.text = "";
        }

        public void ShowBlocked(int turnsLeft)
        {
            if (background != null) background.color = blockedColor;
            if (label != null) label.text = $"虚\n{turnsLeft}";
        }

        public void ShowHint(Color directionColor)
        {
            if (background != null)
                background.color = new Color(directionColor.r, directionColor.g, directionColor.b, hintAlpha);
            if (label != null) label.text = "";
        }

        /// <summary>Inspector 右键菜单：按物体名 Cell_行_列 反推坐标，省得一个个手填。</summary>
        [ContextMenu("从名字解析坐标")]
        public void ParseCoordsFromName()
        {
            var parts = name.Split('_');
            if (parts.Length >= 3 && int.TryParse(parts[1], out int r) && int.TryParse(parts[2], out int c))
            {
                row = r;
                col = c;
            }
            else
            {
                Debug.LogWarning($"[{name}] 名字不是 Cell_行_列 的格式，坐标要手填。", this);
            }
        }
    }
}
