
using System.Globalization;

namespace MetaArt.Core;
public class Scene {
    public readonly float width, height;
    readonly Func<bool> getAllowInput;

    public Rect Bounds => new Rect(0, 0, width, height);

    InputState inputState;
    readonly NoInputState noInputState;


    public Scene(float width, float height, Func<bool> getAllowInput) {
        this.width = width;
        this.height = height;
        this.getAllowInput = getAllowInput;
        this.inputState = this.noInputState = new NoInputState(point => {
            var element = HitTest(point);
            if(element != null) {
                var getPressState = element.GetPressState;
                if(getPressState == null)
                    return inputState!;
                return getPressState(point) ?? noInputState!;
            }
            return noInputState!;
        });
    }

    public Element? HitTest(Vector2 point) {
        for(int i = elements.Count - 1; i >= 0; i--) {
            var element = elements[i];
            if(element.IsVisible && element.HitTestVisible && element.Rect.Contains(point)) {
                return element;
            }
        }
        return null;
    }

    List<Element> elements = new();

    public IEnumerable<Element> VisibleElements => elements.Where(x => x.IsVisible);

    public void AddElement(Element element) {
        elements.Add(element);
    }
    public void AddElementBehind(Element element) {
        elements.Insert(0, element);
    }
    public bool RemoveElement(Element element) {
        return elements.Remove(element);
    }
    public void ClearElements() => elements.Clear();
    public void SendToBack(Element element) {
        if(!RemoveElement(element)) throw new InvalidOperationException();
        AddElementBehind(element);
    }

    public void Press(Vector2 point) {
        if(getAllowInput())
            inputState = inputState.Press(point) ?? noInputState;
    }
    public void Drag(Vector2 point) {
        //if(getAllowInput())
            inputState = inputState.Drag(point) ?? noInputState;
    }
    public void Release(Vector2 point) {
        //if(getAllowInput())
            inputState = inputState.Release(point) ?? noInputState;
    }
}
public abstract class Element {
    public static Func<Vector2, InputState?> GetAnchorAndSnapDragStateFactory(
       Element element,
       Func<float> getAnchorDistance,
       Func<(float snapDistance, Vector2 snapPoint)?> getSnapInfo,
       Action<Element> onElementSnap,
       Action? onMove = null,
       Func<Rect, Vector2>? coerceRectLocation = null,
       Func<bool, bool>? onRelease = null,
       Action? onClick = null
     ) {
        bool allowDrag = true;
        bool anchored = true;
        Rect startRect = default;
        bool IsAnchored(Vector2 delta) {
            var anchorDistance = getAnchorDistance();
            return anchored && delta.LengthSquared() < anchorDistance * anchorDistance;
        }
        return DragInputState.GetDragHandler( 
           onDrag: delta => {
                Rect newRect = startRect;
                if (!IsAnchored(delta)) {
                    newRect = newRect.Offset(delta);
                    anchored = false;
                }
                var snapInfo = getSnapInfo();
                if (snapInfo != null && (newRect.Location - snapInfo.Value.snapPoint).LengthSquared() <= snapInfo.Value.snapDistance * snapInfo.Value.snapDistance) {
                    newRect = new Rect(snapInfo.Value.snapPoint, newRect.Size);
                    allowDrag = false;
                    onElementSnap(element);
                }
                if(coerceRectLocation != null)
                    newRect = new Rect(coerceRectLocation(newRect), newRect.Size);
                element.Rect = newRect;
                onMove?.Invoke();
                return allowDrag;
            },
            onRelease: delta => {
                if(IsAnchored(delta)) { 
                    onClick?.Invoke();
                }
                anchored = onRelease?.Invoke(anchored) ?? false;
            }, 
            canDrag: () => {
                startRect = element.Rect;
                return allowDrag;
        });
    }
    public static Action CreateSetOffsetAction(Element parent, Element[] children) {
        var pairs = children.Select(element => (element, parentOffset: element.Rect.Location - parent.Rect.Location)).ToArray();
        return () => {
            foreach(var (element, parentOffset) in pairs) {
                element.Rect = new Rect(parent.Rect.Location + parentOffset, element.Rect.Size);
            }
        };
    }

    public Rect Rect { get; set; }
    public bool HitTestVisible { get; set; }
    public bool IsVisible { get; set; } = true;
    public Func<Vector2, InputState?>? GetPressState { get; set; }
}

public abstract class InputState {
    public abstract InputState? Press(Vector2 point);
    public abstract InputState? Release(Vector2 point);
    public abstract InputState? Drag(Vector2 point);
}

public class InputHandlerElement : Element {
    public float Opacity { get; set; } = 0;
    public InputHandlerElement() {
        HitTestVisible = true;
    }
}

public class NoInputState : InputState {
    readonly Func<Vector2, InputState> getPressState;

    public NoInputState(Func<Vector2, InputState> getPressState) {
        this.getPressState = getPressState;
    }

    public override InputState? Drag(Vector2 point) {
        return this;
    }

    public override InputState? Press(Vector2 point) {
        return getPressState(point);
    }

    public override InputState? Release(Vector2 point) {
        return this;
    }
}

public class DragInputState : InputState {
    public static Func<Vector2, InputState?> GetDragHandler(Func<Vector2, bool> onDrag, Action<Vector2>? onRelease = null, Func<bool>? canDrag = null) {
        return startPoint => {
            if(canDrag?.Invoke() == false)
                return null;
            return new DragInputState(
                startPoint,
                onDrag,
                onRelease ?? (_ => { })
            );
        };
    }

    readonly Vector2 startPoint;
    readonly Func<Vector2, bool> onDrag;
    readonly Action<Vector2> onRelease;

    DragInputState(Vector2 startPoint, Func<Vector2, bool> onDrag, Action<Vector2> onRelease) {
        this.startPoint = startPoint;
        this.onDrag = onDrag;
        this.onRelease = onRelease;
    }

    public override InputState? Drag(Vector2 point) {
        if(onDrag(point - startPoint))
            return this;
        return null;
    }

    public override InputState? Press(Vector2 point) {
        return null; // throw new InvalidOperationException();
    }

    public override InputState? Release(Vector2 point) {
        onRelease(point - startPoint);
        return null;
    }
}

public class HoverInputState : InputState {
    public static Func<Vector2, InputState?> GetHoverHandler(Scene scene, Element element, Action<Element> onHover, Action onRelease) {
        return startPoint => {
            return new HoverInputState(
                scene,
                element,
                onHover,
                onRelease
            );
        };
    }

    readonly Scene scene;
    readonly Action<Element> onHover;
    readonly Action onRelease;

    HoverInputState(Scene scene, Element element, Action<Element> onHover, Action onRelease) {
        this.scene = scene;
        this.onHover = onHover;
        this.onRelease = onRelease;
        onHover(element);
    }

    public override InputState? Drag(Vector2 point) {
        var element = scene.HitTest(point);
        if(element != null)
            onHover(element);
        return this;
    }

    public override InputState? Press(Vector2 point) {
        throw new InvalidOperationException();
    }

    public override InputState? Release(Vector2 point) {
        onRelease();
        return null;
    }
}

public class TapInputState : InputState {
    public static Func<Vector2, InputState?> GetClickHandler(Element element, Action click, Action<bool> setState) {
        return startPoint => {
            return new TapInputState(
                element,
                click,
                setState: setState
            );
        };
    }
    public static Func<Vector2, InputState?> GetPressReleaseHandler(
        Element element,
        Action onPress,
        Action onRelease
     ) {
        return startPoint => {
            if(!element.HitTestVisible) {
                Debug.Fail("handler on hit test invisible element");
                return null;
            }
            onPress();
            return new TapInputState(
                element,
                () => { },
                setState: isPressed => {
                    if(!isPressed) onRelease?.Invoke();
                }            );
        };
    }

    readonly Element element;
    readonly Action onTap;
    readonly Action<bool> setState;

    TapInputState(Element element, Action onTap, Action<bool> setState) {
        this.element = element;
        this.onTap = onTap;
        this.setState = setState;
        setState(true);
    }

    public override InputState? Drag(Vector2 point) {
        if(!element.Rect.Contains(point)) {
            setState(false);
            return null;
        }
        return this;
    }

    public override InputState? Press(Vector2 point) {
        throw new InvalidOperationException();
    }

    public override InputState? Release(Vector2 point) {
        setState(false);
        if(element.IsVisible)
            onTap();
        return null;
    }
}


public sealed class Engine {
    public readonly Scene scene;
    public readonly AnimationsController animations = new();
    public Engine(float width, float height) {
        scene = new Scene(width, height, () => animations.AllowInput);
    }
    public void NextFrame(TimeSpan delta) {
        animations.Next(delta);
    }

    public void StartFade(Action end, TimeSpan duration) {
        StartFadeCore(0, 255, end, duration);
    }
    void StartFadeCore(float from, float to, Action end, TimeSpan duration) {
        var element = new FadeOutElement() { Rect = new Rect(0, 0, scene.width, scene.height), Opacity = from };
        var animation = new LerpAnimation<float> {
            Duration = duration,
            From = from,
            To = to,
            SetValue = value => element.Opacity = value,
            Lerp = MathF.Lerp,
            End = () => {
                scene.RemoveElement(element);
                end();
            }
        };
        animations.AddAnimation(animation, blockInput: false);
        scene.AddElement(element);
    }
    Action? clearScene;
    public void SetScene(Func<SceneContext> start, TimeSpan duration) {
        clearScene?.Invoke();
        scene.ClearElements();
#if DEBUG
        animations.VerifyEmpty();//TODO call log function instead
#endif
        animations.ClearAll();
        clearScene = start().clear;
        StartFadeCore(255, 0, () => { }, duration);
    }
}
public class FadeOutElement : Element {
    public float Opacity { get; set; }
    public FadeOutElement() {
        HitTestVisible = true;
    }
}

public record struct Storage(Func<string, string?> getValue, Action<string, string?> setValue);

public static class StorageExtensions {
    public static Storage CreateInMemoryStorage() {
        var dictionary = new Dictionary<string, string?>();
        return new Storage(
            name => {
                if(dictionary.TryGetValue(name, out var value))
                    return value;
                return null;
            },
            (name, value) => dictionary[name] = value
        );
    }

    public static int GetInt(this Storage storage, string name) => int.Parse(storage.getValue(name) ?? "0", CultureInfo.InvariantCulture);
    public static void SetInt(this Storage storage, string name, int value) => storage.setValue(name, value.ToString(CultureInfo.InvariantCulture));
}

public record struct SceneContext(Action? clear);

