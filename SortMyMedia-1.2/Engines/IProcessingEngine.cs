namespace SortMyMedia.Engines
{
    public interface IProcessingEngine
    {
        void Process(
            string inputFolder,
            string outputFolder,
            Action<string> log,
            Action<int> progress
        );
    }
}