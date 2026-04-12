//This data structure is for local data save purpose

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace T2G.Assistant
{

    [Serializable]
    public class ProjectContext
    {
        public string ProjectName;
        public string ProjectPath;
        public string Genre;
        public string Engine;
        public string CurrentSpace;

        static string GetRootDirectory()
        {
            string directory = Path.Combine(Assistant.Instance.PersistentDataPath, "Projects");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            return directory;
        }

        public bool Save()
        {
            if (string.IsNullOrWhiteSpace(ProjectPath) || string.IsNullOrWhiteSpace(ProjectName))
            {
                return false;
            }

            string directory = GetRootDirectory();

            string projectPath = Path.Combine(directory, ProjectName + ".prj");
            string projectJson = JsonUtility.ToJson(this);

            File.WriteAllText(projectPath, projectJson);
            return true;
        }

        static public List<string> GetProjectList()
        {
            string directory = GetRootDirectory();
            string[] projects = Directory.GetFiles(directory, "*.prj");
            List<string> prjList = new List<string>();
            for (int i = 0; i < projects.Length; ++i)
            {
                string fn = Path.GetFileName(projects[i]);
                prjList.Add(fn);
            }
            return prjList;
        }

        static public ProjectContext LoadOrCreate(ProjectInfo projectInfo)
        {
            ProjectContext context;
            if (File.Exists(projectInfo.ProjectPath))
            {
                string directory = GetRootDirectory();
                string path = Path.Combine(directory, projectInfo.ProjectName + ".prj");
                string projectJson = File.ReadAllText(path);
                context = JsonUtility.FromJson<ProjectContext>(projectJson);
                context.CurrentSpace = projectInfo.CurrentSpace;
            }
            else
            {
                context = new ProjectContext
                {
                    ProjectName = projectInfo.ProjectName,
                    ProjectPath = projectInfo.ProjectPath,
                    Genre = "",
                    Engine = "Unity",
                    CurrentSpace = projectInfo.CurrentSpace
                };
            }
            context.Save();
            return context;

        }
    }
}