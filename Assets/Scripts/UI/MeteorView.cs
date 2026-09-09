using UnityEngine;
using UnityEngine.UI;
using StarCard.Drift;

namespace StarCard.UI
{
    /// <summary>一颗在星海里飞的流星。运行时生成，不是场景物体。</summary>
    public class MeteorView : MonoBehaviour
    {
        public MeteorKind Kind;
        public Vector2 Velocity;

        private RectTransform _rt;
        private System.Action<MeteorView> _onClick;

        public RectTransform Rect => _rt;

        public static MeteorView Create(Transform parent, MeteorKind kind, float radius, Vector2 pos, Vector2 velocity,
                                        float tailLengthRatio, System.Action<MeteorView> onClick)
        {
            var color = MeteorPalette.ColorOf(kind);
            var img = UIFactory.CreateImage("Meteor", parent, color, UIFactory.Circle);
            img.type = Image.Type.Simple;
            var rt = (RectTransform)img.transform;
            UIFactory.AnchorCenter(rt, pos, new Vector2(radius * 2f, radius * 2f));

            // 拖尾
            var tail = UIFactory.CreateImage("Tail", rt, new Color(color.r, color.g, color.b, 0.25f), UIFactory.Rounded);
            tail.raycastTarget = false;
            var trt = (RectTransform)tail.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.sizeDelta = new Vector2(radius * tailLengthRatio, radius * 0.9f);
            trt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-velocity.y, -velocity.x) * Mathf.Rad2Deg);
            trt.anchoredPosition = Vector2.zero;
            tail.transform.SetAsFirstSibling();

            var view = img.gameObject.AddComponent<MeteorView>();
            view._rt = rt;
            view.Kind = kind;
            view.Velocity = velocity;
            view._onClick = onClick;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => view._onClick?.Invoke(view));
            return view;
        }

        public void Tick(float dt) => _rt.anchoredPosition += Velocity * dt;
    }
}
