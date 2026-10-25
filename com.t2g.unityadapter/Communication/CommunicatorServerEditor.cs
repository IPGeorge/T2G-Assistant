#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

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

        // Unity GUI text is limited to ~16383 characters per control (65535
        // vertices / 4). A long instruction log otherwise gets truncated, so keep only the most recent part of the console.
        const int MaxConsoleCharacters = 16000;


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

            // SessionState is scoped to this editor instance/project and survives
            // domain reloads. EditorPrefs are shared by every Unity project on
            // the machine, so multiple editors (e.g. the Assistant and the game
            // project) would overwrite each other's console text.
            SessionState.SetString(PrefsKeys.k_ServerConsoleText, _text);

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
            _text = SessionState.GetString(PrefsKeys.k_ServerConsoleText, string.Empty);
            TrimConsoleText();

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
                        _server.StartServer();
                    }
                }
                else
                {
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
                Debug.Log("System> Server started.");
            };

            _server.AfterShutdownServer += () =>
            {
                Debug.Log("System> Server was shut down.");
            };

            _server.OnFailedToStartServer += () =>
            {
                Debug.Log("\n System> Failed to start litsening server!");
            };

            _server.OnClientConnected += () =>
            {
                Debug.Log("\n System> Client was connected!");
            };

            _server.OnClientDisconnected += () =>
            {
                Debug.Log("System> Client was disconnected!");
            };

            _server.OnReceivedMessage += (type, message) =>
            {
                if (type == CommunicatorBase.eMessageType.Message ||
                    type == CommunicatorBase.eMessageType.Instruction ||
                    type == CommunicatorBase.eMessageType.Response)
                {
                    AddConsoleText("Received> " + message);
                }
                else
                {
                    Debug.Log("Received> " + message);
                }
            };

            _server.OnSentMessage += (type, message) =>
            {
                if (type == CommunicatorBase.eMessageType.Message ||
                    type == CommunicatorBase.eMessageType.Instruction ||
                    type == CommunicatorBase.eMessageType.Response)
                {
                    AddConsoleText("Sent> " + message);
                }
                else
                {
                    Debug.Log("Sent> " + message);
                }
            };

            _server.OnLogMessage += (message) =>
            {
                Debug.Log("Received> " + message);
            };
            #endregion Handle server events

            _execution = Execution.Instance;
            _execution.OnDisplayText += (message) =>
            {
                AddConsoleText("\n Execution> " + message);
            };
        }

        public static void AddConsoleText(string textToAdd)
        {
            _text += textToAdd + "\n";
            TrimConsoleText();
            _repaintText = true;
        }

        static void TrimConsoleText()
        {
            if (_text.Length <= MaxConsoleCharacters)
            {
                return;
            }

            int excess = _text.Length - MaxConsoleCharacters;
            int lineStart = _text.IndexOf('\n', excess);
            _text = lineStart >= 0 && lineStart + 1 < _text.Length
                ? _text.Substring(lineStart + 1)
                : _text.Substring(excess);
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

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear"))
            {
                _text = string.Empty;
                _scroll = Vector2.zero;
                _repaintText = true;
                Repaint();
                GUI.FocusControl(null);
            }

            if (GUILayout.Button("Persistent"))
            {
                string persistantPath = Application.persistentDataPath.Replace("/", "\\");
                System.Diagnostics.Process.Start("explorer.exe", persistantPath);
            }
            EditorGUILayout.EndHorizontal();

            // Use a read-only selectable label instead of a TextArea. An
            // editable TextArea keeps its own TextEditor state while focused,
            // so when new console text is appended in the background it can
            // render as empty until the control loses focus.
            GUIStyle consoleStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = false,
                richText = false
            };

            // The label must be as wide as the longest line, otherwise the
            // ScrollView has nothing to scroll horizontally.
            float viewportWidth = Mathf.Max(100f, EditorGUIUtility.currentViewWidth - 50f);
            float contentWidth = viewportWidth;
            string[] lines = _text.Split('\n');
            for (int i = 0; i < lines.Length; ++i)
            {
                float lineWidth = consoleStyle.CalcSize(new GUIContent(lines[i])).x;
                if (lineWidth > contentWidth)
                {
                    contentWidth = lineWidth;
                }
            }
            contentWidth += 8f;

            float contentHeight = consoleStyle.CalcHeight(new GUIContent(_text), contentWidth);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, true, true);
            EditorGUILayout.SelectableLabel(
                _text,
                consoleStyle,
                GUILayout.Width(contentWidth),
                GUILayout.Height(Mathf.Max(contentHeight + 8f, 100f)));
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
                _repaintText = false;

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