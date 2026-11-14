using System.Collections.Generic;
using System.Text;

namespace CargoSort.Core
{
    /// <summary>
    /// Makes Persian text display correctly in Unity's legacy UI Text, which has no right-to-left or
    /// letter-joining support: picks the joined letter form (isolated, final, initial, medial) for each
    /// letter, then reverses the line while keeping numbers and Latin words left-to-right.
    /// </summary>
    public static class PersianShaper
    {
        // letter -> isolated, final, initial, medial (0 = this letter does not join to the next one)
        private static readonly Dictionary<char, char[]> Forms = new Dictionary<char, char[]>
        {
            { 'ء', new[] { 'ﺀ', 'ﺀ', '\0', '\0' } }, // ء
            { 'آ', new[] { 'ﺁ', 'ﺂ', '\0', '\0' } }, // آ
            { 'أ', new[] { 'ﺃ', 'ﺄ', '\0', '\0' } }, // أ
            { 'ؤ', new[] { 'ﺅ', 'ﺆ', '\0', '\0' } }, // ؤ
            { 'إ', new[] { 'ﺇ', 'ﺈ', '\0', '\0' } }, // إ
            { 'ئ', new[] { 'ﺉ', 'ﺊ', 'ﺋ', 'ﺌ' } }, // ئ
            { 'ا', new[] { 'ﺍ', 'ﺎ', '\0', '\0' } }, // ا
            { 'ب', new[] { 'ﺏ', 'ﺐ', 'ﺑ', 'ﺒ' } }, // ب
            { 'ة', new[] { 'ﺓ', 'ﺔ', '\0', '\0' } }, // ة
            { 'ت', new[] { 'ﺕ', 'ﺖ', 'ﺗ', 'ﺘ' } }, // ت
            { 'ث', new[] { 'ﺙ', 'ﺚ', 'ﺛ', 'ﺜ' } }, // ث
            { 'ج', new[] { 'ﺝ', 'ﺞ', 'ﺟ', 'ﺠ' } }, // ج
            { 'ح', new[] { 'ﺡ', 'ﺢ', 'ﺣ', 'ﺤ' } }, // ح
            { 'خ', new[] { 'ﺥ', 'ﺦ', 'ﺧ', 'ﺨ' } }, // خ
            { 'د', new[] { 'ﺩ', 'ﺪ', '\0', '\0' } }, // د
            { 'ذ', new[] { 'ﺫ', 'ﺬ', '\0', '\0' } }, // ذ
            { 'ر', new[] { 'ﺭ', 'ﺮ', '\0', '\0' } }, // ر
            { 'ز', new[] { 'ﺯ', 'ﺰ', '\0', '\0' } }, // ز
            { 'س', new[] { 'ﺱ', 'ﺲ', 'ﺳ', 'ﺴ' } }, // س
            { 'ش', new[] { 'ﺵ', 'ﺶ', 'ﺷ', 'ﺸ' } }, // ش
            { 'ص', new[] { 'ﺹ', 'ﺺ', 'ﺻ', 'ﺼ' } }, // ص
            { 'ض', new[] { 'ﺽ', 'ﺾ', 'ﺿ', 'ﻀ' } }, // ض
            { 'ط', new[] { 'ﻁ', 'ﻂ', 'ﻃ', 'ﻄ' } }, // ط
            { 'ظ', new[] { 'ﻅ', 'ﻆ', 'ﻇ', 'ﻈ' } }, // ظ
            { 'ع', new[] { 'ﻉ', 'ﻊ', 'ﻋ', 'ﻌ' } }, // ع
            { 'غ', new[] { 'ﻍ', 'ﻎ', 'ﻏ', 'ﻐ' } }, // غ
            { 'ف', new[] { 'ﻑ', 'ﻒ', 'ﻓ', 'ﻔ' } }, // ف
            { 'ق', new[] { 'ﻕ', 'ﻖ', 'ﻗ', 'ﻘ' } }, // ق
            { 'ك', new[] { 'ﻙ', 'ﻚ', 'ﻛ', 'ﻜ' } }, // ك
            { 'ل', new[] { 'ﻝ', 'ﻞ', 'ﻟ', 'ﻠ' } }, // ل
            { 'م', new[] { 'ﻡ', 'ﻢ', 'ﻣ', 'ﻤ' } }, // م
            { 'ن', new[] { 'ﻥ', 'ﻦ', 'ﻧ', 'ﻨ' } }, // ن
            { 'ه', new[] { 'ﻩ', 'ﻪ', 'ﻫ', 'ﻬ' } }, // ه
            { 'و', new[] { 'ﻭ', 'ﻮ', '\0', '\0' } }, // و
            { 'ي', new[] { 'ﻱ', 'ﻲ', 'ﻳ', 'ﻴ' } }, // ي
            { 'پ', new[] { 'ﭖ', 'ﭗ', 'ﭘ', 'ﭙ' } }, // پ
            { 'چ', new[] { 'ﭺ', 'ﭻ', 'ﭼ', 'ﭽ' } }, // چ
            { 'ژ', new[] { 'ﮊ', 'ﮋ', '\0', '\0' } }, // ژ
            { 'ک', new[] { 'ﮎ', 'ﮏ', 'ﮐ', 'ﮑ' } }, // ک
            { 'گ', new[] { 'ﮒ', 'ﮓ', 'ﮔ', 'ﮕ' } }, // گ
            { 'ی', new[] { 'ﯼ', 'ﯽ', 'ﯾ', 'ﯿ' } }, // ی
        };

        private const char ZeroWidthNonJoiner = '‌';

        public static bool HasRtl(string s)
        {
            foreach (char c in s)
                if (c >= '؀' && c <= 'ۿ') return true;
            return false;
        }

        /// <summary>Shapes and reorders each line. Text without Persian letters is returned unchanged.</summary>
        public static string Fix(string text)
        {
            if (string.IsNullOrEmpty(text) || !HasRtl(text)) return text;
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = Reorder(Shape(lines[i]));
            return string.Join("\n", lines);
        }

        private static bool JoinsNext(char c) => Forms.TryGetValue(c, out var f) && f[2] != '\0';

        public static string Shape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!Forms.TryGetValue(c, out var forms))
                {
                    if (c != ZeroWidthNonJoiner) sb.Append(c);
                    continue;
                }

                char prev = i > 0 ? s[i - 1] : '\0';
                char next = i + 1 < s.Length ? s[i + 1] : '\0';
                bool joinPrev = JoinsNext(prev);

                // Lam + Alef ligature.
                if (c == 'ل' && (next == 'ا' || next == 'آ'))
                {
                    bool madda = next == 'آ';
                    sb.Append(joinPrev ? (madda ? 'ﻶ' : 'ﻼ') : (madda ? 'ﻵ' : 'ﻻ'));
                    i++;
                    continue;
                }

                bool joinNext = forms[2] != '\0' && Forms.ContainsKey(next);
                int form = joinPrev ? (joinNext ? 3 : 1) : (joinNext ? 2 : 0);
                sb.Append(forms[form]);
            }
            return sb.ToString();
        }

        private static bool IsLtr(char c) =>
            (c >= '0' && c <= '9') || (c >= '۰' && c <= '۹') ||
            (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

        private static bool IsLtrJoiner(char c) => c == '.' || c == ',' || c == ':' || c == '/' || c == '%' || c == '-';

        private static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '<': return '>';
                case '>': return '<';
                case '«': return '»';
                case '»': return '«';
                default: return c;
            }
        }

        /// <summary>Reverses a logical right-to-left line into left-to-right display order.</summary>
        public static string Reorder(string s)
        {
            var runs = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                if (IsLtr(s[i]))
                {
                    int j = i + 1;
                    while (j < s.Length && (IsLtr(s[j]) || (IsLtrJoiner(s[j]) && j + 1 < s.Length && IsLtr(s[j + 1])) ||
                                            (s[j] == ' ' && j + 1 < s.Length && IsLatin(s[j + 1]) && IsLatin(s[j - 1]))))
                        j++;
                    runs.Add(s.Substring(i, j - i)); // left-to-right run kept as is
                    i = j;
                }
                else
                {
                    var sb = new StringBuilder();
                    sb.Append(Mirror(s[i]));
                    runs.Add(sb.ToString());
                    i++;
                }
            }
            runs.Reverse();
            return string.Concat(runs);
        }

        private static bool IsLatin(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
    }
}
