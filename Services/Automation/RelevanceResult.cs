namespace MITANZ360Pro.Web.Services.Automation
{
    public class RelevanceResult
    {
        public bool IsRelevant { get; set; }

        public int Score { get; set; }

        public string Reason { get; set; } = string.Empty;

        // ✅ REQUIRED (fixes your errors)
        public List<string> MatchedKeywords { get; set; } = new();

        public List<string> MissingKeywords { get; set; } = new();
    }
}