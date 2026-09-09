using TMPro;
using UnityEngine;

namespace StarCard.UI
{
    /// <summary>点中流星后的飘字。运行时生成。</summary>
    public class FloatingText : MonoBehaviour
    {
        private RectTransform _rt;
        private TMP_Text _text;
        private float _life;
        private float _duration = 1.1f;
        private float _riseSpeed = 52f;

        public static void Spawn(Transform parent, Vector2 pos, string content, Color color,
                                 TMP_FontAsset font, float fontSize, float duration, float riseSpeed)
        {
            var text = UIFactory.CreateText("Floating", parent, content, fontSize, color, font);
            var rt = (RectTransform)text.transform;
            UIFactory.AnchorCenter(rt, pos, new Vector2(360f, 44f));
            var ft = text.gameObject.AddComponent<FloatingText>();
            ft._rt = rt;
            ft._text = text;
            ft._duration = Mathf.Max(0.1f, duration);
            ft._riseSpeed = riseSpeed;
        }

        private void Update()
        {
            _life += Time.deltaTime;
            _rt.anchoredPosition += new Vector2(0f, _riseSpeed * Time.deltaTime);
            var c = _text.color;
            c.a = Mathf.Clamp01(1f - _life / _duration);
            _text.color = c;
            if (_life >= _duration) Destroy(gameObject);
        }
    }
}
