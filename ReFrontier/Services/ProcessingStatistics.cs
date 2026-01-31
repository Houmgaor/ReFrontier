using System.Threading;

namespace ReFrontier.Services
{
    /// <summary>
    /// Thread-safe statistics for file processing operations.
    /// </summary>
    public class ProcessingStatistics
    {
        private int _totalFiles;
        private int _processedFiles;
        private int _skippedFiles;
        private int _errorFiles;
        private int _generatedFiles;

        /// <summary>
        /// Total number of files to process (initial count).
        /// </summary>
        public int TotalFiles => _totalFiles;

        /// <summary>
        /// Number of files successfully processed.
        /// </summary>
        public int ProcessedFiles => _processedFiles;

        /// <summary>
        /// Number of files skipped (not valid containers).
        /// </summary>
        public int SkippedFiles => _skippedFiles;

        /// <summary>
        /// Number of files that caused errors.
        /// </summary>
        public int ErrorFiles => _errorFiles;

        /// <summary>
        /// Number of new files generated from unpacking.
        /// </summary>
        public int GeneratedFiles => _generatedFiles;

        /// <summary>
        /// Total files handled (processed + skipped + errors).
        /// </summary>
        public int HandledFiles => _processedFiles + _skippedFiles + _errorFiles;

        /// <summary>
        /// Set the initial total file count.
        /// </summary>
        public void SetTotalFiles(int count)
        {
            Interlocked.Exchange(ref _totalFiles, count);
        }

        /// <summary>
        /// Increment processed file count.
        /// </summary>
        public void IncrementProcessed()
        {
            Interlocked.Increment(ref _processedFiles);
        }

        /// <summary>
        /// Increment skipped file count.
        /// </summary>
        public void IncrementSkipped()
        {
            Interlocked.Increment(ref _skippedFiles);
        }

        /// <summary>
        /// Increment error file count.
        /// </summary>
        public void IncrementError()
        {
            Interlocked.Increment(ref _errorFiles);
        }

        /// <summary>
        /// Add to generated files count.
        /// </summary>
        public void AddGeneratedFiles(int count)
        {
            Interlocked.Add(ref _generatedFiles, count);
        }
    }
}
