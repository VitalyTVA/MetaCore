
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
            if(element != null)
                return (element.GetPressState?.Invoke(point, noInputState!) ?? inputState)!;
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
            inputState = inputState.Press(point);
    }
    public void Drag(Vector2 point) {
        //if(getAllowInput())
            inputState = inputState.Drag(point);
    }
    public void Release(Vector2 point) {
        //if(getAllowInput())
            inputState = inputState.Release(point);
    }
}
public abstract class Element {
    public static Func<Vector2, NoInputState, InputState> GetAnchorAndSnapDragStateFactory(
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
        return (startPoint, releaseState) => {
            if (!allowDrag)
                return releaseState;

            var startRect = element.Rect;

            bool IsAnchored(Vector2 delta) {
                var anchorDistance = getAnchorDistance();
                return anchored && delta.LengthSquared() < anchorDistance * anchorDistance;
            }

            return new DragInputState(startPoint, 
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
                releaseState);
        };
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
    public Func<Vector2, NoInputState, InputState>? GetPressState { get; set; }
}

public abstract class InputState {
    public abstract InputState Press(Vector2 point);
    public abstract InputState Release(Vector2 point);
    public abstract InputState Drag(Vector2 point);
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

    public override InputState Drag(Vector2 point) {
        return this;
    }

    public override InputState Press(Vector2 point) {
        return getPressState(point);
    }

    public override InputState Release(Vector2 point) {
        return this;
    }
}

public class DragInputState : InputState {
    readonly Vector2 startPoint;
    readonly Func<Vector2, bool> onDrag;
    readonly Action<Vector2> onRelease;
    readonly InputState releaseState;

    public DragInputState(Vector2 startPoint, Func<Vector2, bool> onDrag, Action<Vector2> onRelease, InputState releaseState) {
        this.startPoint = startPoint;
        this.onDrag = onDrag;
        this.onRelease = onRelease;
        this.releaseState = releaseState;
    }

    public override InputState Drag(Vector2 point) {
        if(onDrag(point - startPoint))
            return this;
        return releaseState;
    }

    public override InputState Press(Vector2 point) {
        return releaseState; // throw new InvalidOperationException();
    }

    public override InputState Release(Vector2 point) {
        onRelease(point - startPoint);
        return releaseState;
    }
}

public class HoverInputState : InputState {
    readonly Scene scene;
    readonly Action<Element> onHover;
    readonly Action onRelease;
    readonly InputState releaseState;

    public HoverInputState(Scene scene, Element element, Action<Element> onHover, Action onRelease, InputState releaseState) {
        this.scene = scene;
        this.onHover = onHover;
        this.onRelease = onRelease;
        this.releaseState = releaseState;
        onHover(element);
    }

    public override InputState Drag(Vector2 point) {
        var element = scene.HitTest(point);
        if(element != null)
            onHover(element);
        return this;
    }

    public override InputState Press(Vector2 point) {
        throw new InvalidOperationException();
    }

    public override InputState Release(Vector2 point) {
        onRelease();
        return releaseState;
    }
}

public class TapInputState : InputState {
    public static Func<Vector2, NoInputState, InputState> GetClickHandler(Element element, Action click, Action<bool> setState) {
        return (startPoint, releaseState) => {
            return new TapInputState(
                element,
                click,
                setState: setState,
                releaseState
            );
        };
    }
    public static Func<Vector2, NoInputState, InputState> GetPressReleaseHandler(
        Element element,
        Action onPress,
        Action onRelease
     ) {
        return (startPoint, releaseState) => {
            if(!element.HitTestVisible) {
                Debug.Fail("handler on hit test invisible element");
                return releaseState;
            }
            onPress();
            return new TapInputState(
                element,
                () => { },
                setState: isPressed => {
                    if(!isPressed) onRelease?.Invoke();
                },
                releaseState
            );
        };
    }

    readonly Element element;
    readonly Action onTap;
    readonly Action<bool> setState;
    readonly InputState releaseState;

    TapInputState(Element element, Action onTap, Action<bool> setState, InputState releaseState) {
        this.element = element;
        this.onTap = onTap;
        this.setState = setState;
        this.releaseState = releaseState;
        setState(true);
    }

    public override InputState Drag(Vector2 point) {
        if(!element.Rect.Contains(point)) {
            setState(false);
            return releaseState;
        }
        return this;
    }

    public override InputState Press(Vector2 point) {
        throw new InvalidOperationException();
    }

    public override InputState Release(Vector2 point) {
        setState(false);
        if(element.IsVisible)
            onTap();
        return releaseState;
    }
}

