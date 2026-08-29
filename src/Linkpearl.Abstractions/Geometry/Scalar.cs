namespace Linkpearl.Geometry;

// Math.Clamp throws if min > max, including when float rounding inverts two "equal" bounds.
// Layout code that derives min/max independently needs a clamp that still returns a point
// inside the interval after swapping.
public static class Scalar
{
    public static float Clamp(float value, float min, float max)
    {
        var lo = min < max ? min : max;
        var hi = min < max ? max : min;
        if (value < lo)
        {
            return lo;
        }

        if (value > hi)
        {
            return hi;
        }

        return value;
    }
}
