using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ForgeHash.Analysis;
using Microsoft.Win32;
using ForgeHashParameters = global::ForgeHash.ForgeHashParameters;

namespace Forgeh.CollisionLab;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> _log = [];
    private CancellationTokenSource? _cts;
    private CollisionCampaignResult? _lastResult;
    private bool _suppressPresetEvent;

    private static readonly (string Label, CollisionCampaignKind Kind)[] Campaigns =
    [
        ("Distinct passwords (fixed salt)", CollisionCampaignKind.DistinctPasswords),
        ("Distinct salts (fixed password)", CollisionCampaignKind.DistinctSalts),
        ("Random password + salt pairs", CollisionCampaignKind.RandomPairs),
        ("Nearby password bit-flips", CollisionCampaignKind.NearbyPasswordBitFlips),
        ("Distinct parameter sets", CollisionCampaignKind.DistinctParameterSets),
        ("Truncated 16-byte outputs", CollisionCampaignKind.TruncatedOutputs),
    ];

    public MainWindow()
    {
        InitializeComponent();
        LogList.ItemsSource = _log;

        foreach ((string label, _) in Campaigns)
        {
            CampaignCombo.Items.Add(label);
        }

        CampaignCombo.SelectedIndex = 2; // random pairs — mass-hunt default

        _suppressPresetEvent = true;
        PresetCombo.Items.Add("Development");
        PresetCombo.Items.Add("Interactive");
        PresetCombo.Items.Add("Custom");
        PresetCombo.SelectedIndex = 0;
        _suppressPresetEvent = false;
        ApplyPreset("Development");

        AppendLog("Ready. Pick a campaign and press Start.");
    }

    private void PresetCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressPresetEvent || PresetCombo.SelectedItem is not string name)
        {
            return;
        }

        if (name is "Development" or "Interactive")
        {
            ApplyPreset(name);
        }
    }

    private void ApplyPreset(string name)
    {
        ForgeHashParameters p = name switch
        {
            "Interactive" => ForgeHashParameters.Interactive,
            _ => ForgeHashParameters.Development,
        };
        MemoryBox.Text = p.MemoryKiB.ToString();
        IterationsBox.Text = p.Iterations.ToString();
        ParallelismBox.Text = p.Parallelism.ToString();
        OutputLengthBox.Text = p.OutputLength.ToString();
        UpdateCostWarning(p.MemoryKiB);
    }

    private void UpdateCostWarning(int memoryKiB)
    {
        if (memoryKiB > ForgeHashParameters.MinimumMemoryKiB)
        {
            WarningBanner.Text =
                $"Warning: memory is {memoryKiB} KiB. Mass campaigns will be very slow. Prefer Development (8192 KiB) unless you mean it.";
        }
        else
        {
            WarningBanner.Text =
                "Tip: use the Development profile (8 MiB) for mass runs. Larger profiles are extremely slow.";
        }
    }

    private async void StartButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_cts is not null)
        {
            return;
        }

        if (!TryReadInputs(out CollisionCampaignKind kind, out int samples, out ForgeHashParameters parameters, out int? rngSeed, out string error))
        {
            MessageBox.Show(this, error, "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdateCostWarning(parameters.MemoryKiB);
        if (parameters.MemoryKiB > 8192 && samples >= 100)
        {
            MessageBoxResult confirm = MessageBox.Show(
                this,
                $"Running {samples} samples at {parameters.MemoryKiB} KiB will take a long time.\n\nContinue?",
                "Slow campaign",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _cts = new CancellationTokenSource();
        _lastResult = null;
        SetRunning(true);
        AppendLog($"Starting {kind} · N={samples} · m={parameters.MemoryKiB},t={parameters.Iterations},p={parameters.Parallelism},out={parameters.OutputLength}");

        var progress = new Progress<CollisionProgress>(OnProgress);
        CancellationToken token = _cts.Token;
        bool stopOnFirst = StopOnFirstCheck.IsChecked == true;

        try
        {
            CollisionCampaignResult result = await Task.Run(
                () => CollisionCampaign.Run(
                    kind,
                    samples,
                    parameters,
                    stopOnFirstCollision: stopOnFirst,
                    rngSeed: rngSeed,
                    progress: progress,
                    cancellationToken: token),
                token);

            _lastResult = result;
            OnProgress(new CollisionProgress(
                result.CompletedSamples,
                result.RequestedSamples,
                result.CollisionCount,
                result.HashesPerSecond,
                result.Elapsed,
                null,
                result.StopReason.ToString()));

            foreach (CollisionHit hit in result.Collisions)
            {
                AppendLog(
                    $"COLLISION [{hit.Channel}] sample {hit.SampleIndex} == prior {hit.PriorSampleIndex}: {hit.DigestHex}",
                    isHit: true);
            }

            AppendLog(
                $"Done · {result.StopReason} · collisions={result.CollisionCount} · unique={result.UniqueDigests} · {result.HashesPerSecond:F2} H/s · {result.Elapsed.TotalSeconds:F1}s");
            StatusText.Text = result.CollisionCount == 0
                ? "Finished with no collisions in this sample set."
                : $"Finished with {result.CollisionCount} collision(s).";
            ExportJsonButton.IsEnabled = true;
            ExportCsvButton.IsEnabled = true;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Cancelled.";
            AppendLog("Cancelled.");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Failed.";
            AppendLog("Error: " + ex.Message, isHit: true);
            MessageBox.Show(this, ex.Message, "Campaign failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetRunning(false);
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        StatusText.Text = "Cancelling…";
    }

    private void ClearButton_OnClick(object sender, RoutedEventArgs e)
    {
        _log.Clear();
        _lastResult = null;
        ExportJsonButton.IsEnabled = false;
        ExportCsvButton.IsEnabled = false;
        RunProgress.Value = 0;
        CompletedText.Text = "0 / 0";
        RateText.Text = "—";
        ElapsedText.Text = "0.0s";
        CollisionsText.Text = "0";
        StatusText.Text = "Idle.";
        AppendLog("Cleared.");
    }

    private void ExportJsonButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = $"forgeh-collision-{_lastResult.Kind}-{DateTime.Now:yyyyMMdd-HHmmss}.json",
        };
        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, _lastResult.ToJson());
            AppendLog("Wrote " + dialog.FileName);
        }
    }

    private void ExportCsvButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"forgeh-collision-{_lastResult.Kind}-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
        };
        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, _lastResult.ToCsv());
            AppendLog("Wrote " + dialog.FileName);
        }
    }

    private void OnProgress(CollisionProgress p)
    {
        double pct = p.Total <= 0 ? 0 : 100.0 * p.Completed / p.Total;
        RunProgress.Value = Math.Clamp(pct, 0, 100);
        CompletedText.Text = $"{p.Completed} / {p.Total}";
        RateText.Text = p.HashesPerSecond.ToString("F2");
        ElapsedText.Text = $"{p.Elapsed.TotalSeconds:F1}s";
        CollisionsText.Text = p.CollisionCount.ToString();
        StatusText.Text = p.LatestMessage ?? $"Running… last={Truncate(p.LastDigestHex, 16)}";
    }

    private bool TryReadInputs(
        out CollisionCampaignKind kind,
        out int samples,
        out ForgeHashParameters parameters,
        out int? rngSeed,
        out string error)
    {
        kind = default;
        samples = 0;
        parameters = ForgeHashParameters.Development;
        rngSeed = null;
        error = string.Empty;

        int idx = CampaignCombo.SelectedIndex;
        if (idx < 0 || idx >= Campaigns.Length)
        {
            error = "Select a campaign.";
            return false;
        }

        kind = Campaigns[idx].Kind;

        if (kind == CollisionCampaignKind.DistinctParameterSets)
        {
            samples = 6;
        }
        else if (!int.TryParse(SampleCountBox.Text.Trim(), out samples) || samples < 1)
        {
            error = "Samples (N) must be a positive integer.";
            return false;
        }

        if (!int.TryParse(MemoryBox.Text.Trim(), out int memory) ||
            !int.TryParse(IterationsBox.Text.Trim(), out int iterations) ||
            !int.TryParse(ParallelismBox.Text.Trim(), out int parallelism) ||
            !int.TryParse(OutputLengthBox.Text.Trim(), out int outputLength))
        {
            error = "Memory, iterations, parallelism, and output length must be integers.";
            return false;
        }

        parameters = new ForgeHashParameters
        {
            MemoryKiB = memory,
            Iterations = iterations,
            Parallelism = parallelism,
            OutputLength = outputLength,
            SaltLength = 16,
        };

        string seedText = RngSeedBox.Text.Trim();
        if (seedText.Length > 0)
        {
            if (!int.TryParse(seedText, out int seed))
            {
                error = "RNG seed must be an integer or blank.";
                return false;
            }

            rngSeed = seed;
        }

        // Mark custom if values diverge from presets.
        if (PresetCombo.SelectedItem is string preset &&
            preset != "Custom" &&
            !MatchesPreset(preset, parameters))
        {
            _suppressPresetEvent = true;
            PresetCombo.SelectedItem = "Custom";
            _suppressPresetEvent = false;
        }

        return true;
    }

    private static bool MatchesPreset(string name, ForgeHashParameters p)
    {
        ForgeHashParameters refParams = name == "Interactive"
            ? ForgeHashParameters.Interactive
            : ForgeHashParameters.Development;
        return p.MemoryKiB == refParams.MemoryKiB
               && p.Iterations == refParams.Iterations
               && p.Parallelism == refParams.Parallelism
               && p.OutputLength == refParams.OutputLength;
    }

    private void SetRunning(bool running)
    {
        StartButton.IsEnabled = !running;
        CancelButton.IsEnabled = running;
        CampaignCombo.IsEnabled = !running;
        SampleCountBox.IsEnabled = !running;
        PresetCombo.IsEnabled = !running;
        MemoryBox.IsEnabled = !running;
        IterationsBox.IsEnabled = !running;
        ParallelismBox.IsEnabled = !running;
        OutputLengthBox.IsEnabled = !running;
        RngSeedBox.IsEnabled = !running;
        StopOnFirstCheck.IsEnabled = !running;
    }

    private void AppendLog(string line, bool isHit = false)
    {
        string stamped = $"[{DateTime.Now:HH:mm:ss}] {(isHit ? "!! " : "")}{line}";
        _log.Add(stamped);
        if (_log.Count > 5000)
        {
            _log.RemoveAt(0);
        }

        if (LogList.Items.Count > 0)
        {
            LogList.ScrollIntoView(LogList.Items[^1]);
        }

        if (isHit)
        {
            CollisionsText.Foreground = (Brush)FindResource("HitBrush");
        }
    }

    private static string Truncate(string? value, int chars)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "—";
        }

        return value.Length <= chars ? value : value[..chars] + "…";
    }
}
