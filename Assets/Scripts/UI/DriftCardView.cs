using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using StarCard.Data;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 一张星宿牌。棋盘牌、手牌、取舍面板里的牌用的都是同一个 prefab。
    ///
    /// **这是 prefab，不是场景物体** —— 牌的数量随局势变化，没法预先摆在场景里。
    /// 场景里 Board/Cards 下面那 6 张是我上版代码生成后你粘出来的，可以留一张做 prefab，其余删掉。
    /// 具体做法见 SETUP_GUIDE 第 3 节。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DriftCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("子控件（拖 prefab 内部的物体）")]
        [Tooltip("卡面主图。会按方位染色")]
        public Image face;

        [Tooltip("描边框（选中金边 / 连结绿边）。盖在 face 底下、比卡牌大一圈")]
        public Image outline;

        [Tooltip("宿名，例如 角")]
        public TMP_Text nameText;

        [Tooltip("左下角的 东-木")]
        public TMP_Text cornerText;

        [Tooltip("整张牌的点击按钮。留空则自动用自己身上的 Button")]
        public Button button;

        [Header("描边配色")]
        public Color selectedColor = new Color(1f, 0.95f, 0.4f, 1f);
        public Color linkedColor = new Color(0.45f, 1f, 0.65f, 0.95f);

        [Header("手感")]
        [Tooltip("鼠标悬停时的放大倍数")]
        public float hoverScale = 1.05f;

        [Tooltip("选中时的放大倍数")]
        public float selectedScale = 1.08f;

        [Tooltip("位移插值速度。越大越快，0 = 瞬移")]
        public float moveSpeed = 12f;

        [Tooltip("卡面染色时混入的白色比例，0 = 纯方位色，1 = 纯白")]
        [Range(0f, 1f)]
        public float faceTint = 0.35f;

        private RectTransform _rt;
        private Core.StarCard _card;
        private Action<DriftCardView> _onClick;

        private Vector2 _target;
        private bool _moving;
        private bool _hovering;
        private bool _selected;
        private bool _linked;

        public Core.StarCard Card => _card;
        public RectTransform Rect => _rt != null ? _rt : (_rt = (RectTransform)transform);

        /// <summary>棋盘牌记录自己所在格；手牌为 Invalid。</summary>
        public GridPos Pos { get; set; } = GridPos.Invalid;
        public int HandIndex { get; set; } = -1;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => _onClick?.Invoke(this));
        }

        public void Bind(Core.StarCard card, Action<DriftCardView> onClick)
        {
            _card = card;
            _onClick = onClick;
            if (nameText != null) nameText.text = StarCardDatabase.GetChineseName(card.Name);
            if (cornerText != null)
                cornerText.text = $"{StarCardDatabase.GetChineseDirection(card.Direction)}-{StarCardDatabase.GetChineseElement(card.Element)}";
            if (face != null)
                face.color = Color.Lerp(MeteorPalette.ColorOfDirection(card.Direction), Color.white, faceTint);
        }

        public void SetClickHandler(Action<DriftCardView> onClick) => _onClick = onClick;

        public void SetLinked(bool linked)
        {
            _linked = linked;
            RefreshOutline();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            RefreshOutline();
            Rect.localScale = Vector3.one * (selected ? selectedScale : 1f);
        }

        private void RefreshOutline()
        {
            if (outline == null) return;
            if (_selected) outline.color = selectedColor;
            else if (_linked) outline.color = linkedColor;
            else outline.color = new Color(1f, 1f, 1f, 0f);
        }

        public void SetInteractable(bool on)
        {
            if (button != null) button.interactable = on;
        }

        public void SetScale(float s) => Rect.localScale = Vector3.one * s;

        /// <summary>移动到目标位置。animate = false 时瞬移。</summary>
        public void MoveTo(Vector2 anchoredPos, bool animate)
        {
            _target = anchoredPos;
            if (animate && moveSpeed > 0f)
            {
                _moving = true;
            }
            else
            {
                _moving = false;
                Rect.anchoredPosition = anchoredPos;
            }
        }

        private void Update()
        {
            if (!_moving) return;
            var cur = Rect.anchoredPosition;
            var next = Vector2.Lerp(cur, _target, 1f - Mathf.Exp(-moveSpeed * Time.deltaTime));
            if ((next - _target).sqrMagnitude < 0.25f)
            {
                next = _target;
                _moving = false;
            }
            Rect.anchoredPosition = next;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovering = true;
            if (!_selected) Rect.localScale = Vector3.one * hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            if (!_selected) Rect.localScale = Vector3.one;
        }

        public bool IsHovering => _hovering;
    }
}
