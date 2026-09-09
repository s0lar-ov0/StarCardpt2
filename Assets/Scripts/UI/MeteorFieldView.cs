using TMPro;
using UnityEngine;
using UnityEngine.UI;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>
    /// 流星定位阶段。挂在场景里的 MeteorField 上。
    ///
    /// 背景、计时条、提示文字、结束按钮都是**场景物体**（拖引用）；
    /// 流星和飘字是**运行时生成**的（数量不定），分别挂到 meteorLayer / fxLayer 下。
    /// </summary>
    public class MeteorFieldView : MonoBehaviour
    {
        [Header("显示 / 隐藏")]
        [Tooltip("画外坐标面板。留空则退化成 SetActive 开关")]
        public OffscreenPanel panel;

        [Header("层（场景物体，运行时往里塞流星/飘字）")]
        [Tooltip("流星挂在这个物体下。一般拖 MeteorField/Meteors")]
        public RectTransform meteorLayer;

        [Tooltip("收益飘字挂在这个物体下。一般拖 MeteorField/Fx")]
        public RectTransform fxLayer;

        [Header("计时条")]
        [Tooltip("Image Type 要设成 Filled / Horizontal")]
        public Image timerFill;

        [Tooltip("「定位剩余 8.3 秒 | 已获…」")]
        public TMP_Text timerText;

        [Header("按钮")]
        [Tooltip("提前结束定位")]
        public Button skipButton;

        [Header("流星外观")]
        [Tooltip("大 / 中 / 小三种流星的半径")]
        public float bigRadius = 34f;
        public float mediumRadius = 21f;
        public float smallRadius = 12f;

        [Tooltip("拖尾长度 = 半径 × 本值")]
        public float tailLengthRatio = 7f;

        [Header("飘字")]
        [Tooltip("飘字用的字体。留空则用 TMP 默认字体（可能没有中文字形）")]
        public TMP_FontAsset floatingFont;
        public float floatingFontSize = 28f;
        public float floatingDuration = 1.1f;
        public float floatingRiseSpeed = 52f;

        [Header("出生范围")]
        [Tooltip("流星在画面外多远处出生")]
        public float spawnMargin = 100f;

        [Tooltip("飞向画面中心区域的比例（0.6 = 瞄准中间 60% 的范围）")]
        [Range(0.1f, 1f)]
        public float targetSpread = 0.6f;

        private DriftGameManager _game;
        private readonly System.Collections.Generic.List<MeteorView> _meteors = new();
        private float _bigTimer, _mediumTimer, _smallTimer;

        public void Init(DriftGameManager game)
        {
            _game = game;

            if (meteorLayer == null)
            {
                Debug.LogError("[MeteorFieldView] meteorLayer 没拖！流星不会出现。（一般拖 MeteorField/Meteors）", this);
                meteorLayer = (RectTransform)transform;
            }
            if (fxLayer == null) fxLayer = meteorLayer;

            if (skipButton != null) skipButton.onClick.AddListener(() => _game.EndMeteorPhase());

            if (panel == null) panel = GetComponent<OffscreenPanel>();
            SetVisible(false);
            _game.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (_game != null) _game.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(DriftPhase phase)
        {
            bool on = phase == DriftPhase.Meteor;
            SetVisible(on);
            ClearMeteors();
            if (on)
            {
                _bigTimer = 0.6f;
                _mediumTimer = 0.2f;
                _smallTimer = 1.4f;
            }
        }

        private void SetVisible(bool on)
        {
            if (panel != null) panel.SetShown(on);
            else gameObject.SetActive(on);
        }

        private void ClearMeteors()
        {
            for (int i = 0; i < _meteors.Count; i++)
                if (_meteors[i] != null) Destroy(_meteors[i].gameObject);
            _meteors.Clear();
        }

        private void Update()
        {
            if (_game == null || _game.Phase != DriftPhase.Meteor) return;

            float dt = Time.deltaTime;
            var cfg = _game.Config;

            if (timerFill != null)
                timerFill.fillAmount = _game.MeteorDuration <= 0f ? 0f : _game.MeteorTimeLeft / _game.MeteorDuration;
            if (timerText != null)
                timerText.text = $"定位剩余 {_game.MeteorTimeLeft:0.0} 秒　|　已获：行动 +{_game.PendingBonusActions}　" +
                                 $"星宿牌 {_game.PendingCards.Count}　祝福 {_game.PendingBlessings.Count}";

            _bigTimer -= dt;
            if (_bigTimer <= 0f)
            {
                _bigTimer = cfg.bigSpawnInterval * Random.Range(0.75f, 1.25f);
                Spawn(new MeteorKind(MeteorSize.Big), bigRadius, cfg.bigSpeed);
            }

            _mediumTimer -= dt;
            if (_mediumTimer <= 0f)
            {
                _mediumTimer = cfg.mediumSpawnInterval * Random.Range(0.7f, 1.3f);
                var elements = _game.AvailablePoolElements();
                if (elements.Count > 0)
                    Spawn(new MeteorKind(MeteorSize.Medium, elements[Random.Range(0, elements.Count)]),
                          mediumRadius, cfg.mediumSpeed);
            }

            _smallTimer -= dt;
            if (_smallTimer <= 0f)
            {
                _smallTimer = cfg.smallSpawnInterval * Random.Range(0.7f, 1.3f);
                if (_game.CanSpawnPinkMeteor)
                    Spawn(new MeteorKind(MeteorSize.Small), smallRadius, cfg.smallSpeed);
            }

            // 移动 + 出界回收
            var rect = ((RectTransform)transform).rect;
            float halfW = rect.width * 0.5f + spawnMargin + 40f;
            float halfH = rect.height * 0.5f + spawnMargin + 40f;
            for (int i = _meteors.Count - 1; i >= 0; i--)
            {
                var m = _meteors[i];
                if (m == null)
                {
                    _meteors.RemoveAt(i);
                    continue;
                }
                m.Tick(dt);
                var p = m.Rect.anchoredPosition;
                if (Mathf.Abs(p.x) > halfW || Mathf.Abs(p.y) > halfH)
                {
                    Destroy(m.gameObject);
                    _meteors.RemoveAt(i);
                }
            }
        }

        private void Spawn(MeteorKind kind, float radius, float speed)
        {
            var rect = ((RectTransform)transform).rect;
            float halfW = rect.width * 0.5f;
            float halfH = rect.height * 0.5f;
            if (halfW < 100f) { halfW = 960f; halfH = 540f; }

            // 从画面外某条边进场，朝画面内某点飞
            Vector2 start;
            switch (Random.Range(0, 4))
            {
                case 0: start = new Vector2(-halfW - spawnMargin, Random.Range(-halfH, halfH)); break;
                case 1: start = new Vector2(halfW + spawnMargin, Random.Range(-halfH, halfH)); break;
                case 2: start = new Vector2(Random.Range(-halfW, halfW), halfH + spawnMargin); break;
                default: start = new Vector2(Random.Range(-halfW, halfW), -halfH - spawnMargin); break;
            }
            var target = new Vector2(Random.Range(-halfW * targetSpread, halfW * targetSpread),
                                     Random.Range(-halfH * targetSpread, halfH * targetSpread));

            _meteors.Add(MeteorView.Create(meteorLayer, kind, radius, start,
                                           (target - start).normalized * speed, tailLengthRatio, OnMeteorClicked));
        }

        private void OnMeteorClicked(MeteorView meteor)
        {
            if (_game.Phase != DriftPhase.Meteor) return;

            string reward = _game.CollectMeteor(meteor.Kind);
            if (!string.IsNullOrEmpty(reward))
                FloatingText.Spawn(fxLayer, meteor.Rect.anchoredPosition, reward, MeteorPalette.ColorOf(meteor.Kind),
                                   floatingFont, floatingFontSize, floatingDuration, floatingRiseSpeed);

            _meteors.Remove(meteor);
            Destroy(meteor.gameObject);
        }
    }
}
