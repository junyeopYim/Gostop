namespace Hwatu.Run
{
    /// <summary>
    /// 난수 용도 스트림. 스트림이 다르면 같은 문맥 인덱스에서도 완전히 다른 시드가 나온다.
    /// 값을 명시해 두어 이후 항목이 추가·재배열되어도 기존 파생 결과가 변하지 않게 한다.
    /// </summary>
    public enum RngStream
    {
        MapGen = 1,
        DeckShuffle = 2,
        Shop = 3,
        Event = 4,
        FloorJitter = 5,
    }

    /// <summary>
    /// 무상태 난수 파생. 모든 난수는 (런시드, 용도 스트림, 문맥 인덱스)에서 그때그때
    /// 파생하며, 호출 순서에 의존하는 난수 상태를 절대 저장하지 않는다. 어떤 난수도
    /// "몇 번 뽑았는지"에 의존하지 않으므로 세이브/로드 후에도 결정론이 유지된다.
    ///
    /// 사용 규약:
    ///   딜 시드     = Derive(runSeed, RngStream.DeckShuffle, currentDay, dayAttempt)
    ///   상점 리롤   = Derive(runSeed, RngStream.Shop, day, rerollCount)
    ///   맵 생성     = Derive(runSeed, RngStream.MapGen)
    ///   이벤트 굴림 = Derive(runSeed, RngStream.Event, day, 이벤트 내 인덱스)
    ///
    /// 파생 인자로 문자열 해시(GetHashCode)는 금지 — 런타임 간 값이 불안정하다.
    /// </summary>
    public static class SeedDerivation
    {
        public static int Derive(int runSeed, RngStream stream, int a = 0, int b = 0)
        {
            uint h = 0x811C9DC5u; // 임의의 비영 초기 상태
            h = Absorb(h, (uint)runSeed);
            h = Absorb(h, (uint)stream);
            h = Absorb(h, (uint)a);
            h = Absorb(h, (uint)b);
            return (int)h;
        }

        /// <summary>
        /// splitmix32 스타일 정수 믹싱: 입력을 순서대로 흡수하며 매 단계 황금비 상수를
        /// 더하고 파이널라이저를 통과시킨다. 입력이 1비트만 달라도 결과가 눈사태처럼 바뀐다.
        /// </summary>
        private static uint Absorb(uint state, uint value)
        {
            uint z = state + value + 0x9E3779B9u;
            z ^= z >> 16;
            z *= 0x21F0AAADu;
            z ^= z >> 15;
            z *= 0x735A2D97u;
            z ^= z >> 15;
            return z;
        }
    }
}
