using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StarCard.Core;
using StarCard.Data;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 图鉴里的一个方位分栏：模板小图 + 七宿列表 + 归位祝福。挂在场景里的 Formation_East 等物体上。
    ///
    /// 小图的**格子**是运行时按字符画生成的 —— 阵型形状会改（改 FormationDatabase 的字符画），
    /// 格子数跟着变，所以这部分没法预摆。其余文字都是场景物体。
    /// </summary>
    public class FormationDisplay : MonoBehaviour
    {
        [Header("这一栏显示哪个方位")]
        public Direction direction = Direction.East;

        [Header("子控件（场景物体）")]
        [Tooltip("青龙（东方）")] public TMP_Text nameText;
        [Tooltip("角 亢 氐 房 心 尾 箕")] public TMP_Text mansionsText;
        [Tooltip("归位祝福：…")] public TMP_Text blessingText;

        [Tooltip("模板小格生成到这个物体下。一般拖本物体下的 Grid")]
        public RectTransform grid;

        [Header("模板小图")]
        [Tooltip("一个小格的 prefab（一个带 Image 的物体即可）")]
        public Image cellPrefab;

        [Tooltip("小格边长")] public float cellSize = 30f;
        [Tooltip("小格间隙")] public float cellGap = 3f;

        [Tooltip("阵型格的颜色取方位色，这里是它的透明度")]
        [Range(0f, 1f)] public float onAlpha = 0.9f;

        [Tooltip("空格的颜色")]
        public Color offColor = new Color(1f, 1f, 1f, 0.07f);

        public void Build()
        {
            var color = MeteorPalette.ColorOfDirection(direction);

            if (nameText != null)
                nameText.text = $"{DirectionBlessing.GetBeastName(direction)}（{StarCardDatabase.GetChineseDirection(direction)}方）";

            if (mansionsText != null)
            {
                var mansions = StarCardDatabase.GetMansionsOfDirection(direction);
                var sb = new StringBuilder();
                for (int i = 0; i < mansions.Count; i++)
                {
                    sb.Append(StarCardDatabase.GetChineseName(mansions[i]));
                    if (i < mansions.Count - 1) sb.Append(' ');
                }
                mansionsText.text = sb.ToString();
            }

            if (blessingText != null)
                blessingText.text = $"归位祝福：{DirectionBlessing.GetName(direction)}\n{DirectionBlessing.GetDesc(direction)}";

            BuildGrid(color);
        }

        private void BuildGrid(Color color)
        {
            if (grid == null || cellPrefab == null) return;

            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);

            var rows = FormationDatabase.GetShapeRows(direction);
            int rowCount = rows.Length;
            int colCount = 0;
            for (int r = 0; r < rowCount; r++) colCount = Mathf.Max(colCount, rows[r].Length);

            float step = cellSize + cellGap;
            for (int r = 0; r < rowCount; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    bool on = rows[r][c] == 'X';
                    var cell = Instantiate(cellPrefab, grid);
                    cell.gameObject.SetActive(true);
                    cell.name = $"c{r}_{c}";
                    cell.color = on ? new Color(color.r, color.g, color.b, onAlpha) : offColor;
                    cell.raycastTarget = false;

                    var rt = (RectTransform)cell.transform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(cellSize, cellSize);
                    rt.anchoredPosition = new Vector2((c - (colCount - 1) * 0.5f) * step,
                                                      -(r - (rowCount - 1) * 0.5f) * step);
                }
            }
        }
    }
}
