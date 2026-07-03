using System.Runtime.CompilerServices;

// 테스트가 내부 시임(InkEffectResources의 폴백 강제/경고 초기화)에 접근하기 위한 공개.
[assembly: InternalsVisibleTo("HwatuCore.Tests")]
[assembly: InternalsVisibleTo("HwatuView.Tests")]
