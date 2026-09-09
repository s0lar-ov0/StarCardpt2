using UnityEngine;

namespace StarCard.UI
{
    /// <summary>
    /// 「画外坐标」面板。与老项目 star-card 的模态面板同一套做法。
    ///
    /// **物体始终 active**，显示/隐藏靠两件事：
    ///   1. `anchoredPosition` 在画外位 / 画内位之间瞬移
    ///   2. `CanvasGroup` 的 alpha / blocksRaycasts / interactable
    ///
    /// 这么做的好处是 Scene 视图里可以把几个面板**并排摊开同时编辑**，不用反复勾 SetActive；
    /// 而且面板上的脚本 Update 一直在跑，不会因为 SetActive(false) 断掉。
    ///
    /// 用法：挂在面板根物体上，把它在 Scene 里拖到画面旁边，然后点组件右键菜单
    /// 「记录当前位置为画外位」。运行时脚本调 Show() / Hide() 即可。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class OffscreenPanel : MonoBehaviour
    {
        [Header("画外 / 画内坐标")]
        [Tooltip("隐藏时停靠的 anchoredPosition。各面板互相错开，免得在 Scene 里堆成一坨")]
        public Vector2 hiddenAnchoredPos = new Vector2(3000f, 0f);

        [Tooltip("显示时瞬移到的 anchoredPosition，一般是 (0,0)")]
        public Vector2 shownAnchoredPos = Vector2.zero;

        [Header("行为")]
        [Tooltip("Awake 时立刻藏起来。四个模态面板都该勾上")]
        public bool hideOnAwake = true;

        [Tooltip("隐藏时把 alpha 归零。取消勾选的话面板只是移出画外（调试时想在 Game 视图外看见它就取消）")]
        public bool fadeWhenHidden = true;

        private RectTransform _rect;
        private CanvasGroup _group;

        /// <summary>当前是否处于显示状态。</summary>
        public bool IsShown { get; private set; }

        public RectTransform Rect => _rect != null ? _rect : (_rect = (RectTransform)transform);

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            if (hideOnAwake) Hide();
        }

        public void Show()
        {
            IsShown = true;
            Rect.anchoredPosition = shownAnchoredPos;
            if (_group == null) return;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
        }

        public void Hide()
        {
            IsShown = false;
            Rect.anchoredPosition = hiddenAnchoredPos;
            if (_group == null) return;
            if (fadeWhenHidden) _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        public void SetShown(bool shown)
        {
            if (shown) Show();
            else Hide();
        }

        public void Toggle() => SetShown(!IsShown);

        /// <summary>
        /// 在 Scene 视图里把面板拖到想要的停靠位置，然后点这个，把当前坐标记为画外位。
        /// </summary>
        [ContextMenu("记录当前位置为画外位")]
        private void CaptureHiddenPos()
        {
            hiddenAnchoredPos = ((RectTransform)transform).anchoredPosition;
            Debug.Log($"[{name}] 画外位记为 {hiddenAnchoredPos}", this);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>把面板挪回画内位，方便在 Scene 里对着调版式。</summary>
        [ContextMenu("移到画内位（编辑用）")]
        private void MoveToShownPos()
        {
            ((RectTransform)transform).anchoredPosition = shownAnchoredPos;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>把面板挪回画外位。</summary>
        [ContextMenu("移到画外位（编辑用）")]
        private void MoveToHiddenPos()
        {
            ((RectTransform)transform).anchoredPosition = hiddenAnchoredPos;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
