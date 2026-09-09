using System;
using System.Collections.Generic;
using UnityEngine;
using StarCard.Core;
using StarCard.Data;

namespace StarCard.Drift
{
    public enum DriftPhase
    {
        /// <summary>还没开局</summary>
        Ready,
        /// <summary>流星定位</summary>
        Meteor,
        /// <summary>流星收获的取舍</summary>
        RewardPick,
        /// <summary>棋盘操作</summary>
        Board,
        /// <summary>回合结束后的短暂停顿，让玩家看清漂移/变换的结果</summary>
        TurnReview,
        /// <summary>一局结束</summary>
        GameOver
    }

    /// <summary>
    /// 《漂泊的星宿》主流程。
    /// 一局：棋盘初始化 →（流星定位 → 收获取舍 → 棋盘操作 → 漂移）× N
    /// 棋盘操作期间每 (5 - 已归位方位数) 次行动触发一次随机事件。
    /// </summary>
    public class DriftGameManager : MonoBehaviour
    {
        [SerializeField] private DriftConfig config = new DriftConfig();

        public DriftConfig Config => config;

        // ---------- 运行时状态 ----------
        public BoardModel Board { get; private set; }
        public DriftPhase Phase { get; private set; } = DriftPhase.Ready;
        public int TurnIndex { get; private set; }
        public int ActionsLeft { get; private set; }
        public int Score { get; private set; }
        public LinkState Links { get; private set; } = new LinkState();

        private readonly List<Core.StarCard> _pool = new();
        private readonly List<Core.StarCard> _hand = new();
        private readonly List<OwnedBlessing> _blessings = new();
        private readonly HashSet<Direction> _homecomed = new();

        private readonly List<Core.StarCard> _pendingCards = new();
        private readonly List<BlessingId> _pendingBlessings = new();

        /// <summary>本回合是否已经拿到"可升级一次"的机会（点满 3 个祝福后的粉色流星给的）。</summary>
        private bool _upgradeOffered;
        private int _pendingBonusActions;

        private int _actionsSinceEvent;
        private float _meteorTimeLeft;
        private float _meteorDuration;
        private float _reviewTimeLeft;
        private System.Random _rng;

        public IReadOnlyList<Core.StarCard> Hand => _hand;
        public IReadOnlyList<Core.StarCard> Pool => _pool;
        public IReadOnlyList<OwnedBlessing> Blessings => _blessings;
        public IReadOnlyCollection<Direction> Homecomed => _homecomed;
        public IReadOnlyList<Core.StarCard> PendingCards => _pendingCards;
        public IReadOnlyList<BlessingId> PendingBlessings => _pendingBlessings;

        /// <summary>本回合有一次祝福升级机会待使用（取舍界面要显示升级选项）。</summary>
        public bool UpgradeOffered => _upgradeOffered;
        public int PendingBonusActions => _pendingBonusActions;

        public float MeteorTimeLeft => _meteorTimeLeft;
        public float MeteorDuration => _meteorDuration;
        /// <summary>回合结束停顿的剩余秒数（TurnReview 阶段有效）。</summary>
        public float ReviewTimeLeft => _reviewTimeLeft;
        public bool IsWin => _homecomed.Count >= 4;

        // ---------- 事件（UI 订阅） ----------
        /// <summary>任何状态变化后触发，UI 全量刷新。</summary>
        public event Action StateChanged;
        /// <summary>
        /// 一行日志。右侧星语栏已改成祝福面板，所以现在**默认没有 UI 订阅它**；
        /// 事件保留是因为 Log() 在二十多处被调用、内容对排查很有用。
        /// 勾上 logToConsole 就能在 Console 里看全过程。
        /// </summary>
        public event Action<string> Logged;
        public event Action<DriftPhase> PhaseChanged;
        /// <summary>流星阶段结束、有收获需要取舍。</summary>
        public event Action RewardsReady;
        public event Action<Direction> Homecoming;
        public event Action<RandomEventDef> RandomEventFired;
        /// <summary>抽到了但被方位祝福屏蔽掉的事件。</summary>
        public event Action<RandomEventDef, Direction> RandomEventBlocked;
        public event Action GameOverEvent;
        /// <summary>棋盘发生位移（漂移 / 事件），UI 用动画跟随。</summary>
        public event Action BoardShuffled;

        // ---------- 派生数值 ----------
        //
        // 众星祝福的效果全部按等级生效：BlessingLevel(id) 没有该祝福时返回 0，
        // 所以下面直接乘等级即可，不需要先判断有没有。

        /// <summary>禄存：每回合行动次数额外 +等级</summary>
        public int ActionsPerTurn =>
            config.baseActionsPerTurn + BlessingLevel(BlessingId.LuCun);

        /// <summary>廉贞：总回合数额外 +等级</summary>
        public int MaxTurns =>
            config.maxTurns <= 0 ? 0 : config.maxTurns + BlessingLevel(BlessingId.LianZhen);

        public int MinLink => Mathf.Max(2, config.minLinkCount);

        public bool AllowRotation => false;

        /// <summary>文曲：随机事件所需的行动数额外 +等级</summary>
        public int EventInterval =>
            Mathf.Max(1, config.eventIntervalBase - _homecomed.Count + BlessingLevel(BlessingId.WenQu));

        public int ActionsUntilEvent => Mathf.Max(0, EventInterval - _actionsSinceEvent);

        /// <summary>贪狼：流星定位限时 +2×等级 秒</summary>
        public float ComputeMeteorDuration() =>
            config.meteorPhaseDuration + 2f * BlessingLevel(BlessingId.TanLang);

        /// <summary>破军：流星移动速度乘 (1 - 0.08×等级)</summary>
        public float MeteorSpeedScale =>
            Mathf.Max(0.1f, 1f - 0.08f * BlessingLevel(BlessingId.PoJun));

        /// <summary>巨门：流星体型乘 (1 + 0.16×等级)</summary>
        public float MeteorSizeScale =>
            1f + 0.16f * BlessingLevel(BlessingId.JuMen);

        /// <summary>武曲：每回合结束时不发生漂移的概率（20%×等级）</summary>
        public float NoDriftChance =>
            0.2f * BlessingLevel(BlessingId.WuQu);

        /// <summary>持有该祝福的等级；没有则 0。</summary>
        public int BlessingLevel(BlessingId id)
        {
            for (int i = 0; i < _blessings.Count; i++)
                if (_blessings[i].Id == id) return _blessings[i].Level;
            return 0;
        }

        public bool HasBlessing(BlessingId id) => BlessingLevel(id) > 0;
        public bool IsHomecomed(Direction dir) => _homecomed.Contains(dir);

        /// <summary>
        /// 还该不该刷小型粉色流星。
        /// 未满 3 个 → 刷（点了随机抽一个新祝福）；
        /// 已满 3 个 → 只要还有没满级的就继续刷（点了进升级选择）。
        /// </summary>
        public bool CanSpawnPinkMeteor
        {
            get
            {
                // 注意：这里**不判断"本回合是否已拿到"** ——
                // 那条限制在 CollectMeteor 里拦（点了会提示"本回合已有祝福待取"）。
                // 如果连生成都掐掉，玩家点中第一颗后剩下的定位时间里粉色流星
                // 会彻底消失，看起来像"整局只有一颗"。
                if (_blessings.Count < config.blessingLimit)
                {
                    if (_pendingBlessings.Count >= config.blessingCandidateLimit) return false;
                    return BlessingDatabase.AllDefs.Count > _blessings.Count + _pendingBlessings.Count;
                }

                // 三个都满级了才真的没必要再刷
                return HasUpgradableBlessing;
            }
        }

        /// <summary>持有的祝福里还有没满级的吗。</summary>
        public bool HasUpgradableBlessing
        {
            get
            {
                for (int i = 0; i < _blessings.Count; i++)
                    if (!_blessings[i].IsMaxed) return true;
                return false;
            }
        }


        /// <summary>卡池里还剩哪些属性（中型流星只生成这些颜色）。</summary>
        public List<Element> AvailablePoolElements()
        {
            var set = new HashSet<Element>();
            for (int i = 0; i < _pool.Count; i++) set.Add(_pool[i].Element);
            return new List<Element>(set);
        }

        // ---------- 生命周期 ----------
        private void Update()
        {
            if (Phase == DriftPhase.Meteor)
            {
                _meteorTimeLeft -= Time.deltaTime;
                if (_meteorTimeLeft <= 0f)
                {
                    _meteorTimeLeft = 0f;
                    EndMeteorPhase();
                }
                return;
            }

            if (Phase == DriftPhase.TurnReview)
            {
                _reviewTimeLeft -= Time.deltaTime;
                if (_reviewTimeLeft <= 0f)
                {
                    _reviewTimeLeft = 0f;
                    FinishTurnReview();
                }
            }
        }

        public void StartGame()
        {
            _rng = config.randomSeed != 0 ? new System.Random(config.randomSeed)
                                          : new System.Random(Environment.TickCount);

            FormationDatabase.ClearCache();
            Board = new BoardModel(config.rows, config.cols);

            _pool.Clear();
            _pool.AddRange(StarCardDatabase.BuildFullDeck());
            Shuffle(_pool);

            _hand.Clear();
            _blessings.Clear();
            _homecomed.Clear();
            _pendingCards.Clear();
            _pendingBlessings.Clear();
            _pendingBonusActions = 0;
            _upgradeOffered = false;
            Score = 0;
            TurnIndex = 0;
            _actionsSinceEvent = 0;

            // 棋盘初始化：随机 N 张牌落在随机空位
            for (int i = 0; i < config.initialCards && _pool.Count > 0; i++)
            {
                var free = Board.FreeSlots();
                if (free.Count == 0) break;
                var card = TakeFromPool(_pool.Count - 1);
                Board.Place(free[_rng.Next(free.Count)], card);
            }

            Log("星海初开，六宿漂泊于天盘之上。");
            RecomputeLinks();
            BeginTurn();
        }

        private void BeginTurn()
        {
            if (MaxTurns > 0 && TurnIndex >= MaxTurns)   // 廉贞会加总回合数
            {
                FinishGame("时辰已尽");
                return;
            }

            TurnIndex++;
            _pendingCards.Clear();
            _pendingBlessings.Clear();
            _pendingBonusActions = 0;
            _upgradeOffered = false;

            _meteorDuration = ComputeMeteorDuration();
            _meteorTimeLeft = _meteorDuration;
            SetPhase(DriftPhase.Meteor);
            Log($"—— 第 {TurnIndex} 回合 | 流星定位 ——");
            Notify();
        }

        /// <summary>玩家点“跳过定位”，或计时结束。</summary>
        public void EndMeteorPhase()
        {
            if (Phase != DriftPhase.Meteor) return;
            _meteorTimeLeft = 0f;

            if (_pendingCards.Count == 0 && _pendingBlessings.Count == 0 && !_upgradeOffered)
            {
                if (_pendingBonusActions > 0) Log($"定位所得：行动次数 +{_pendingBonusActions}");
                BeginBoardPhase();
                return;
            }

            SetPhase(DriftPhase.RewardPick);
            RewardsReady?.Invoke();
            Notify();
        }

        /// <summary>取舍确认。keepCards / keepBlessings 与 PendingCards / PendingBlessings 一一对应。</summary>
        /// <param name="blessingChoice">
        /// 要收下的候选祝福在 PendingBlessings 里的下标；-1 = 都不要。
        /// **只能收一个** —— 没选的不作记录，下回合还能再抽到。
        /// </param>
        /// <param name="upgradeIndex">
        /// 要升级的祝福在 Blessings 里的下标；-1 = 不升级。
        /// 只在本回合有升级机会（UpgradeOffered）时有意义，且**只能升一个、只升一级**。
        /// </param>
        public void ConfirmRewards(IList<bool> keepCards, int blessingChoice = -1, int upgradeIndex = -1)
        {
            if (Phase != DriftPhase.RewardPick) return;

            for (int i = 0; i < _pendingCards.Count; i++)
            {
                bool keep = keepCards != null && i < keepCards.Count && keepCards[i];
                if (keep && _hand.Count < config.handLimit)
                {
                    _hand.Add(_pendingCards[i]);
                }
                else
                {
                    // 删去（或手牌已满）→ 退回卡池
                    _pool.Add(_pendingCards[i]);
                    if (keep) Log($"手牌已满，{Describe(_pendingCards[i])} 退回卡池。");
                }
            }

            // 候选祝福里**只能收下一个**（blessingChoice 是候选池的下标，-1 = 都不要）。
            // 没被选中的不做任何记录 —— 下回合还能再抽到，这是有意的。
            if (blessingChoice >= 0 && blessingChoice < _pendingBlessings.Count)
            {
                var picked = _pendingBlessings[blessingChoice];
                if (_blessings.Count >= config.blessingLimit)
                {
                    Log($"众星祝福已满 {config.blessingLimit} 个，{BlessingDatabase.GetName(picked)} 散去。");
                }
                else
                {
                    _blessings.Add(new OwnedBlessing(picked, 1));
                    Log($"获得众星祝福：{BlessingDatabase.GetName(picked)}-1");
                }
            }
            if (_pendingBlessings.Count > 0)
            {
                int passed = _pendingBlessings.Count - (blessingChoice >= 0 ? 1 : 0);
                if (passed > 0) Log($"另有 {passed} 个祝福未选（下回合仍可再抽到）。");
            }

            // 升级：只能一个、只升一级、满级的不能升
            if (_upgradeOffered && upgradeIndex >= 0 && upgradeIndex < _blessings.Count)
            {
                var ob = _blessings[upgradeIndex];
                if (ob.IsMaxed)
                {
                    Log($"{ob.Name} 已达上限 {ob.MaxLevel} 级，无法升级。");
                }
                else
                {
                    ob.Level++;
                    _blessings[upgradeIndex] = ob;
                    Log($"众星祝福升级：{ob.Title}（{ob.Desc}）");
                }
            }
            _upgradeOffered = false;

            _pendingCards.Clear();
            _pendingBlessings.Clear();
            BeginBoardPhase();
        }

        private void BeginBoardPhase()
        {
            ActionsLeft = ActionsPerTurn + _pendingBonusActions;
            _pendingBonusActions = 0;
            _actionsSinceEvent = 0;

            SetPhase(DriftPhase.Board);
            Log($"棋盘操作开始：行动次数 {ActionsLeft}，每 {EventInterval} 次行动生变。");
            RecomputeLinks();
            Notify();
        }

        // ---------- 流星收集 ----------
        /// <summary>点中一颗流星。返回给 UI 显示的飘字，null 表示没吃到东西。</summary>
        public string CollectMeteor(MeteorKind kind)
        {
            if (Phase != DriftPhase.Meteor) return null;

            switch (kind.Size)
            {
                case MeteorSize.Big:
                    _pendingBonusActions++;
                    Notify();
                    return "行动次数 +1";

                case MeteorSize.Medium:
                {
                    int idx = FindPoolIndexOfElement(kind.Element);
                    if (idx < 0) return $"{StarCardDatabase.GetChineseElement(kind.Element)}宿已尽";
                    var card = TakeFromPool(idx);
                    _pendingCards.Add(card);
                    Notify();
                    return $"+{Describe(card)}";
                }

                case MeteorSize.Small:
                {
                    // 未满 3 个：每颗流星都能点，抽到的都进候选池；
                    // 结算界面从候选里**选一个**收下，没选的下回合还能再抽到。
                    if (_blessings.Count < config.blessingLimit)
                    {
                        if (_pendingBlessings.Count >= config.blessingCandidateLimit)
                            return "候选已满";

                        var exclude = new List<BlessingId>();
                        for (int i = 0; i < _blessings.Count; i++) exclude.Add(_blessings[i].Id);
                        exclude.AddRange(_pendingBlessings);          // 同回合不重复抽同一个
                        var id = BlessingDatabase.RollNew(_rng, exclude);
                        if (id == BlessingId.None) return "众星祝福已尽";
                        _pendingBlessings.Add(id);
                        Notify();
                        return $"+{BlessingDatabase.GetName(id)}";
                    }

                    // 已满 3 个：进入升级流程，**点多颗也只给一次升级机会**
                    if (_upgradeOffered) return "本回合已可升级";
                    if (!HasUpgradableBlessing) return "祝福均已满级";
                    _upgradeOffered = true;
                    Notify();
                    return "可升级一个祝福";
                }
            }
            return null;
        }

        // ---------- 棋盘操作 ----------
        public bool CanPlaceOnBoard => Board != null && Board.CardCount < config.boardCardLimit;

        public bool TryPlaceFromHand(int handIndex, GridPos pos)
        {
            if (Phase != DriftPhase.Board || ActionsLeft <= 0) return false;
            if (handIndex < 0 || handIndex >= _hand.Count) return false;
            if (!Board.IsFree(pos)) return false;
            if (!CanPlaceOnBoard)
            {
                Log($"棋盘已满（上限 {config.boardCardLimit} 张）。");
                return false;
            }

            var card = _hand[handIndex];
            if (!Board.Place(pos, card)) return false;
            _hand.RemoveAt(handIndex);
            Log($"放置 {Describe(card)} 到 {pos}");
            ConsumeAction(false);
            return true;
        }

        public bool TryMove(GridPos from, GridPos to)
        {
            if (Phase != DriftPhase.Board || ActionsLeft <= 0) return false;
            if (!Board.HasCard(from) || !Board.IsFree(to)) return false;

            var card = Board.GetCard(from);
            if (!Board.Move(from, to)) return false;

            Log($"移动 {Describe(card)}：{from} 到 {to}");
            ConsumeAction(false);
            return true;
        }

        public bool TrySwap(GridPos a, GridPos b)
        {
            if (Phase != DriftPhase.Board || ActionsLeft <= 0) return false;
            if (!Board.HasCard(a) || !Board.HasCard(b) || a == b) return false;

            var ca = Board.GetCard(a);
            var cb = Board.GetCard(b);
            if (!Board.Swap(a, b)) return false;
            Log($"交换 {Describe(ca)} 与 {Describe(cb)}");
            ConsumeAction(false);
            return true;
        }

        /// <summary>玩家主动结束回合（也用于行动次数用尽后的自动收尾）。</summary>
        public void EndTurnByPlayer()
        {
            if (Phase != DriftPhase.Board) return;
            EndTurn();
        }

        private void ConsumeAction(bool free)
        {
            if (!free) ActionsLeft--;
            _actionsSinceEvent++;

            RecomputeLinks();

            if (_actionsSinceEvent >= EventInterval)
            {
                _actionsSinceEvent = 0;
                TriggerRandomEvent();
            }

            Notify();

            if (Phase == DriftPhase.Board && ActionsLeft <= 0)
            {
                Log("行动次数用尽。");
                EndTurn();
            }
        }

        // ---------- 回合结束：漂移 ----------
        private void EndTurn()
        {
            ApplyDrift("漂移");
            RecomputeLinks();

            int linked = Links.LinkedCells.Count;
            if (linked > 0)
            {
                int gain = linked * config.scorePerLinkedCard;
                Score += gain;
                Log($"回合结算：连结 {linked} 张，+{gain} 分。");
            }

            // 手牌不跨回合：没打出去的牌回卡池，下回合靠流星重新获取
            DiscardHandToPool();

            Board.TickBlocks();

            // 归位四方就直接结束，不必再停顿
            if (IsWin)
            {
                Notify();
                FinishGame("四方归位");
                return;
            }

            // 停顿一下让玩家看清漂移后的棋盘，再进下一回合
            _reviewTimeLeft = Mathf.Max(0f, config.turnReviewDuration);
            SetPhase(DriftPhase.TurnReview);
            Notify();

            if (_reviewTimeLeft <= 0f) FinishTurnReview();
        }

        /// <summary>停顿结束（或被玩家点掉），进入下一回合。</summary>
        public void FinishTurnReview()
        {
            if (Phase != DriftPhase.TurnReview) return;
            _reviewTimeLeft = 0f;
            BeginTurn();
        }

        /// <summary>把手上没打出去的牌全部退回卡池。</summary>
        private void DiscardHandToPool()
        {
            if (_hand.Count == 0) return;
            int n = _hand.Count;
            _pool.AddRange(_hand);
            _hand.Clear();
            Log($"回合结束：{n} 张未放置的星宿牌退回卡池。");
        }

        /// <summary>棋盘上所有非连结的牌随机移动到合法空位。</summary>
        public void ApplyDrift(string reason)
        {
            var drifters = new List<KeyValuePair<GridPos, Core.StarCard>>();
            foreach (var kv in Board.AllCards())
                if (!Links.IsLinked(kv.Key)) drifters.Add(kv);

            if (drifters.Count == 0)
            {
                Log($"{reason}：所有星宿皆已连结，天盘不动。");
                return;
            }

            // 武曲：整回合按概率完全不漂移（20% × 等级）
            float noDrift = NoDriftChance;
            if (noDrift > 0f && _rng.NextDouble() < noDrift)
            {
                Log($"{reason}：武曲镇星，本回合不发生漂移。");
                return;
            }

            foreach (var d in drifters) Board.Remove(d.Key, out _);

            Shuffle(drifters);
            foreach (var d in drifters)
            {
                var free = Board.FreeSlots();
                if (free.Count > 1) free.Remove(d.Key); // 尽量真的挪个位置
                if (free.Count == 0)
                {
                    Board.Place(d.Key, d.Value);
                    continue;
                }
                Board.Place(free[_rng.Next(free.Count)], d.Value);
            }

            Log($"{reason}：{drifters.Count} 张非连结星宿随星海漂流。");
            BoardShuffled?.Invoke();
        }

        // ---------- 随机事件 ----------
        //
        // 全部是棋盘整体变换，等概率。**作用于所有牌，包括已连结的** ——
        // 变换后统一 RecomputeLinks()，所以可能凭空多出连结甚至归位，
        // 也可能让原有连结失效（高亮跟着消失）。
        private void TriggerRandomEvent()
        {
            var def = RandomEventDatabase.Roll(_rng);

            // 方位祝福会屏蔽一类事件：抽到就空过，不重抽
            // （重抽会让"被屏蔽的事件"变成"其他事件概率上升"，那是另一种设计）
            var blocker = FindBlocker(def.Id);
            if (blocker.HasValue)
            {
                Log($"【随机事件】{def.Name} —— 被「{DirectionBlessing.GetName(blocker.Value)}」镇住，未发生。");
                RandomEventBlocked?.Invoke(def, blocker.Value);
                return;
            }

            string detail = ApplyBoardEvent(def.Id);

            Log($"【随机事件】{def.Name} —— {def.Desc}" + (string.IsNullOrEmpty(detail) ? "" : $"（{detail}）"));
            RandomEventFired?.Invoke(def);

            RecomputeLinks();
            BoardShuffled?.Invoke();
        }

        /// <summary>找出屏蔽这个事件的已归位方位；没有则返回 null。</summary>
        private Direction? FindBlocker(RandomEventId id)
        {
            foreach (var dir in _homecomed)
                if (DirectionBlessing.Blocks(dir, id)) return dir;
            return null;
        }

        /// <summary>某个随机事件当前是否会被屏蔽（UI 提示用）。</summary>
        public bool IsEventBlocked(RandomEventId id) => FindBlocker(id).HasValue;

        /// <summary>执行一个事件的棋盘变换。返回补充说明（如旋转选中的九宫格），没有则空串。</summary>
        private string ApplyBoardEvent(RandomEventId id)
        {
            switch (id)
            {
                case RandomEventId.ShiftLeft:  Board.ShiftLeft();  return null;
                case RandomEventId.ShiftRight: Board.ShiftRight(); return null;
                case RandomEventId.ShiftUp:    Board.ShiftUp();    return null;
                case RandomEventId.ShiftDown:  Board.ShiftDown();  return null;

                case RandomEventId.MirrorVertical:   Board.MirrorVertical();   return null;
                case RandomEventId.MirrorHorizontal: Board.MirrorHorizontal(); return null;

                case RandomEventId.RotateClockwise:
                case RandomEventId.RotateCounter:
                {
                    var origins = Board.BlockOrigins();
                    if (origins.Count == 0) return "棋盘放不下九宫格，无事发生";
                    var o = origins[_rng.Next(origins.Count)];
                    bool cw = id == RandomEventId.RotateClockwise;
                    Board.RotateBlock(o.Row, o.Col, cw);
                    return $"以 {o} 为左上角的九宫格";
                }
            }
            return null;
        }

        // ---------- 连结 / 归位 ----------
        private void RecomputeLinks()
        {
            if (Board == null) return;

            Links = LinkResolver.Resolve(Board, MinLink, AllowRotation, _homecomed);

            // 归位可能连锁（归位后重算，理论上不会再触发，但保险起见循环）
            int guard = 0;
            while (Links.Homecoming.Count > 0 && guard++ < 8)
            {
                var dir = Links.Homecoming[0];
                ResolveHomecoming(dir);
                Links = LinkResolver.Resolve(Board, MinLink, AllowRotation, _homecomed);
            }
        }

        private void ResolveHomecoming(Direction dir)
        {
            if (_homecomed.Contains(dir)) return;
            _homecomed.Add(dir);

            // 该方位所有星宿从棋盘、卡池、手牌上一并消失
            var toRemove = Board.PositionsOfDirection(dir);
            foreach (var p in toRemove) Board.Remove(p, out _);
            _pool.RemoveAll(c => c.Direction == dir);
            _hand.RemoveAll(c => c.Direction == dir);
            _pendingCards.RemoveAll(c => c.Direction == dir);

            Score += config.scorePerHomecoming;
            Log($"【{StarCardDatabase.GetChineseDirection(dir)}方七宿归位】获得方位祝福 {DirectionBlessing.GetName(dir)}（{DirectionBlessing.GetDesc(dir)}）");
            Homecoming?.Invoke(dir);
            BoardShuffled?.Invoke();
        }

        // ---------- 工具 ----------
        private void FinishGame(string reason)
        {
            SetPhase(DriftPhase.GameOver);
            Log($"—— 本局结束（{reason}）：归位 {_homecomed.Count} 方，得分 {Score} ——");
            GameOverEvent?.Invoke();
            Notify();
        }

        private void DrawToHand(string reason)
        {
            if (_pool.Count == 0)
            {
                Log($"{reason}：卡池已空。");
                return;
            }
            if (_hand.Count >= config.handLimit)
            {
                Log($"{reason}：手牌已满。");
                return;
            }
            var card = TakeFromPool(_rng.Next(_pool.Count));
            _hand.Add(card);
            Log($"{reason}：抽到 {Describe(card)}");
        }

        private Core.StarCard TakeFromPool(int index)
        {
            var card = _pool[index];
            _pool.RemoveAt(index);
            return card;
        }

        private int FindPoolIndexOfElement(Element e)
        {
            var candidates = new List<int>();
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i].Element == e) candidates.Add(i);
            if (candidates.Count == 0) return -1;
            return candidates[_rng.Next(candidates.Count)];
        }


        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static string Describe(Core.StarCard card) =>
            $"{StarCardDatabase.GetChineseName(card.Name)}宿" +
            $"({StarCardDatabase.GetChineseDirection(card.Direction)}-{StarCardDatabase.GetChineseElement(card.Element)})";

        private void SetPhase(DriftPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private void Log(string line)
        {
            if (config.logToConsole) Debug.Log("[漂泊的星宿] " + line);
            Logged?.Invoke(line);
        }
        private void Notify() => StateChanged?.Invoke();
    }
}
