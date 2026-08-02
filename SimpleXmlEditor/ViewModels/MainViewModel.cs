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

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IAiTranslationService _aiTranslationService;
        private readonly IXmlRepository _xmlRepository;
        private readonly IConfigService _configService;
        private readonly IExpertProfileManager _profileManager;
        private readonly IGlossaryManager _glossary;
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

        // ── Undo infrastructure ──
        private readonly object _undoLock = new object();
        private readonly Stack<Dictionary<string, string>> _undoStack = new Stack<Dictionary<string, string>>();

        /// <summary>Record a snapshot of affected entries before a bulk mutation.</summary>
        public void PushUndoSnapshot(IEnumerable<LocalizationEntry> affected)
        {
            var snapshot = new Dictionary<string, string>();
            foreach (var entry in affected)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key)) continue;
                snapshot[entry.Key] = entry.Translation ?? "";
            }
            if (snapshot.Count == 0) return;

            lock (_undoLock)
            {
                _undoStack.Push(snapshot);
                // Keep the latest 50 snapshots to bound memory usage.
                if (_undoStack.Count > 50)
                {
                    var keep = _undoStack.Take(50).Reverse().ToList();
                    _undoStack.Clear();
                    foreach (var s in keep) _undoStack.Push(s);
                }
            }
        }

        /// <summary>Revert the most recent mutation. Returns the list of restored entries (empty if nothing to undo).</summary>
        public List<LocalizationEntry> UndoLast()
        {
            Dictionary<string, string> snapshot;
            lock (_undoLock)
            {
                if (_undoStack.Count == 0) return new List<LocalizationEntry>();
                snapshot = _undoStack.Pop();
            }

            var restored = new List<LocalizationEntry>();
            foreach (var entry in Entries)
            {
                if (snapshot.TryGetValue(entry.Key, out var original))
                {
                    entry.Translation = original;
                    restored.Add(entry);
                }
            }
            return restored;
        }

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
            _configService = configService ?? new ConfigService();
            _aiTranslationService = aiTranslationService ?? new AiTranslationService(_configService);
            _xmlRepository = xmlRepository ?? new XmlRepository();
            _evaluator = evaluator ?? new TranslationEvaluator(_aiTranslationService, _configService);
            _orchestrator = orchestrator ?? new TranslationOrchestrator(
                _aiTranslationService, _configService, _glossary, _profileManager,
                msg => OnLogMessage(msg));
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

        public void StartTranslationTracking(int totalToTranslate)
        {
            _translationStartTime = DateTime.Now;
            _totalToTranslate = totalToTranslate;
            TranslatedCount = 0;
            ProgressPercentage = 0;
            TranslationSpeed = 0;
            EstimatedTimeRemaining = "...";
        }

        public void UpdateTranslationProgress(int translatedCount)
        {
            TranslatedCount = translatedCount;
            if (_totalToTranslate > 0)
            {
                ProgressPercentage = Math.Round(translatedCount * 100.0 / _totalToTranslate, 1);
            }
            var elapsed = (DateTime.Now - _translationStartTime).TotalSeconds;
            if (elapsed > 0.5 && translatedCount > 0)
            {
                TranslationSpeed = Math.Round(translatedCount / elapsed, 1);
                var remaining = _totalToTranslate - translatedCount;
                if (TranslationSpeed > 0)
                {
                    var remainingSeconds = remaining / TranslationSpeed;
                    EstimatedTimeRemaining = remainingSeconds < 60
                        ? $"{remainingSeconds:F0}s"
                        : $"{remainingSeconds / 60:F0}m {remainingSeconds % 60:F0}s";
                }
            }
        }

        public string GetTranslationStatusIndicator()
        {
            if (!IsTranslationRunning) return "⚪";
            return IsTranslationPaused ? "🟡" : "🟢";
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

        public void SyncScoresToCache(IEnumerable<LocalizationEntry> entries)
        {
            _configService.SyncScoresToCache(entries);
        }

        public void SaveScoreCache()
        {
            _configService.SaveScoreCache();
        }

        public int RestoreScores(IEnumerable<LocalizationEntry> entries)
        {
            return _configService.RestoreScores(entries);
        }

        public Dictionary<string, string> GetCacheForSave(IEnumerable<LocalizationEntry> entries)
        {
            return _configService.GetCacheForSave(entries);
        }

        public string GetCacheKey(string text)
        {
            return _configService.GetCacheKey(text);
        }

        // ═══════════════════════════════════════════════════════════
        //  Entry processing (moved from MainWindow)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Process an entry during load: cache write/read, Chinese-source handling,
        /// glossary application, and adding to the Entries collection.
        /// </summary>
        public LocalizationEntry ProcessEntry(LocalizationEntry entry)
        {
            entry.RowNumber = Entries.Count + 1;

            var valueIsChinese = entry.Value.HasChineseChars();

            if (!string.IsNullOrEmpty(entry.Translation))
            {
                if (!string.IsNullOrWhiteSpace(entry.Value))
                    _configService.Cache.TryAdd(entry.Key, entry.Translation);
            }
            else if (valueIsChinese)
            {
                entry.Translation = entry.Value;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(entry.Value))
                {
                    if (_configService.Cache.TryGetValue(entry.Key, out var cachedByKey))
                    {
                        entry.Translation = cachedByKey;
                    }
                    else
                    {
                        var cacheKey = _configService.GetCacheKey(entry.Value);
                        if (cacheKey != null && _configService.Cache.TryGetValue(cacheKey, out var cachedByValue))
                        {
                            entry.Translation = cachedByValue;
                        }
                    }

                    TryApplyDictionary(entry);
                }
            }

            if (valueIsChinese)
            {
                entry.Value = "";
            }

            Entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// Try to apply glossary lookup. Only exact-match on Key or Value.
        /// Term-level substitution is handled by BuildGlossaryContext via AI prompt.
        /// </summary>
        public bool TryApplyDictionary(LocalizationEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.Translation))
                return false;

            // Exact match on Key (e.g., "UPGRADE_TECH" → "科技升级")
            if (_glossary.TryGetValue(entry.Key, out var dictTranslation))
            {
                entry.Translation = dictTranslation;
                IncrementGlossaryHits();
                return true;
            }
            // Exact match on entire Value (single-word entries like "Jedi" → "绝地")
            if (_glossary.TryGetValue(entry.Value, out dictTranslation))
            {
                entry.Translation = dictTranslation;
                IncrementGlossaryHits();
                return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════
        //  Save helpers (moved from MainWindow)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Save entries to XML. Returns true on success.</summary>
        public bool SaveXml(string fileName = "stable_us.xml")
        {
            try
            {
                SyncEntriesToCache(Entries);

                var entriesList = Entries.ToList();
                _xmlRepository.SaveXml(fileName, entriesList);

                SaveConfig();
                RaiseStatusMessage(LocalizationManager.GetString("SavedEntries", Entries.Count, System.IO.Path.GetFileName(fileName)));
                OnLogMessage($"💾 {LocalizationManager.GetString("LogXmlSaved", fileName, Entries.Count)}");
                return true;
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ {LocalizationManager.GetString("ErrorSavingXml", ex.Message)}");
                return false;
            }
        }

        /// <summary>Persist the translation cache to disk.</summary>
        public void SaveCache()
        {
            try
            {
                _configService.SaveCache();
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ {LocalizationManager.GetString("LogCacheWriteError", ex.Message)}");
            }
        }

        /// <summary>Cancel the running translation pipeline.</summary>
        public void CancelTranslation()
        {
            _translationCts?.Cancel();
        }

        // ═══════════════════════════════════════════════════════════
        //  Translation pipeline (moved from MainWindow.TranslateEntries)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Full translation pipeline: batch scheduling, pause/resume, cancellation,
        /// progress tracking, incremental saves, and auto-save. UI renders via events.
        /// </summary>
        public async Task TranslateEntriesAsync(List<LocalizationEntry> entries, bool forceRefresh = false)
        {
            _translationCts = new CancellationTokenSource();
            IsTranslationRunning = true;
            IsTranslationPaused = false;

            try
            {
                // Session begin — UI shows controls and resets progress display
                TranslationStarted?.Invoke(0);

                // Reset cost tracking for this translation session
                TotalCost = 0;
                TotalInputChars = 0;
                TotalOutputChars = 0;

                var successCount = 0;
                var failCount = 0;

                // Filter out entries that need translation
                var entriesToTranslate = entries.Where(e => !string.IsNullOrEmpty(e.Value) && string.IsNullOrEmpty(e.Translation)).ToList();

                if (!entriesToTranslate.Any())
                {
                    OnLogMessage($"ℹ️ {LocalizationManager.GetString("LogNoTranslationNeeded")}");
                    RaiseStatusMessage(LocalizationManager.GetString("NoEntriesForTranslation"));
                    return;
                }

                // Record undo snapshot before mutating translations
                PushUndoSnapshot(entriesToTranslate);

                // Create batches based on token limits
                var batches = _orchestrator.CreateBatches(entriesToTranslate, CustomPrompt, BatchSize);

                StartTranslationTracking(entriesToTranslate.Count);
                TranslationStarted?.Invoke(entriesToTranslate.Count);

                OnLogMessage($"🌍 {LocalizationManager.GetString("LogBatchStart", entriesToTranslate.Count, batches.Count, forceRefresh ? " (force refresh)" : "")}");
                OnLogMessage($"📊 {LocalizationManager.GetString("LogBatchModel", _aiTranslationService.Model)}");

                for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    // Check for cancellation
                    if (_translationCts.Token.IsCancellationRequested)
                    {
                        OnLogMessage($"⏹️ {LocalizationManager.GetString("LogBatchCancelled", batchIndex + 1, batches.Count)}");
                        break;
                    }

                    // Handle pause
                    while (IsTranslationPaused && !_translationCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(500, _translationCts.Token);
                    }

                    if (_translationCts.Token.IsCancellationRequested)
                        break;

                    var batch = batches[batchIndex];
                    var batchSize = batch.Count;

                    RaiseStatusMessage(LocalizationManager.GetString("TranslatingBatch", batchIndex + 1, batches.Count, batchSize));
                    OnLogMessage($"🔄 {LocalizationManager.GetString("LogBatchProgress", batchIndex + 1, batches.Count, batchSize)}");

                    // Track request for rate limiting
                    TrackRequest();

                    var batchResults = await _orchestrator.TranslateBatchAsync(batch, forceRefresh, CustomPrompt);

                    // Apply translations
                    var batchSuccessCount = 0;
                    var batchFailCount = 0;

                    foreach (var entry in batch)
                    {
                        if (batchResults.ContainsKey(entry.Value))
                        {
                            entry.Translation = batchResults[entry.Value];
                            batchSuccessCount++;
                        }
                        else
                        {
                            batchFailCount++;
                        }
                    }

                    successCount += batchSuccessCount;
                    failCount += batchFailCount;

                    // Update entry-based progress
                    var totalTranslated = successCount + failCount;
                    UpdateTranslationProgress(totalTranslated);
                    TranslationProgressChanged?.Invoke(totalTranslated, entriesToTranslate.Count);

                    if (batchFailCount > 0)
                    {
                        // Only log failed keys individually (for debugging)
                        var failedKeys = batch.Where(e => !batchResults.ContainsKey(e.Value))
                            .Select(e => e.Key.Length > 40 ? e.Key[..40] : e.Key);
                        OnLogMessage($"❌ {LocalizationManager.GetString("LogBatchFails", batchFailCount, string.Join(", ", failedKeys.Take(5)))}");
                    }

                    // Incremental save: write progress to recovery file after each batch
                    _configService.SaveTranslationProgress(Entries);

                    OnLogMessage($"📊 {LocalizationManager.GetString("LogBatchDone", batchIndex + 1, batches.Count, batchSuccessCount, batchFailCount)}");

                    // Use model-specific optimal delay between batches
                    if (batchIndex < batches.Count - 1 && !_translationCts.Token.IsCancellationRequested)
                    {
                        var delay = _aiTranslationService.CalculateOptimalDelay();
                        RaiseStatusMessage(LocalizationManager.GetString("WaitingRateLimit", delay / 1000));

                        try
                        {
                            await Task.Delay(delay, _translationCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }

                UpdateTranslationProgress(entriesToTranslate.Count);
                TranslationProgressChanged?.Invoke(entriesToTranslate.Count, entriesToTranslate.Count);

                // Auto-save if we have successful translations
                if (successCount > 0)
                {
                    SaveXml();
                    SaveCache();
                    OnLogMessage($"💾 {LocalizationManager.GetString("LogCacheSaved")}");
                    // Translation complete — delete recovery file
                    _configService.DeleteProgressFile();
                }

                var statusMessage = _translationCts.Token.IsCancellationRequested
                    ? LocalizationManager.GetString("StatusStoppedResult", successCount, failCount)
                    : LocalizationManager.GetString("StatusBatchComplete", successCount, failCount);

                RaiseStatusMessage(statusMessage);
                OnLogMessage($"🎉 {LocalizationManager.GetString("LogTranslationDone", statusMessage)}");

                if (failCount > 0)
                {
                    OnLogMessage($"💡 {LocalizationManager.GetString("LogTipHeader")}");
                    OnLogMessage(LocalizationManager.GetString("LogTip1"));
                    OnLogMessage(LocalizationManager.GetString("LogTip2"));
                    OnLogMessage(LocalizationManager.GetString("LogTip3"));
                }

                // Show efficiency stats
                var efficiency = entriesToTranslate.Count > 0 ? (successCount * 100.0 / entriesToTranslate.Count) : 0;
                OnLogMessage($"📈 {LocalizationManager.GetString("LogEfficiency", efficiency.ToString("F1"), successCount, entriesToTranslate.Count)}");
                OnLogMessage($"⚡ {LocalizationManager.GetString("LogBatchEfficiency", batches.Count, entriesToTranslate.Count, entriesToTranslate.Count - batches.Count)}");

                // Show rate limit summary
                if (_aiTranslationService.ModelLimits.ContainsKey(_aiTranslationService.Model))
                {
                    var limits = _aiTranslationService.ModelLimits[_aiTranslationService.Model];
                    var requestsInLastMinute = _aiTranslationService.RecentRequests.Count;
                    OnLogMessage($"📊 {LocalizationManager.GetString("LogRateLimitStatus", requestsInLastMinute, limits.requestsPerMinute)}");
                }
            }
            catch (OperationCanceledException)
            {
                OnLogMessage($"⏹️ {LocalizationManager.GetString("LogTranslationCancelled")}");
                RaiseStatusMessage(LocalizationManager.GetString("TranslationCancelled"));
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ {LocalizationManager.GetString("TranslationError", ex.Message)}");
                RaiseStatusMessage(LocalizationManager.GetString("TranslationError", ex.Message));
                TranslationErrorOccurred?.Invoke(LocalizationManager.GetString("TranslationError", ex.Message));
            }
            finally
            {
                IsTranslationRunning = false;
                IsTranslationPaused = false;
                _translationCts?.Dispose();
                _translationCts = null;
                TranslationFinished?.Invoke();
            }
        }

        // ── Command implementations ──

        private async void ExecuteTranslateSelected()
        {
            var selected = Entries.Where(entry => entry.IsSelected).ToList();
            if (!selected.Any())
            {
                MessageRequested?.Invoke(LocalizationManager.GetString("SelectEntriesFirst"), LocalizationManager.GetString("MsgTip"));
                return;
            }

            var toClear = selected.Where(en => !string.IsNullOrEmpty(en.Translation)).ToList();
            if (toClear.Count > 0)
                PushUndoSnapshot(toClear);

            foreach (var entry in selected)
            {
                entry.Translation = "";
            }

            await TranslateEntriesAsync(selected, forceRefresh: true);
        }

        private async Task ExecuteTranslateAllAsync()
        {
            var untranslated = Entries.Where(e => string.IsNullOrEmpty(e.Translation) && !string.IsNullOrEmpty(e.Value)).ToList();
            if (!untranslated.Any())
            {
                MessageRequested?.Invoke(LocalizationManager.GetString("NoUntranslatedEntries"), LocalizationManager.GetString("MsgTip"));
                return;
            }

            var confirmed = await (ConfirmationRequested?.Invoke(
                LocalizationManager.GetString("ConfirmTranslate", untranslated.Count),
                LocalizationManager.GetString("MsgConfirm")) ?? Task.FromResult(true));

            if (confirmed)
                await TranslateEntriesAsync(untranslated);
        }

        private void ExecuteConsistencyScan()
        {
            OnLogMessage($"🔍 {LocalizationManager.GetString("ConsistencyScanning")}");
            var issues = ScanConsistencyIssues();
            ConsistencyScanCompleted?.Invoke(issues);
        }

        // ═══════════════════════════════════════════════════════════
        //  Evaluation / voting orchestration (moved from MainWindow)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Evaluate selected entries (or all translated entries) with AI quality scoring.</summary>
        public async Task EvaluateEntriesAsync(IEnumerable<LocalizationEntry> selection)
        {
            var entries = selection?.ToList() ?? new List<LocalizationEntry>();
            if (entries.Count == 0)
                entries = Entries.Where(e => !string.IsNullOrEmpty(e.Translation)).ToList();

            if (entries.Count == 0)
            {
                OnLogMessage($"⚠ {LocalizationManager.GetString("NoTranslatedToEvaluate")}");
                EvaluationCompleted?.Invoke(null);
                return;
            }

            // Single entry evaluation
            if (entries.Count == 1)
            {
                var entry = entries.First();
                OnLogMessage($"🤖 {LocalizationManager.GetString("LogEvaluating", entry.Key)}");
                EvaluationStatusText?.Invoke($"⏳ {LocalizationManager.GetString("EvalEvaluating")}");

                var result = await EvaluateEntry(entry);

                if (result == null)
                {
                    EvaluationCompleted?.Invoke(new EvaluationOutcome { Failed = true });
                    return;
                }

                var outcome = new EvaluationOutcome { SingleResult = result, EntryKey = entry.Key };
                outcome.ResultMap[entry.Key] = result;
                EvaluationCompleted?.Invoke(outcome);
                return;
            }

            // Batch evaluation for multiple entries (batched API calls for speed)
            OnLogMessage($"🤖 {LocalizationManager.GetString("LogBatchEvaluating", entries.Count)}");
            EvaluationStatusText?.Invoke($"⏳ {LocalizationManager.GetString("EvalBatchProgress", entries.Count)}");

            var context = GetEvaluationContext();
            var items = entries
                .Where(e => !string.IsNullOrEmpty(e.Translation))
                .Select(e => (e.Key, e.Value, e.Translation))
                .ToList();

            List<EvaluationResult> results;
            try
            {
                results = await _evaluator.EvaluateBatchAsync(items, _aiTranslationService.TargetLanguage, context);
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ {LocalizationManager.GetString("TranslationError", ex.Message)}");
                EvaluationCompleted?.Invoke(new EvaluationOutcome { Failed = true });
                return;
            }

            if (results.Count == 0)
            {
                EvaluationCompleted?.Invoke(new EvaluationOutcome { Failed = true });
                return;
            }

            // 安全构建 ResultMap：遇到重复键时后者覆盖前者，避免 ToDictionary 抛异常崩溃
            // 重复键来源：AI 返回 JSON 中 index 重复/错乱、fallback 路径默认值、XML 重复 Key
            var resultMap = new Dictionary<string, EvaluationResult>();
            foreach (var r in results)
            {
                var key = r.TranslatedText ?? "";
                resultMap[key] = r;
            }

            EvaluationCompleted?.Invoke(new EvaluationOutcome
            {
                Results = results,
                ResultMap = resultMap,
                AverageScore = results.Where(r => r.Score > 0).Select(r => r.Score).DefaultIfEmpty(0).Average(),
                HighCount = results.Count(r => r.Score >= 8),
                LowCount = results.Count(r => r.Score > 0 && r.Score < 5)
            });
        }

        /// <summary>Run multi-agent voting on selected entries (or all translated entries).</summary>
        public async Task VoteEntriesAsync(IEnumerable<LocalizationEntry> selection)
        {
            var entries = selection?.ToList() ?? new List<LocalizationEntry>();
            if (entries.Count == 0)
                entries = Entries.Where(e => !string.IsNullOrEmpty(e.Translation)).ToList();

            if (entries.Count == 0)
            {
                OnLogMessage($"⚠ {LocalizationManager.GetString("NoTranslatedToVote")}");
                VotingCompleted?.Invoke(null);
                return;
            }

            // Batch voting for multiple entries (candidate generation + batched API calls)
            if (entries.Count > 1)
            {
                OnLogMessage($"🗳 {LocalizationManager.GetString("LogBatchVoting", entries.Count)}");
                VotingStatusText?.Invoke($"⏳ {LocalizationManager.GetString("VoteBatchProgress", entries.Count)}");

                var context = GetEvaluationContext();
                var targetLang = _aiTranslationService.TargetLanguage;

                // Build candidate sets: current translation + AI-generated alternatives
                var items = new List<(string Key, string Original, string[] Candidates)>();
                var totalForCandidates = entries.Count(e => !string.IsNullOrEmpty(e.Translation));
                var candidateIdx = 0;
                foreach (var e in entries)
                {
                    if (string.IsNullOrEmpty(e.Translation)) continue;
                    candidateIdx++;
                    OnLogMessage($"📝 {LocalizationManager.GetString("LogGeneratingCandidate", candidateIdx, totalForCandidates, e.Key)}");
                    VotingStatusText?.Invoke($"📝 {LocalizationManager.GetString("VoteCandidateProgress", candidateIdx, totalForCandidates)}");

                    var candidates = new List<string> { e.Translation };
                    try
                    {
                        var generated = await _evaluator.GenerateCandidatesAsync(e.Value, targetLang, context, 2);
                        foreach (var g in generated)
                        {
                            if (!string.IsNullOrEmpty(g) && !candidates.Contains(g))
                                candidates.Add(g);
                        }
                    }
                    catch (Exception ex)
                    {
                        OnLogMessage($"⚠ {LocalizationManager.GetString("TranslationError", ex.Message)}");
                    }
                    items.Add((e.Key, e.Value, candidates.ToArray()));
                }

                OnLogMessage($"🗳 {LocalizationManager.GetString("LogVotingStart", items.Count)}");
                VotingStatusText?.Invoke($"🗳 {LocalizationManager.GetString("VoteVotingProgress", items.Count)}");

                List<VotingResult> results;
                try
                {
                    results = await _evaluator.VoteBatchAsync(items, targetLang, context);
                }
                catch (Exception ex)
                {
                    OnLogMessage($"❌ {LocalizationManager.GetString("TranslationError", ex.Message)}");
                    VotingCompleted?.Invoke(new VotingOutcome { Failed = true });
                    return;
                }

                var completed = 0;
                var bestCount = 0;
                var needsReview = new List<VotingResult>();
                foreach (var vr in results)
                {
                    completed++;
                    var match = Entries.FirstOrDefault(en => en.Key == vr.EntryKey);
                    if (vr.BestTranslation == (match?.Translation ?? ""))
                    {
                        bestCount++;
                        continue;
                    }
                    if (match != null && !string.IsNullOrEmpty(vr.BestTranslation))
                        needsReview.Add(vr);
                }

                if (needsReview.Count > 0)
                    OnLogMessage($"🤝 {LocalizationManager.GetString("VoteNeedsReview", needsReview.Count)}");

                // 不自动覆盖译文：需人工确认的条目由 UI 弹出候选对比窗口，用户选定后调用 ApplyVotingSelections
                VotingCompleted?.Invoke(new VotingOutcome
                {
                    Completed = completed,
                    BestCount = bestCount,
                    NeedsReview = needsReview,
                    Results = results
                });
                return;
            }

            // Single entry voting
            var entry = entries.First();
            OnLogMessage($"🗳 {LocalizationManager.GetString("LogVoting", entry.Key)}");
            VotingStatusText?.Invoke($"⏳ {LocalizationManager.GetString("EvalVoting")}");

            var result = await VoteEntry(entry);

            if (result == null)
            {
                VotingCompleted?.Invoke(new VotingOutcome { Failed = true });
                return;
            }

            VotingCompleted?.Invoke(new VotingOutcome { SingleResult = result, HasSingleResult = true });
        }

        // ═══════════════════════════════════════════════════════════
        //  Smart pre-translate + consistency scan (moved from MainWindow)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Smart Pre-translate: fill translations from glossary and cache without API calls.
        /// </summary>
        public void SmartPreTranslate(List<LocalizationEntry> selected)
        {
            var entries = selected?.ToList() ?? new List<LocalizationEntry>();
            if (entries.Count == 0)
                entries = Entries.Where(en => !string.IsNullOrEmpty(en.Value)).ToList();

            if (entries.Count == 0)
            {
                PreTranslateCompleted?.Invoke(null);
                return;
            }

            var glossaryFilled = 0;
            var cacheFilled = 0;

            // Record undo snapshot before mutating translations
            var toFill = entries.Where(en => string.IsNullOrEmpty(en.Translation)).ToList();
            PushUndoSnapshot(toFill);

            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Translation))
                    continue;

                // Try glossary first
                if (_glossary.TryGetValue(entry.Key, out var dictVal))
                {
                    entry.Translation = dictVal;
                    glossaryFilled++;
                    continue;
                }
                if (_glossary.TryGetValue(entry.Value, out dictVal))
                {
                    entry.Translation = dictVal;
                    glossaryFilled++;
                    continue;
                }

                // Try cache
                var cacheKey = _configService.GetCacheKey(entry.Value);
                if (cacheKey != null && _configService.Cache.TryGetValue(cacheKey, out var cached))
                {
                    entry.Translation = cached;
                    cacheFilled++;
                }
            }

            PreTranslateCompleted?.Invoke(new PreTranslateOutcome { GlossaryFilled = glossaryFilled, CacheFilled = cacheFilled });
        }

        /// <summary>Consistency scan: check same source text translated differently.</summary>
        public List<ConsistencyIssue> ScanConsistencyIssues()
        {
            var issues = new List<ConsistencyIssue>();
            var groups = Entries
                .Where(en => !string.IsNullOrEmpty(en.Value) && !string.IsNullOrEmpty(en.Translation))
                .GroupBy(en => en.Value)
                .Where(g => g.Select(en => en.Translation).Distinct().Count() > 1);

            foreach (var group in groups)
            {
                issues.Add(new ConsistencyIssue
                {
                    Source = group.Key,
                    Translations = group.Select(en => en.Translation).Distinct().ToList(),
                    Keys = group.Select(en => en.Key).Distinct().ToList()
                });
            }

            return issues;
        }

        // ═══════════════════════════════════════════════════════════
        //  Evaluation / voting primitives (single-entry)
        // ═══════════════════════════════════════════════════════════

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
                var context = GetEvaluationContext();
                var result = await _evaluator.EvaluateAsync(entry.Value, entry.Translation, targetLang, context);
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
        /// Generates AI candidate alternatives first, then votes (candidate generation + context).
        /// </summary>
        public async Task<VotingResult> VoteEntry(LocalizationEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Value))
                return null;

            IsEvaluating = true;
            try
            {
                var targetLang = _aiTranslationService.TargetLanguage;
                var context = GetEvaluationContext();

                // Build candidate set: current translation + AI-generated alternatives
                var candidates = new List<string>();
                if (!string.IsNullOrEmpty(entry.Translation))
                    candidates.Add(entry.Translation);

                var generated = await _evaluator.GenerateCandidatesAsync(entry.Value, targetLang, context, 2);
                foreach (var g in generated)
                {
                    if (!string.IsNullOrEmpty(g) && !candidates.Contains(g))
                        candidates.Add(g);
                }
                if (candidates.Count == 0)
                    candidates.Add(entry.Value);

                var result = await _evaluator.VoteAsync(entry.Value, candidates.ToArray(), targetLang, context);
                LastEvaluationResult = result.ConsensusSummary;
                OnLogMessage($"🗳 Vote: {entry.Key} → {result.ConsensusSummary}");

                // 不在此处自动应用：若 AI 建议的译文与当前不同，由 UI 弹出候选对比窗口供用户选择
                return result;
            }
            finally
            {
                IsEvaluating = false;
            }
        }

        /// <summary>
        /// 应用用户在投票候选确认窗口中选定的译文（key → 选中的译文文本）。
        /// 值为空或与当前译文相同时跳过。
        /// </summary>
        public int ApplyVotingSelections(Dictionary<string, string> selections)
        {
            if (selections == null || selections.Count == 0)
                return 0;

            var applied = 0;
            foreach (var pair in selections)
            {
                var match = Entries.FirstOrDefault(en => en.Key == pair.Key);
                if (match == null || string.IsNullOrEmpty(pair.Value))
                    continue;
                if (match.Translation == pair.Value)
                    continue;

                PushUndoSnapshot(new[] { match });
                match.Translation = pair.Value;
                applied++;
            }

            if (applied > 0)
                OnLogMessage($"✅ {LocalizationManager.GetString("VoteAppliedBest", applied)}");
            return applied;
        }

        /// <summary>
        /// Builds evaluation/voting context from the active expert profile.
        /// </summary>
        private string GetEvaluationContext()
        {
            try
            {
                var profile = _profileManager.ActiveProfile;
                if (profile != null)
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(profile.Description))
                        parts.Add(profile.Description);
                    if (!string.IsNullOrEmpty(profile.Context))
                        parts.Add(profile.Context);
                    if (parts.Count > 0)
                        return string.Join("\n", parts);
                }
            }
            catch { /* profile manager may be uninitialized; fall through to empty context */ }

            return "";
        }
    }
}
