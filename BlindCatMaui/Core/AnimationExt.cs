using Microsoft.Maui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatMaui.Core
{
    public static  class AnimationExt
    {
        public static Task<bool> FadeTos(this View view, double opacity, uint length, string animName, Easing? easing = null)
        {
            return AnimateTo(view, view.Opacity, opacity, animName, (v, value) => v.Opacity = value, length, easing)
                .ContinueWith(x =>
                {
                    return !x.Result;
                });
        }

        static Task<bool> AnimateTo(this VisualElement view, double start, double end, string name,
            Action<VisualElement, double> updateAction, uint length = 250, Easing? easing = null)
        {
            if (easing == null)
                easing = Easing.Linear;

            var tcs = new TaskCompletionSource<bool>();

            var weakView = new WeakReference<VisualElement>(view);

            void UpdateProperty(double f)
            {
                if (weakView.TryGetTarget(out VisualElement? v))
                {
                    updateAction(v, f);
                }
            }

            new Animation(UpdateProperty, start, end, easing).Commit(view, name, 16, length, finished: (f, a) => tcs.SetResult(a));

            return tcs.Task;
        }
    }
}
