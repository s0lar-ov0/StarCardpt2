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

            EditorUtility.SetDirty(boot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);
            Debug.Log("自动填充完成。请检查 Console 里的警告 —— 那些是场景里没找到、需要你手动补的引用。\n" +
                      "prefab 字段（cardPrefab / cardEntryPrefab / blessingEntryPrefab / cellPrefab）必须手动拖，" +
                      "见 SETUP_GUIDE 第 3 节。", boot);
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

            // 手牌栏搭了滚动的话，牌要塞进 Content，不能塞在 Hand 根上 ——
            // 否则会被 Viewport 的裁剪排除在外、也不跟着滚动。
            var scroll = hand.GetComponent<ScrollRect>();
            if (scroll != null)
            {
                hand.scrollRect = scroll;
                if (scroll.content != null)
                {
                    hand.content = scroll.content;
                    hand.cardLayer = scroll.content;
                }
                else
                {
                    Warn(hand, "Hand 上有 ScrollRect 但 content 没填 —— 先跑「搭建手牌栏滚动条」");
                }
            }
            else if (hand.cardLayer == null)
            {
                hand.cardLayer = (RectTransform)hand.transform;
            }

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

            if (side != null)
            {
                hud.blessingText = FindText(side, "BlessList");
                hud.logText = FindText(side, "LogList");
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
