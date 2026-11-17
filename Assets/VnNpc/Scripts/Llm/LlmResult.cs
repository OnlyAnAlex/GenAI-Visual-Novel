namespace VnNpc
{
    public enum LlmFailure
    {
        None,
        Configuration,
        Network,
        Auth,
        Credits,
        BadRequest,
        Moderation,
        Refused,
        RateLimited,
        Server,
        Truncated,
        BadResponse
    }

    public static class LlmFailureExtensions
    {
        /// <summary>Falhas que podem passar sozinhas, então vale repetir a mesma requisição.</summary>
        public static bool IsTransient(this LlmFailure failure) =>
            failure == LlmFailure.Network || failure == LlmFailure.RateLimited || failure == LlmFailure.Server;

        /// <summary>Filtro ou recusa causados pelo que o jogador escreveu. Não é defeito a mostrar na interface.</summary>
        public static bool IsContentRelated(this LlmFailure failure) =>
            failure == LlmFailure.Moderation || failure == LlmFailure.Refused;
    }

    public readonly struct LlmResult
    {
        public readonly LlmFailure Failure;
        public readonly string Content;
        public readonly string Detail;
        public readonly float RetryAfterSeconds;

        private LlmResult(LlmFailure failure, string content, string detail, float retryAfterSeconds)
        {
            Failure = failure;
            Content = content;
            Detail = detail;
            RetryAfterSeconds = retryAfterSeconds;
        }

        public bool Succeeded => Failure == LlmFailure.None;

        public static LlmResult Success(string content) => new LlmResult(LlmFailure.None, content, null, 0f);

        public static LlmResult Fail(LlmFailure failure, string detail, float retryAfterSeconds = 0f) =>
            new LlmResult(failure, null, detail, retryAfterSeconds);

        public LlmResult WithRetryAfter(float seconds) => new LlmResult(Failure, Content, Detail, seconds);
    }
}
