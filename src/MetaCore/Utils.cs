namespace MetaArt.Core {
    public static class Utils {
        public static Stream GetStream(Type typeFromAssembly, string filename) {
            return typeFromAssembly.Assembly.GetManifestResourceStream(typeFromAssembly.Assembly.GetName().Name + ".Assets." + filename);
        }
    }
}
