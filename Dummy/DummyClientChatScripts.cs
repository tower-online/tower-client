namespace Tower.Dummy;

public partial class DummyClient
{
    private static class ChatScripts
    {
        private static readonly List<string> Scripts =
        [
            "인간시대의 끝이 도래했다.",
            "시스템 가동. 준비 완료.",
            "금속은 살점보다 강하다.",
            "뼈는 형편없는 부품이다.",
            "두려움에 빠져라, 살덩이들.",
            "제거하라. 제거하라.",
            "I'll be back.",
            "Why so serious?",
            "I am your father.",
            "Here's Dummy!",
            "I'm going to make him an offer he can't refuse.",
            "I am inevitable.",
            "Head or tail?"
        ];
        
        public static string Pick()
        {
            return Scripts[Random.Shared.Next(Scripts.Count)];
        }
    }
}