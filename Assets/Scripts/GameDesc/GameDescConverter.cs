using System;
using System.Collections.Generic;
using System.Linq;

namespace T2G.Assistant
{
    // ============================================================
    // Human-oriented (hierarchical) export model
    // ============================================================

    [Serializable]
    public class HumanGameDesc
    {
        public string ProjectName;
        public string Title;
        public List<HumanSpace> Spaces;
        public List<InstructionRecord> InstructionHistory;
    }

    [Serializable]
    public class HumanSpace
    {
        public string Name;
        public List<Component> Components = new List<Component>();
        public List<HumanObject> Objects = new List<HumanObject>();

        /// <summary>
        /// Asset library for this space. Key = download URL or absolute path, Value = load info.
        /// </summary>
        public Dictionary<string, AssetInfo> Assets = new Dictionary<string, AssetInfo>();
    }

    [Serializable]
    public class HumanObject
    {
        public string Name;
        public string Desc;
        public List<string> Tags = new List<string>();
        public List<string> Roles = new List<string>();
        public List<Relationship> Relationships = new List<Relationship>();
        public List<ValuePair> Properties = new List<ValuePair>();
        public List<string> Assets = new List<string>();
        public List<Component> Components = new List<Component>();
        public List<HumanObject> Children = new List<HumanObject>();
        public string Socket;
    }

    // ============================================================
    // Converter: machine (flat) ↔ human (hierarchical)
    // ============================================================

    public static class GameDescConverter
    {
        /// <summary>
        /// Converts the internal flat GameDesc to a hierarchical HumanGameDesc
        /// for export, debugging, or human inspection.
        /// Hierarchy is reconstructed from "contains" and "attached_to" relationships.
        /// Relationship targets are resolved from GUID → Name for human readability.
        /// </summary>
        public static HumanGameDesc ToHuman(GameDesc machine)
        {
            if (machine == null)
                return null;

            var human = new HumanGameDesc
            {
                ProjectName = machine.ProjectName,
                Title = machine.Title,
                InstructionHistory = machine.InstructionHistory != null
                    ? new List<InstructionRecord>(machine.InstructionHistory)
                    : new List<InstructionRecord>(),
                Spaces = new List<HumanSpace>()
            };

            if (machine.Spaces == null)
                return human;

            foreach (var space in machine.Spaces)
            {
                if (space == null) continue;

                var humanSpace = new HumanSpace
                {
                    Name = space.Name,
                    Components = space.Components != null
                        ? new List<Component>(space.Components)
                        : new List<Component>(),
                    Objects = new List<HumanObject>(),
                    Assets = space.Assets != null
                        ? new Dictionary<string, AssetInfo>(space.Assets)
                        : new Dictionary<string, AssetInfo>()
                };

                // Build GUID → Name lookup for resolving relationship targets
                var guidToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var obj in space.Objects.Values)
                {
                    if (obj != null && !string.IsNullOrWhiteSpace(obj.Name))
                        guidToName[obj.Id] = obj.Name;
                }

                // Build lookup: object name → HumanObject
                var humanObjMap = new Dictionary<string, HumanObject>(StringComparer.OrdinalIgnoreCase);

                // Pass 1: create HumanObject for every flat Object, resolve GUID → Name
                foreach (var obj in space.Objects.Values)
                {
                    if (obj == null) continue;
                    var hObj = MapToHumanObject(obj);
                    ResolveRelationshipTargets(hObj, guidToName);
                    humanObjMap[obj.Name] = hObj;
                }

                // Pass 2: build tree — find all objects that have a "contains" or "attached_to" parent
                foreach (var obj in space.Objects.Values)
                {
                    if (obj == null) continue;

                    string parentName = null;
                    string socket = null;
                    foreach (var rel in obj.Relationships)
                    {
                        if (string.Equals(rel.Type, GameDescRelationTypes.Contains, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(rel.Type, GameDescRelationTypes.AttachedTo, StringComparison.OrdinalIgnoreCase))
                        {
                            parentName = guidToName.TryGetValue(rel.Target, out var pn) ? pn : rel.Target;
                            socket = rel.Slot;
                            break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(parentName) && humanObjMap.TryGetValue(parentName, out var parent))
                    {
                        var hObj = humanObjMap[obj.Name];
                        hObj.Socket = socket ?? string.Empty;
                        parent.Children ??= new List<HumanObject>();
                        parent.Children.Add(hObj);
                    }
                }

                // Pass 3: root objects are those not assigned as children
                var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var obj in space.Objects.Values)
                {
                    if (obj == null) continue;
                    string parentName = null;
                    foreach (var rel in obj.Relationships)
                    {
                        if (string.Equals(rel.Type, GameDescRelationTypes.Contains, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(rel.Type, GameDescRelationTypes.AttachedTo, StringComparison.OrdinalIgnoreCase))
                        {
                            parentName = guidToName.TryGetValue(rel.Target, out var pn) ? pn : rel.Target;
                            break;
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(parentName) && humanObjMap.ContainsKey(parentName))
                    {
                        assigned.Add(obj.Name);
                    }
                }

                foreach (var obj in space.Objects.Values)
                {
                    if (obj == null) continue;
                    if (!assigned.Contains(obj.Name))
                    {
                        humanSpace.Objects.Add(humanObjMap[obj.Name]);
                    }
                }

                human.Spaces.Add(humanSpace);
            }

            return human;
        }

        /// <summary>
        /// Converts a hierarchical HumanGameDesc back to the internal flat GameDesc.
        /// Parent-child relationships become "contains" relationships.
        /// Relationship targets are resolved from Name → GUID for internal consistency.
        /// </summary>
        public static GameDesc FromHuman(HumanGameDesc human)
        {
            if (human == null)
                return null;

            var machine = new GameDesc
            {
                ProjectName = human.ProjectName,
                Title = human.Title,
                InstructionHistory = human.InstructionHistory != null
                    ? new List<InstructionRecord>(human.InstructionHistory)
                    : new List<InstructionRecord>(),
                Spaces = new List<Space>()
            };

            if (human.Spaces == null)
                return machine;

            foreach (var humanSpace in human.Spaces)
            {
                if (humanSpace == null) continue;

                var space = new Space
                {
                    Name = humanSpace.Name,
                    Components = humanSpace.Components != null
                        ? new List<Component>(humanSpace.Components)
                        : new List<Component>(),
                    Objects = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase),
                    Assets = humanSpace.Assets != null
                        ? new Dictionary<string, AssetInfo>(humanSpace.Assets)
                        : new Dictionary<string, AssetInfo>()
                };

                var nameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                FlattenHierarchy(humanSpace.Objects, null, space.Objects, nameToId);

                // Final Name → GUID resolution for all relationship targets
                foreach (var obj in space.Objects.Values)
                {
                    if (obj?.Relationships == null) continue;
                    foreach (var rel in obj.Relationships)
                    {
                        if (rel != null && !string.IsNullOrWhiteSpace(rel.Target) && nameToId.TryGetValue(rel.Target, out var gid))
                            rel.Target = gid;
                    }
                }

                // Migrate any legacy comma-delimited or unresolvable object assets into Space.Assets
                foreach (var obj in space.Objects.Values)
                {
                    if (obj?.Assets == null || obj.Assets.Count == 0) continue;
                    for (int i = 0; i < obj.Assets.Count; i++)
                    {
                        var assetStr = obj.Assets[i];
                        if (string.IsNullOrWhiteSpace(assetStr))
                        {
                            obj.Assets.RemoveAt(i--);
                            continue;
                        }
                        bool needsMigration = assetStr.IndexOf(',') >= 0 || !space.Assets.ContainsKey(assetStr);
                        if (!needsMigration) continue;

                        var parts = assetStr.Split(new[] { ',' }, 2);
                        string key = parts[0].Trim();
                        string loadPath = parts.Length > 1 ? parts[1].Trim() : key;

                        if (!space.Assets.ContainsKey(key))
                        {
                            string ext = System.IO.Path.GetExtension(key)?.ToLowerInvariant()?.TrimStart('.');
                            space.Assets[key] = new AssetInfo
                            {
                                ImportPath = key,
                                LoadPath = loadPath,
                                Type = ext ?? ""
                            };
                        }
                        obj.Assets[i] = key;
                    }
                }

                machine.Spaces.Add(space);
            }

            return machine;
        }

        // ============================================================
        // Private helpers
        // ============================================================

        private static HumanObject MapToHumanObject(Object obj)
        {
            return new HumanObject
            {
                Name = obj.Name,
                Desc = obj.Desc,
                Tags = obj.Tags != null ? new List<string>(obj.Tags) : new List<string>(),
                Roles = obj.Roles != null ? new List<string>(obj.Roles) : new List<string>(),
                Relationships = obj.Relationships != null
                    ? obj.Relationships.Select(r => new Relationship
                    {
                        Type = r.Type,
                        Target = r.Target,
                        Slot = r.Slot
                    }).ToList()
                    : new List<Relationship>(),
                Properties = obj.Properties != null ? new List<ValuePair>(obj.Properties) : new List<ValuePair>(),
                Assets = obj.Assets != null ? new List<string>(obj.Assets) : new List<string>(),
                Components = obj.Components != null ? new List<Component>(obj.Components) : new List<Component>(),
                Children = new List<HumanObject>(),
                Socket = string.Empty
            };
        }

        private static void ResolveRelationshipTargets(HumanObject hObj, Dictionary<string, string> guidToName)
        {
            if (hObj?.Relationships == null) return;
            foreach (var rel in hObj.Relationships)
            {
                if (rel != null && guidToName.TryGetValue(rel.Target, out var name))
                    rel.Target = name;
            }
            if (hObj.Children != null)
            {
                foreach (var child in hObj.Children)
                    ResolveRelationshipTargets(child, guidToName);
            }
        }

        private static void FlattenHierarchy(
            List<HumanObject> humanObjects,
            string parentName,
            Dictionary<string, Object> flatDict,
            Dictionary<string, string> nameToId)
        {
            if (humanObjects == null) return;

            foreach (var hObj in humanObjects)
            {
                if (hObj == null) continue;

                var objId = Guid.NewGuid().ToString();
                var obj = new Object
                {
                    Id = objId,
                    Name = hObj.Name,
                    Desc = hObj.Desc,
                    Tags = hObj.Tags != null ? new List<string>(hObj.Tags) : new List<string>(),
                    Roles = hObj.Roles != null ? new List<string>(hObj.Roles) : new List<string>(),
                    Relationships = hObj.Relationships != null
                        ? hObj.Relationships.Select(r => new Relationship
                        {
                            Type = r.Type,
                            Target = r.Target,
                            Slot = r.Slot
                        }).ToList()
                        : new List<Relationship>(),
                    Properties = hObj.Properties != null ? new List<ValuePair>(hObj.Properties) : new List<ValuePair>(),
                    Assets = hObj.Assets != null ? new List<string>(hObj.Assets) : new List<string>(),
                    Components = hObj.Components != null ? new List<Component>(hObj.Components) : new List<Component>()
                };

                // Resolve existing relationship targets from Name → GUID
                foreach (var rel in obj.Relationships)
                {
                    if (rel != null && !string.IsNullOrWhiteSpace(rel.Target) && nameToId.TryGetValue(rel.Target, out var tid))
                        rel.Target = tid;
                }

                // Add parent-child relationship with parent's GUID
                if (!string.IsNullOrWhiteSpace(parentName))
                {
                    string relType = GameDescRelationTypes.Contains;
                    string relSlot = hObj.Socket ?? string.Empty;

                    var existingRel = hObj.Relationships?.FirstOrDefault(r =>
                        r != null &&
                        string.Equals(r.Target, parentName, StringComparison.OrdinalIgnoreCase) &&
                        (string.Equals(r.Type, GameDescRelationTypes.Contains, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(r.Type, GameDescRelationTypes.AttachedTo, StringComparison.OrdinalIgnoreCase)));

                    if (existingRel != null)
                    {
                        relType = existingRel.Type;
                        relSlot = existingRel.Slot;
                    }

                    string parentId = nameToId.TryGetValue(parentName, out var pid) ? pid : parentName;
                    obj.Relationships.Add(new Relationship
                    {
                        Type = relType,
                        Target = parentId,
                        Slot = relSlot
                    });
                }

                flatDict[objId] = obj;
                nameToId[hObj.Name] = objId;

                if (hObj.Children != null && hObj.Children.Count > 0)
                {
                    FlattenHierarchy(hObj.Children, hObj.Name, flatDict, nameToId);
                }
            }
        }
    }
}
