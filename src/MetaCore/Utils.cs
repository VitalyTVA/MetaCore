namespace MetaArt.Core {
    public static class Utils {
        public static Stream GetStream(Type typeFromAssembly, string filename) {
            return typeFromAssembly.Assembly.GetManifestResourceStream(typeFromAssembly.Assembly.GetName().Name + ".Assets." + filename);
        }
        public static TR Trasform<TS, TR>(this TS value, Func<TS, TR> selector) => selector(value);
        public static IEnumerable<T> Yield<T>(this T value) { 
            yield return value;
        }
        public static IEnumerable<T> YieldIfNotNull<T>(this T? value) {
            if(value is not null)
                yield return value;
        }
        public static IEnumerable<T> Yield<T>(this (T, T) value) {
            yield return value.Item1;
            yield return value.Item2;
        }
        public static IEnumerable<T> Yield<T>(this (T, T, T) value) {
            yield return value.Item1;
            yield return value.Item2;
            yield return value.Item3;
        }
    }
}
