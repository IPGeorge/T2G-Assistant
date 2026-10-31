using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace T2G.Assistant
{
    public class SkillManager
    {
        private readonly string _skillsPath;

        public SkillManager()
        {
            _skillsPath = Path.Combine(Application.streamingAssetsPath, "Skills");
        }

        public string LoadSkill(string skillName)
        {
            string path = Path.Combine(_skillsPath, skillName, "SKILL.md");

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SkillManager] Skill not found: {skillName}");
                return null;
            }

            return File.ReadAllText(path);
        }

        public List<string> GetAvailableSkills()
        {
            var skills = new List<string>();

            if (!Directory.Exists(_skillsPath))
                return skills;

            foreach (string directory in Directory.GetDirectories(_skillsPath))
            {
                string path = Path.Combine(directory, "SKILL.md");

                if (File.Exists(path))
                {
                    skills.Add(Path.GetFileName(directory));
                }
            }

            return skills;
        }
    }
}