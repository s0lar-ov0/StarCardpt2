using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 流星定位结束后的取舍面板。挂在场景里的 RewardPicker 上。
    ///
    /// 面板、标题、说明、确认按钮都是**场景物体**；
    /// 每条收获（牌 / 祝福）是**运行时从 prefab 生成**的，塞进 cardRow / blessingRow。
    /// </summary>
    public class RewardPickerView : MonoBehaviour
    {
        [Header("显示 / 隐藏")]
        [Tooltip("画外坐标面板。留空则退化成 SetActive 开关")]
        public OffscreenPanel panel;

        [Header("条目容器（场景物体）")]
        [Tooltip("星宿牌条目挂这下面。一般拖 Panel/CardRow")]
        public RectTransform cardRow;

        [Tooltip("祝福条目挂这下面。一般拖 Panel/BlessingRow")]
        public RectTransform blessingRow;

        [Header("条目 prefab")]
        public RewardCardEntry cardEntryPrefab;
        public RewardBlessingEntry blessingEntryPrefab;

        [Header("排列")]
        [Tooltip("相邻两个牌条目的间距")]
        public float cardSpacing = 133f;

        [Tooltip("相邻两个祝福条目的间距")]
        public float blessingSpacing = 340f;

        [Header("其它控件")]
        [Tooltip("底部的上限说明文字")]
        public TMP_Text summaryText;

        [Tooltip("确认按钮")]
        public Button confirmButton;

        [Tooltip("没有任何收获时显示的文字（可留空）")]
        public TMP_Text emptyCardHint;
        public TMP_Text emptyBlessingHint;

        [Header("祝福升级（满 3 个后点粉色流星触发）")]
        [Tooltip("升级选项挂这下面。留空则复用 blessingRow")]
        public RectTransform upgradeRow;

        [Tooltip("升级区的说明文字（可留空）")]
        public TMP_Text upgradeHint;

        private DriftGameManager _game;
        private readonly List<bool> _keepCards = new();
        private int _blessingChoice = -1;   // 候选祝福里选中哪个，-1 = 都不要
        private readonly List<RewardCardEntry> _cardEntries = new();
        private readonly List<RewardBlessingEntry> _blessingEntries = new();
        private readonly List<RewardBlessingEntry> _upgradeEntries = new();
        private int _upgradeChoice = -1;

        public void Init(DriftGameManager game)
        {
            _game = game;

            if (cardEntryPrefab == null) Debug.LogError("[RewardPickerView] cardEntryPrefab 没拖！", this);
            if (blessingEntryPrefab == null) Debug.LogError("[RewardPickerView] blessingEntryPrefab 没拖！", this);
            if (cardRow == null) Debug.LogError("[RewardPickerView] cardRow 没拖！（一般拖 Panel/CardRow）", this);
            if (blessingRow == null) Debug.LogError("[RewardPickerView] blessingRow 没拖！（一般拖 Panel/BlessingRow）", this);

            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);

            if (panel == null) panel = GetComponent<OffscreenPanel>();
            SetVisible(false);
            _game.RewardsReady += Show;
        }

        private void OnDestroy()
        {
            if (_game != null) _game.RewardsReady -= Show;
        }

        private void Show()
        {
            Clear();
            SetVisible(true);

            var cards = _game.PendingCards;
            var blessings = _game.PendingBlessings;

            int handRoom = Mathf.Max(0, _game.Config.handLimit - _game.Hand.Count);
            int blessRoom = Mathf.Max(0, _game.Config.blessingLimit - _game.Blessings.Count);

            for (int i = 0; i < cards.Count; i++) _keepCards.Add(i < handRoom);
            // 候选祝福单选：有位置就默认选第一个，方便一路确认
            _blessingChoice = (blessRoom > 0 && blessings.Count > 0) ? 0 : -1;

            if (cardEntryPrefab != null && cardRow != null)
            {
                float cardStep = FitSpacing(cardSpacing, cards.Count, cardRow, 148f);
                float startX = -(cards.Count - 1) * 0.5f * cardStep;
                for (int i = 0; i < cards.Count; i++)
                {
                    var entry = Instantiate(cardEntryPrefab, cardRow);
                    entry.gameObject.SetActive(true);
                    var rt = (RectTransform)entry.transform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(startX + i * cardStep, 0f);

                    int index = i;
                    entry.Bind(cards[i], () =>
                    {
                        _keepCards[index] = !_keepCards[index];
                        RefreshToggles();
                    });
                    _cardEntries.Add(entry);
                }
            }

            if (blessingEntryPrefab != null && blessingRow != null)
            {
                // 候选可能攒到 7 个（祝福总数），固定间距会横向溢出 —— 按数量压缩
                float step = FitSpacing(blessingSpacing, blessings.Count, blessingRow, 320f);
                float startX = -(blessings.Count - 1) * 0.5f * step;
                for (int i = 0; i < blessings.Count; i++)
                {
                    var entry = Instantiate(blessingEntryPrefab, blessingRow);
                    entry.gameObject.SetActive(true);
                    var rt = (RectTransform)entry.transform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(startX + i * step, 0f);

                    int index = i;
                    entry.Bind(BlessingDatabase.Get(blessings[i]), 1, () =>
                    {
                        _blessingChoice = _blessingChoice == index ? -1 : index;
                        RefreshToggles();
                    });
                    _blessingEntries.Add(entry);
                }
            }

            if (emptyCardHint != null) emptyCardHint.gameObject.SetActive(cards.Count == 0);
            if (emptyBlessingHint != null) emptyBlessingHint.gameObject.SetActive(blessings.Count == 0);

            BuildUpgradeOptions();
            RefreshToggles();
        }

        /// <summary>
        /// 祝福满 3 个后点粉色流星，这里列出三个已有祝福供选一个升级。
        /// **只能升一个、只升一级**；已满级的选项禁用。
        /// </summary>
        private void BuildUpgradeOptions()
        {
            var row = upgradeRow != null ? upgradeRow : blessingRow;
            bool show = _game.UpgradeOffered && row != null && blessingEntryPrefab != null;

            if (upgradeHint != null)
            {
                upgradeHint.gameObject.SetActive(show);
                if (show) upgradeHint.text = "本回合可将一个祝福升 1 级（只能选一个）";
            }
            if (!show) return;

            var owned = _game.Blessings;
            float upStep = FitSpacing(blessingSpacing, owned.Count, row, 320f);
            float startX = -(owned.Count - 1) * 0.5f * upStep;

            for (int i = 0; i < owned.Count; i++)
            {
                var ob = owned[i];
                var entry = Instantiate(blessingEntryPrefab, row);
                entry.gameObject.SetActive(true);
                var rt = (RectTransform)entry.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(startX + i * upStep, 0f);

                // 满级的显示当前级、且点不动；可升的显示"升到下一级"的效果
                int shownLevel = ob.IsMaxed ? ob.Level : ob.Level + 1;
                int index = i;
                entry.Bind(BlessingDatabase.Get(ob.Id), shownLevel,
                           ob.IsMaxed ? (System.Action)null : () =>
                           {
                               _upgradeChoice = _upgradeChoice == index ? -1 : index;
                               RefreshToggles();
                           });
                if (ob.IsMaxed && entry.toggleButton != null) entry.toggleButton.interactable = false;
                _upgradeEntries.Add(entry);
            }
        }

        private void RefreshToggles()
        {
            int handRoom = Mathf.Max(0, _game.Config.handLimit - _game.Hand.Count);
            int blessRoom = Mathf.Max(0, _game.Config.blessingLimit - _game.Blessings.Count);

            int keptCards = 0;
            for (int i = 0; i < _keepCards.Count; i++) if (_keepCards[i]) keptCards++;
            if (blessRoom <= 0) _blessingChoice = -1;      // 满了就不能再收
            int keptBless = _blessingChoice >= 0 ? 1 : 0;

            // 超上限时，从后往前把多余的「留下」改回「删去」
            for (int i = _keepCards.Count - 1; i >= 0 && keptCards > handRoom; i--)
                if (_keepCards[i]) { _keepCards[i] = false; keptCards--; }

            for (int i = 0; i < _cardEntries.Count && i < _keepCards.Count; i++)
                _cardEntries[i].SetKeep(_keepCards[i]);
            for (int i = 0; i < _blessingEntries.Count; i++)
            {
                bool chosen = _blessingChoice == i;
                _blessingEntries[i].SetLabel(chosen ? "保留" : "舍弃");
                _blessingEntries[i].SetHighlight(chosen);
            }

            for (int i = 0; i < _upgradeEntries.Count && i < _game.Blessings.Count; i++)
            {
                var ob = _game.Blessings[i];
                if (ob.IsMaxed)
                {
                    _upgradeEntries[i].SetLabel("已满级");
                    continue;
                }
                _upgradeEntries[i].SetLabel(_upgradeChoice == i ? "升级" : "未分配");
                _upgradeEntries[i].SetHighlight(_upgradeChoice == i);
            }

            if (summaryText != null)
                summaryText.text =
                    $"留下的星宿牌进入手牌（手牌上限 {_game.Config.handLimit}，当前 {_game.Hand.Count}，本次可留 {handRoom} 张，已选 {keptCards}）\n" +
                    $"众星祝福上限 {_game.Config.blessingLimit}（当前 {_game.Blessings.Count}，本次可留 1 个，已选 {keptBless}）\n" +
                    "删去的星宿牌会回到卡池，之后仍可能再被流星带来。";
        }

        private void OnConfirm()
        {
            var keepCards = new List<bool>(_keepCards);
            int blessPick = _blessingChoice;
            int upgrade = _upgradeChoice;
            SetVisible(false);
            Clear();
            _game.ConfirmRewards(keepCards, blessPick, upgrade);
        }

        private void SetVisible(bool on)
        {
            if (panel != null) panel.SetShown(on);
            else gameObject.SetActive(on);
        }

        /// <summary>
        /// 按条目数算实际间距：放得下用 preferred，放不下就压缩到刚好塞进容器
        /// （下限是条目宽的 55%，再挤就完全叠住看不清了）。
        /// </summary>
        private static float FitSpacing(float preferred, int count, RectTransform row, float itemWidth)
        {
            if (count <= 1 || row == null) return preferred;
            float usable = row.rect.width - itemWidth;      // 首尾各留半个条目
            if (usable <= 0f) return preferred;
            float needed = (count - 1) * preferred;
            if (needed <= usable) return preferred;
            return Mathf.Max(itemWidth * 0.55f, usable / (count - 1));
        }

        private void Clear()
        {
            for (int i = 0; i < _cardEntries.Count; i++)
                if (_cardEntries[i] != null) Destroy(_cardEntries[i].gameObject);
            for (int i = 0; i < _blessingEntries.Count; i++)
                if (_blessingEntries[i] != null) Destroy(_blessingEntries[i].gameObject);
            for (int i = 0; i < _upgradeEntries.Count; i++)
                if (_upgradeEntries[i] != null) Destroy(_upgradeEntries[i].gameObject);
            _upgradeEntries.Clear();
            _upgradeChoice = -1;

            _cardEntries.Clear();
            _blessingEntries.Clear();
            _keepCards.Clear();
            _blessingChoice = -1;
        }
    }
}
