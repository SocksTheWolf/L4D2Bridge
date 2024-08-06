using System;

namespace L4D2Bridge.Utils
{
    public static class RandomExtension
    {
        public static bool NextBool(this Random rng)
        {
            return (rng.Next(0, 2) == 1) ? true : false;
        }
    }
}
