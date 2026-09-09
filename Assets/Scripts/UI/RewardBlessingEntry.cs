using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 取舍面板里的一个「众星祝福」条目：名称 + 描述 + 「留下/删去」按钮。
    /// **这是 prefab** —— 收获数量不定，没法预摆在场景里。
    /// </summary>
    public class RewardBlessingEntry : MonoBehaviour
    {
        [Header("子控件")]
        public TMP_Text nameText;
        public TMP_Text descText;
        public Button toggleButton;
        public TMP_Text toggleLabel;

        [Tooltip("按钮底图。留空则取 toggleButton 的 targetGraphic")]
        public Image toggleBackground;

        [Header("配色")]
        public Color keepColor = new Color(0.24f, 0.45f, 0.32f, 1f);
        public Color dropColor = new Color(0.42f, 0.22f, 0.24f, 1f);

        private Action _onToggle;

        private void Awake()
        {
            if (toggleBackground == null && toggleButton != null)
                toggleBackground = toggleButton.targetGraphic as Image;
            if (toggleButton != null) toggleButton.onClick.AddListener(() => _onToggle?.Invoke());
        }

        public void Bind(BlessingDef def, Action onToggle)
        {
            _onToggle = onToggle;
            if (nameText != null) nameText.text = def.Name;
            if (descText != null) descText.text = def.Desc;
        }

        public void SetKeep(bool keep)
        {
            if (toggleBackground != null) toggleBackground.color = keep ? keepColor : dropColor;
            if (toggleLabel != null) toggleLabel.text = keep ? "留下" : "删去";
        }
    }
}
