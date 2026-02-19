using System;

public static class JsonExtraction
{
    /// <summary>
    /// Extracts the first top-level JSON object/array from a text blob.
    /// Handles leading prose and markdown code fences.
    /// Returns null if no JSON found.
    /// </summary>
    public static string ExtractFirstJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Remove common markdown fences but keep content
        text = text.Replace("```json", "```", StringComparison.OrdinalIgnoreCase);

        // If there are code fences, prefer content inside the first fenced block
        int fenceStart = text.IndexOf("```", StringComparison.Ordinal);
        if (fenceStart >= 0)
        {
            int fenceEnd = text.IndexOf("```", fenceStart + 3, StringComparison.Ordinal);
            if (fenceEnd > fenceStart)
            {
                var inside = text.Substring(fenceStart + 3, fenceEnd - (fenceStart + 3));
                var candidate = inside.Trim();
                var extractedFromFence = ExtractByBraces(candidate);
                if (!string.IsNullOrEmpty(extractedFromFence))
                    return extractedFromFence;
            }
        }

        // Otherwise, extract from whole text
        return ExtractByBraces(text);
    }

    private static string ExtractByBraces(string text)
    {
        int startObj = text.IndexOf('{');
        int startArr = text.IndexOf('[');

        if (startObj < 0 && startArr < 0)
            return null;

        int start = (startObj < 0) ? startArr : (startArr < 0 ? startObj : Math.Min(startObj, startArr));
        char open = text[start];
        char close = open == '{' ? '}' : ']';

        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (escape)
                {
                    escape = false;
                }
                else if (c == '\\')
                {
                    escape = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == open) depth++;
            if (c == close) depth--;

            if (depth == 0)
            {
                // Inclusive end
                return text.Substring(start, i - start + 1).Trim();
            }
        }

        // Unbalanced braces
        return null;
    }
}
