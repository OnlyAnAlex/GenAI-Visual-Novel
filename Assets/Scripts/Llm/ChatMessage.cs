namespace VnNpc
{
    public readonly struct ChatMessage
    {
        public const string SystemRole = "system";
        public const string UserRole = "user";
        public const string AssistantRole = "assistant";

        public readonly string Role;
        public readonly string Content;

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
