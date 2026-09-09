using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StarCard.UI
{
    /// <summary>
    /// 场景搭建模式下剩下的一点点运行时工具。
    ///
    /// UI 层级现在全部在场景里手工搭好、靠 Inspector 拖引用，这里**不再生成任何界面**。
    /// 只保留两件事：
    ///   1. 加载共用贴图（圆形 / 圆角），给运行时才产生的东西（流星）用；
    ///   2. 几个改 RectTransform 的小函数，卡牌和流星的定位还要用。
    /// </summary>
    public static class UIFactory
    {
        /// <summary>
        /// 全局中文字体的 Resources 路径。**换字体只改这一行** ——
        /// Editor 工具（自动填充引用 / 提取 Prefab）填字体字段时都读这里。
        /// 场景和 prefab 里已经拖好的引用不受影响，那些是资产 guid。
        /// </summary>
        public const string FontResourcePath = "Fonts/霞鹜文楷max SDF";

        private static Sprite _circle;
        private static Sprite _rounded;
        private static TMP_FontAsset _font;

        /// <summary>全局中文字体。运行时生成的东西（飘字）用它兜底。</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<TMP_FontAsset>(FontResourcePath);
                return _font;
            }
        }

        /// <summary>圆形贴图，流星用。</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = Resources.Load<Sprite>("Arts/UI/Circle");
                return _circle;
            }
        }

        /// <summary>圆角九宫格贴图，流星拖尾用。</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded == null) _rounded = Resources.Load<Sprite>("Arts/UI/Rounded");
                return _rounded;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static Image CreateImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static TMP_Text CreateText(string name, Transform parent, string content, float size,
                                          Color color, TMP_FontAsset font,
                                          TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = CreateRect(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>铺满父物体（可留内边距）。</summary>
        public static void Stretch(RectTransform rt, float padding = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        public static void AnchorCenter(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
