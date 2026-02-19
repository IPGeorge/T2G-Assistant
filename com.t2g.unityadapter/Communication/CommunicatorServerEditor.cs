#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;

namespace T2G
{
    public class CommunicatorServerEditor : EditorWindow
    {
        public static string SessionId = string.Empty;
        
        private const string MENU_PATH = "T2G/Communicator Server";
        private static bool _isWindowOpen = false;

        static CommunicatorServer _server = null;
        static Execution _execution = null;

        static Vector2 _scroll = Vector2.zero;
        static string _text = string.Empty;
        static bool _repaintText = false;
        static CommunicatorServerEditor _CommunicatorWindow = null;


        [MenuItem(MENU_PATH, false)]
        public static void OpenCloseDashboard()
        {
            if (_isWindowOpen)
            {
                CloseDashboard();
            }
            else
            {
                Init();
                Type inspectorType = Type.GetType("UnityEditor.InspectorWindow,UnityEditor.dll");
                _CommunicatorWindow = EditorWindow.GetWindow<CommunicatorServerEditor>("Communicator Server", new Type[] { inspectorType });
                SetMenuChecked(true);
            }
        }

        public static void SetMenuChecked(bool isChecked)
        {
            Menu.SetChecked(MENU_PATH, isChecked);
            _isWindowOpen = isChecked;
        }

        public static void CloseDashboard()
        {
            Uninit();
            _CommunicatorWindow?.Close();
            SetMenuChecked(false);
        }

        private void OnDestroy()
        {
            Debug.Log("[ServerEditor] Called when closing the server dashboard window.");

            _text = string.Empty;
            Uninit();
            SetMenuChecked(false);
        }

        private static void On_EditorApplication_quitting()
        {
            Debug.Log("[ServerEditor] Called when the editor quits.");

            Uninit();
            EditorPrefs.DeleteKey(PrefsKeys.k_SessionId);
            EditorApplication.quitting -= On_EditorApplication_quitting;
        }

        private static void BeforeAssemblyReload()
        {
            Debug.Log("[ServerEditor] before assemble reload: called after code change (1-new code doesn't take effect)");

            _text = string.Empty;
            if (_server != null)
            {
                EditorPrefs.SetBool(PrefsKeys.k_StartListener, _server.IsActive);
                if (_server.IsActive)
                {
                    _server.StopServer();
                    EditorPrefs.SetBool(PrefsKeys.k_StartListener, true);
                }
            }
            Uninit();
        }

        private static void AfterAssemblyReload()
        {
            Debug.Log("[ServerEditor] After assemble reload: called after code change or openning the editor (3-new code takes effect)");

            EditorPrefs.SetBool(PrefsKeys.k_IsReloadLaunch, true);
            if (EditorPrefs.GetBool(PrefsKeys.k_StartListener, false))
            {
                if(_server != null && !_server.IsActive)
                {
                    _server.StartServer();
                }
            }
        }

        private static void OnDomainUnload(object sender, EventArgs e)
        {
            Debug.Log("[ServerEditor] dowmain unload: called after code change (2-new code doesn't take effect)");
            //Don't do anything here.
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            Debug.Log("[ServerEditor] process exit");
            //Don't do anything here.
        }

        [InitializeOnLoadMethod]
        public static void InitOnLoadMethod()
        {
            Debug.Log($"[CommunicatorServerEditor] InitOnLoadMethod is called.");
            Init();

            // Delay execution until editor is fully initialized
            EditorApplication.delayCall += () =>
            {
                OpenCloseDashboard();

                string productName = Application.productName;
                if (productName.IndexOf("T2G Assistant") >= 0)
                {
                    bool startServer = EditorPrefs.GetBool(PrefsKeys.k_StartListener, true);
                    Debug.Log($"[CommunicatorServerEditor.InitOnLoadMethod] Start Server is {startServer}.");
                    if (startServer)
                    {
                        _text = string.Empty;
                        _server.StartServer();
                    }
                }
                else
                {
                    _text = string.Empty;
                    _server.StartServer();
                }

                string prevSessinId = EditorPrefs.GetString(PrefsKeys.k_SessionId, "");
                SessionId = System.Guid.NewGuid().ToString();
                EditorPrefs.SetString(PrefsKeys.k_SessionId, SessionId);

                if (string.IsNullOrEmpty(prevSessinId))
                {
                    EditorPrefs.SetBool(PrefsKeys.k_IsReloadLaunch, false);
                    Debug.Log("[CommunicatorServerEditor] Fresh editor launch.");
                }
                else
                {
                    Debug.Log("[CommunicatorServerEditor] Reload editor launch.");
                }
            };
        }

        public static void Init()
        {
            if(_server != null)
            {
                return;
            }

            #region Handle editor events
            EditorApplication.quitting += On_EditorApplication_quitting;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += AfterAssemblyReload;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            #endregion Handle editor events

            _server = CommunicatorServer.Instance;

            #region Handle server events
            _server.OnServerStarted += () =>
            {
                AddConsoleText("\n System> Server started.");
            };

            _server.AfterShutdownServer += () =>
            {
                AddConsoleText("\n System> Server was shut down.");
            };

            _server.OnFailedToStartServer += () =>
            {
                AddConsoleText("\n System> Failed to start litsening server!");
            };

            _server.OnClientConnected += () =>
            {
                AddConsoleText("\n System> Client was connected!");
            };

            _server.OnClientDisconnected += () =>
            {
                AddConsoleText("\n System> Client was disconnected!");
            };

            _server.OnReceivedMessage += (type, message) =>
            {
                AddConsoleText("\n Received> " + message);
            };

            _server.OnSentMessage += (message) =>
            {
                AddConsoleText("\n Sent> " + message);
            };

            _server.OnLogMessage += (message) =>
            {
                AddConsoleText("\n Received> " + message);
            };
            #endregion Handle server events

            _execution = Execution.Instance;
            _execution.OnDisplayText += (message) =>
            {
                AddConsoleText("\n Execution> " + message);
            };
        }

        static void AddConsoleText(string textToAdd)
        {
            _text += textToAdd;
            _repaintText = true;
        }

        public static void Uninit()
        {
            if (_server == null)
            {
                return;
            }

            _server.StopServer();

            _server.OnServerStarted = null;
            _server.AfterShutdownServer = null;
            _server.OnFailedToStartServer = null;
            _server.OnClientConnected = null;
            _server.OnClientDisconnected = null;
            _server.OnReceivedMessage = null;
            _server.OnSentMessage = null;
            _server.OnLogMessage = null;
            _execution.OnDisplayText = null;

            _server = null;

            EditorApplication.quitting -= On_EditorApplication_quitting;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= AfterAssemblyReload;
            AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        }

        public void OnGUI()
        {
            if(_server == null)
            {
                return;
            }

            bool isActive = EditorGUILayout.Toggle("Server is on: ", _server.IsActive);
            if (isActive && !_server.IsActive)
            {
                _server.StartServer();
            }
            else if (!isActive && _server.IsActive)
            {
                _server.StopServer();
            }
            EditorPrefs.SetBool(PrefsKeys.k_StartListener, isActive);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("Client is connected: ", _server.IsConnected);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Clear"))
            {
                _text = string.Empty;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            _text = EditorGUILayout.TextArea(_text, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void Update()
        {
            RepaintText();
        }

        void RepaintText()
        {
            if (_repaintText)
            {
                if (_CommunicatorWindow == null)
                {
                    _CommunicatorWindow = GetWindow<CommunicatorServerEditor>();
                }
                _CommunicatorWindow?.Repaint();
            }
        }

        public static CommunicatorServer GetServer()
        {
            return _server;
        }
    }
}

#endif