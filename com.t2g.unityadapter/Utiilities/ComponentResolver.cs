using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using UnityEngine.Video;

namespace T2G
{
    public static class ComponentResolver
    {
        private static Dictionary<string, List<Type>> _componentCache;
        private static readonly object _cacheLock = new object();
        private static bool _isInitialized = false;

        /// <summary>
        /// Gets the component type from a string name
        /// </summary>
        public static Type GetComponentType(string componentName)
        {
            if (string.IsNullOrWhiteSpace(componentName))
                return null;

            EnsureInitialized();

            string normalized = NormalizeName(componentName);

            // Try exact match
            if (_componentCache.TryGetValue(normalized, out List<Type> types))
            {
                if (types.Count == 1)
                    return types[0];

                return ResolveAmbiguousMatch(types, normalized);
            }

            // Try partial match
            foreach (var kvp in _componentCache)
            {
                if (kvp.Key.Contains(normalized) || normalized.Contains(kvp.Key))
                {
                    if (kvp.Value.Count == 1)
                        return kvp.Value[0];

                    return ResolveAmbiguousMatch(kvp.Value, normalized);
                }
            }

            return null;
        }


        /// <summary>
        /// Gets all registered component types
        /// </summary>
        public static Dictionary<string, List<Type>> GetAllComponentTypes()
        {
            EnsureInitialized();
            return new Dictionary<string, List<Type>>(_componentCache);
        }

        /// <summary>
        /// Gets all component types that derive from a specific base type
        /// </summary>
        public static List<Type> GetComponentsDerivingFrom<T>() where T : Component
        {
            EnsureInitialized();

            return _componentCache.Values
                .SelectMany(list => list)
                .Where(t => typeof(T).IsAssignableFrom(t))
                .Distinct()
                .ToList();
        }


        public static void Reset()
        {
            _isInitialized = false;
        }

        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                lock (_cacheLock)
                {
                    InitializeCache();
                    _isInitialized = true;
                }
            }
        }

        private static void InitializeCache()
        {
            _componentCache = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);

            // First, add all known Unity component types explicitly (guaranteed to work)
            AddUnityComponents();

            // Then scan all assemblies for additional components
            ScanAssembliesForComponents();

            // Finally, generate common aliases
            GenerateAliases();

            Debug.Log($"Component resolver initialized with {_componentCache.Count} unique names");
        }

        private static void AddUnityComponents()
        {
            // Core Unity components
            AddComponentType(typeof(Transform));
            AddComponentType(typeof(RectTransform));
            AddComponentType(typeof(Camera));
            AddComponentType(typeof(Light));
            AddComponentType(typeof(AudioSource));
            AddComponentType(typeof(AudioListener));
            AddComponentType(typeof(Animation));
            AddComponentType(typeof(Animator));
            AddComponentType(typeof(Canvas));
            AddComponentType(typeof(CanvasGroup));
            AddComponentType(typeof(CanvasRenderer));
            AddComponentType(typeof(ParticleSystem));
            AddComponentType(typeof(ParticleSystemRenderer));
            AddComponentType(typeof(TrailRenderer));
            AddComponentType(typeof(LineRenderer));
            AddComponentType(typeof(Terrain));
            AddComponentType(typeof(TerrainCollider));
            AddComponentType(typeof(LODGroup));
            AddComponentType(typeof(ReflectionProbe));
            AddComponentType(typeof(LightProbeGroup));
            AddComponentType(typeof(FlareLayer));
            AddComponentType(typeof(Skybox));
            AddComponentType(typeof(WindZone));
            AddComponentType(typeof(Cloth));
            AddComponentType(typeof(PlayableDirector));
            AddComponentType(typeof(VideoPlayer));

            // Renderers
            AddComponentType(typeof(Renderer));
            AddComponentType(typeof(MeshRenderer));
            AddComponentType(typeof(SkinnedMeshRenderer));
            AddComponentType(typeof(SpriteRenderer));
            AddComponentType(typeof(LineRenderer));
            AddComponentType(typeof(TrailRenderer));
            AddComponentType(typeof(ParticleSystemRenderer));

            // Physics 3D
            AddComponentType(typeof(Rigidbody));
            AddComponentType(typeof(Collider));
            AddComponentType(typeof(BoxCollider));
            AddComponentType(typeof(SphereCollider));
            AddComponentType(typeof(CapsuleCollider));
            AddComponentType(typeof(MeshCollider));
            AddComponentType(typeof(WheelCollider));
            AddComponentType(typeof(TerrainCollider));
            AddComponentType(typeof(CharacterController));
            AddComponentType(typeof(ConstantForce));
            AddComponentType(typeof(Joint));
            AddComponentType(typeof(HingeJoint));
            AddComponentType(typeof(FixedJoint));
            AddComponentType(typeof(SpringJoint));
            AddComponentType(typeof(CharacterJoint));
            AddComponentType(typeof(ConfigurableJoint));

            // Physics 2D
            AddComponentType(typeof(Rigidbody2D));
            AddComponentType(typeof(Collider2D));
            AddComponentType(typeof(BoxCollider2D));
            AddComponentType(typeof(CircleCollider2D));
            AddComponentType(typeof(CapsuleCollider2D));
            AddComponentType(typeof(PolygonCollider2D));
            AddComponentType(typeof(EdgeCollider2D));
            AddComponentType(typeof(CompositeCollider2D));
            AddComponentType(typeof(Effector2D));
            AddComponentType(typeof(AreaEffector2D));
            AddComponentType(typeof(BuoyancyEffector2D));
            AddComponentType(typeof(PointEffector2D));
            AddComponentType(typeof(SurfaceEffector2D));
            AddComponentType(typeof(PlatformEffector2D));
            AddComponentType(typeof(DistanceJoint2D));
            AddComponentType(typeof(FixedJoint2D));
            AddComponentType(typeof(FrictionJoint2D));
            AddComponentType(typeof(HingeJoint2D));
            AddComponentType(typeof(RelativeJoint2D));
            AddComponentType(typeof(SliderJoint2D));
            AddComponentType(typeof(TargetJoint2D));
            AddComponentType(typeof(WheelJoint2D));

            // UI Components
            AddComponentType(typeof(Button));
            AddComponentType(typeof(Text));
            AddComponentType(typeof(Image));
            AddComponentType(typeof(RawImage));
            AddComponentType(typeof(Slider));
            AddComponentType(typeof(Scrollbar));
            AddComponentType(typeof(ScrollRect));
            AddComponentType(typeof(InputField));
            AddComponentType(typeof(Toggle));
            AddComponentType(typeof(ToggleGroup));
            AddComponentType(typeof(Dropdown));
            AddComponentType(typeof(Selectable));
            AddComponentType(typeof(Mask));
            AddComponentType(typeof(RectMask2D));
            AddComponentType(typeof(ContentSizeFitter));
            AddComponentType(typeof(LayoutElement));
            AddComponentType(typeof(HorizontalLayoutGroup));
            AddComponentType(typeof(VerticalLayoutGroup));
            AddComponentType(typeof(GridLayoutGroup));
            AddComponentType(typeof(AspectRatioFitter));
            AddComponentType(typeof(CanvasScaler));
            AddComponentType(typeof(GraphicRaycaster));
            AddComponentType(typeof(EventTrigger));
            AddComponentType(typeof(EventSystem));
            AddComponentType(typeof(StandaloneInputModule));
            //AddComponentType(typeof(TouchInputModule));   //deprecated
            AddComponentType(typeof(BaseInput));
            AddComponentType(typeof(BaseInputModule));

            // Navigation
            AddComponentType(typeof(UnityEngine.AI.NavMeshAgent));
            AddComponentType(typeof(UnityEngine.AI.NavMeshObstacle));
            //AddComponentType(typeof(UnityEngine.AI.OffMeshLink));
            //AddComponentType(typeof(NavMeshLink));
            //AddComponentType(typeof(NavMeshSurface));
            //AddComponentType(typeof(NavMeshModifier));
            //AddComponentType(typeof(NavMeshModifierVolume));

            // Audio Effects
            AddComponentType(typeof(AudioReverbZone));
            AddComponentType(typeof(AudioChorusFilter));
            AddComponentType(typeof(AudioDistortionFilter));
            AddComponentType(typeof(AudioEchoFilter));
            AddComponentType(typeof(AudioHighPassFilter));
            AddComponentType(typeof(AudioLowPassFilter));
            AddComponentType(typeof(AudioReverbFilter));

            // Tilemap
            AddComponentType(typeof(Tilemap));
            AddComponentType(typeof(TilemapRenderer));
            AddComponentType(typeof(TilemapCollider2D));

            // Sprite
            AddComponentType(typeof(SpriteMask));
            AddComponentType(typeof(SpriteRenderer));

            // Animation
            AddComponentType(typeof(AnimatorOverrideController));
            AddComponentType(typeof(StateMachineBehaviour));

            // TextMeshPro (if available)
            TryAddType("TMPro.TextMeshPro", "TMPro");
            TryAddType("TMPro.TextMeshProUGUI", "TMPro");
            TryAddType("TMPro.TMP_Text", "TMPro");
            TryAddType("TMPro.TMP_InputField", "TMPro");
            TryAddType("TMPro.TMP_Dropdown", "TMPro");

            // Cinemachine (if available)
            TryAddType("Cinemachine.CinemachineVirtualCamera", "Cinemachine");
            TryAddType("Cinemachine.CinemachineFreeLook", "Cinemachine");
            TryAddType("Cinemachine.CinemachineBlendListCamera", "Cinemachine");
            TryAddType("Cinemachine.CinemachineClearShot", "Cinemachine");
            TryAddType("Cinemachine.CinemachineCollider", "Cinemachine");
            TryAddType("Cinemachine.CinemachineComposer", "Cinemachine");
            TryAddType("Cinemachine.CinemachineConfiner", "Cinemachine");
            TryAddType("Cinemachine.CinemachineDollyCart", "Cinemachine");

            // Post-processing (if available)
            TryAddType("UnityEngine.Rendering.PostProcessing.PostProcessVolume", "Unity.RenderPipelines.PostProcessing");
            TryAddType("UnityEngine.Rendering.PostProcessing.PostProcessLayer", "Unity.RenderPipelines.PostProcessing");
        }

        private static void AddComponentType(Type type)
        {
            if (type == null) return;

            // Add by full name
            AddNameToCache(type.FullName, type);

            // Add by simple name
            AddNameToCache(type.Name, type);

            // Add by name without namespace
            string shortName = type.Name;
            AddNameToCache(shortName, type);

            // Add by name without "Component" suffix
            if (type.Name.EndsWith("Component"))
            {
                string withoutComponent = type.Name.Substring(0, type.Name.Length - 9);
                AddNameToCache(withoutComponent, type);
            }

            // Add by name without "2D" suffix for 2D components
            if (type.Name.EndsWith("2D"))
            {
                string without2D = type.Name.Substring(0, type.Name.Length - 2);
                AddNameToCache(without2D, type);

                // Also add with space (e.g., "box collider 2d")
                AddNameToCache(without2D + "2d", type);
                AddNameToCache(without2D + " 2d", type);
            }

            // Add by name with spaces (for multi-word components)
            string withSpaces = InsertSpaces(type.Name);
            if (withSpaces != type.Name)
            {
                AddNameToCache(withSpaces, type);
                AddNameToCache(withSpaces.ToLower(), type);
            }

            // Add by lowercase version
            AddNameToCache(type.Name.ToLower(), type);
        }

        private static void TryAddType(string typeName, string assemblyName)
        {
            try
            {
                Type type = Type.GetType($"{typeName}, {assemblyName}");
                if (type != null && typeof(Component).IsAssignableFrom(type))
                {
                    AddComponentType(type);
                }
            }
            catch
            {
                // Type not available, ignore
            }
        }

        private static void ScanAssembliesForComponents()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    // Skip system assemblies
                    if (assembly.FullName.StartsWith("System") ||
                        assembly.FullName.StartsWith("Microsoft") ||
                        assembly.FullName.StartsWith("mscorlib") ||
                        assembly.FullName.StartsWith("netstandard"))
                    {
                        continue;
                    }

                    var types = assembly.GetTypes()
                        .Where(t => typeof(Component).IsAssignableFrom(t) &&
                                    !t.IsAbstract &&
                                    t.IsPublic &&
                                    !t.IsGenericType);

                    foreach (var type in types)
                    {
                        AddComponentType(type);
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be loaded
                    continue;
                }
                catch
                {
                    continue;
                }
            }
        }

        private static void AddNameToCache(string name, Type type)
        {
            if (string.IsNullOrEmpty(name))
                return;

            string normalized = NormalizeName(name);

            if (!_componentCache.ContainsKey(normalized))
            {
                _componentCache[normalized] = new List<Type>();
            }

            if (!_componentCache[normalized].Contains(type))
            {
                _componentCache[normalized].Add(type);
            }
        }

        private static void GenerateAliases()
        {
            var aliases = new Dictionary<string, string[]>
            {
                ["transform"] = new[] { "tr", "t", "xform" },
                ["rigidbody"] = new[] { "rb", "body", "rigid" },
                ["rigidbody2d"] = new[] { "rb2d", "body2d", "rigidbody2d", "rigidbody 2d" },
                ["collider"] = new[] { "col", "collide", "collision" },
                ["boxcollider"] = new[] { "box", "boxcol", "boxcollide", "box collider" },
                ["spherecollider"] = new[] { "sphere", "spherecol", "sphere collider" },
                ["capsulecollider"] = new[] { "capsule", "capcol", "capsule collider" },
                ["meshcollider"] = new[] { "mesh", "meshcol", "mesh collider" },
                ["wheelcollider"] = new[] { "wheel", "wheelcol", "wheel collider" },
                ["boxcollider2d"] = new[] { "box2d", "box collider 2d", "box2d collider", "2d box" },
                ["circlecollider2d"] = new[] { "circle2d", "circle collider 2d", "2d circle", "sphere2d" },
                ["capsulecollider2d"] = new[] { "capsule2d", "capsule collider 2d", "2d capsule" },
                ["charactercontroller"] = new[] { "charcontrol", "controller", "character control" },
                ["animator"] = new[] { "anim", "animation", "animcontroller" },
                ["camera"] = new[] { "cam" },
                ["audiosource"] = new[] { "audio", "sound", "audiosrc" },
                ["audiolistener"] = new[] { "listener" },
                ["light"] = new[] { "l", "lighting", "light source" },
                ["canvas"] = new[] { "ui", "canvasui" },
                ["canvasgroup"] = new[] { "cg", "canvasg", "group" },
                ["button"] = new[] { "btn", "ui button" },
                ["text"] = new[] { "txt", "label", "uitext", "ui text" },
                ["image"] = new[] { "img", "picture", "uiimage", "ui image" },
                ["rawimage"] = new[] { "raw", "rawimg" },
                ["slider"] = new[] { "slide", "uislider", "ui slider" },
                ["toggle"] = new[] { "tog", "checkbox", "uitoggle", "ui toggle" },
                ["inputfield"] = new[] { "input", "textfield", "input field", "uitextfield" },
                ["dropdown"] = new[] { "drop", "select", "dropdownlist", "ui dropdown" },
                ["scrollrect"] = new[] { "scroll", "scrollview", "scroll rect" },
                ["navmeshagent"] = new[] { "agent", "navagent", "navigator", "nav agent" },
                ["navmeshobstacle"] = new[] { "obstacle", "navobstacle", "nav obstacle" },
                ["particlesystem"] = new[] { "particle", "ps", "vfx", "particles" },
                ["trailrenderer"] = new[] { "trail", "trailrender" },
                ["linenderer"] = new[] { "line", "linerender" },
                ["spriterenderer"] = new[] { "sprite", "sprite renderer" },
                ["meshrenderer"] = new[] { "renderer", "meshrend" },
                ["skinnedmeshrenderer"] = new[] { "skinned", "skinnedmesh" },
                ["terrain"] = new[] { "ground", "terrain" },
                ["windzone"] = new[] { "wind" },
                ["reflectionprobe"] = new[] { "probe", "reflection" },
                ["lightprobegroup"] = new[] { "lightprobe" },
                ["lodgroup"] = new[] { "lod" }
            };

            foreach (var alias in aliases)
            {
                string normalizedKey = NormalizeName(alias.Key);
                if (_componentCache.TryGetValue(normalizedKey, out List<Type> types))
                {
                    foreach (string aliasName in alias.Value)
                    {
                        string normalizedAlias = NormalizeName(aliasName);
                        if (!_componentCache.ContainsKey(normalizedAlias))
                        {
                            _componentCache[normalizedAlias] = new List<Type>(types);
                        }
                        else
                        {
                            foreach (var type in types)
                            {
                                if (!_componentCache[normalizedAlias].Contains(type))
                                {
                                    _componentCache[normalizedAlias].Add(type);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            // Remove all special characters and convert to lowercase
            string normalized = name.Replace(" ", "")
                                    .Replace("-", "")
                                    .Replace("_", "")
                                    .Replace(".", "")
                                    .Replace("(", "")
                                    .Replace(")", "")
                                    .Replace("[", "")
                                    .Replace("]", "")
                                    .ToLowerInvariant();

            // Remove common suffixes
            normalized = normalized.Replace("component", "");
            normalized = normalized.Replace("behaviour", "");
            normalized = normalized.Replace("behavior", "");
            normalized = normalized.Replace("script", "");
            normalized = normalized.Replace("controller", "");
            normalized = normalized.Replace("manager", "");

            return normalized;
        }

        private static string InsertSpaces(string name)
        {
            // Insert spaces before capital letters (e.g., "BoxCollider2D" -> "Box Collider 2 D")
            string result = "";
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    result += " ";
                }
                result += name[i];
            }
            return result;
        }

        private static Type ResolveAmbiguousMatch(List<Type> types, string normalizedName)
        {
            if (types == null || types.Count == 0)
                return null;

            if (types.Count == 1)
                return types[0];

            // Score each type
            var scored = types.Select(t => new
            {
                Type = t,
                Score = CalculateRelevanceScore(t, normalizedName)
            }).OrderByDescending(x => x.Score);

            return scored.First().Type;
        }

        private static int CalculateRelevanceScore(Type type, string normalizedName)
        {
            int score = 0;

            // Prefer UnityEngine types
            if (type.Namespace?.StartsWith("UnityEngine") == true)
                score += 10;

            // Prefer non-abstract concrete types
            if (!type.IsAbstract)
                score += 5;

            // Exact name match gets high score
            if (NormalizeName(type.Name) == normalizedName)
                score += 20;

            // Common types get bonus
            if (type == typeof(Transform)) score += 50;
            if (type == typeof(Camera)) score += 40;
            if (type == typeof(Light)) score += 40;
            if (type == typeof(Rigidbody)) score += 40;
            if (type == typeof(Rigidbody2D)) score += 40;
            if (type == typeof(Animator)) score += 30;
            if (type == typeof(Canvas)) score += 30;
            if (type == typeof(Button)) score += 30;
            if (type == typeof(Text)) score += 30;
            if (type == typeof(Slider)) score += 30;
            if (type == typeof(InputField)) score += 30;
            if (type == typeof(BoxCollider2D)) score += 25;
            if (type == typeof(CircleCollider2D)) score += 25;
            if (type == typeof(SphereCollider)) score += 25;

            return score;
        }


        /// <summary>
        /// Checks if a string is a valid Unity component name
        /// </summary>
        public static bool IsValidComponentName(this string componentName)
        {
            return ComponentResolver.GetComponentType(componentName) != null;
        }

        /// <summary>
        /// Gets the component type from a string name
        /// </summary>
        public static Type ToComponentType(this string componentName)
        {
            return ComponentResolver.GetComponentType(componentName);
        }

        /// <summary>
        /// Adds a component to a GameObject using a string name
        /// </summary>
        public static Component AddComponentByName(this GameObject gameObject, string componentName)
        {
            Type componentType = componentName.ToComponentType();
            if (componentType != null)
            {
                return gameObject.AddComponent(componentType);
            }
            return null;
        }

        /// <summary>
        /// Gets a component from a GameObject using a string name
        /// </summary>
        public static Component GetComponentByName(this GameObject gameObject, string componentName)
        {
            Type componentType = componentName.ToComponentType();
            if (componentType != null)
            {
                return gameObject.GetComponent(componentType);
            }
            return null;
        }

        /// <summary>
        /// Gets all components of a specific type by name
        /// </summary>
        public static Component[] GetComponentsByName(this GameObject gameObject, string componentName)
        {
            Type componentType = componentName.ToComponentType();
            if (componentType != null)
            {
                return gameObject.GetComponents(componentType);
            }
            return new Component[0];
        }
    }
}