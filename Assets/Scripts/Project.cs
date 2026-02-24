using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class Project
{
    public string ProjectName;
    public string ProjectPath;
    public string Genre;
    public string Engine;


    static string GetRootSirectory()
    {
        string directory = Path.Combine(Application.persistentDataPath, "Projects");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return directory;
    }

    public bool Save()
    {
        if(string.IsNullOrWhiteSpace(ProjectPath) || string.IsNullOrWhiteSpace(ProjectName))
        {
            return false;
        }

        string directory = GetRootSirectory();

        string projectPath = Path.Combine(directory, ProjectName + ".prj");
        string projectJson = JsonUtility.ToJson(this);

        File.WriteAllText(projectJson, projectPath);
        return true;
    }

    static public List<string> GetProjectList()
    {
        string directory = GetRootSirectory();
        string[] projects = Directory.GetFiles(directory, "*.prj");
        List<string> prjList = new List<string>();
        for(int i = 0; i < projects.Length; ++i)
        {
            string fn = Path.GetFileName(projects[i]);
            prjList.Add(fn);
        }
        return prjList;
    }

    static public Project Load(string projectName)
    {
        string directory = GetRootSirectory();
        string projectPath = Path.Combine(directory, projectName + ".prj");
        if(File.Exists(projectPath))
        {
            string projectJson = File.ReadAllText(projectPath);
            return JsonUtility.FromJson<Project>(projectJson);
        }
        return null;
    }

}
