namespace SortMyMedia
{
    public enum AppMode
    {
        MediaSorter,
        OCR,
        FaceGrouping
    }

    public static class AppModeState
    {
        public static AppMode CurrentMode { get; set; } = AppMode.MediaSorter;
    }
}
