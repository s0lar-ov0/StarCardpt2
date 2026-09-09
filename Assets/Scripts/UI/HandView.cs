using System;
using System.Collections.Generic;
using UnityEngine;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 待放置的星宿牌。挂在场景里的 Hand 上。
    /// 牌是运行时从 cardPrefab 生成的（数量随局势变），横向居中排开。
    /// 点一张 = 选中，然后点棋盘空位落子。
    /// </summary>
    public class HandView : MonoBehaviour
    {
        [Header("牌（运行时生成）")]
        [Tooltip("星宿牌 prefab（挂 DriftCardView）。可以和 BoardView 用同一个")]
        public DriftCardView cardPrefab;

        [Tooltip("生成的牌挂在哪个物体下。留空则挂在自己身上")]
        public RectTransform cardLayer;

        [Header("排列")]
        [Tooltip("相邻两张牌的中心间距")]
        public float spacing = 120f;

        [Tooltip("整排牌的竖直偏移")]
        public float verticalOffset = 0f;

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

            float startX = -(hand.Count - 1) * 0.5f * spacing;
            for (int i = 0; i < hand.Count; i++)
            {
                var view = _views[i];
                view.gameObject.SetActive(true);
                view.HandIndex = i;
                view.Pos = GridPos.Invalid;
                view.Bind(hand[i], OnCardClicked);
                view.MoveTo(new Vector2(startX + i * spacing, verticalOffset), true);
                view.SetLinked(false);
                view.SetInteractable(_game.Phase == DriftPhase.Board);
            }

            RefreshSelectionVisual();
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
