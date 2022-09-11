
namespace MetaArt.Core {
    public record struct Rect(Vector2 Location, Vector2 Size) {
        public static readonly Rect Empty = new Rect(0, 0, 0, 0);
        public static Rect FromCenter(Vector2 center, Vector2 size) => new Rect(center - size / 2, size);
        public static Rect FromLTRB(float left, float top, float right, float bottom) => new Rect(left, top, right - left, bottom - top);

        public float Left => Location.X;
        public float Right => Left + Width;
        public float Top => Location.Y;
        public float Bottom => Top + Height;
        public float Width => Size.X;
        public float Height => Size.Y;
        public Vector2 Mid => Location + Size / 2;
        public float MidX => Location.X + Size.X / 2;
        public float MidY => Location.Y + Size.Y / 2;

        public Vector2 TopRight => Location + new Vector2(Width, 0);
        public Vector2 BottomRight => Location + Size;
        public Vector2 BottomLeft => Location + new Vector2(0, Height);

        public Rect(float left, float top, float width, float height)
            : this(new Vector2(left, top), new Vector2(width, height)) { }

        public Rect Offset(Vector2 offset)
            => new Rect(Location + offset, Size);

        public Rect SetLocation(Vector2 location)
            => new Rect(location, Size);

        public Rect SetSize(Vector2 size)
            => new Rect(Location, size);

        public bool Contains(Vector2 point) =>
            MathF.LessOrEqual(Location.X, point.X) &&
            MathF.LessOrEqual(Location.Y, point.Y) &&
            MathF.LessOrEqual(point.X, Location.X + Size.X) &&
            MathF.LessOrEqual(point.Y, Location.Y + Size.Y);

        public bool Intersects(Rect rect) => 
            Contains(rect.Location) || Contains(rect.Location + rect.Size) || 
            rect.Contains(Location) || rect.Contains(Location + Size);

        public bool Contains(Rect rect) => 
            Contains(rect.Location) && Contains(rect.Location + rect.Size);

        public Rect ContainingRect(Rect other) {
            return FromLTRB(
                MathF.Min(Left, other.Left),
                MathF.Min(Top, other.Top),
                MathF.Max(Right, other.Right),
                MathF.Max(Bottom, other.Bottom)
            );
        }
    }
}

