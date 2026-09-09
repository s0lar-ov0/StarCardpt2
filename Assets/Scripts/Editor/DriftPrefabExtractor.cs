using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StarCard.UI;

namespace StarCard.EditorTools
{
    /// <summary>
    /// 一次性工具：把场景里已有的物体提成 prefab，并把 prefab 引用填回各个 View。
    ///
    /// 为什么这几样东西必须是 prefab：**数量随局势变化**，没法预先摆在场景里 ——
    /// 棋盘上的牌 0~14 张、手牌 0~5 张、每回合收获 0~N 条、阵型小图的格子数随字符画变。
    /// 除此之外的所有界面都是场景物体、走 Inspector 拖引用。
    ///
    /// 提完之后 prefab 就是普通资产，双击进去随便改，改完全局生效。这个工具只需要跑一次。
    /// </summary>
    public static class DriftPrefabExtractor
    {
        private const string PrefabDir = "Assets/Prefabs";

        [MenuItem("漂泊的星宿/提取 Prefab（只需跑一次）")]
        public static void Extract()
        {
            var boot = Object.FindObjectOfType<DriftGameBootstrap>();
            if (boot == null)
            {
                Debug.LogError("场景里找不到 DriftGameBootstrap。");
                return;
            }

            EnsurePrefabDir();

            // 卡牌 / 阵型格从场景里提；提不到就不往下走，免得建出内部是空的条目 prefab
            var cardPrefab = ExtractCard(boot);
            var cellPrefab = ExtractFormationCell(boot);

            // 两个条目 prefab 是新建的。重跑本工具会整个覆盖它们 ——
            // 所以你要是已经手动改过条目样式，重跑前先备份，或者干脆把下面两行注释掉。
            var cardEntry = BuildRewardCardEntry(cardPrefab);
            var blessEntry = BuildRewardBlessingEntry();

            if (boot.board != null)
            {
                boot.board.cardPrefab = cardPrefab;
                EditorUtility.SetDirty(boot.board);
            }
            if (boot.hand != null)
            {
                boot.hand.cardPrefab = cardPrefab;
                EditorUtility.SetDirty(boot.hand);
            }
            if (boot.rewardPicker != null)
            {
                boot.rewardPicker.cardEntryPrefab = cardEntry;
                boot.rewardPicker.blessingEntryPrefab = blessEntry;
                EditorUtility.SetDirty(boot.rewardPicker);
            }
            if (boot.helpPanel != null && boot.helpPanel.formations != null)
            {
                foreach (var f in boot.helpPanel.formations)
                {
                    if (f == null) continue;
                    f.cellPrefab = cellPrefab;
                    EditorUtility.SetDirty(f);
                }
            }

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(boot.gameObject.scene);

            bool allOk = cardPrefab != null && cellPrefab != null && cardEntry != null && blessEntry != null;
            if (allOk)
                Debug.Log($"Prefab 提取完成，4 个都存在 {PrefabDir}/，引用已填回。场景里用来提 prefab 的临时物体已删除。\n" +
                          "以后想改牌面/条目的样子，直接双击对应 prefab 编辑即可。", boot);
            else
                Debug.LogWarning($"Prefab 部分失败：卡牌={Ok(cardPrefab)} 阵型格={Ok(cellPrefab)} " +
                                 $"牌条目={Ok(cardEntry)} 祝福条目={Ok(blessEntry)}。看上面的报错，修完再跑一次。", boot);
        }

        private static string Ok(Object o) => o != null ? "✓" : "✗";

        /// <summary>
        /// 建 Assets/Prefabs 并让 AssetDatabase 认得它。
        /// 光 Directory.CreateDirectory 是不够的 —— 目录没进资产库时 SaveAsPrefabAsset 会静默失败返回 null。
        /// </summary>
        private static void EnsurePrefabDir()
        {
            if (AssetDatabase.IsValidFolder(PrefabDir)) return;
            AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.Refresh();
        }

        /// <summary>存 prefab，失败时把原因报出来，不静默返回 null。</summary>
        private static GameObject Save(GameObject source, string fileName)
        {
            string path = $"{PrefabDir}/{fileName}.prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(source, path, out bool success);
            if (!success || saved == null)
                Debug.LogError($"[提取 Prefab] {fileName} 保存失败（路径 {path}）。" +
                               "最常见原因：Assets/Prefabs 目录不存在或没被 AssetDatabase 认到 —— 右键 Project 窗口 Refresh 后重试。");
            return saved;
        }

        /// <summary>把 Board/Cards 下的第一张 Card 提成 prefab，其余全删。</summary>
        private static DriftCardView ExtractCard(DriftGameBootstrap boot)
        {
            if (boot.board == null || boot.board.cardLayer == null)
            {
                Debug.LogWarning("[提取 Prefab] board.cardLayer 没填，跳过卡牌 prefab。先跑「自动填充引用」。");
                return null;
            }

            var layer = boot.board.cardLayer;
            Transform source = null;
            for (int i = 0; i < layer.childCount; i++)
            {
                if (layer.GetChild(i).GetComponent<DriftCardView>() != null)
                {
                    source = layer.GetChild(i);
                    break;
                }
            }
            if (source == null)
            {
                Debug.LogWarning("[提取 Prefab] Board/Cards 下没有带 DriftCardView 的物体，卡牌 prefab 要自己做。");
                return null;
            }

            WireCard(source.GetComponent<DriftCardView>());
            source.name = "StarCardView";

            var prefab = Save(source.gameObject, "StarCardView");
            if (prefab == null)
            {
                Debug.LogError("[提取 Prefab] 卡牌 prefab 没存成，场景里的牌**保留不删**，改完再跑一次。");
                return null;
            }

            // 场景里剩下的牌全删掉 —— 它们从现在起由 BoardView 运行时生成
            for (int i = layer.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(layer.GetChild(i).gameObject);

            return prefab.GetComponent<DriftCardView>();
        }

        /// <summary>补齐 DriftCardView 的子控件引用。</summary>
        private static void WireCard(DriftCardView card)
        {
            if (card == null) return;
            var t = card.transform;
            if (card.outline == null) card.outline = FindImage(t, "Outline");
            if (card.face == null) card.face = FindImage(t, "Face");
            if (card.nameText == null) card.nameText = FindText(t, "Name");
            if (card.cornerText == null) card.cornerText = FindText(t, "Corner");
            if (card.button == null)
            {
                card.button = card.GetComponent<Button>();
                if (card.button == null)
                {
                    card.button = card.gameObject.AddComponent<Button>();
                    card.button.targetGraphic = card.face;
                    card.button.transition = Selectable.Transition.None;
                }
            }
            EditorUtility.SetDirty(card);
        }

        /// <summary>把阵型小图的一个格子提成 prefab，各 Grid 下的临时格子全删。</summary>
        private static Image ExtractFormationCell(DriftGameBootstrap boot)
        {
            if (boot.helpPanel == null || boot.helpPanel.formations == null) return null;

            Image prefab = null;
            foreach (var f in boot.helpPanel.formations)
            {
                if (f == null || f.grid == null) continue;

                if (prefab == null && f.grid.childCount > 0)
                {
                    var src = f.grid.GetChild(0).gameObject;
                    src.name = "FormationCell";
                    var saved = Save(src, "FormationCell");
                    if (saved == null)
                    {
                        Debug.LogError("[提取 Prefab] 阵型小格没存成，场景里的小格**保留不删**，改完再跑一次。");
                        return null;
                    }
                    prefab = saved.GetComponent<Image>();
                }

                for (int i = f.grid.childCount - 1; i >= 0; i--)
                    Undo.DestroyObjectImmediate(f.grid.GetChild(i).gameObject);
            }

            if (prefab == null)
                Debug.LogWarning("[提取 Prefab] 阵型小图里没有现成格子，FormationCell prefab 要自己做（一个带 Image 的物体即可）。");
            return prefab;
        }

        /// <summary>取舍面板的「星宿牌条目」prefab：一张牌 + 一个留下/删去按钮。场景里没有现成的，新建。</summary>
        private static RewardCardEntry BuildRewardCardEntry(DriftCardView cardPrefab)
        {
            var root = new GameObject("RewardCardEntry", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(104f, 210f);

            var entry = root.AddComponent<RewardCardEntry>();

            if (cardPrefab != null)
            {
                var card = (DriftCardView)PrefabUtility.InstantiatePrefab(cardPrefab, root.transform);
                var crt = card.Rect;
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
                crt.anchoredPosition = new Vector2(0f, 28f);
                entry.cardView = card;
            }

            var btn = MakeButton(root.transform, "Toggle", new Vector2(104f, 42f), new Vector2(0f, -80f), "留下");
            entry.toggleButton = btn;
            entry.toggleBackground = btn.targetGraphic as Image;
            entry.toggleLabel = btn.GetComponentInChildren<TMP_Text>();

            var saved = Save(root, "RewardCardEntry");
            Object.DestroyImmediate(root);
            return saved == null ? null : saved.GetComponent<RewardCardEntry>();
        }

        /// <summary>取舍面板的「众星祝福条目」prefab。</summary>
        private static RewardBlessingEntry BuildRewardBlessingEntry()
        {
            var root = new GameObject("RewardBlessingEntry", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(320f, 120f);

            var bg = root.GetComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.07f);
            bg.sprite = Resources.Load<Sprite>("Arts/UI/Rounded");
            bg.type = Image.Type.Sliced;

            var entry = root.AddComponent<RewardBlessingEntry>();
            entry.nameText = MakeText(root.transform, "Name", "摇光·续行", 26f,
                                      new Color(1f, 0.85f, 0.95f), new Vector2(300f, 32f), new Vector2(0f, 36f));
            entry.descText = MakeText(root.transform, "Desc", "每回合行动次数 +1", 18f,
                                      new Color(1f, 1f, 1f, 0.75f), new Vector2(300f, 44f), new Vector2(0f, 4f));

            var btn = MakeButton(root.transform, "Toggle", new Vector2(140f, 34f), new Vector2(0f, -42f), "留下");
            entry.toggleButton = btn;
            entry.toggleBackground = btn.targetGraphic as Image;
            entry.toggleLabel = btn.GetComponentInChildren<TMP_Text>();

            var saved = Save(root, "RewardBlessingEntry");
            Object.DestroyImmediate(root);
            return saved == null ? null : saved.GetComponent<RewardBlessingEntry>();
        }

        // ---------- 小工具 ----------
        private static Button MakeButton(Transform parent, string name, Vector2 size, Vector2 pos, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.24f, 0.45f, 0.32f, 1f);
            img.sprite = Resources.Load<Sprite>("Arts/UI/Rounded");
            img.type = Image.Type.Sliced;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            MakeText(go.transform, "Label", label, 22f, Color.white, size, Vector2.zero);
            return btn;
        }

        private static TMP_Text MakeText(Transform parent, string name, string content, float size,
                                         Color color, Vector2 rect, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            var font = Resources.Load<TMP_FontAsset>(UIFactory.FontResourcePath);
            if (font != null) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = rect;
            rt.anchoredPosition = pos;
            return text;
        }

        private static Image FindImage(Transform root, string name)
        {
            var t = FindDeep(root, name);
            return t == null ? null : t.GetComponent<Image>();
        }

        private static TMP_Text FindText(Transform root, string name)
        {
            var t = FindDeep(root, name);
            return t == null ? null : t.GetComponent<TMP_Text>();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child;
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
