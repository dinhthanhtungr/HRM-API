using System.Diagnostics;
using System.Globalization;

namespace HRM.Application.Features.Warehouse.Services;

internal sealed class WarehouseMovementQueryTiming(string operation = "wm") : IDisposable
{
    private readonly string _operation = operation is "wm" or "wj" ? operation : throw new ArgumentOutOfRangeException(nameof(operation));
    private readonly Activity? _activity = Activity.Current?.GetTagItem(WarehouseMovementDiagnosticTags.Enabled) is true ? Activity.Current : null;
    private readonly Stopwatch _total = Stopwatch.StartNew();
    private readonly List<string> _metrics = [];

    public void RecordScope() { if (_activity != null) Add("scope", _total.ElapsedMilliseconds); }

    public async Task<T> RunAsync<T>(string stage, Func<Task<T>> action)
    {
        if (_activity == null) return await action();
        var watch = Stopwatch.StartNew();
        try { return await action(); }
        finally { Add(stage, watch.ElapsedMilliseconds); }
    }

    public T Run<T>(string stage, Func<T> action)
    {
        if (_activity == null) return action();
        var watch = Stopwatch.StartNew();
        try { return action(); }
        finally { Add(stage, watch.ElapsedMilliseconds); }
    }

    private void Add(string stage, long elapsed)
    {
        if (WarehouseMovementDiagnosticTags.IsStage(_operation, stage))
            _metrics.Add(_operation + "_" + stage + ";dur=" + elapsed.ToString(CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        if (_activity == null) return;
        Add("handler", _total.ElapsedMilliseconds);
        _activity.SetTag(WarehouseMovementDiagnosticTags.ServerTiming, string.Join(", ", _metrics));
    }
}
