using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using SimpleXmlEditor.Commands;
using SimpleXmlEditor.ExpertProfiles;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ViewModels
{
    /// <summary>Outcome of an AI evaluation run (single or batch).</summary>
    public class EvaluationOutcome
    {
        public bool Failed { get; set; }
        public List<EvaluationResult> Results { get; set; } = new();
        public Dictionary<string, EvaluationResult> ResultMap { get; set; } = new();
        public EvaluationResult SingleResult { get; set; }
        public string EntryKey { get; set; } = "";
        public double AverageScore { get; set; }
        public int HighCount { get; set; }
        public int LowCount { get; set; }
    }

    /// <summary>Outcome of a multi-agent voting run (single or batch).</summary>
    public class VotingOutcome
    {
        public bool Failed { get; set; }
        public VotingResult SingleResult { get; set; }
        public bool HasSingleResult { get; set; }
        public int Completed { get; set; }
        public int BestCount { get; set; }
        public int AppliedCount { get; set; }
        public List<VotingResult> Results { get; set; } = new();
        /// <summary>Entries where the AI suggests a translation different from the current one — need manual review.</summary>
        public List<VotingResult> NeedsReview { get; set; } = new();
    }

    /// <summary>Outcome of the smart pre-translate (glossary + cache fill).</summary>
    public class PreTranslateOutcome
    {
        public int GlossaryFilled { get; set; }
        public int CacheFilled { get; set; }
        public int Total => GlossaryFilled + CacheFilled;
    }

    /// <summary>
    /// Core business-state coordinator. Members are grouped into partial files by
    /// responsibility: Properties / Undo / Config / Cache / EntryProcessing /
    /// Translation / Evaluation / Voting / Consistency (each file &lt; 400 lines).
    /// </summary>
    public partial class MainViewModel : INotifyPropertyChanged
    {
        private readonly IAiTranslationService _aiTranslationService;
        private readonly IXmlRepository _xmlRepository;
        private readonly IConfigService _configService;
        private readonly IExpertProfileManager _profileManager;
        private readonly IGlossaryManager _glossary;
        private readonly IBlacklistManager _blacklistManager;
        private readonly object _translationLock = new();
        private readonly ITranslationEvaluator _evaluator;
        private readonly TranslationOrchestrator _orchestrator;
        private readonly PluginLoader _pluginLoader;

        private ObservableCollection<LocalizationEntry> _entries;
        private string _programLanguage = "zh";
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
        private int _translatedCount = 0;
        private int _totalToTranslate = 0;
        private double _translationSpeed = 0;
        private string _estimatedTimeRemaining = "";
        private double _progressPercentage = 0;
        private DateTime _translationStartTime;
        private CancellationTokenSource _translationCts;

        // ── UI interaction events (consumed by MainWindow for pure UI rendering) ──
        public event Action<string> StatusMessageChanged;
        public event Action<int> TranslationStarted;
        public event Action<int, int> TranslationProgressChanged;
        public event Action TranslationFinished;
        public event Action<string> TranslationErrorOccurred;
        public event Action<string> EvaluationStatusText;
        public event Action<EvaluationOutcome> EvaluationCompleted;
        public event Action<string> VotingStatusText;
        public event Action<VotingOutcome> VotingCompleted;
        public event Action<PreTranslateOutcome> PreTranslateCompleted;
        public event Action<List<ConsistencyIssue>> ConsistencyScanCompleted;
        public event Func<string, string, Task<bool>> ConfirmationRequested;
        public event Action<string, string> MessageRequested;

        // ── Commands (ICommand bindings for toolbar / menu) ──
        public RelayCommand TranslateSelectedCommand { get; }
        public RelayCommand TranslateAllCommand { get; }
        public RelayCommand EvaluateCommand { get; }
        public RelayCommand VoteCommand { get; }
        public RelayCommand SmartPreTranslateCommand { get; }
        public RelayCommand ConsistencyScanCommand { get; }

        public MainViewModel(
            IAiTranslationService aiTranslationService = null,
            IXmlRepository xmlRepository = null,
            IConfigService configService = null,
            IExpertProfileManager profileManager = null,
            IGlossaryManager glossary = null,
            ITranslationEvaluator evaluator = null,
            TranslationOrchestrator orchestrator = null,
            IBlacklistManager blacklistManager = null)
        {
            _entries = new ObservableCollection<LocalizationEntry>();
            _profileManager = profileManager ?? new ExpertProfileManager();
            _glossary = glossary ?? new GlossaryManager();
            _configService = configService ?? new ConfigService();
            _aiTranslationService = aiTranslationService ?? new AiTranslationService(_configService);
            _xmlRepository = xmlRepository ?? new XmlRepository();
            _evaluator = evaluator ?? new TranslationEvaluator(_aiTranslationService, _configService);
            _orchestrator = orchestrator ?? new TranslationOrchestrator(
                _aiTranslationService, _configService, _glossary, _profileManager,
                msg => OnLogMessage(msg));
            _blacklistManager = blacklistManager ?? new BlacklistManager();
            _pluginLoader = new PluginLoader();
            _pluginLoader.LogMessage += msg => OnLogMessage(msg);
            _pluginLoader.DiscoverBuiltInPlugins();

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
            _aiTranslationService.CacheHit += count => IncrementCacheHits();
            _aiTranslationService.ApiCallCounted += count => IncrementApiCalls();
            _aiTranslationService.ApiCharsCounted += (input, output) =>
            {
                TotalInputChars += input;
                TotalOutputChars += output;
                TotalCost += _aiTranslationService.CalculateCost(input, output, _aiTranslationService.Model);
            };

            _xmlRepository.LogMessage += msg => OnLogMessage(msg);
            _configService.LogMessage += msg => OnLogMessage(msg);
            _evaluator.LogMessage += msg => OnLogMessage(msg);

            // ── Commands ──
            TranslateSelectedCommand = new RelayCommand(_ => ExecuteTranslateSelected(), _ => !IsTranslationRunning);
            TranslateAllCommand = new RelayCommand(async _ => await ExecuteTranslateAllAsync(), _ => !IsTranslationRunning);
            EvaluateCommand = new RelayCommand(async p => await EvaluateEntriesAsync(ExtractEntries(p)), _ => !IsEvaluating);
            VoteCommand = new RelayCommand(async p => await VoteEntriesAsync(ExtractEntries(p)), _ => !IsEvaluating);
            SmartPreTranslateCommand = new RelayCommand(p => SmartPreTranslate(ExtractEntries(p)), _ => !IsTranslationRunning);
            ConsistencyScanCommand = new RelayCommand(_ => ExecuteConsistencyScan());
        }

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnLogMessage(string message)
        {
            LogMessage?.Invoke(message);
        }

        private void RaiseStatusMessage(string message)
        {
            StatusMessage = message;
            StatusMessageChanged?.Invoke(message);
        }

        private static List<LocalizationEntry> ExtractEntries(object parameter)
        {
            if (parameter is System.Collections.IEnumerable enumerable)
                return enumerable.OfType<LocalizationEntry>().ToList();
            return new List<LocalizationEntry>();
        }

        public IAiTranslationService AiTranslationService => _aiTranslationService;
        public IXmlRepository XmlRepository => _xmlRepository;
        public IConfigService ConfigService => _configService;
        public IExpertProfileManager ProfileManager => _profileManager;
        public IGlossaryManager Glossary => _glossary;
        public IBlacklistManager BlacklistManager => _blacklistManager;
        public ITranslationEvaluator Evaluator => _evaluator;
        public TranslationOrchestrator Orchestrator => _orchestrator;

        /// <summary>Thread-safe glossary hits increment.</summary>
        public int IncrementGlossaryHits() => Interlocked.Increment(ref _glossaryHits);

        /// <summary>Thread-safe cache hits increment.</summary>
        public int IncrementCacheHits() => Interlocked.Increment(ref _cacheHits);

        /// <summary>Thread-safe API calls increment.</summary>
        public int IncrementApiCalls() => Interlocked.Increment(ref _apiCalls);
    }
}
