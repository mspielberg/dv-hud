using System;

namespace DvMod.HeadsUpDisplay
{
    public static class NullableOps
    {
        public static U? Map<T, U>(this T? value, Func<T, U> f)
            where T : class
            where U : struct =>
            (value == null) ? null : (U?)f(value);

        public static U? FlatMap<T, U>(this T? value, Func<T, U?> f)
            where T : class
            where U : struct =>
            (value == null) ? null : f(value);

        public static T? When<T>(bool condition, T value)
            where T : struct =>
            condition ? (T?)value : null;
    }
}