using System;
using System.ComponentModel;

namespace StarforgeLauncher.data
{
    public class DownloadItem : INotifyPropertyChanged
    {
        private string _statusText = "Welcome to Starforge";
        private string _fileName = "Preparing update.";
        private long _fileSize;
        private long _bytesDownloaded;
        private long _bytesRemaining;
        private double _progressPercentage;
        private string _bytesRemainingDisplay = "Bytes remaining: --";
        private string _etaDisplay = "ETA: --";
        private string _progressDisplay = "0%";
        private bool _isIndeterminate = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value, nameof(StatusText));
        }

        public string FileName
        {
            get => _fileName;
            set => SetField(ref _fileName, value, nameof(FileName));
        }

        public long FileSize
        {
            get => _fileSize;
            private set => SetField(ref _fileSize, value, nameof(FileSize));
        }

        public long BytesDownloaded
        {
            get => _bytesDownloaded;
            private set => SetField(ref _bytesDownloaded, value, nameof(BytesDownloaded));
        }

        public long BytesRemaining
        {
            get => _bytesRemaining;
            private set => SetField(ref _bytesRemaining, value, nameof(BytesRemaining));
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            private set => SetField(ref _progressPercentage, value, nameof(ProgressPercentage));
        }

        public string BytesRemainingDisplay
        {
            get => _bytesRemainingDisplay;
            private set => SetField(ref _bytesRemainingDisplay, value, nameof(BytesRemainingDisplay));
        }

        public string EtaDisplay
        {
            get => _etaDisplay;
            private set => SetField(ref _etaDisplay, value, nameof(EtaDisplay));
        }

        public string ProgressDisplay
        {
            get => _progressDisplay;
            private set => SetField(ref _progressDisplay, value, nameof(ProgressDisplay));
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            private set => SetField(ref _isIndeterminate, value, nameof(IsIndeterminate));
        }

        public void Reset(string? statusText = null)
        {
            StatusText = statusText ?? "Welcome to Starforge";
            FileName = "Preparing update.";
            FileSize = 0;
            BytesDownloaded = 0;
            BytesRemaining = 0;
            ProgressPercentage = 0;
            BytesRemainingDisplay = "Bytes remaining: --";
            EtaDisplay = "ETA: --";
            ProgressDisplay = "0%";
            IsIndeterminate = true;
        }

        public void BeginDownload(string fileName, long totalBytes)
        {
            FileName = "Downloading update package.";
            FileSize = Math.Max(0, totalBytes);
            BytesDownloaded = 0;
            BytesRemaining = Math.Max(0, totalBytes);
            ProgressPercentage = 0;
            ProgressDisplay = "0%";
            IsIndeterminate = totalBytes <= 0;
            BytesRemainingDisplay = totalBytes > 0
                ? $"Bytes remaining: {FormatBytes(totalBytes)}"
                : "Bytes remaining: unknown";
            EtaDisplay = "ETA: calculating.";
        }

        public void ReportProgress(long bytesDownloaded, long totalBytes, TimeSpan? eta)
        {
            if (totalBytes > 0)
            {
                FileSize = totalBytes;
                IsIndeterminate = false;
            }
            else
            {
                IsIndeterminate = true;
            }

            BytesDownloaded = Math.Max(0, bytesDownloaded);

            if (FileSize > 0)
            {
                BytesRemaining = Math.Max(0, FileSize - BytesDownloaded);
                ProgressPercentage = Math.Clamp((double)BytesDownloaded / FileSize * 100d, 0d, 100d);
                ProgressDisplay = $"{ProgressPercentage:0.0}%";
                BytesRemainingDisplay = $"Bytes remaining: {FormatBytes(BytesRemaining)}";
            }
            else
            {
                BytesRemaining = 0;
                ProgressPercentage = 0;
                ProgressDisplay = $"{FormatBytes(BytesDownloaded)} downloaded";
                BytesRemainingDisplay = "Bytes remaining: unknown";
            }

            if (eta.HasValue && eta.Value > TimeSpan.Zero)
            {
                EtaDisplay = $"ETA: {FormatEta(eta.Value)}";
            }
            else if (BytesDownloaded > 0)
            {
                EtaDisplay = "ETA: calculating.";
            }
            else
            {
                EtaDisplay = "ETA: --";
            }
        }

        public void SetIndeterminateState(string fileName, string etaText = "ETA: --")
        {
            FileName = "Processing update.";
            IsIndeterminate = true;
            EtaDisplay = etaText;
        }

        public void MarkComplete(string fileName)
        {
            FileName = "Update package ready.";
            BytesDownloaded = FileSize;
            BytesRemaining = 0;
            ProgressPercentage = FileSize > 0 ? 100d : ProgressPercentage;
            ProgressDisplay = FileSize > 0 ? "100%" : ProgressDisplay;
            BytesRemainingDisplay = "Bytes remaining: 0 B";
            EtaDisplay = "ETA: complete";
            IsIndeterminate = false;
        }

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = Math.Max(bytes, 0);
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return unitIndex == 0 ? $"{size:0} {units[unitIndex]}" : $"{size:0.##} {units[unitIndex]}";
        }

        private static string FormatEta(TimeSpan eta)
        {
            if (eta.TotalHours >= 1)
            {
                return $"{(int)eta.TotalHours}h {eta.Minutes}m {eta.Seconds}s";
            }

            if (eta.TotalMinutes >= 1)
            {
                return $"{eta.Minutes}m {eta.Seconds}s";
            }

            return $"{Math.Max(1, eta.Seconds)}s";
        }
    }

    internal static class DownloadHandler
    {
        public static DownloadItem ItemREF { get; } = new DownloadItem();
    }
}
