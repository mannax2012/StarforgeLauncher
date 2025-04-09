using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarforgeLauncher.data
{
    public class DownloadItem : INotifyPropertyChanged
    {
        private string _fileName;
        private long _fileSize;
        private long _bytesRemaining;
        private double _progressPercentage;

        public event PropertyChangedEventHandler PropertyChanged;

        public string FileName
        {
            get => _fileName;
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    OnPropertyChanged(nameof(FileName));
                }
            }
        }

        public long FileSize
        {
            get => _fileSize;
            set
            {
                if (_fileSize != value)
                {
                    _fileSize = value;
                    OnPropertyChanged(nameof(FileSize));
                    UpdateProgress(); // Update progress when file size changes
                }
            }
        }

        public long BytesRemaining
        {
            get => _bytesRemaining;
            set
            {
                if (_bytesRemaining != value)
                {
                    _bytesRemaining = value;
                    OnPropertyChanged(nameof(BytesRemaining));
                    UpdateProgress(); // Update progress when remaining bytes change
                }
            }
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            private set
            {
                if (_progressPercentage != value)
                {
                    _progressPercentage = value;
                    OnPropertyChanged(nameof(ProgressPercentage));
                }
            }
        }

        private void UpdateProgress()
        {
            ProgressPercentage = FileSize > 0 ? (1 - (double)BytesRemaining / FileSize) * 100 : 0;
            Console.WriteLine($"Progress: {ProgressPercentage}%");
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    internal class DownloadHandler
    {
        public static DownloadItem ItemREF { get; set; } = new DownloadItem();
    }
}
