using System;
using System.Threading.Tasks;

namespace SortMyMedia.Engines
{
    public interface IProcessingEngine
    {
        Task<ProcessingSummary> Process(
            string inputFolder,
            string outputFolder,
            Action<string> log,
            Action<int> progress
        );
    }
}