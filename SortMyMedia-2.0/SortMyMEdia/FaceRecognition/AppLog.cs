using System;

namespace SortMyMedia.FaceRecognition
{
    public static class AppLog
    {
        public static Action<string>? Write { get; set; }
    }
}
