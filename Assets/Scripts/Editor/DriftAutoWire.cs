using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StarCard.Core;
using StarCard.UI;

namespace StarCard.EditorTools
{
    /// <summary>
    /// 按物体名自动填 Inspector 引用的辅助工具，省掉大部分手拖。
    ///
    /// **它只填引用，不创建任何物体** —— 场景里没有的东西它不会替你造，只会在 Console 里报缺哪个。
    /// 填完之后每个字段都还是普通的 Inspector 字段，你想改随时可以手动改。
    ///
    /// 用法：选中 DriftGame → 右键 DriftGameBootstrap 组件标题 → 「自动填充引用」
    /// 或菜单栏 漂泊的星宿 → 自动填充选中物体的引用
    /// </summary>
    public static class DriftAutoWire
    {
        [MenuItem("漂泊的星宿/自动填充选中物体的引用")]
        public static void WireSelected()
        {
            var root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("先在 Hierarchy 里选中 DriftGame（挂 DriftGameBootstrap 的那个物体）。");
                return;
            }

            var boot = root.GetComponentInChildren<DriftGameBootstrap>(true);
            if (boot == null)
            {
                Debug.LogWarning($"[{root.name}] 及其子物体上找不到 DriftGameBootstrap。", root);
                return;
            }

            Wire(boot);
        }

        [MenuItem("漂泊的星宿/自动填充选中物体的引用", true)]
        private static bool WireSelectedValidate() => Selection.activeGameObject != null;

        public static void Wire(DriftGameBootstrap boot)
        {
            var canvas = Find(boot.transform, "DriftCanvas");
            if (canvas == null)
            {
                Debug.LogError("找不到 DriftCanvas 子物体，无法自动填充。", boot);
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(boot.gameObject, "Auto wire drift UI");

            // ---------- 各个 View 组件本身 ----------
            boot.board = GetOrAdd<BoardView>(canvas, "Board");
            boot.hand = GetOrAdd<HandView>(canvas, "Hand");
            boot.hud = GetOrAdd<HudView>(canvas, "Hud");
            boot.meteorField = GetOrAdd<MeteorFieldView>(canvas, "MeteorField");
            boot.rewardPicker = GetOrAdd<RewardPickerView>(canvas, "RewardPicker");
            boot.helpPanel = GetOrAdd<HelpPanelView>(canvas, "HelpPanel");
            boot.gameOver = GetOrAdd<GameOverView>(canvas, "GameOver");

            WireBoard(boot.board);
            WireHand(boot.hand);
            WireHud(boot.hud);
            WireMeteorField(boot.meteorField);
            WireRewardPicker(boot.rewardPicker);
            WireHelpPanel(boot.helpPanel);
            WireGameOver(boot.gameOver);
            WireOffscreenPanels(boot);

            if (boot.hud != null) boot.blessingPanel = boot.hud.blessingPanel;
            WirePrefabFields(boot);

            EditorUtility.SetDirty(boot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            Debug.Log("自动填充完成。请检查 Console 里的警告 —— 那些是场景里没找到、需要你手动补的引用。\n" +
                      "Assets/Prefabs/ 下已存在的 prefab 会自动填进对应字段；还没生成的先跑一次「提取 Prefab」。", boot);
        }

        /// <summary>
        /// 给四个模态面板挂 OffscreenPanel，并把它们从 Stretch 锚点转成 Center 锚点
        /// （跟老项目 star-card 一致 —— 画外坐标靠 anchoredPosition，Stretch 下这个字段不起作用）。
        ///
        /// **转换会保留你在 Scene 里已经拖好的位置**：换算成 Center 锚点下的等效坐标，
        /// 记为该面板的画外位。已经挂过 OffscreenPanel 的不动。
        /// </summary>
        private static void WireOffscreenPanels(DriftGameBootstrap boot)
        {
            var canvas = boot.GetComponentInChildren<Canvas>(true);
            var canvasRect = canvas != null ? (RectTransform)canvas.transform : null;
            Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : new Vector2(1920f, 1080f);
            if (canvasSize.x < 1f || canvasSize.y < 1f) canvasSize = new Vector2(1920f, 1080f);

            // 备用停靠位：万一某个面板本来就在原点，往右排开免得重叠
            float fallbackX = canvasSize.x * 1.25f;
            float fallbackStep = canvasSize.x * 1.1f;

            var targets = new (Component view, string label)[]
            {
                (boot.meteorField, "MeteorField"),
                (boot.rewardPicker, "RewardPicker"),
                (boot.helpPanel, "HelpPanel"),
                (boot.gameOver, "GameOver"),
            };

            for (int i = 0; i < targets.Length; i++)
            {
                var view = targets[i].view;
                if (view == null) continue;

                var go = view.gameObject;
                var rt = (RectTransform)go.transform;
                bool hadPanel = go.GetComponent<OffscreenPanel>() != null;

                // Stretch → Center。先记下当前在父物体里的实际位置，转换后原样放回去
                if (rt.anchorMin != rt.anchorMax)
                {
                    Vector3 worldPos = rt.position;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = canvasSize;
                    rt.position = worldPos;
                }

                var panel = go.GetComponent<OffscreenPanel>();
                if (panel == null)
                {
                    panel = Undo.AddComponent<OffscreenPanel>(go);
                    panel.shownAnchoredPos = Vector2.zero;
                    panel.hideOnAwake = true;

                    // 你拖到哪就以哪为画外位；正好在原点的才给个备用位
                    var current = rt.anchoredPosition;
                    panel.hiddenAnchoredPos = current.sqrMagnitude > 1f
                        ? current
                        : new Vector2(fallbackX + i * fallbackStep, 0f);
                    rt.anchoredPosition = panel.hiddenAnchoredPos;

                    Debug.Log($"[自动填充] {targets[i].label} 画外位记为 {panel.hiddenAnchoredPos}。" +
                              "想换地方就在 Scene 里拖它，然后右键 OffscreenPanel →「记录当前位置为画外位」。", go);
                }
                else if (!hadPanel)
                {
                    // 理论到不了这，兜底
                    panel.hiddenAnchoredPos = rt.anchoredPosition;
                }

                if (go.GetComponent<CanvasGroup>() == null) Undo.AddComponent<CanvasGroup>(go);

                switch (view)
                {
                    case MeteorFieldView v: v.panel = panel; break;
                    case RewardPickerView v: v.panel = panel; break;
                    case HelpPanelView v: v.panel = panel; break;
                    case GameOverView v: v.panel = panel; break;
                }

                EditorUtility.SetDirty(panel);
                EditorUtility.SetDirty(view);
                EditorUtility.SetDirty(rt);
            }
        }

        /// <summary>
        /// 把 Assets/Prefabs/ 下已有的 prefab 填进各字段（只填空着的，不覆盖你手动指定的）。
        /// 这样「自动填充」和「提取 Prefab」两个菜单项的执行顺序就不重要了 ——
        /// 先跑哪个都行，缺的那次跑完再跑一遍另一个即可。
        /// </summary>
        private static void WirePrefabFields(DriftGameBootstrap boot)
        {
            var card = Load<DriftCardView>("StarCardView");
            var cell = Load<Image>("FormationCell");
            var cardEntry = Load<RewardCardEntry>("RewardCardEntry");
            var blessEntry = Load<RewardBlessingEntry>("RewardBlessingEntry");
            var blessCard = Load<BlessingCardView>("BlessingCard");

            if (boot.board != null && boot.board.cardPrefab == null && card != null)
            {
                boot.board.cardPrefab = card;
                EditorUtility.SetDirty(boot.board);
            }
            if (boot.hand != null && boot.hand.cardPrefab == null && card != null)
            {
                boot.hand.cardPrefab = card;
                EditorUtility.SetDirty(boot.hand);
            }
            if (boot.rewardPicker != null)
            {
                if (boot.rewardPicker.cardEntryPrefab == null && cardEntry != null)
                    boot.rewardPicker.cardEntryPrefab = cardEntry;
                if (boot.rewardPicker.blessingEntryPrefab == null && blessEntry != null)
                    boot.rewardPicker.blessingEntryPrefab = blessEntry;
                EditorUtility.SetDirty(boot.rewardPicker);
            }
            if (boot.helpPanel != null && boot.helpPanel.formations != null && cell != null)
            {
                foreach (var f in boot.helpPanel.formations)
                {
                    if (f == null || f.cellPrefab != null) continue;
                    f.cellPrefab = cell;
                    EditorUtility.SetDirty(f);
                }
            }
            if (boot.blessingPanel != null && boot.blessingPanel.cardPrefab == null && blessCard != null)
            {
                boot.blessingPanel.cardPrefab = blessCard;
                EditorUtility.SetDirty(boot.blessingPanel);
            }

            int missing = 0;
            if (card == null) missing++;
            if (cell == null) missing++;
            if (cardEntry == null) missing++;
            if (blessEntry == null) missing++;
            if (blessCard == null) missing++;
            if (missing > 0)
                Debug.LogWarning($"[自动填充] Assets/Prefabs/ 下有 {missing} 个 prefab 还不存在 —— " +
                                 "跑一次「提取 Prefab（只需跑一次）」生成它们，然后再跑本菜单即可。");
        }

        private static T Load<T>(string name) where T : Component =>
            AssetDatabase.LoadAssetAtPath<T>($"Assets/Prefabs/{name}.prefab");
        // ---------- 各面板 ----------
        private static void WireBoard(BoardView board)
        {
            if (board == null) return;
            var cells = Find(board.transform, "Cells");
            var cards = Find(board.transform, "Cards");

            if (cards != null) board.cardLayer = (RectTransform)cards;
            else Warn(board, "Board/Cards 找不到，cardLayer 要手动拖");

            if (cells == null)
            {
                Warn(board, "Board/Cells 找不到，28 个格子要手动拖");
                return;
            }

            var list = new System.Collections.Generic.List<BoardCellView>();
            for (int i = 0; i < cells.childCount; i++)
            {
                var child = cells.GetChild(i);
                var cell = child.GetComponent<BoardCellView>();
                if (cell == null) cell = Undo.AddComponent<BoardCellView>(child.gameObject);

                cell.ParseCoordsFromName();
                if (cell.background == null) cell.background = child.GetComponent<Image>();
                if (cell.button == null)
                {
                    cell.button = child.GetComponent<Button>();
                    if (cell.button == null)
                    {
                        cell.button = Undo.AddComponent<Button>(child.gameObject);
                        cell.button.targetGraphic = cell.background;
                        cell.button.transition = Selectable.Transition.None;
                    }
                }
                if (cell.label == null) cell.label = child.GetComponentInChildren<TMP_Text>(true);

                EditorUtility.SetDirty(cell);
                list.Add(cell);
            }
            board.cells = list.ToArray();
            EditorUtility.SetDirty(board);
        }

        private static void WireHand(HandView hand)
        {
            if (hand == null) return;
            if (hand.cardLayer == null) hand.cardLayer = (RectTransform)hand.transform;
            EditorUtility.SetDirty(hand);
        }

        private static void WireHud(HudView hud)
        {
            if (hud == null) return;
            var top = Find(hud.transform, "TopBar");
            var side = Find(hud.transform, "SidePanel");

            // 顶栏的 6 个 Label 是按创建顺序排的，这里也按顺序取
            if (top != null)
            {
                var labels = new System.Collections.Generic.List<TMP_Text>();
                for (int i = 0; i < top.childCount; i++)
                {
                    var child = top.GetChild(i);
                    if (child.name != "Label") continue;
                    var t = child.GetComponent<TMP_Text>();
                    if (t != null) labels.Add(t);
                }
                if (labels.Count >= 6)
                {
                    hud.turnText = labels[0];
                    hud.phaseText = labels[1];
                    hud.actionText = labels[2];
                    hud.eventText = labels[3];
                    hud.poolText = labels[4];
                    hud.scoreText = labels[5];
                }
                else
                {
                    Warn(hud, $"TopBar 下只找到 {labels.Count} 个叫 Label 的文字，需要 6 个（回合/阶段/行动/生变/卡池/分数），请手动拖");
                }

                // 四方位 Chip，顺序固定为 东 北 西 南
                string[] chipNames = { "Chip_East", "Chip_North", "Chip_West", "Chip_South" };
                hud.directionChips = new Image[4];
                hud.directionTexts = new TMP_Text[4];
                for (int i = 0; i < 4; i++)
                {
                    var chip = Find(top, chipNames[i]);
                    if (chip == null)
                    {
                        Warn(hud, $"找不到 {chipNames[i]}，该方位的进度显示要手动拖");
                        continue;
                    }
                    hud.directionChips[i] = chip.GetComponent<Image>();
                    hud.directionTexts[i] = chip.GetComponentInChildren<TMP_Text>(true);
                }
            }
            else Warn(hud, "Hud/TopBar 找不到");

            // 右侧栏现在是祝福面板（原来的星语日志已移除）
            if (side != null)
            {
                var panel = side.GetComponent<BlessingPanelView>();
                if (panel == null) panel = Undo.AddComponent<BlessingPanelView>(side.gameObject);
                WireBlessingPanel(panel, side);
                hud.blessingPanel = panel;
            }
            else Warn(hud, "Hud/SidePanel 找不到");

            hud.endTurnButton = FindButton(hud.transform, "EndTurn");
            hud.helpButton = FindButton(hud.transform, "Help");
            hud.restartButton = FindButton(hud.transform, "Restart");

            var banner = Find(hud.transform, "Banner");
            if (banner != null)
            {
                hud.banner = banner.gameObject;
                hud.bannerText = FindText(banner, "Text");
            }
            else Warn(hud, "Hud/Banner 找不到");

            EditorUtility.SetDirty(hud);
        }

        /// <summary>
        /// 祝福面板：需要两个容器（方位/众星）。场景里没有就现建 ——
        /// 这是唯一破例创建物体的地方，因为原来的星语栏结构（BlessList/LogList）
        /// 跟新面板对不上，让用户手搭两个空物体反而更啰嗦。
        /// </summary>
        private static void WireBlessingPanel(BlessingPanelView panel, Transform side)
        {
            // 上下分区，两组卡片各占一段 —— 都铺满父物体的话会从同一个顶部开始排、叠在一起
            float sideH = ((RectTransform)side).rect.height;
            if (sideH < 100f) sideH = 756f;                 // 编辑器里 rect 偶尔还没算好

            const float titleH = 34f;
            const float titleTop = 6f;      // 标题距所属分区顶部
            const float afterTitle = 8f;    // 标题与第一张卡之间的呼吸

            // 卡片容器要从**标题下方**开始 —— 从分区顶部开始的话第一张卡会压住标题
            float dirRowTop = titleTop + titleH + afterTitle;
            float dirRowH = 4 * panel.cardSpacing + 6f;             // 方位祝福固定 4 张
            float starTitleTop = dirRowTop + dirRowH + 10f;
            float starRowTop = starTitleTop + titleH + afterTitle;

            panel.directionRow = EnsureChild(side, "DirectionRow");
            SetTopBand(panel.directionRow, dirRowTop, dirRowH);

            panel.starRow = EnsureChild(side, "StarRow");
            SetTopBand(panel.starRow, starRowTop, Mathf.Max(120f, sideH - starRowTop - 8f));

            // 复用原星语栏的两个文字物体当标题/提示，省得用户再拖
            panel.directionTitle = FindText(side, "BlessTitle");
            panel.starTitle = FindText(side, "LogTitle");
            panel.starEmptyHint = FindText(side, "BlessList");

            if (panel.directionTitle != null)
                SetTopBand((RectTransform)panel.directionTitle.transform, titleTop, titleH);
            if (panel.starTitle != null)
                SetTopBand((RectTransform)panel.starTitle.transform, starTitleTop, titleH);
            if (panel.starEmptyHint != null)
                SetTopBand((RectTransform)panel.starEmptyHint.transform, starRowTop + 4f, 30f);

            // 卡片在容器内从顶部半个卡高处开始（Place() 用中心 pivot）
            panel.topPadding = 40f;

            // 旧的日志文本物体留着没用，隐藏掉
            var oldLog = Find(side, "LogList");
            if (oldLog != null) oldLog.gameObject.SetActive(false);

            // prefab 字段：优先用已存在的资产，省掉"必须先跑提取"的顺序依赖
            if (panel.cardPrefab == null)
            {
                panel.cardPrefab = AssetDatabase.LoadAssetAtPath<BlessingCardView>(
                    "Assets/Prefabs/BlessingCard.prefab");
                if (panel.cardPrefab == null)
                    Warn(panel, "找不到 Assets/Prefabs/BlessingCard.prefab —— 跑一次「提取 Prefab」会生成它，之后再跑本菜单即可");
            }

            EditorUtility.SetDirty(panel);
        }

        /// <summary>把 rt 钉到父物体顶部的一段：距顶 offsetTop、高 height、左右铺满。</summary>
        private static void SetTopBand(RectTransform rt, float offsetTop, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(6f, -(offsetTop + height));
            rt.offsetMax = new Vector2(-6f, -offsetTop);
        }

        /// <summary>找不到就建一个铺满父物体的空 RectTransform。</summary>
        private static RectTransform EnsureChild(Transform parent, string name)
        {
            var t = parent.Find(name) as RectTransform;
            if (t != null) return t;
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void WireMeteorField(MeteorFieldView field)
        {
            if (field == null) return;
            var meteors = Find(field.transform, "Meteors");
            var fx = Find(field.transform, "Fx");
            var timerBg = Find(field.transform, "TimerBg");

            if (meteors != null) field.meteorLayer = (RectTransform)meteors;
            else Warn(field, "MeteorField/Meteors 找不到，meteorLayer 要手动拖");

            if (fx != null) field.fxLayer = (RectTransform)fx;

            if (timerBg != null)
            {
                var fill = Find(timerBg, "TimerFill");
                if (fill != null)
                {
                    field.timerFill = fill.GetComponent<Image>();
                    if (field.timerFill != null && field.timerFill.type != Image.Type.Filled)
                    {
                        field.timerFill.type = Image.Type.Filled;
                        field.timerFill.fillMethod = Image.FillMethod.Horizontal;
                        EditorUtility.SetDirty(field.timerFill);
                    }
                }
            }
            field.timerText = FindText(field.transform, "TimerText");
            field.skipButton = FindButton(field.transform, "SkipButton");
            if (field.floatingFont == null)
                field.floatingFont = Resources.Load<TMP_FontAsset>(UIFactory.FontResourcePath);

            EditorUtility.SetDirty(field);
        }

        private static void WireRewardPicker(RewardPickerView picker)
        {
            if (picker == null) return;
            var panel = Find(picker.transform, "Panel");
            if (panel == null)
            {
                Warn(picker, "RewardPicker/Panel 找不到");
                return;
            }

            var cardRow = Find(panel, "CardRow");
            var blessRow = Find(panel, "BlessingRow");
            if (cardRow != null) picker.cardRow = (RectTransform)cardRow;
            else Warn(picker, "Panel/CardRow 找不到");
            if (blessRow != null) picker.blessingRow = (RectTransform)blessRow;
            else Warn(picker, "Panel/BlessingRow 找不到");

            picker.summaryText = FindText(panel, "Summary");
            picker.confirmButton = FindButton(panel, "Confirm");
            EditorUtility.SetDirty(picker);
        }

        private static void WireHelpPanel(HelpPanelView help)
        {
            if (help == null) return;
            var panel = Find(help.transform, "Panel");
            if (panel == null)
            {
                Warn(help, "HelpPanel/Panel 找不到");
                return;
            }

            string[] names = { "Formation_East", "Formation_North", "Formation_West", "Formation_South" };
            Direction[] dirs = { Direction.East, Direction.North, Direction.West, Direction.South };
            help.formations = new FormationDisplay[4];

            for (int i = 0; i < 4; i++)
            {
                var t = Find(panel, names[i]);
                if (t == null)
                {
                    Warn(help, $"找不到 {names[i]}");
                    continue;
                }
                var display = t.GetComponent<FormationDisplay>();
                if (display == null) display = Undo.AddComponent<FormationDisplay>(t.gameObject);

                display.direction = dirs[i];
                display.nameText = FindText(t, "Name");
                display.mansionsText = FindText(t, "Mansions");
                display.blessingText = FindText(t, "Bless");
                var grid = Find(t, "Grid");
                if (grid != null) display.grid = (RectTransform)grid;

                EditorUtility.SetDirty(display);
                help.formations[i] = display;
            }

            help.rulesText = FindText(panel, "Rules");
            help.closeButton = FindButton(panel, "Close");
            EditorUtility.SetDirty(help);
        }

        private static void WireGameOver(GameOverView over)
        {
            if (over == null) return;
            var panel = Find(over.transform, "Panel");
            if (panel == null)
            {
                Warn(over, "GameOver/Panel 找不到");
                return;
            }
            over.titleText = FindText(panel, "Title");
            over.bodyText = FindText(panel, "Body");
            over.againButton = FindButton(panel, "Again");
            EditorUtility.SetDirty(over);
        }

        // ---------- 小工具 ----------
        /// <summary>深度优先按名字找子物体（含未激活的）。</summary>
        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child;
                var found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static TMP_Text FindText(Transform root, string name)
        {
            var t = Find(root, name);
            return t == null ? null : t.GetComponent<TMP_Text>();
        }

        private static Button FindButton(Transform root, string name)
        {
            var t = Find(root, name);
            return t == null ? null : t.GetComponent<Button>();
        }

        private static T GetOrAdd<T>(Transform root, string childName) where T : Component
        {
            var t = Find(root, childName);
            if (t == null)
            {
                Debug.LogWarning($"场景里找不到 {childName}，{typeof(T).Name} 要手动挂并拖引用。");
                return null;
            }
            var comp = t.GetComponent<T>();
            if (comp == null) comp = Undo.AddComponent<T>(t.gameObject);
            return comp;
        }

        private static void Warn(Object context, string message) =>
            Debug.LogWarning($"[自动填充] {message}", context);
    }
}
