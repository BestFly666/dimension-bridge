using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleXmlEditor.ExpertProfiles;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IAiTranslationService _aiTranslationService;
        private readonly IXmlRepository _xmlRepository;
        private readonly IConfigService _configService;
        private readonly IExpertProfileManager _profileManager;
        private readonly IGlossaryManager _glossary;
        private readonly ITranslationEvaluator _evaluator;
        private readonly TranslationOrchestrator _orchestrator;

        private ObservableCollection<LocalizationEntry> _entries;
        private string _programLanguage = "en";
        private string _customPrompt = "";
        private string _activeExpertProfileName = "";
        private int _batchSize = 50;
        private AIProvider _aiProvider = AIProvider.GoogleGemini;
        private int _cacheHits = 0;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private int _glossaryHits = 0;
        private int _apiCalls = 0;
        private int _totalInputChars = 0;
        private int _totalOutputChars = 0;
        private double _totalCost = 0.0;
        private bool _isTranslationPaused = false;
        private bool _isTranslationRunning = false;
        private string _lastLoadedFilePath = "";
        private string _statusMessage = "";

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
            }
        }

        public bool IsTranslationRunning
        {
            get => _isTranslationRunning;
            set
            {
                _isTranslationRunning = value;
                OnPropertyChanged();
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

        public bool IsEvaluating
        {
            get => _isEvaluating;
            set
            {
                _isEvaluating = value;
                OnPropertyChanged();
            }
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

        public MainViewModel(
            IAiTranslationService aiTranslationService = null,
            IXmlRepository xmlRepository = null,
            IConfigService configService = null,
            IExpertProfileManager profileManager = null,
            IGlossaryManager glossary = null,
            ITranslationEvaluator evaluator = null,
            TranslationOrchestrator orchestrator = null)
        {
            _entries = new ObservableCollection<LocalizationEntry>();
            _profileManager = profileManager ?? new ExpertProfileManager();
            _glossary = glossary ?? new GlossaryManager();
            _aiTranslationService = aiTranslationService ?? new AiTranslationService();
            _xmlRepository = xmlRepository ?? new XmlRepository();
            _configService = configService ?? new ConfigService();
            _evaluator = evaluator ?? new TranslationEvaluator(_aiTranslationService);
            _orchestrator = orchestrator ?? new TranslationOrchestrator(
                _aiTranslationService, _configService, _glossary, _profileManager,
                msg => OnLogMessage(msg));

            _orchestrator.OnCacheHit += count => IncrementCacheHits();
            _orchestrator.OnGlossaryHit += count => IncrementGlossaryHits();
            _orchestrator.OnApiCall += count => IncrementApiCalls();
            _orchestrator.OnApiChars += (input, output) =>
            {
                TotalInputChars += input;
                TotalOutputChars += output;
                TotalCost += _aiTranslationService.CalculateCost(input, output, _aiTranslationService.Model);
            };

            _aiTranslationService.LogMessage += msg => OnLogMessage(msg);
            _xmlRepository.LogMessage += msg => OnLogMessage(msg);
            _configService.LogMessage += msg => OnLogMessage(msg);
            _evaluator.LogMessage += msg => OnLogMessage(msg);
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnLogMessage(string message)
        {
            LogMessage?.Invoke(message);
        }

        public IAiTranslationService AiTranslationService => _aiTranslationService;
        public IXmlRepository XmlRepository => _xmlRepository;
        public IConfigService ConfigService => _configService;
        public IExpertProfileManager ProfileManager => _profileManager;
        public IGlossaryManager Glossary => _glossary;
        public ITranslationEvaluator Evaluator => _evaluator;
        public TranslationOrchestrator Orchestrator => _orchestrator;

        /// <summary>Thread-safe glossary hits increment.</summary>
        public int IncrementGlossaryHits() => Interlocked.Increment(ref _glossaryHits);

        /// <summary>Thread-safe cache hits increment.</summary>
        public int IncrementCacheHits() => Interlocked.Increment(ref _cacheHits);

        /// <summary>Thread-safe API calls increment.</summary>
        public int IncrementApiCalls() => Interlocked.Increment(ref _apiCalls);

        public void LoadConfig()
        {
            _configService.LoadConfig();
            
            if (_configService.Config.ActiveExpertProfile != null)
                ActiveExpertProfileName = _configService.Config.ActiveExpertProfile;
            if (_configService.Config.CustomPrompt != null)
                CustomPrompt = _configService.Config.CustomPrompt;
            if (_configService.Config.LastLoadedFilePath != null)
                LastLoadedFilePath = _configService.Config.LastLoadedFilePath;
            BatchSize = _configService.Config.BatchSize;
            AiProvider = Enum.TryParse<AIProvider>(_configService.Config.AiProvider, out var provider) ? provider : AIProvider.GoogleGemini;
            if (_configService.Config.ProgramLanguage != null)
                ProgramLanguage = _configService.Config.ProgramLanguage;
            Localization.LocalizationManager.CurrentLanguage = ProgramLanguage;
            if (_configService.Config.EncryptedApiKey != null)
                _aiTranslationService.ApiKey = _configService.GetApiKey();
            if (_configService.Config.GeminiModel != null)
                _aiTranslationService.Model = _configService.Config.GeminiModel;
            if (_configService.Config.TargetLanguage != null)
                _aiTranslationService.TargetLanguage = _configService.Config.TargetLanguage;
        }

        public void SaveConfig()
        {
            _configService.Config.ActiveExpertProfile = ActiveExpertProfileName;
            _configService.Config.CustomPrompt = CustomPrompt;
            _configService.Config.LastLoadedFilePath = LastLoadedFilePath;
            _configService.Config.BatchSize = BatchSize;
            _configService.Config.AiProvider = AiProvider.ToString();
            _configService.Config.ProgramLanguage = ProgramLanguage;
            _configService.SetApiKey(_aiTranslationService.ApiKey);
            _configService.Config.GeminiModel = _aiTranslationService.Model;
            _configService.Config.TargetLanguage = _aiTranslationService.TargetLanguage;
            _configService.SaveConfig();
        }

        public void UpdateCacheInfo()
        {
            var cacheCount = _configService.Cache.Count;
            StatusMessage = $"📊 {cacheCount} {Localization.LocalizationManager.GetString("CacheInfo", CacheHits)}";
        }

        public void UpdateDictInfo()
        {
            var dictCount = _glossary.Count;
            StatusMessage = $"📖 {dictCount} {Localization.LocalizationManager.GetString("DictionaryInfo", GlossaryHits)}";
        }

        public void TrackRequest()
        {
            _aiTranslationService.TrackRequest();
            _lastRequestTime = DateTime.Now;
        }

        public int RestoreTranslationProgress(IEnumerable<LocalizationEntry> entries)
        {
            return _configService.RestoreTranslationProgress(entries);
        }

        public void SyncEntriesToCache(IEnumerable<LocalizationEntry> entries)
        {
            _configService.SyncEntriesToCache(entries);
        }

        public Dictionary<string, string> GetCacheForSave(IEnumerable<LocalizationEntry> entries)
        {
            return _configService.GetCacheForSave(entries);
        }

        public string GetCacheKey(string text)
        {
            return _configService.GetCacheKey(text);
        }

        /// <summary>
        /// Evaluate a single translation entry with AI quality scoring.
        /// </summary>
        public async Task<EvaluationResult> EvaluateEntry(LocalizationEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Value) || string.IsNullOrEmpty(entry.Translation))
                return null;

            IsEvaluating = true;
            try
            {
                var targetLang = _aiTranslationService.TargetLanguage;
                var result = await _evaluator.EvaluateAsync(entry.Value, entry.Translation, targetLang);
                LastEvaluationResult = $"{entry.Key}: Score {result.Score:F1}/10 — {result.Explanation}";
                OnLogMessage($"📊 Evaluation: {entry.Key} → {result.Score:F1}/10");
                return result;
            }
            finally
            {
                IsEvaluating = false;
            }
        }

        /// <summary>
        /// Run multi-agent voting on a single entry to find the best translation.
        /// </summary>
        public async Task<VotingResult> VoteEntry(LocalizationEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Value))
                return null;

            IsEvaluating = true;
            try
            {
                var candidates = new[] { entry.Translation ?? entry.Value };
                // Also try re-translating to get alternative candidates for comparison
                if (!string.IsNullOrEmpty(entry.Translation))
                {
                    // Include the original as well for comparison
                    candidates = new[] { entry.Translation, entry.Value };
                }

                var targetLang = _aiTranslationService.TargetLanguage;
                var result = await _evaluator.VoteAsync(entry.Value, candidates, targetLang);
                LastEvaluationResult = result.ConsensusSummary;
                OnLogMessage($"🗳 Vote: {entry.Key} → {result.ConsensusSummary}");
                return result;
            }
            finally
            {
                IsEvaluating = false;
            }
        }
    }
}