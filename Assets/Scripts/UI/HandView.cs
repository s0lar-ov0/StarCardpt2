using System;
using System.Collections.Generic;
using UnityEngine;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 待放置的星宿牌。挂在场景里的 Hand 上。
    /// 牌是运行时从 cardPrefab 生成的（数量随局势变），**竖向排列在棋盘右侧**。
    /// 点一张 = 选中，然后点棋盘空位落子。
    ///
    /// 手牌可能比栏高放得下的张数多，这里**不用滚动条**：
    /// 张数少时按 `spacing` 正常排开，多到放不下就自动缩小间距让牌**部分重叠**
    /// （下面的牌盖住上面那张的底部，宿名和角标仍露在外面）。
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [Header("牌（运行时生成）")]
        [Tooltip("星宿牌 prefab（挂 DriftCardView）。可以和 BoardView 用同一个")]
        public DriftCardView cardPrefab;

        [Tooltip("生成的牌挂在哪个物体下。留空则挂在自己身上")]
        public RectTransform cardLayer;

        [Header("竖向排列")]
        [Tooltip("张数少、放得下时的间距。一般设成卡牌高度 + 一点空隙")]
        public float spacing = 212f;

        [Tooltip("第一张牌中心距栏顶部的距离")]
        public float topPadding = 112f;

        [Tooltip("最后一张牌下方要留的空隙（给按钮/边框让位）")]
        public float bottomPadding = 24f;

        [Tooltip("牌在栏内的水平偏移")]
        public float horizontalOffset = 0f;

        [Header("重叠")]
        [Tooltip("放不下时允许压缩到的最小间距。太小会把宿名也盖住 —— 建议不低于卡牌高度的 1/3")]
        public float minSpacing = 74f;

        private DriftGameManager _game;
        private readonly List<DriftCardView> _views = new();

        public int SelectedIndex { get; private set; } = -1;
        public event Action<int> SelectionChanged;

        public void Init(DriftGameManager game)
        {
            _game = game;
            if (cardLayer == null) cardLayer = (RectTransform)transform;
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

        /// <summary>按当前张数算实际间距：放得下就用 spacing，放不下就压缩（下限 minSpacing）。</summary>
        private float ComputeSpacing(int count)
        {
            if (count <= 1) return spacing;

            // 可用高度要扣掉最后一张牌的下半部分 —— 否则算出来的间距会让末张溢出栏底
            float cardH = cardPrefab != null ? cardPrefab.Rect.sizeDelta.y : 0f;
            float usable = ((RectTransform)transform).rect.height
                           - topPadding - bottomPadding - cardH * 0.5f;
            if (usable <= 0f) return minSpacing;

            float needed = (count - 1) * spacing;
            if (needed <= usable) return spacing;

            return Mathf.Max(minSpacing, usable / (count - 1));
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

            float step = ComputeSpacing(hand.Count);

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
                view.MoveTo(new Vector2(horizontalOffset, -(topPadding + i * step)), true);

                // 重叠时后面的牌要盖在前面那张上面，否则下半张会被压住看不出层次
                rt.SetSiblingIndex(i);

                view.SetLinked(false);
                view.SetInteractable(_game.Phase == DriftPhase.Board);
            }

            RefreshSelectionVisual();
        }

        private void RefreshSelectionVisual()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                if (!_views[i].gameObject.activeSelf) continue;
                bool sel = i == SelectedIndex;
                _views[i].SetSelected(sel);
                // 选中的提到最前，重叠时才能看清整张
                if (sel) _views[i].Rect.SetAsLastSibling();
            }
        }

        private void OnCardClicked(DriftCardView view)
        {
            if (_game.Phase != DriftPhase.Board) return;
            Select(view.HandIndex);
        }
    }
}
