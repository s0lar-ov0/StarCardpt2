using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using StarCard.UI;

namespace StarCard.EditorTools
{
    /// <summary>
    /// 给手牌栏搭滚动层级（Viewport + Content + 竖向滚动条），并把引用填进 HandView。
    ///
    /// 一次性工具。搭出来的结构：
    ///   Hand                        ← ScrollRect + Image(底) + Mask 无
    ///   ├── HandLabel               ← 原有标题，保持在顶部
    ///   ├── Viewport                ← RectMask2D 裁剪
    ///   │   └── Content             ← HandView 往这里塞牌
    ///   └── Scrollbar Vertical
    ///       └── Sliding Area/Handle
    ///
    /// 已经搭过的会复用同名物体，不会重复建。
    /// </summary>
    public static class DriftHandScrollSetup
    {
        [MenuItem("漂泊的星宿/搭建手牌栏滚动条")]
        public static void Setup()
        {
            var hand = Object.FindObjectOfType<HandView>();
            if (hand == null)
            {
                Debug.LogError("场景里找不到 HandView。");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(hand.gameObject, "Setup hand scroll");

            var handRt = (RectTransform)hand.transform;
            float labelH = 60f;      // 顶部标题让出的高度
            float barW = 16f;        // 滚动条宽度

            // ---------- Hand 自己：底板 + ScrollRect ----------
            var bg = hand.GetComponent<Image>();
            if (bg == null)
            {
                bg = Undo.AddComponent<Image>(hand.gameObject);
                bg.sprite = Resources.Load<Sprite>("Arts/UI/Rounded");
                bg.type = Image.Type.Sliced;
                bg.color = new Color(1f, 1f, 1f, 0.05f);
            }

            var scroll = hand.GetComponent<ScrollRect>();
            if (scroll == null) scroll = Undo.AddComponent<ScrollRect>(hand.gameObject);

            // ---------- Viewport ----------
            var viewport = FindOrCreate(handRt, "Viewport");
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(1f, 1f);
            viewport.offsetMin = new Vector2(6f, 6f);
            viewport.offsetMax = new Vector2(-(barW + 6f), -labelH);
            if (viewport.GetComponent<RectMask2D>() == null)
                Undo.AddComponent<RectMask2D>(viewport.gameObject);

            // ---------- Content ----------
            var content = FindOrCreate(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, content.offsetMin.y);
            content.offsetMax = new Vector2(0f, 0f);
            content.sizeDelta = new Vector2(0f, Mathf.Max(600f, handRt.rect.height));
            content.anchoredPosition = Vector2.zero;

            // ---------- 竖向滚动条 ----------
            var bar = FindOrCreate(handRt, "Scrollbar Vertical");
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(barW, 0f);
            bar.offsetMin = new Vector2(-barW, 6f);
            bar.offsetMax = new Vector2(0f, -labelH);

            var barImg = bar.GetComponent<Image>();
            if (barImg == null)
            {
                barImg = Undo.AddComponent<Image>(bar.gameObject);
                barImg.sprite = Resources.Load<Sprite>("Arts/UI/Rounded");
                barImg.type = Image.Type.Sliced;
                barImg.color = new Color(1f, 1f, 1f, 0.08f);
            }

            var slidingArea = FindOrCreate(bar, "Sliding Area");
            slidingArea.anchorMin = Vector2.zero;
            slidingArea.anchorMax = Vector2.one;
            slidingArea.offsetMin = Vector2.zero;
            slidingArea.offsetMax = Vector2.zero;

            var handle = FindOrCreate(slidingArea, "Handle");
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            var handleImg = handle.GetComponent<Image>();
            if (handleImg == null)
            {
                handleImg = Undo.AddComponent<Image>(handle.gameObject);
                handleImg.sprite = Resources.Load<Sprite>("Arts/UI/Rounded");
                handleImg.type = Image.Type.Sliced;
                handleImg.color = new Color(0.75f, 0.82f, 0.95f, 0.65f);
            }

            var scrollbar = bar.GetComponent<Scrollbar>();
            if (scrollbar == null) scrollbar = Undo.AddComponent<Scrollbar>(bar.gameObject);
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImg;

            // ---------- 接线 ----------
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = 4f;

            hand.scrollRect = scroll;
            hand.content = content;
            hand.cardLayer = content;

            // 原有的 HandLabel 移到顶部、置于最前，避免被 Viewport 盖住
            var label = handRt.Find("HandLabel") as RectTransform;
            if (label != null)
            {
                label.anchorMin = new Vector2(0f, 1f);
                label.anchorMax = new Vector2(1f, 1f);
                label.pivot = new Vector2(0.5f, 1f);
                label.offsetMin = new Vector2(6f, -labelH + 4f);
                label.offsetMax = new Vector2(-6f, -6f);
                label.SetAsLastSibling();
            }

            EditorUtility.SetDirty(hand);
            EditorUtility.SetDirty(scroll);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hand.gameObject.scene);
            Debug.Log("手牌栏滚动条搭好了。HandView 的 scrollRect / content / cardLayer 已自动填上。\n" +
                      "牌数超出可视高度时滚动条自动出现；鼠标滚轮也能滚。", hand);
        }

        private static RectTransform FindOrCreate(RectTransform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
    }
}
