using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ViewModels
{
    public partial class MainViewModel
    {
        public ObservableCollection<LocalizationEntry> Entries
        {
            get => _entries;
            set
            {
                _entries = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalCount));
            }
        }

        public string ProgramLanguage
        {
            get => _programLanguage;
            set
            {
                _programLanguage = value;
                OnPropertyChanged();
            }
        }

        public string CustomPrompt
        {
            get => _customPrompt;
            set
            {
                _customPrompt = value;
                OnPropertyChanged();
            }
        }

        public string ActiveExpertProfileName
        {
            get => _activeExpertProfileName;
            set
            {
                _activeExpertProfileName = value;
                // 同步到 ExpertProfileManager：翻译时 BuildExpertContext() 读取的
                // 是 _profileManager.ActiveProfileName，二者不同步会导致专家 Context 永不注入。
                if (_profileManager != null && _profileManager.ActiveProfileName != value)
                {
                    _profileManager.ActiveProfileName = value;
                    _profileManager.SaveProfiles();
                }
                OnPropertyChanged();
            }
        }

        public int BatchSize
        {
            get => _batchSize;
            set
            {
                _batchSize = value;
                OnPropertyChanged();
            }
        }

        public int MaxConcurrentBatches
        {
            get => _maxConcurrentBatches;
            set
            {
                _maxConcurrentBatches = value;
                OnPropertyChanged();
            }
        }

        public AIProvider AiProvider
        {
            get => _aiProvider;
            set
            {
                _aiProvider = value;
                _aiTranslationService.CurrentProvider = value;
                OnPropertyChanged();
            }
        }

        public int CacheHits
        {
            get => _cacheHits;
            set
            {
                _cacheHits = value;
                OnPropertyChanged();
            }
        }

        public int GlossaryHits
        {
            get => _glossaryHits;
            set
            {
                _glossaryHits = value;
                OnPropertyChanged();
            }
        }

        public int ApiCalls
        {
            get => _apiCalls;
            set
            {
                _apiCalls = value;
                OnPropertyChanged();
            }
        }

        public int TotalInputChars
        {
            get => _totalInputChars;
            set
            {
                _totalInputChars = value;
                OnPropertyChanged();
            }
        }

        public int TotalOutputChars
        {
            get => _totalOutputChars;
            set
            {
                _totalOutputChars = value;
                OnPropertyChanged();
            }
        }

        public double TotalCost
        {
            get => _totalCost;
            set
            {
                _totalCost = value;
                OnPropertyChanged();
            }
        }

        public bool IsTranslationPaused
        {
            get => _isTranslationPaused;
            set
            {
                _isTranslationPaused = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsTranslationRunning
        {
            get => _isTranslationRunning;
            set
            {
                _isTranslationRunning = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string LastLoadedFilePath
        {
            get => _lastLoadedFilePath;
            set
            {
                _lastLoadedFilePath = value;
                OnPropertyChanged();
            }
        }

        private bool _isEvaluating = false;
        private string _lastEvaluationResult = "";

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public int TotalCount => Entries?.Count ?? 0;

        public PluginLoader PluginLoader => _pluginLoader;

        public int TranslatedCount
        {
            get => _translatedCount;
            set { _translatedCount = value; OnPropertyChanged(); }
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set { _progressPercentage = value; OnPropertyChanged(); }
        }

        public double TranslationSpeed
        {
            get => _translationSpeed;
            set { _translationSpeed = value; OnPropertyChanged(); }
        }

        public string EstimatedTimeRemaining
        {
            get => _estimatedTimeRemaining;
            set { _estimatedTimeRemaining = value; OnPropertyChanged(); }
        }

        public bool IsEvaluating
        {
            get => _isEvaluating;
            set { _isEvaluating = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        public string LastEvaluationResult
        {
            get => _lastEvaluationResult;
            set
            {
                _lastEvaluationResult = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<string> LogMessage;
    }
}
