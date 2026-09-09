using System;
using UnityEngine;

namespace StarCard.Drift
{
    /// <summary>全部数值集中在这里，挂在 DriftGameManager 上，Inspector 直接调。</summary>
    [Serializable]
    public class DriftConfig
    {
        [Header("棋盘")]
        [Tooltip("棋盘行数")] public int rows = 4;
        [Tooltip("棋盘列数")] public int cols = 7;
        [Tooltip("棋盘上最多同时存在的牌数")] public int boardCardLimit = 14;
        [Tooltip("开局随机铺在棋盘上的牌数")] public int initialCards = 6;

        [Header("回合")]
        [Tooltip("一局最多多少回合，0 = 不限（只能靠归位四方位结束）")] public int maxTurns = 15;
        [Tooltip("每回合基础行动次数")] public int baseActionsPerTurn = 4;
        [Tooltip("手牌上限，超过后不再获得新牌")] public int handLimit = 5;
        [Tooltip("回合结束后的停顿秒数，让玩家看清漂移/变换的结果。0 = 不停顿")] public float turnReviewDuration = 2f;

        [Header("连结 / 随机事件")]
        [Tooltip("构成连结所需的最少同方位牌数")] public int minLinkCount = 3;
        [Tooltip("随机事件间隔的基数：每（本值 - 已归位方位数）次行动触发一次")] public int eventIntervalBase = 5;

        [Header("流星定位")]
        [Tooltip("流星阶段基础时长（秒）")] public float meteorPhaseDuration = 14f;
        [Tooltip("众星祝福持有上限")] public int blessingLimit = 3;
        [Tooltip("每回合最多能攒几个候选祝福（结算界面从里面选一个收下）")] public int blessingCandidateLimit = 3;
        [Tooltip("大型白色流星生成间隔（秒）")] public float bigSpawnInterval = 3.2f;
        [Tooltip("中型属性流星生成间隔（秒）")] public float mediumSpawnInterval = 1.1f;
        [Tooltip("小型粉色流星生成间隔（秒）")] public float smallSpawnInterval = 4.5f;
        [Tooltip("大型流星速度（像素/秒，1920x1080 参考分辨率下）")] public float bigSpeed = 110f;
        [Tooltip("中型流星速度")] public float mediumSpeed = 230f;
        [Tooltip("小型流星速度")] public float smallSpeed = 380f;

        [Header("计分")]
        [Tooltip("每个归位方位的分数")] public int scorePerHomecoming = 1000;
        [Tooltip("每回合结束时，每张连结牌的分数")] public int scorePerLinkedCard = 20;

        [Header("随机数")]
        [Tooltip("0 = 用时间做种子；非 0 = 固定种子，便于复现 bug")] public int randomSeed = 0;

        [Header("调试")]
        [Tooltip("把游戏过程日志打到 Console。右侧星语栏已移除，想看过程就勾上这个")]
        public bool logToConsole = false;
    }
}
