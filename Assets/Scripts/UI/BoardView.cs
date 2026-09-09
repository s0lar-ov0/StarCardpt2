using System.Collections.Generic;
using UnityEngine;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 棋盘显示 + 全部棋盘交互。挂在场景里的 Board 上。
    ///
    /// 格子是**场景物体**（Cells 下的 28 个 Cell_r_c，每个挂 BoardCellView），拖进 cells 数组即可；
    /// 牌是**运行时实例化**的（数量随局势变，没法预摆），从 cardPrefab 生成到 cardLayer 下。
    ///
    /// 交互约定：
    ///   选中手牌 → 点空位 = 放置
    ///   点棋盘上的牌 = 选中；再点空位 = 移动；再点另一张牌 = 交换；点自己 = 取消
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        [Header("格子（场景物体）")]
        [Tooltip("28 个 Cell_r_c。可以直接框选 Cells 下所有子物体一起拖进来，顺序无所谓 —— 每个格子自己知道坐标")]
        public BoardCellView[] cells;

        [Header("牌（运行时生成）")]
        [Tooltip("星宿牌 prefab（挂 DriftCardView）")]
        public DriftCardView cardPrefab;

        [Tooltip("生成的牌挂在哪个物体下。一般拖 Board/Cards")]
        public RectTransform cardLayer;

        private DriftGameManager _game;
        private HandView _hand;

        private readonly Dictionary<GridPos, BoardCellView> _cellMap = new();
        private readonly Dictionary<GridPos, DriftCardView> _cardViews = new();
        private GridPos _selected = GridPos.Invalid;

        public void Init(DriftGameManager game, HandView hand)
        {
            _game = game;
            _hand = hand;

            _cellMap.Clear();
            if (cells != null)
            {
                for (int i = 0; i < cells.Length; i++)
                {
                    var cell = cells[i];
                    if (cell == null) continue;
                    _cellMap[cell.Pos] = cell;
                    cell.SetClickHandler(OnCellClicked);
                }
            }

            ValidateSetup();

            _game.StateChanged += Refresh;
            _game.BoardShuffled += Refresh;
            _hand.SelectionChanged += OnHandSelectionChanged;
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.StateChanged -= Refresh;
                _game.BoardShuffled -= Refresh;
            }
            if (_hand != null) _hand.SelectionChanged -= OnHandSelectionChanged;
        }

        /// <summary>把拖漏 / 拖重的情况直接报到 Console，省得默默不动。</summary>
        private void ValidateSetup()
        {
            if (cardPrefab == null)
                Debug.LogError("[BoardView] cardPrefab 没拖！棋盘上不会出现任何牌。", this);
            if (cardLayer == null)
                Debug.LogError("[BoardView] cardLayer 没拖！（一般拖 Board/Cards）", this);

            int expected = _game.Config.rows * _game.Config.cols;
            if (_cellMap.Count != expected)
            {
                Debug.LogError($"[BoardView] 格子数对不上：拖进来 {(cells == null ? 0 : cells.Length)} 个、" +
                               $"去重后 {_cellMap.Count} 个，棋盘需要 {expected} 个（{_game.Config.rows}×{_game.Config.cols}）。" +
                               "常见原因：漏拖、或多个格子的 row/col 填成了同一个坐标。", this);
            }

            for (int r = 0; r < _game.Config.rows; r++)
            {
                for (int c = 0; c < _game.Config.cols; c++)
                {
                    if (!_cellMap.ContainsKey(new GridPos(r, c)))
                        Debug.LogError($"[BoardView] 缺少坐标 ({r},{c}) 的格子。", this);
                }
            }
        }

        /// <summary>格子的锚点位置，牌就摆在这。</summary>
        private Vector2 CellPos(GridPos pos)
        {
            if (_cellMap.TryGetValue(pos, out var cell) && cell != null)
                return cell.Rect.anchoredPosition;
            return Vector2.zero;
        }

        // ---------- 交互 ----------
        private void OnHandSelectionChanged(int index)
        {
            if (index >= 0) ClearSelection();
            Refresh();
        }

        private void OnCellClicked(GridPos pos)
        {
            if (_game.Phase != DriftPhase.Board) return;

            // 点到有牌的格子交给卡牌自己处理（卡牌盖在格子上，一般不会走到这里）
            if (_game.Board.HasCard(pos))
            {
                OnCardClicked(_cardViews.TryGetValue(pos, out var v) ? v : null);
                return;
            }

            if (_game.Board.IsBlocked(pos)) return;

            int handIndex = _hand.SelectedIndex;
            if (handIndex >= 0)
            {
                if (_game.TryPlaceFromHand(handIndex, pos)) _hand.ClearSelection();
                return;
            }

            if (_selected.IsValid)
            {
                var from = _selected;
                ClearSelection();
                _game.TryMove(from, pos);
            }
        }

        private void OnCardClicked(DriftCardView view)
        {
            if (view == null || _game.Phase != DriftPhase.Board) return;
            var pos = view.Pos;

            if (_hand.SelectedIndex >= 0)
            {
                // 手上拿着牌时点棋盘上的牌 = 放下手牌、改选这张
                _hand.ClearSelection();
                SetSelection(pos);
                return;
            }

            if (!_selected.IsValid)
            {
                SetSelection(pos);
                return;
            }

            if (_selected == pos)
            {
                ClearSelection();
                Refresh();
                return;
            }

            var a = _selected;
            ClearSelection();
            _game.TrySwap(a, pos);
        }

        private void SetSelection(GridPos pos)
        {
            _selected = pos;
            Refresh();
        }

        public void ClearSelection() => _selected = GridPos.Invalid;

        // ---------- 刷新 ----------
        public void Refresh()
        {
            if (_game == null || _game.Board == null) return;
            var board = _game.Board;

            // 1. 收走已经不在棋盘上的卡视图
            var stale = new List<GridPos>();
            foreach (var kv in _cardViews)
                if (!board.HasCard(kv.Key) || !board.GetCard(kv.Key).Equals(kv.Value.Card)) stale.Add(kv.Key);

            var recycled = new List<DriftCardView>();
            foreach (var pos in stale)
            {
                recycled.Add(_cardViews[pos]);
                _cardViews.Remove(pos);
            }

            // 2. 为棋盘上每张牌准备视图（尽量复用同一张牌的视图，让漂移有位移动画）
            foreach (var kv in board.AllCards())
            {
                if (_cardViews.ContainsKey(kv.Key)) continue;

                DriftCardView view = null;
                for (int i = 0; i < recycled.Count; i++)
                {
                    if (recycled[i].Card.Equals(kv.Value))
                    {
                        view = recycled[i];
                        recycled.RemoveAt(i);
                        break;
                    }
                }
                if (view == null && recycled.Count > 0)
                {
                    view = recycled[0];
                    recycled.RemoveAt(0);
                }
                if (view == null)
                {
                    if (cardPrefab == null || cardLayer == null) continue;
                    view = Instantiate(cardPrefab, cardLayer);
                    view.gameObject.SetActive(true);
                    view.MoveTo(CellPos(kv.Key), false);
                }

                view.Bind(kv.Value, OnCardClicked);
                view.Pos = kv.Key;
                _cardViews[kv.Key] = view;
            }

            foreach (var leftover in recycled) Destroy(leftover.gameObject);

            // 3. 位置 / 连结描边 / 选中态
            foreach (var kv in _cardViews)
            {
                var view = kv.Value;
                view.Pos = kv.Key;
                view.MoveTo(CellPos(kv.Key), true);
                view.SetLinked(_game.Links.IsLinked(kv.Key));
                view.SetSelected(_selected == kv.Key);
                view.SetInteractable(_game.Phase == DriftPhase.Board);
            }

            // 4. 格子底色：封锁 / 阵型提示
            GridPos[] hint = null;
            Color hintColor = Color.white;
            if (_selected.IsValid && board.HasCard(_selected))
            {
                var dir = board.GetCard(_selected).Direction;
                if (_game.Links.BestPlacement.TryGetValue(dir, out var placement))
                {
                    hint = placement;
                    hintColor = MeteorPalette.ColorOfDirection(dir);
                }
            }

            foreach (var kv in _cellMap)
            {
                var pos = kv.Key;
                var cell = kv.Value;
                if (cell == null) continue;

                if (board.IsBlocked(pos))
                {
                    cell.ShowBlocked(board.BlockedTurns(pos));
                    continue;
                }

                bool inHint = false;
                if (hint != null)
                    for (int i = 0; i < hint.Length; i++)
                        if (hint[i] == pos) { inHint = true; break; }

                if (inHint) cell.ShowHint(hintColor);
                else cell.ShowNormal();
            }
        }
    }
}
