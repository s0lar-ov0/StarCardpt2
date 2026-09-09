using System.Collections.Generic;
using TMPro;
using UnityEngine;
using StarCard.Core;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 右侧栏的祝福面板 —— 取代原来的「星语」日志栏。
    ///
    /// 上半列出**四个方位祝福**（未归位的压暗显示，让玩家知道有什么可拿），
    /// 下半列出已获得的**众星祝福**。卡片是运行时从 prefab 生成的。
    /// </summary>
    public class BlessingPanelView : MonoBehaviour
    {
        [Header("卡片 prefab")]
        public BlessingCardView cardPrefab;

        [Header("容器（场景物体）")]
        [Tooltip("方位祝福卡片挂这下面")]
        public RectTransform directionRow;

        [Tooltip("众星祝福卡片挂这下面")]
        public RectTransform starRow;

        [Header("标题（可选）")]
        public TMP_Text directionTitle;
        public TMP_Text starTitle;

        [Tooltip("还没有众星祝福时显示的提示")]
        public TMP_Text starEmptyHint;

        [Header("排列")]
        [Tooltip("相邻两张卡片的竖直间距")]
        public float cardSpacing = 78f;

        [Tooltip("第一张卡距容器顶部的距离")]
        public float topPadding = 40f;

        [Header("未归位方位的显示")]
        [Tooltip("勾上 = 未归位的方位也列出来（压暗），让玩家看到目标")]
        public bool showLockedDirections = true;

        [Tooltip("未归位时标题显示成什么，{0} 会替换成神兽名")]
        public string lockedTitleFormat = "{0}（未归位）";

        private DriftGameManager _game;
        private readonly List<BlessingCardView> _dirCards = new();
        private readonly List<BlessingCardView> _starCards = new();

        private static readonly Direction[] DirOrder =
        {
            Direction.East, Direction.North, Direction.West, Direction.South
        };

        public void Init(DriftGameManager game)
        {
            _game = game;

            if (cardPrefab == null)
                Debug.LogError("[BlessingPanelView] cardPrefab 没拖！祝福面板会是空的。", this);
            if (directionRow == null) Debug.LogError("[BlessingPanelView] directionRow 没拖！", this);
            if (starRow == null) Debug.LogWarning("[BlessingPanelView] starRow 没拖，众星祝福不会显示。", this);

            if (directionTitle != null) directionTitle.text = "方位祝福";
            if (starTitle != null) starTitle.text = "众星祝福";

            _game.StateChanged += Refresh;
            _game.Homecoming += OnHomecoming;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_game == null) return;
            _game.StateChanged -= Refresh;
            _game.Homecoming -= OnHomecoming;
        }

        private void OnHomecoming(Direction dir) => Refresh();

        public void Refresh()
        {
            if (_game == null || cardPrefab == null) return;
            RefreshDirections();
            RefreshStars();
        }

        private void RefreshDirections()
        {
            if (directionRow == null) return;

            // 要显示哪些方位：全列（含未归位）或只列已归位的
            var list = new List<Direction>();
            for (int i = 0; i < DirOrder.Length; i++)
            {
                var d = DirOrder[i];
                if (showLockedDirections || _game.IsHomecomed(d)) list.Add(d);
            }

            EnsureCount(_dirCards, list.Count, directionRow);

            for (int i = 0; i < list.Count; i++)
            {
                var dir = list[i];
                bool got = _game.IsHomecomed(dir);
                var card = _dirCards[i];
                card.gameObject.SetActive(true);
                Place(card, i);

                string title = got
                    ? DirectionBlessing.GetName(dir)
                    : string.Format(lockedTitleFormat, DirectionBlessing.GetBeastName(dir));
                string desc = got
                    ? DirectionBlessing.GetDesc(dir)
                    : $"集齐{DirectionBlessing.GetBeastName(dir)}七宿后：{DirectionBlessing.GetDesc(dir)}";

                card.Bind(title, desc, MeteorPalette.ColorOfDirection(dir), got);
            }
            for (int i = list.Count; i < _dirCards.Count; i++)
                _dirCards[i].gameObject.SetActive(false);
        }

        private void RefreshStars()
        {
            if (starRow == null) return;

            var blessings = _game.Blessings;
            EnsureCount(_starCards, blessings.Count, starRow);

            for (int i = 0; i < blessings.Count; i++)
            {
                var def = BlessingDatabase.Get(blessings[i]);
                var card = _starCards[i];
                card.gameObject.SetActive(true);
                Place(card, i);
                card.Bind(def.Name, def.Desc, new Color(0.85f, 0.72f, 0.95f), true);
            }
            for (int i = blessings.Count; i < _starCards.Count; i++)
                _starCards[i].gameObject.SetActive(false);

            if (starEmptyHint != null)
            {
                starEmptyHint.gameObject.SetActive(blessings.Count == 0);
                if (blessings.Count == 0) starEmptyHint.text = "（点中小型粉色流星可获得）";
            }
        }

        private void EnsureCount(List<BlessingCardView> pool, int need, RectTransform parent)
        {
            while (pool.Count < need)
            {
                var card = Instantiate(cardPrefab, parent);
                card.name = $"{parent.name}_Card_{pool.Count}";
                pool.Add(card);
            }
        }

        /// <summary>卡片竖排：锚定容器顶部，从上往下。</summary>
        private void Place(BlessingCardView card, int index)
        {
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(topPadding + index * cardSpacing));
        }
    }
}
