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

        private DriftGameManager _game;
        private readonly List<bool> _keepCards = new();
        private readonly List<bool> _keepBlessings = new();
        private readonly List<RewardCardEntry> _cardEntries = new();
        private readonly List<RewardBlessingEntry> _blessingEntries = new();

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
            for (int i = 0; i < blessings.Count; i++) _keepBlessings.Add(i < blessRoom);

            if (cardEntryPrefab != null && cardRow != null)
            {
                float startX = -(cards.Count - 1) * 0.5f * cardSpacing;
                for (int i = 0; i < cards.Count; i++)
                {
                    var entry = Instantiate(cardEntryPrefab, cardRow);
                    entry.gameObject.SetActive(true);
                    var rt = (RectTransform)entry.transform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(startX + i * cardSpacing, 0f);

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
                float startX = -(blessings.Count - 1) * 0.5f * blessingSpacing;
                for (int i = 0; i < blessings.Count; i++)
                {
                    var entry = Instantiate(blessingEntryPrefab, blessingRow);
                    entry.gameObject.SetActive(true);
                    var rt = (RectTransform)entry.transform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(startX + i * blessingSpacing, 0f);

                    int index = i;
                    entry.Bind(BlessingDatabase.Get(blessings[i]), () =>
                    {
                        _keepBlessings[index] = !_keepBlessings[index];
                        RefreshToggles();
                    });
                    _blessingEntries.Add(entry);
                }
            }

            if (emptyCardHint != null) emptyCardHint.gameObject.SetActive(cards.Count == 0);
            if (emptyBlessingHint != null) emptyBlessingHint.gameObject.SetActive(blessings.Count == 0);

            RefreshToggles();
        }

        private void RefreshToggles()
        {
            int handRoom = Mathf.Max(0, _game.Config.handLimit - _game.Hand.Count);
            int blessRoom = Mathf.Max(0, _game.Config.blessingLimit - _game.Blessings.Count);

            int keptCards = 0, keptBless = 0;
            for (int i = 0; i < _keepCards.Count; i++) if (_keepCards[i]) keptCards++;
            for (int i = 0; i < _keepBlessings.Count; i++) if (_keepBlessings[i]) keptBless++;

            // 超上限时，从后往前把多余的「留下」改回「删去」
            for (int i = _keepCards.Count - 1; i >= 0 && keptCards > handRoom; i--)
                if (_keepCards[i]) { _keepCards[i] = false; keptCards--; }
            for (int i = _keepBlessings.Count - 1; i >= 0 && keptBless > blessRoom; i--)
                if (_keepBlessings[i]) { _keepBlessings[i] = false; keptBless--; }

            for (int i = 0; i < _cardEntries.Count && i < _keepCards.Count; i++)
                _cardEntries[i].SetKeep(_keepCards[i]);
            for (int i = 0; i < _blessingEntries.Count && i < _keepBlessings.Count; i++)
                _blessingEntries[i].SetKeep(_keepBlessings[i]);

            if (summaryText != null)
                summaryText.text =
                    $"留下的星宿牌进入手牌（手牌上限 {_game.Config.handLimit}，当前 {_game.Hand.Count}，本次可留 {handRoom} 张，已选 {keptCards}）\n" +
                    $"众星祝福上限 {_game.Config.blessingLimit}（当前 {_game.Blessings.Count}，本次可留 {blessRoom} 个，已选 {keptBless}）\n" +
                    "删去的星宿牌会回到卡池，之后仍可能再被流星带来。";
        }

        private void OnConfirm()
        {
            var keepCards = new List<bool>(_keepCards);
            var keepBless = new List<bool>(_keepBlessings);
            SetVisible(false);
            Clear();
            _game.ConfirmRewards(keepCards, keepBless);
        }

        private void SetVisible(bool on)
        {
            if (panel != null) panel.SetShown(on);
            else gameObject.SetActive(on);
        }

        private void Clear()
        {
            for (int i = 0; i < _cardEntries.Count; i++)
                if (_cardEntries[i] != null) Destroy(_cardEntries[i].gameObject);
            for (int i = 0; i < _blessingEntries.Count; i++)
                if (_blessingEntries[i] != null) Destroy(_blessingEntries[i].gameObject);
            _cardEntries.Clear();
            _blessingEntries.Clear();
            _keepCards.Clear();
            _keepBlessings.Clear();
        }
    }
}
