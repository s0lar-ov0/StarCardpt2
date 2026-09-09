using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StarCard.UI
{
    /// <summary>
    /// 取舍面板里的一个「星宿牌」条目：一张牌 + 下面一个「留下/删去」按钮。
    /// **这是 prefab** —— 收获数量不定，没法预摆在场景里。
    /// </summary>
    public class RewardCardEntry : MonoBehaviour
    {
        [Header("子控件")]
        [Tooltip("显示牌面的 DriftCardView（prefab 里内置一张，不用运行时再生成）")]
        public DriftCardView cardView;

        [Tooltip("留下/删去 切换按钮")]
        public Button toggleButton;

        [Tooltip("按钮上的文字")]
        public TMP_Text toggleLabel;

        [Tooltip("按钮底图。留空则取 toggleButton 的 targetGraphic")]
        public Image toggleBackground;

        [Header("配色")]
        public Color keepColor = new Color(0.24f, 0.45f, 0.32f, 1f);
        public Color dropColor = new Color(0.42f, 0.22f, 0.24f, 1f);

        [Header("牌的显示")]
        [Tooltip("牌在条目里的缩放")]
        public float cardScale = 0.92f;

        private Action _onToggle;

        private void Awake()
        {
            if (toggleBackground == null && toggleButton != null)
                toggleBackground = toggleButton.targetGraphic as Image;
            if (toggleButton != null) toggleButton.onClick.AddListener(() => _onToggle?.Invoke());
        }

        public void Bind(Core.StarCard card, Action onToggle)
        {
            _onToggle = onToggle;
            if (cardView != null)
            {
                cardView.Bind(card, null);
                cardView.SetInteractable(false);
                cardView.SetScale(cardScale);
            }
        }

        public void SetKeep(bool keep)
        {
            if (toggleBackground != null) toggleBackground.color = keep ? keepColor : dropColor;
            if (toggleLabel != null) toggleLabel.text = keep ? "留下" : "删去";
        }
    }
}
