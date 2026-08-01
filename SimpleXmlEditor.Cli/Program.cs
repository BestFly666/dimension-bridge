using System.Text.Json;
using SimpleXmlEditor.Services;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.ExpertProfiles;

namespace SimpleXmlEditor.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        try
        {
            var command = args[0].ToLower();
            return command switch
            {
                "translate" => await TranslateCommand(args[1..]),
                "batch" => await BatchCommand(args[1..]),
                "export-tmx" => await ExportTmxCommand(args[1..]),
                "validate" => await ValidateCommand(args[1..]),
                "help" or "--help" or "-h" => PrintHelpAndReturn(),
                _ => HandleUnknown(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    static async Task<int> TranslateCommand(string[] args)
    {
        var inputPath = GetArg(args, 0, "-i", "--input");
        var outputPath = GetArg(args, 1, "-o", "--output");
        var apiKey = GetArg(args, 2, "-k", "--api-key");
        var provider = GetArg(args, 3, "-p", "--provider") ?? "GoogleGemini";

        if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
        {
            Console.Error.WriteLine("Error: Input file not found. Use -i <path>");
            return 1;
        }

        Console.WriteLine($"Loading: {inputPath}");

        var configService = new ConfigService();
        var glossary = new GlossaryManager();
        var profile = new ExpertProfileManager();
        var aiService = new AiTranslationService
        {
            CurrentProvider = Enum.Parse<AIProvider>(provider),
            TargetLanguage = "Chinese"
        };
        if (!string.IsNullOrEmpty(apiKey))
        {
            aiService.ApiKey = apiKey;
            configService.SetApiKey(apiKey);
        }

        var xmlRepo = new XmlRepository();
        var orchestrator = new TranslationOrchestrator(aiService, configService, glossary, profile, msg => Console.WriteLine(msg));

        var entries = xmlRepo.LoadXml(inputPath);
        Console.WriteLine($"Loaded {entries.Count} entries");

        // Translate only untranslated entries
        var untranslated = entries.Where(e => !string.IsNullOrEmpty(e.Value) && string.IsNullOrEmpty(e.Translation)).ToList();
        Console.WriteLine($"Translating {untranslated.Count} entries...");

        var batches = orchestrator.CreateBatches(untranslated, "", 50);
        var successCount = 0;
        var failCount = 0;

        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            Console.Write($"\rBatch {i + 1}/{batches.Count}: {batch.Count} entries...");

            var results = await orchestrator.TranslateBatchAsync(batch, false, "");

            foreach (var entry in batch)
            {
                if (results.ContainsKey(entry.Value))
                {
                    entry.Translation = results[entry.Value];
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }
        }

        Console.WriteLine();

        outputPath ??= Path.ChangeExtension(inputPath, ".translated.xml");
        xmlRepo.SaveXml(outputPath, entries);
        Console.WriteLine($"Saved to: {outputPath}");
        Console.WriteLine($"Done: {successCount} success, {failCount} failed");

        return failCount > 0 ? 1 : 0;
    }

    static async Task<int> BatchCommand(string[] args)
    {
        var dir = GetArg(args, 0, "-d", "--dir");
        var provider = GetArg(args, 1, "-p", "--provider") ?? "GoogleGemini";
        var apiKey = GetArg(args, 2, "-k", "--api-key");

        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            Console.Error.WriteLine("Error: Directory not found. Use -d <path>");
            return 1;
        }

        var xmlFiles = Directory.GetFiles(dir, "*.xml");
        Console.WriteLine($"Found {xmlFiles.Length} XML files in {dir}");

        foreach (var file in xmlFiles)
        {
            Console.WriteLine($"\nProcessing: {Path.GetFileName(file)}");
            var exitCode = await TranslateCommand(new[] { "-i", file, "-p", provider, "-k", apiKey ?? "" });
            if (exitCode != 0)
                Console.WriteLine($"  Warning: translation had failures");
        }

        Console.WriteLine("\nBatch complete.");
        return 0;
    }

    static Task<int> ExportTmxCommand(string[] args)
    {
        var inputPath = GetArg(args, 0, "-i", "--input");
        var outputPath = GetArg(args, 1, "-o", "--output");

        if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
        {
            Console.Error.WriteLine("Error: Input file not found. Use -i <path>");
            return Task.FromResult(1);
        }

        outputPath ??= Path.ChangeExtension(inputPath, ".tmx");

        var xmlRepo = new XmlRepository();
        var entries = xmlRepo.LoadXml(inputPath);

        var translated = entries.Where(e => !string.IsNullOrEmpty(e.Translation)).ToList();
        Console.WriteLine($"Exporting {translated.Count} translations to TMX...");

        using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);
        writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        writer.WriteLine("<tmx version=\"1.4\">");
        writer.WriteLine("  <header creationtool=\"XmlAiTranslator\" srclang=\"en\" datatype=\"plaintext\"/>");
        writer.WriteLine("  <body>");

        foreach (var entry in translated)
        {
            writer.WriteLine("    <tu>");
            writer.WriteLine($"      <tuv xml:lang=\"en\"><seg>{XmlEncode(entry.Value)}</seg></tuv>");
            writer.WriteLine($"      <tuv xml:lang=\"zh\"><seg>{XmlEncode(entry.Translation)}</seg></tuv>");
            writer.WriteLine("    </tu>");
        }

        writer.WriteLine("  </body>");
        writer.WriteLine("</tmx>");

        Console.WriteLine($"Exported to: {outputPath}");
        return Task.FromResult(0);
    }

    static Task<int> ValidateCommand(string[] args)
    {
        var inputPath = GetArg(args, 0, "-i", "--input");

        if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
        {
            Console.Error.WriteLine("Error: Input file not found. Use -i <path>");
            return Task.FromResult(1);
        }

        var xmlRepo2 = new XmlRepository();
        var entries2 = xmlRepo2.LoadXml(inputPath);

        var issues = new List<string>();

        // Check empty keys
        var emptyKeys = entries2.Where(e => string.IsNullOrEmpty(e.Key)).ToList();
        if (emptyKeys.Any())
            issues.Add($"Empty keys: {emptyKeys.Count}");

        // Check empty original text
        var emptyValues2 = entries2.Where(e => string.IsNullOrEmpty(e.Value) && !string.IsNullOrEmpty(e.Key)).ToList();
        if (emptyValues2.Any())
            issues.Add($"Empty original text: {emptyValues2.Count}");

        // Check untranslated
        var untranslated2 = entries2.Where(e => !string.IsNullOrEmpty(e.Value) && string.IsNullOrEmpty(e.Translation)).ToList();
        if (untranslated2.Any())
            issues.Add($"Untranslated: {untranslated2.Count}");

        // Check duplicate keys
        var dupKeys2 = entries2.GroupBy(e => e.Key).Where(g => g.Count() > 1).ToList();
        if (dupKeys2.Any())
            issues.Add($"Duplicate keys: {dupKeys2.Count}");

        Console.WriteLine($"Validated: {entries2.Count} entries");
        if (issues.Any())
        {
            Console.WriteLine("Issues found:");
            foreach (var issue in issues)
                Console.WriteLine($"  - {issue}");
            return Task.FromResult(1);
        }

        Console.WriteLine("All entries valid.");
        return Task.FromResult(0);
    }

    static int PrintHelpAndReturn()
    {
        PrintHelp();
        return 0;
    }

    static int HandleUnknown(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 1;
    }

    static void PrintHelp()
    {
        Console.WriteLine(@"
XML AI Translator - CLI

Commands:
  translate   Translate a single XML file
  batch       Batch translate all XML files in a directory
  export-tmx  Export translations as TMX format
  validate    Validate XML file structure

Usage:
  XmlAiTranslator translate -i <input.xml> [-o <output.xml>] [-p <provider>] [-k <api-key>]
  XmlAiTranslator batch -d <directory> [-p <provider>] [-k <api-key>]
  XmlAiTranslator export-tmx -i <input.xml> [-o <output.tmx>]
  XmlAiTranslator validate -i <input.xml>

Options:
  -i, --input     Input XML file path
  -o, --output    Output file path
  -d, --dir       Directory containing XML files
  -p, --provider  AI provider (GoogleGemini, DeepSeek, OpenAI, etc.)
  -k, --api-key   API key for the AI provider
");
    }

    static string? GetArg(string[] args, int positionalIndex, string shortFlag, string longFlag)
    {
        // Try named argument first
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == shortFlag || args[i] == longFlag)
                return args[i + 1];
        }
        // Fall back to positional
        return positionalIndex < args.Length ? args[positionalIndex] : null;
    }

    static string XmlEncode(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text ?? "");
    }
}
