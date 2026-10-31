using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace T2G.Assistant
{


    public static class SkillTriggerEvaluator
    {
        /// <summary>
        /// Determines if a specific skill should be used based on the prompt.
        /// </summary>
        /// <param name="prompt">The user's input text.</param>
        /// <param name="skillName">The name of the skill (e.g., "FirstPersonShooter", "first-person-shooter").</param>
        /// <returns>True if the prompt indicates the skill should be used; otherwise, false.</returns>
        public static bool ShouldTriggerSkill(string prompt, string skillName)
        {
            if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(skillName))
                return false;

            // 1. Split the prompt into a HashSet of words for fast O(1) lookup.
            // We remove punctuation and convert to lowercase.
            var promptWords = Regex.Split(prompt.ToLowerInvariant(), @"[^a-z0-9]+")
                                   .Where(w => !string.IsNullOrWhiteSpace(w))
                                   .ToHashSet();

            // 2. Extract keywords from the skill name.
            // "FirstPersonShooter" -> ["first", "person", "shooter"]
            // "first-person-shooter" -> ["first", "person", "shooter"]
            var skillKeywords = GetSkillKeywords(skillName);

            // 3. Check if ANY of the keywords exist in the prompt.
            // (You can change this to require ALL keywords, or a specific threshold).
            foreach (var keyword in skillKeywords)
            {
                if (promptWords.Contains(keyword))
                {
                    return true;
                }
            }

            // Optional: Also check if the entire canonical skill name appears as a phrase in the prompt
            // e.g., prompt contains "first person shooter"
            string canonicalPrompt = string.Join("", promptWords); // e.g. "iwantafirstpersonshooter"
            string canonicalSkill = GetCanonicalSkillName(skillName); // e.g. "firstpersonshooter"

            if (canonicalPrompt.Contains(canonicalSkill))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a list of skill names that are triggered by the prompt.
        /// </summary>
        public static List<string> GetTriggeredSkills(string prompt, IEnumerable<string> availableSkills)
        {
            return availableSkills.Where(skill => ShouldTriggerSkill(prompt, skill)).ToList();
        }

        #region Helper Methods

        /// <summary>
        /// Splits a skill name into normalized lowercase keywords.
        /// Handles camelCase, kebab-case, and snake_case.
        /// </summary>
        private static string[] GetSkillKeywords(string skillName)
        {
            // Insert a space between lowercase and uppercase letters (FirstPerson -> First Person)
            string spaced = Regex.Replace(skillName, @"(?<=[a-z])(?=[A-Z])", " ");

            // Split by spaces, hyphens, and underscores
            return Regex.Split(spaced, @"[\s\-_]+")
                        .Select(w => w.ToLowerInvariant())
                        .Where(w => !string.IsNullOrEmpty(w))
                        .ToArray();
        }

        /// <summary>
        /// Converts any format to a single lowercase string.
        /// "FirstPersonShooter" -> "firstpersonshooter"
        /// </summary>
        private static string GetCanonicalSkillName(string skillName)
        {
            return Regex.Replace(skillName, @"[\s\-_]+", "").ToLowerInvariant();
        }

        #endregion
    }
}