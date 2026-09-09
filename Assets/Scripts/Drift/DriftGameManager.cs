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
        private readonly List<BlessingId> _blessings = new();
        private readonly HashSet<Direction> _homecomed = new();

        private readonly List<Core.StarCard> _pendingCards = new();
        private readonly List<BlessingId> _pendingBlessings = new();
        private int _pendingBonusActions;

        private int _actionsSinceEvent;
        private bool _freeMoveUsedThisTurn;
        private float _meteorTimeLeft;
        private float _meteorDuration;
        private System.Random _rng;

        public IReadOnlyList<Core.StarCard> Hand => _hand;
        public IReadOnlyList<Core.StarCard> Pool => _pool;
        public IReadOnlyList<BlessingId> Blessings => _blessings;
        public IReadOnlyCollection<Direction> Homecomed => _homecomed;
        public IReadOnlyList<Core.StarCard> PendingCards => _pendingCards;
        public IReadOnlyList<BlessingId> PendingBlessings => _pendingBlessings;
        public int PendingBonusActions => _pendingBonusActions;

        public float MeteorTimeLeft => _meteorTimeLeft;
        public float MeteorDuration => _meteorDuration;
        public bool IsWin => _homecomed.Count >= 4;

        // ---------- 事件（UI 订阅） ----------
        /// <summary>任何状态变化后触发，UI 全量刷新。</summary>
        public event Action StateChanged;
        /// <summary>一行日志（右侧星语栏）。</summary>
        public event Action<string> Logged;
        public event Action<DriftPhase> PhaseChanged;
        /// <summary>流星阶段结束、有收获需要取舍。</summary>
        public event Action RewardsReady;
        public event Action<Direction> Homecoming;
        public event Action<RandomEventDef> RandomEventFired;
        public event Action GameOverEvent;
        /// <summary>棋盘发生位移（漂移 / 事件），UI 用动画跟随。</summary>
        public event Action BoardShuffled;

        // ---------- 派生数值 ----------
        public int ActionsPerTurn =>
            config.baseActionsPerTurn
            + (_homecomed.Contains(Direction.East) ? 2 : 0)
            + (HasBlessing(BlessingId.YaoGuangStride) ? 1 : 0);

        public int MinLink =>
            Mathf.Max(2, HasBlessing(BlessingId.TianXuanHeart) ? config.minLinkCount - 1 : config.minLinkCount);

        public bool AllowRotation => HasBlessing(BlessingId.TianShuMirror);

        public int EventInterval =>
            Mathf.Max(1, config.eventIntervalBase - _homecomed.Count + (HasBlessing(BlessingId.KaiYangSteady) ? 1 : 0));

        public int ActionsUntilEvent => Mathf.Max(0, EventInterval - _actionsSinceEvent);

        public float ComputeMeteorDuration() =>
            config.meteorPhaseDuration
            + (_homecomed.Contains(Direction.West) ? 5f : 0f)
            + (HasBlessing(BlessingId.YaoGuangGaze) ? 4f : 0f);

        public bool HasBlessing(BlessingId id) => _blessings.Contains(id);
        public bool IsHomecomed(Direction dir) => _homecomed.Contains(dir);

        public bool CanSpawnPinkMeteor =>
            _blessings.Count + _pendingBlessings.Count < config.blessingLimit
            && BlessingDatabase.AllDefs.Count > _blessings.Count + _pendingBlessings.Count;

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
            if (Phase != DriftPhase.Meteor) return;
            _meteorTimeLeft -= Time.deltaTime;
            if (_meteorTimeLeft <= 0f)
            {
                _meteorTimeLeft = 0f;
                EndMeteorPhase();
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
            if (config.maxTurns > 0 && TurnIndex >= config.maxTurns)
            {
                FinishGame("时辰已尽");
                return;
            }

            TurnIndex++;
            _pendingCards.Clear();
            _pendingBlessings.Clear();
            _pendingBonusActions = 0;
            _freeMoveUsedThisTurn = false;

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

            if (_pendingCards.Count == 0 && _pendingBlessings.Count == 0)
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
        public void ConfirmRewards(IList<bool> keepCards, IList<bool> keepBlessings)
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

            for (int i = 0; i < _pendingBlessings.Count; i++)
            {
                bool keep = keepBlessings != null && i < keepBlessings.Count && keepBlessings[i];
                if (!keep) continue;
                if (_blessings.Count >= config.blessingLimit)
                {
                    Log($"众星祝福已满 {config.blessingLimit} 个，{BlessingDatabase.GetName(_pendingBlessings[i])} 散去。");
                    continue;
                }
                _blessings.Add(_pendingBlessings[i]);
                Log($"获得众星祝福：{BlessingDatabase.GetName(_pendingBlessings[i])}");
            }

            _pendingCards.Clear();
            _pendingBlessings.Clear();
            BeginBoardPhase();
        }

        private void BeginBoardPhase()
        {
            ActionsLeft = ActionsPerTurn + _pendingBonusActions;
            _pendingBonusActions = 0;
            _actionsSinceEvent = 0;
            _freeMoveUsedThisTurn = false;

            if (HasBlessing(BlessingId.YuHengGather)) DrawToHand("玉衡-聚灵");
            if (_homecomed.Contains(Direction.South)) DrawToHand("朱雀-衔火");

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
                    var exclude = new List<BlessingId>(_blessings);
                    exclude.AddRange(_pendingBlessings);
                    var id = BlessingDatabase.RollNew(_rng, exclude);
                    if (id == BlessingId.None) return "众星祝福已尽";
                    _pendingBlessings.Add(id);
                    Notify();
                    return $"+{BlessingDatabase.GetName(id)}";
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

            bool free = HasBlessing(BlessingId.TianQuanShift) && !_freeMoveUsedThisTurn;
            if (free) _freeMoveUsedThisTurn = true;
            Log($"移动 {Describe(card)}：{from} 到 {to}{(free ? "（天权·移山，免费）" : "")}");
            ConsumeAction(free);
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

            Board.TickBlocks();
            Notify();

            if (IsWin)
            {
                FinishGame("四方归位");
                return;
            }
            BeginTurn();
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

            // 天玑·织星：每张 50% 概率不动
            if (HasBlessing(BlessingId.TianJiWeave))
                drifters.RemoveAll(_ => _rng.Next(2) == 0);

            // 玄武·镇渊：随机半数不动
            if (_homecomed.Contains(Direction.North) && drifters.Count > 1)
            {
                Shuffle(drifters);
                int keep = drifters.Count / 2;
                drifters.RemoveRange(0, keep);
            }

            if (drifters.Count == 0)
            {
                Log($"{reason}：星力护持，无牌移动。");
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
        private void TriggerRandomEvent()
        {
            var def = RandomEventDatabase.Roll(_rng);
            Log($"【随机事件】{def.Name} —— {def.Desc}");
            RandomEventFired?.Invoke(def);

            switch (def.Id)
            {
                case RandomEventId.StardustSquall:
                    ApplyDrift("星尘骤起");
                    break;

                case RandomEventId.MeteorImpact:
                {
                    var target = PickRandomUnlinked();
                    if (target.IsValid && Board.Remove(target, out var card))
                    {
                        _pool.Add(card);
                        Log($"{Describe(card)} 被击回卡池。");
                        BoardShuffled?.Invoke();
                    }
                    break;
                }

                case RandomEventId.OrbitPull:
                {
                    var target = PickRandomUnlinked();
                    if (!target.IsValid) break;
                    var card = Board.GetCard(target);
                    var dest = FindLinkingSlot(card.Direction, target);
                    if (!dest.IsValid)
                    {
                        var free = Board.FreeSlots();
                        if (free.Count == 0) break;
                        dest = free[_rng.Next(free.Count)];
                    }
                    Board.Move(target, dest);
                    Log($"{Describe(card)} 被星轨牵引至 {dest}。");
                    BoardShuffled?.Invoke();
                    break;
                }

                case RandomEventId.Gift:
                    DrawToHand("天赐流光");
                    break;

                case RandomEventId.VoidBite:
                {
                    var free = Board.FreeSlots();
                    if (free.Count == 0) break;
                    var p = free[_rng.Next(free.Count)];
                    Board.Block(p, 2);
                    Log($"{p} 被虚空吞噬，2 回合内不可落牌。");
                    break;
                }

                case RandomEventId.SkyReverse:
                    ActionsLeft += 2;
                    Log("行动次数 +2。");
                    break;

                case RandomEventId.StarTide:
                    ApplyTide();
                    break;
            }

            RecomputeLinks();
        }

        private void ApplyTide()
        {
            int[] dr = { -1, 1, 0, 0 };
            int[] dc = { 0, 0, -1, 1 };
            string[] names = { "上", "下", "左", "右" };
            int d = _rng.Next(4);

            var cards = new List<GridPos>();
            foreach (var kv in Board.AllCards())
                if (!Links.IsLinked(kv.Key)) cards.Add(kv.Key);

            // 顺着潮水方向从最前面的牌开始挪，才不会互相挡路
            cards.Sort((a, b) =>
            {
                int pa = a.Row * dr[d] + a.Col * dc[d];
                int pb = b.Row * dr[d] + b.Col * dc[d];
                return pb.CompareTo(pa);
            });

            int moved = 0;
            foreach (var p in cards)
            {
                var to = p.Offset(dr[d], dc[d]);
                if (Board.IsFree(to) && Board.Move(p, to)) moved++;
            }
            Log($"星潮向{names[d]}涌动，{moved} 张牌被推移。");
            if (moved > 0) BoardShuffled?.Invoke();
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

        private GridPos PickRandomUnlinked()
        {
            var list = new List<GridPos>();
            foreach (var kv in Board.AllCards())
                if (!Links.IsLinked(kv.Key)) list.Add(kv.Key);
            if (list.Count == 0) return GridPos.Invalid;
            return list[_rng.Next(list.Count)];
        }

        /// <summary>找一个空位，把 dir 方位的这张牌挪过去后能构成连结。</summary>
        private GridPos FindLinkingSlot(Direction dir, GridPos self)
        {
            var occupied = new HashSet<GridPos>(Board.PositionsOfDirection(dir));
            occupied.Remove(self);

            var placements = FormationDatabase.GetPlacements(dir, Board.Rows, Board.Cols, AllowRotation);
            var good = new List<GridPos>();
            foreach (var placement in placements)
            {
                int hit = 0;
                for (int i = 0; i < placement.Length; i++)
                    if (occupied.Contains(placement[i])) hit++;
                if (hit + 1 < MinLink) continue;

                for (int i = 0; i < placement.Length; i++)
                {
                    var cell = placement[i];
                    if (cell != self && Board.IsFree(cell)) good.Add(cell);
                }
            }
            if (good.Count == 0) return GridPos.Invalid;
            return good[_rng.Next(good.Count)];
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

        private void Log(string line) => Logged?.Invoke(line);
        private void Notify() => StateChanged?.Invoke();
    }
}
