namespace Hwatu.Core
{
    /// <summary>
    /// 판 단위 설정. 코드 기본값 + 주입 가능 (파일 외부화는 4단계).
    /// </summary>
    public sealed class RoundConfig
    {
        public int TargetScore { get; set; } = 5;
        public int HandSize { get; set; } = 10;
        public int FloorSize { get; set; } = 8;
    }
}
