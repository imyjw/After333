using System;
using System.Text;

namespace Project333.Runtime.Presentation.Battle
{
    public static class BattleRichTextStyler
    {
        public static string StyleTile(string title, string content, string highlightLabel, bool isOccupied)
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(highlightLabel))
            {
                builder.Append("<size=85%><b>");
                builder.Append(highlightLabel);
                builder.AppendLine("</b></size>");
            }

            builder.Append("<size=80%><color=#FFFFFFCC>");
            builder.Append(title ?? string.Empty);
            builder.AppendLine("</color></size>");

            if (!isOccupied)
            {
                builder.Append("<size=95%><i>");
                builder.Append(string.IsNullOrWhiteSpace(content) ? "Empty" : content);
                builder.Append("</i></size>");
                return builder.ToString();
            }

            var lines = SplitLines(content);
            if (lines.Length == 0)
            {
                builder.Append("<i>Unknown occupant</i>");
                return builder.ToString();
            }

            builder.Append("<size=110%><b>");
            builder.Append(lines[0]);
            builder.AppendLine("</b></size>");

            if (lines.Length > 1)
            {
                builder.Append("<size=90%>");
                builder.Append(lines[1]);
                builder.AppendLine("</size>");
            }

            if (lines.Length > 2)
            {
                builder.Append("<size=95%>");
                builder.Append(lines[2]);
                builder.AppendLine("</size>");
            }

            for (var i = 3; i < lines.Length; i++)
            {
                builder.Append("<size=85%><b>");
                builder.Append(lines[i]);
                builder.AppendLine("</b></size>");
            }

            return builder.ToString().TrimEnd();
        }

        public static string StyleHandCard(string label, string highlightLabel, bool hasCard, string emptyLabel)
        {
            var resolvedLabel = hasCard
                ? label ?? string.Empty
                : emptyLabel ?? "Empty";

            var colonIndex = resolvedLabel.IndexOf(':');
            var slotLabel = colonIndex >= 0
                ? resolvedLabel.Substring(0, colonIndex).Trim()
                : resolvedLabel.Trim();
            var cardLabel = colonIndex >= 0
                ? resolvedLabel.Substring(colonIndex + 1).Trim()
                : string.Empty;

            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(highlightLabel))
            {
                builder.Append("<size=80%><b>");
                builder.Append(highlightLabel);
                builder.AppendLine("</b></size>");
            }

            builder.Append("<size=78%><color=#FFFFFFCC>");
            builder.Append(slotLabel);
            builder.AppendLine("</color></size>");

            if (!hasCard || string.IsNullOrWhiteSpace(cardLabel))
            {
                builder.Append("<size=96%><i>Empty</i></size>");
                return builder.ToString();
            }

            builder.Append("<size=102%><b>");
            builder.Append(cardLabel);
            builder.Append("</b></size>");
            return builder.ToString();
        }

        public static string StyleSummary(string summary, string fallbackText)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return $"<i>{fallbackText}</i>";
            }

            var lines = SplitLines(summary);
            var builder = new StringBuilder();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    builder.Append(line);
                }
                else
                {
                    var label = line.Substring(0, separatorIndex).Trim();
                    var value = line.Substring(separatorIndex + 1).Trim();
                    builder.Append("<b>");
                    builder.Append(label);
                    builder.Append("</b>: ");
                    builder.Append(value);
                }

                if (i < lines.Length - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        public static string StyleTurnSummary(string turnSummary)
        {
            return $"<size=112%><b>{turnSummary}</b></size>";
        }

        public static string StyleResourceSummary(string ownerLabel, string resourceSummary)
        {
            return $"<b>{ownerLabel}</b>  {StyleResourceTokens(resourceSummary)}";
        }

        public static string StyleCombatLog(string logText, string fallbackText)
        {
            if (string.IsNullOrWhiteSpace(logText))
            {
                return $"<i>{fallbackText}</i>";
            }

            var lines = SplitLines(logText);
            var builder = new StringBuilder();

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                builder.Append("• ");
                builder.Append(StyleLogLine(line));

                if (i < lines.Length - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        public static string StyleOutcome(string outcomeText)
        {
            if (string.IsNullOrWhiteSpace(outcomeText))
            {
                return string.Empty;
            }

            var styledColor = string.Equals(outcomeText, "Victory", StringComparison.OrdinalIgnoreCase)
                ? "#8FF7A7"
                : "#FF8F8F";

            return $"<size=170%><b><color={styledColor}>{outcomeText}</color></b></size>";
        }

        public static string StylePlayerInputLabel(string labelText)
        {
            if (string.IsNullOrWhiteSpace(labelText))
            {
                return string.Empty;
            }

            var separatorIndex = labelText.IndexOf(':');
            if (separatorIndex <= 0)
            {
                return labelText;
            }

            var label = labelText.Substring(0, separatorIndex).Trim();
            var value = labelText.Substring(separatorIndex + 1).Trim();
            return $"<b>{label}</b>: {value}";
        }

        public static string StyleInteractionStatus(string statusText)
        {
            if (string.IsNullOrWhiteSpace(statusText))
            {
                return "<b>Status</b>: Ready";
            }

            var separatorIndex = statusText.IndexOf(':');
            var rawValue = separatorIndex >= 0
                ? statusText.Substring(separatorIndex + 1).Trim()
                : statusText.Trim();

            var color = rawValue.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rawValue.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                ? "#FFB58F"
                : "#B8F7FF";

            return $"<b>Status</b>: <color={color}>{rawValue}</color>";
        }

        private static string StyleResourceTokens(string resourceSummary)
        {
            if (string.IsNullOrWhiteSpace(resourceSummary))
            {
                return "M:0 Q:0 P:0 G:0";
            }

            var tokens = resourceSummary.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var builder = new StringBuilder();
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (token.Length >= 2 && token[1] == ':')
                {
                    builder.Append("<b>");
                    builder.Append(token[0]);
                    builder.Append("</b>");
                    builder.Append(token.Substring(1));
                }
                else
                {
                    builder.Append(token);
                }

                if (i < tokens.Length - 1)
                {
                    builder.Append("  ");
                }
            }

            return builder.ToString();
        }

        private static string StyleLogLine(string line)
        {
            var color = "#DCE6F2";

            if (ContainsAny(line, "회복", "HP +"))
            {
                color = "#8FF7A7";
            }
            else if (ContainsAny(line, "공격", "피해", "처치", "반격"))
            {
                color = "#FFB0B0";
            }
            else if (ContainsAny(line, "마법카드", " 효과로", "표식"))
            {
                color = "#FFD28F";
            }
            else if (ContainsAny(line, "소환", "건설", "강화 적용"))
            {
                color = "#A8F5B0";
            }
            else if (line.IndexOf("이동", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                color = "#A8DFFF";
            }
            else if (line.IndexOf("교환", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                color = "#FFE8A8";
            }
            else if (line.IndexOf("턴 시작", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                color = "#BFE8FF";
            }
            else if (line.IndexOf("턴 종료", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                color = "#D1D9E6";
            }
            else if (line.IndexOf("전투 시작", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                color = "#CFF4FF";
            }
            else if (line.IndexOf("멀리건", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                color = "#E4D6FF";
            }

            return $"<color={color}>{line}</color>";
        }

        private static bool ContainsAny(string value, params string[] candidates)
        {
            for (var i = 0; i < candidates.Length; i++)
            {
                if (value.IndexOf(candidates[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] SplitLines(string text)
        {
            return (text ?? string.Empty).Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.None);
        }
    }
}
