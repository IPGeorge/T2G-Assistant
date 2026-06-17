using System;
using System.Diagnostics;
using System.Threading;

namespace T2G
{
    public static class UnityProcessManager
    {
        public static void WaitForUnityToFinish(string processName)
        {
            bool unityRunning = true;
            while (unityRunning)
            {
                Process[] processes = Process.GetProcessesByName(processName);
                unityRunning = processes.Length > 0;

                if (unityRunning)
                {
                    Thread.Sleep(1000);
                }
            }
        }

        public static void KillAllUnityProcesses()
        {
            Process[] processes = Process.GetProcessesByName("Unity");
            foreach (Process process in processes)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"Failed to kill process: {ex.Message}");
                }
            }
        }
    }
}