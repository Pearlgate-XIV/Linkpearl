using System.Globalization;

namespace Linkpearl.Applets.Life.Calculator;

// Plain four-function state machine: no expression tree, evaluates left-to-right one operation
// at a time, exactly like a physical calculator does. Deliberately not a parser.
internal sealed class CalculatorState
{
    private double accumulator;
    private char? pendingOperator;
    private bool entryFresh = true;
    private bool errored;

    public string Display { get; private set; } = "0";

    public void PressDigit(char digit)
    {
        if (errored)
        {
            Clear();
        }

        if (entryFresh || Display == "0")
        {
            Display = digit.ToString();
        }
        else
        {
            Display += digit;
        }

        entryFresh = false;
    }

    public void PressDecimal()
    {
        if (errored)
        {
            Clear();
        }

        if (entryFresh)
        {
            Display = "0.";
            entryFresh = false;
            return;
        }

        if (!Display.Contains('.', StringComparison.Ordinal))
        {
            Display += ".";
        }
    }

    public void PressToggleSign()
    {
        if (errored || !double.TryParse(Display, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        Display = FormatNumber(-value);
    }

    public void PressPercent()
    {
        if (errored || !double.TryParse(Display, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        Display = FormatNumber(value / 100.0);
        entryFresh = true;
    }

    public void PressOperator(char operatorSymbol)
    {
        if (errored)
        {
            return;
        }

        var current = ParseDisplay();
        if (pendingOperator is { } pending && !entryFresh)
        {
            if (!TryApply(accumulator, pending, current, out accumulator))
            {
                Fail();
                return;
            }

            Display = FormatNumber(accumulator);
        }
        else
        {
            accumulator = current;
        }

        pendingOperator = operatorSymbol;
        entryFresh = true;
    }

    public void PressEquals()
    {
        if (errored || pendingOperator is not { } pending)
        {
            return;
        }

        var current = ParseDisplay();
        if (!TryApply(accumulator, pending, current, out var result))
        {
            Fail();
            return;
        }

        Display = FormatNumber(result);
        accumulator = result;
        pendingOperator = null;
        entryFresh = true;
    }

    public void Clear()
    {
        Display = "0";
        accumulator = 0.0;
        pendingOperator = null;
        entryFresh = true;
        errored = false;
    }

    private double ParseDisplay() =>
        double.TryParse(Display, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0;

    private void Fail()
    {
        Display = "Error";
        accumulator = 0.0;
        pendingOperator = null;
        entryFresh = true;
        errored = true;
    }

    private static bool TryApply(double left, char operatorSymbol, double right, out double result)
    {
        switch (operatorSymbol)
        {
            case '+':
                result = left + right;
                return true;
            case '-':
                result = left - right;
                return true;
            case '×':
                result = left * right;
                return true;
            case '÷':
                if (right == 0.0)
                {
                    result = 0.0;
                    return false;
                }

                result = left / right;
                return true;
            default:
                result = right;
                return true;
        }
    }

    private static string FormatNumber(double value) =>
        double.IsFinite(value) ? value.ToString("0.##########", CultureInfo.InvariantCulture) : "Error";
}
