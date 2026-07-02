using System;
using System.Collections.Generic;

namespace Hwatu.Core
{
    public sealed class GameRng
    {
        private readonly Random _random;

        public GameRng(int seed)
        {
            _random = new Random(seed);
        }

        public int Next(int max) => _random.Next(max);

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
