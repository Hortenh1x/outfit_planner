namespace OutfitPlanner.Domain;

// Signals invalid user input or a violated domain rule that should map to HTTP 400, as opposed to
// an internal fault (which should surface as 500 with a trace id and no leaked detail). Derives
// from InvalidOperationException so existing callers and tests that catch InvalidOperationException
// keep working during the migration away from overloading it as a control-flow signal.
public sealed class ValidationException : InvalidOperationException
{
    public ValidationException(string message)
        : base(message)
    {
    }
}
