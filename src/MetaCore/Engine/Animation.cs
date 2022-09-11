namespace MetaArt.Core {
    public abstract class AnimationBase {
        public abstract bool Next(TimeSpan deltaTime);
        public Action? End { get; init; }
    }
    public sealed class DelegateAnimation : AnimationBase {
        public static DelegateAnimation Timer(TimeSpan period, Action onTimer) {
            var totalTime = TimeSpan.Zero;
            return new DelegateAnimation(deltaTime => {
                totalTime += deltaTime;
                if(totalTime > period) {
                    totalTime -= period;
                    onTimer();
                }
                return true;
            });
        }

        readonly Func<TimeSpan, bool> next;

        public DelegateAnimation(Func<TimeSpan, bool> next) {
            this.next = next;
        }
        public override bool Next(TimeSpan deltaTime) {
            return next(deltaTime);
        }
    }
    public sealed class WaitConditionAnimation : AnimationBase {
        public static WaitConditionAnimation WaitTime(TimeSpan time, Action end) {
            var totalTime = TimeSpan.Zero;
            return new WaitConditionAnimation(deltaTime => {
                totalTime += deltaTime;
                return totalTime > time;
            }) { End = end };
        }

        readonly Func<TimeSpan, bool> condition;

        public WaitConditionAnimation(Func<TimeSpan, bool> condition) {
            this.condition = condition;
        }
        public sealed override bool Next(TimeSpan deltaTime) {
            return !condition(deltaTime);
        }
    }

    public abstract class LinearAnimationBase<T> : AnimationBase {
        public TimeSpan Duration { get; init; }
        public T From { get; init; } = default!;
        public T To { get; init; } = default!;

        TimeSpan time = TimeSpan.Zero;
        public sealed override bool Next(TimeSpan deltaTime) {
            time += deltaTime;
            float amount = MathF.Min(1, (float)(time.TotalMilliseconds / Duration.TotalMilliseconds));
            var value = LerpCore(From, To, amount);
            SetValueCore(value);
            return amount < 1;
        }

        protected abstract T LerpCore(T from, T to, float amount);
        protected abstract void SetValueCore(T value);
    }

    public sealed class LerpAnimation<T> : LinearAnimationBase<T> {
        public Func<T, T, float, T> Lerp { get; init; } = null!;
        public Action<T> SetValue { get; init; } = null!;

        protected override T LerpCore(T from, T to, float amount) => Lerp(from, to, amount);
        protected override void SetValueCore(T value) => SetValue(value);
    }

    public sealed class RotateAnimation : LinearAnimationBase<float> {
        public float Radius { get; init; } = default!;
        public Vector2 Center { get; init; } = default!;
        public Action<Vector2> SetLocation { get; init; } = null!;

        protected override float LerpCore(float from, float to, float amount) => MathF.Lerp(from, to, amount);
        protected override void SetValueCore(float value) {
            SetLocation(Center + Radius * new Vector2(MathF.Cos(value), MathF.Sin(value)));
        }
    }

    public class AnimationsController {
        public bool AllowInput => !blockInputAnimations.Any();
        readonly List<AnimationBase> animations = new();
        readonly List<AnimationBase> blockInputAnimations = new();
        public AnimationsController() {
            this.animations = animations.ToList();
        }
        public void AddAnimation(AnimationBase animation, bool blockInput) {
            animations.Add(animation);
            if(blockInput)
                blockInputAnimations.Add(animation);
        }
        public void RemoveAnimation(AnimationBase animation) {
            animations.Remove(animation);
            blockInputAnimations.Remove(animation);
        }

        public void Next(TimeSpan deltaTime) {
            foreach(var animation in animations.ToArray()) {
                bool finished = !animation.Next(deltaTime);
                if(finished) {
                    RemoveAnimation(animation);
                    animation.End?.Invoke();
                }
            }
        }

        public void AddAnimations(IEnumerable<AnimationBase> animations) {
            foreach(var item in animations) {
                AddAnimation(item, blockInput: false);
            };
        }

        public void ClearAll() { 
            animations.Clear();
            blockInputAnimations.Clear();
        }

        public void VerifyEmpty() {
            if(animations.Any())
                throw new InvalidOperationException();
        }
    }
}
