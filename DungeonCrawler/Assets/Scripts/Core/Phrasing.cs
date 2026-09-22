namespace DungeonCrawler.Core
{
    /// <summary>Small helpers for building readable log and summary lines.</summary>
    public static class Phrasing
    {
        /// <summary>"a goblin", "an orc brute" — the indefinite article for a monster name.</summary>
        public static string WithArticle(string noun)
        {
            if (string.IsNullOrEmpty(noun)) return noun;
            return Article(noun) + " " + noun;
        }

        public static string Article(string noun)
        {
            if (string.IsNullOrEmpty(noun)) return "a";
            char first = char.ToLowerInvariant(noun[0]);
            bool vowel = first == 'a' || first == 'e' || first == 'i' || first == 'o' || first == 'u';
            return vowel ? "an" : "a";
        }
    }
}
