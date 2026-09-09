using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StarCard.UI
{
    /// <summary>
    /// 右侧栏里的一张「祝福卡片」：左边圆形图标 + 右边「名称-等级」和描述。
    /// 方位祝福和众星祝福共用这一个 prefab。
    ///
    /// **这是 prefab** —— 卡片数量随获得的祝福变化，没法预摆在场景里。
    /// </summary>
    public class BlessingCardView : MonoBehaviour
    {
        [Header("子控件")]
        [Tooltip("卡片底板")]
        public Image background;

        [Tooltip("左侧圆形图标（占位图，以后换成美术图）")]
        public Image icon;

        [Tooltip("「名称-等级」那一行")]
        public TMP_Text titleText;

        [Tooltip("效果描述")]
        public TMP_Text descText;

        [Header("失效态")]
        [Tooltip("整张卡的 CanvasGroup，用来把失效的卡片压暗。留空则自动补")]
        public CanvasGroup canvasGroup;

        [Tooltip("失效时的透明度")]
        [Range(0.1f, 1f)]
        public float dimmedAlpha = 0.45f;

        /// <summary>跟 DriftCardView 保持一致的便捷属性，省得每次现转类型。</summary>
        public RectTransform Rect => (RectTransform)transform;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <param name="title">「青龙-镇河」这种；没有等级概念就只写名称</param>
        /// <param name="desc">效果描述</param>
        /// <param name="tint">底板与图标的染色，一般用方位色</param>
        /// <param name="active">false = 压暗（暂时用不上/已失效）</param>
        public void Bind(string title, string desc, Color tint, bool active = true)
        {
            if (titleText != null) titleText.text = title;
            if (descText != null) descText.text = desc;

            if (background != null)
                background.color = new Color(tint.r, tint.g, tint.b, 0.16f);
            if (icon != null)
                icon.color = new Color(tint.r, tint.g, tint.b, 0.85f);

            if (canvasGroup != null) canvasGroup.alpha = active ? 1f : dimmedAlpha;
        }
    }
}
