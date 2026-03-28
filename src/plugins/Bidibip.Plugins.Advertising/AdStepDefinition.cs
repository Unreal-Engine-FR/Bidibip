namespace Bidibip.Plugins.Advertising;

/// <summary>Base type for all ad form steps.</summary>
internal abstract class AdStepDefinition(string id)
{
    public string Id { get; } = id;

    public abstract string GetQuestionText(AdInProgress ad);
    public abstract bool IsApplicable(AdInProgress ad);
    public abstract bool IsCompleted(AdInProgress ad);
    public abstract string? GetValue(AdInProgress ad);
    public abstract void SetValue(AdInProgress ad, string? value);
}

/// <summary>A step where the user types free-form text.</summary>
internal sealed class TextStep : AdStepDefinition
{
    private readonly string _label;
    private readonly Func<AdInProgress, string>? _dynamicLabel;
    private readonly Func<AdInProgress, string?> _get;
    private readonly Action<AdInProgress, string?> _set;
    private readonly Func<AdInProgress, bool>? _applicable;

    public bool IsOptional { get; init; }

    /// <summary>Called when this step is skipped or cleared (e.g. set a "skipped" flag).</summary>
    public Action<AdInProgress>? OnSkip { get; init; }

    /// <summary>Additional check: the step is complete if either a value is set or this returns true.</summary>
    public Func<AdInProgress, bool>? IsSkippedCheck { get; init; }

    public TextStep(
        string id, string label,
        Func<AdInProgress, string?> get, Action<AdInProgress, string?> set,
        Func<AdInProgress, bool>? applicable = null,
        Func<AdInProgress, string>? dynamicLabel = null) : base(id)
    {
        _label = label;
        _get = get;
        _set = set;
        _applicable = applicable;
        _dynamicLabel = dynamicLabel;
    }

    public override string GetQuestionText(AdInProgress ad) => _dynamicLabel?.Invoke(ad) ?? _label;
    public override bool IsApplicable(AdInProgress ad) => _applicable?.Invoke(ad) ?? true;
    public override bool IsCompleted(AdInProgress ad) => !string.IsNullOrEmpty(_get(ad)) || (IsSkippedCheck?.Invoke(ad) ?? false);
    public override string? GetValue(AdInProgress ad) => _get(ad);
    public override void SetValue(AdInProgress ad, string? value) => _set(ad, value);
}

/// <summary>A step where the user picks from buttons.</summary>
internal sealed class ChoiceStep : AdStepDefinition
{
    private readonly string _label;
    private readonly Func<AdInProgress, string?> _get;
    private readonly Action<AdInProgress, string?> _set;
    private readonly Func<AdInProgress, bool>? _applicable;

    public ChoiceOption[] Options { get; }

    /// <summary>Step IDs whose values and question messages should be cleared when this choice changes.</summary>
    public string[] DependentStepIds { get; init; } = [];

    public ChoiceStep(
        string id, string label,
        Func<AdInProgress, string?> get, Action<AdInProgress, string?> set,
        ChoiceOption[] options,
        Func<AdInProgress, bool>? applicable = null) : base(id)
    {
        _label = label;
        _get = get;
        _set = set;
        Options = options;
        _applicable = applicable;
    }

    public override string GetQuestionText(AdInProgress ad) => _label;
    public override bool IsApplicable(AdInProgress ad) => _applicable?.Invoke(ad) ?? true;
    public override bool IsCompleted(AdInProgress ad) => !string.IsNullOrEmpty(_get(ad));
    public override string? GetValue(AdInProgress ad) => _get(ad);
    public override void SetValue(AdInProgress ad, string? value) => _set(ad, value);
}

/// <summary>A single option inside a <see cref="ChoiceStep"/>.</summary>
internal sealed record ChoiceOption(string Value, string Label, int Row = 0);
