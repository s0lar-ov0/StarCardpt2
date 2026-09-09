using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 待放置的星宿牌。挂在场景里的 Hand 上。
    /// 牌是运行时从 cardPrefab 生成的（数量随局势变），**竖向排列在棋盘右侧**。
    /// 点一张 = 选中，然后点棋盘空位落子。
    ///
    /// 棋盘只有 4 行，手牌可能比可视高度多，所以支持滚动：
    /// 把 `scrollRect` 和 `content` 拖上，脚本会按牌数自动撑高 content，
    /// 超出可视区时滚动条自动出现。
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [Header("牌（运行时生成）")]
        [Tooltip("星宿牌 prefab（挂 DriftCardView）。可以和 BoardView 用同一个")]
        public DriftCardView cardPrefab;

        [Tooltip("生成的牌挂在哪个物体下。用滚动时拖 Viewport/Content；留空则挂在自己身上")]
        public RectTransform cardLayer;

        [Header("滚动（可选，留空则不滚动）")]
        [Tooltip("手牌栏的 ScrollRect。留空 = 不滚动，牌多了会溢出")]
        public ScrollRect scrollRect;

        [Tooltip("被撑高的 Content。一般和 cardLayer 是同一个物体")]
        public RectTransform content;

        [Header("竖向排列")]
        [Tooltip("相邻两张牌的中心间距（竖直方向）")]
        public float spacing = 210f;

        [Tooltip("第一张牌距 Content 顶部的距离")]
        public float topPadding = 110f;

        [Tooltip("最后一张牌下方留的空隙")]
        public float bottomPadding = 20f;

        [Tooltip("牌在栏内的水平偏移")]
        public float horizontalOffset = 0f;

        [Tooltip("新牌加入时自动滚到底部")]
        public bool autoScrollToNewest = true;

        private DriftGameManager _game;
        private readonly List<DriftCardView> _views = new();
        private int _lastCount = -1;

        public int SelectedIndex { get; private set; } = -1;
        public event Action<int> SelectionChanged;

        public void Init(DriftGameManager game)
        {
            _game = game;

            if (cardLayer == null) cardLayer = content != null ? content : (RectTransform)transform;
            if (content == null && scrollRect != null) content = scrollRect.content;
            if (cardPrefab == null)
                Debug.LogError("[HandView] cardPrefab 没拖！手牌不会显示。", this);

            _game.StateChanged += Refresh;
        }

        private void OnDestroy()
        {
            if (_game != null) _game.StateChanged -= Refresh;
        }

        public void ClearSelection()
        {
            if (SelectedIndex == -1) return;
            SelectedIndex = -1;
            SelectionChanged?.Invoke(-1);
            RefreshSelectionVisual();
        }

        private void Select(int index)
        {
            SelectedIndex = SelectedIndex == index ? -1 : index;
            SelectionChanged?.Invoke(SelectedIndex);
            RefreshSelectionVisual();
        }

        public void Refresh()
        {
            if (_game == null || cardPrefab == null) return;
            var hand = _game.Hand;

            while (_views.Count < hand.Count)
            {
                var view = Instantiate(cardPrefab, cardLayer);
                view.name = $"HandCard_{_views.Count}";
                _views.Add(view);
            }
            for (int i = hand.Count; i < _views.Count; i++)
                _views[i].gameObject.SetActive(false);

            if (SelectedIndex >= hand.Count) SelectedIndex = -1;

            ResizeContent(hand.Count);

            // 竖排：从上往下，锚点在 Content 顶部
            for (int i = 0; i < hand.Count; i++)
            {
                var view = _views[i];
                view.gameObject.SetActive(true);
                view.HandIndex = i;
                view.Pos = GridPos.Invalid;
                view.Bind(hand[i], OnCardClicked);

                var rt = view.Rect;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                view.MoveTo(new Vector2(horizontalOffset, -(topPadding + i * spacing)), true);

                view.SetLinked(false);
                view.SetInteractable(_game.Phase == DriftPhase.Board);
            }

            if (autoScrollToNewest && scrollRect != null && hand.Count > _lastCount && _lastCount >= 0)
                ScrollToBottom();
            _lastCount = hand.Count;

            RefreshSelectionVisual();
        }

        /// <summary>按牌数撑高 Content，让 ScrollRect 知道什么时候该出滚动条。</summary>
        private void ResizeContent(int count)
        {
            if (content == null) return;
            float needed = topPadding + Mathf.Max(0, count - 1) * spacing + bottomPadding;

            // 至少和可视区一样高，否则内容比视口小时 ScrollRect 会把内容顶飞
            float viewport = scrollRect != null && scrollRect.viewport != null
                ? scrollRect.viewport.rect.height
                : ((RectTransform)transform).rect.height;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(needed, viewport));
        }

        public void ScrollToBottom()
        {
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
        }

        public void ScrollToTop()
        {
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        private void RefreshSelectionVisual()
        {
            for (int i = 0; i < _views.Count; i++)
                if (_views[i].gameObject.activeSelf) _views[i].SetSelected(i == SelectedIndex);
        }

        private void OnCardClicked(DriftCardView view)
        {
            if (_game.Phase != DriftPhase.Board) return;
            Select(view.HandIndex);
        }
    }
}
