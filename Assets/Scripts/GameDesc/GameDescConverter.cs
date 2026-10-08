using System;
using System.Collections.Generic;
using System.Linq;

namespace T2G.Assistant
{
    // Human-oriented hierarchical export model. It uses the same final
    // PropertyDesc/GameAsset/AssetRef types as the machine representation.
    [Serializable]
    public class HumanGameDesc
    {
        public string ProjectName = string.Empty;
        public string Title = string.Empty;
        public List<HumanSpace> Spaces = new List<HumanSpace>();
        public List<InstructionRecord> InstructionHistory = new List<InstructionRecord>();
    }

    [Serializable]
    public class HumanSpace
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public List<PropertyDesc> Properties = new List<PropertyDesc>();
        public List<Component> Components = new List<Component>();
        public Dictionary<string, GameAsset> Assets =
            new Dictionary<string, GameAsset>(StringComparer.OrdinalIgnoreCase);
        public List<HumanObject> Objects = new List<HumanObject>();
    }

    [Serializable]
    public class HumanObject
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Desc = string.Empty;
        public List<string> Tags = new List<string>();
        public List<string> Roles = new List<string>();
        public List<Relationship> Relationships = new List<Relationship>();
        public List<PropertyDesc> Properties = new List<PropertyDesc>();
        public List<AssetRef> Assets = new List<AssetRef>();
        public List<Component> Components = new List<Component>();
        public List<HumanObject> Children = new List<HumanObject>();
        public string Socket = string.Empty;
    }

    public static class GameDescConverter
    {
        public static HumanGameDesc ToHuman(GameDesc machine)
        {
            if (machine == null)
                return null;

            var human = new HumanGameDesc
            {
                ProjectName = machine.ProjectName ?? string.Empty,
                Title = machine.Title ?? string.Empty,
                InstructionHistory = machine.InstructionHistory != null
                    ? new List<InstructionRecord>(machine.InstructionHistory)
                    : new List<InstructionRecord>()
            };

            if (machine.Spaces == null)
                return human;

            foreach (Space space in machine.Spaces)
            {
                if (space == null) continue;

                var humanSpace = new HumanSpace
                {
                    Id = space.Id ?? string.Empty,
                    Name = space.Name ?? string.Empty,
                    Properties = CloneProperties(space.Properties),
                    Components = CloneComponents(space.Components),
                    Assets = CloneAssets(space.Assets)
                };

                var guidToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (space.Objects != null)
                {
                    foreach (Object obj in space.Objects.Values)
                    {
                        if (obj != null && !string.IsNullOrWhiteSpace(obj.Id) && !string.IsNullOrWhiteSpace(obj.Name))
                            guidToName[obj.Id] = obj.Name;
                    }
                }

                var humanById = new Dictionary<string, HumanObject>(StringComparer.OrdinalIgnoreCase);
                var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (space.Objects != null)
                {
                    foreach (Object obj in space.Objects.Values)
                    {
                        if (obj == null) continue;
                        HumanObject mapped = MapToHumanObject(obj, guidToName);
                        humanById[obj.Id] = mapped;
                    }

                    foreach (Object obj in space.Objects.Values)
                    {
                        if (obj == null || obj.Relationships == null) continue;

                        Relationship parentRel = obj.Relationships.FirstOrDefault(r =>
                            r != null &&
                            (string.Equals(r.Type, GameDescRelationTypes.Contains, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(r.Type, GameDescRelationTypes.AttachedTo, StringComparison.OrdinalIgnoreCase)) &&
                            !string.IsNullOrWhiteSpace(r.Target));

                        if (parentRel == null)
                            continue;

                        HumanObject parent;
                        HumanObject child;
                        if (humanById.TryGetValue(parentRel.Target, out parent) &&
                            humanById.TryGetValue(obj.Id, out child))
                        {
                            child.Socket = parentRel.Slot ?? string.Empty;
                            parent.Children.Add(child);
                            assigned.Add(obj.Id);
                        }
                    }

                    foreach (Object obj in space.Objects.Values)
                    {
                        if (obj == null || assigned.Contains(obj.Id)) continue;
                        HumanObject root;
                        if (humanById.TryGetValue(obj.Id, out root))
                            humanSpace.Objects.Add(root);
                    }
                }

                human.Spaces.Add(humanSpace);
            }

            return human;
        }

        public static GameDesc FromHuman(HumanGameDesc human)
        {
            if (human == null)
                return null;

            var machine = new GameDesc
            {
                ProjectName = human.ProjectName ?? string.Empty,
                Title = human.Title ?? string.Empty,
                InstructionHistory = human.InstructionHistory != null
                    ? new List<InstructionRecord>(human.InstructionHistory)
                    : new List<InstructionRecord>()
            };

            if (human.Spaces == null)
                return machine;

            foreach (HumanSpace humanSpace in human.Spaces)
            {
                if (humanSpace == null) continue;

                var space = new Space
                {
                    Id = !string.IsNullOrWhiteSpace(humanSpace.Id)
                        ? humanSpace.Id
                        : Guid.NewGuid().ToString(),
                    Name = humanSpace.Name ?? string.Empty,
                    Properties = CloneProperties(humanSpace.Properties),
                    Components = CloneComponents(humanSpace.Components),
                    Assets = CloneAssets(humanSpace.Assets),
                    Objects = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase)
                };

                var nameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var pendingRelationships = new List<Relationship>();

                FlattenHierarchy(
                    humanSpace.Objects,
                    null,
                    space.Objects,
                    nameToId,
                    pendingRelationships);

                // All objects are now known, so convert human-readable target names to stable IDs.
                foreach (Object obj in space.Objects.Values)
                {
                    if (obj == null || obj.Relationships == null) continue;
                    foreach (Relationship relation in obj.Relationships)
                    {
                        if (relation == null || string.IsNullOrWhiteSpace(relation.Target)) continue;
                        string targetId;
                        if (nameToId.TryGetValue(relation.Target, out targetId))
                            relation.Target = targetId;
                    }
                    obj.RebuildPropertyMap();
                }

                space.RebuildNameIndex();
                space.RebuildPropertyMap();
                machine.Spaces.Add(space);
            }

            return machine;
        }

        private static HumanObject MapToHumanObject(
            Object obj,
            Dictionary<string, string> guidToName)
        {
            var relationships = new List<Relationship>();
            if (obj.Relationships != null)
            {
                foreach (Relationship relation in obj.Relationships)
                {
                    if (relation == null) continue;
                    string target = relation.Target;
                    string targetName;
                    if (!string.IsNullOrWhiteSpace(target) &&
                        guidToName != null &&
                        guidToName.TryGetValue(target, out targetName))
                        target = targetName;

                    relationships.Add(new Relationship
                    {
                        Type = relation.Type ?? string.Empty,
                        Target = target ?? string.Empty,
                        Slot = relation.Slot ?? string.Empty
                    });
                }
            }

            return new HumanObject
            {
                Id = obj.Id ?? string.Empty,
                Name = obj.Name ?? string.Empty,
                Desc = obj.Desc ?? string.Empty,
                Tags = obj.Tags != null ? new List<string>(obj.Tags) : new List<string>(),
                Roles = obj.Roles != null ? new List<string>(obj.Roles) : new List<string>(),
                Relationships = relationships,
                Properties = CloneProperties(obj.Properties),
                Assets = CloneAssetRefs(obj.Assets),
                Components = CloneComponents(obj.Components)
            };
        }

        private static void FlattenHierarchy(
            List<HumanObject> humanObjects,
            HumanObject parent,
            Dictionary<string, Object> flat,
            Dictionary<string, string> nameToId,
            List<Relationship> unused)
        {
            if (humanObjects == null)
                return;

            foreach (HumanObject humanObject in humanObjects)
            {
                if (humanObject == null) continue;

                string id = !string.IsNullOrWhiteSpace(humanObject.Id)
                    ? humanObject.Id
                    : Guid.NewGuid().ToString();

                var obj = new Object
                {
                    Id = id,
                    Name = humanObject.Name ?? string.Empty,
                    Desc = humanObject.Desc ?? string.Empty,
                    Tags = humanObject.Tags != null ? new List<string>(humanObject.Tags) : new List<string>(),
                    Roles = humanObject.Roles != null ? new List<string>(humanObject.Roles) : new List<string>(),
                    Relationships = CloneRelationships(humanObject.Relationships),
                    Properties = CloneProperties(humanObject.Properties),
                    Assets = CloneAssetRefs(humanObject.Assets),
                    Components = CloneComponents(humanObject.Components)
                };

                flat[id] = obj;
                if (!string.IsNullOrWhiteSpace(obj.Name))
                    nameToId[obj.Name] = id;

                if (parent != null)
                {
                    // Preserve an explicit attached_to/contains relation to the parent if present;
                    // otherwise the hierarchy itself means "contains".
                    Relationship explicitParent = obj.Relationships.FirstOrDefault(r =>
                        r != null &&
                        string.Equals(r.Target, parent.Name, StringComparison.OrdinalIgnoreCase) &&
                        (string.Equals(r.Type, GameDescRelationTypes.Contains, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(r.Type, GameDescRelationTypes.AttachedTo, StringComparison.OrdinalIgnoreCase)));

                    if (explicitParent == null)
                    {
                        obj.Relationships.Add(new Relationship
                        {
                            Type = GameDescRelationTypes.Contains,
                            Target = parent.Name ?? string.Empty,
                            Slot = humanObject.Socket ?? string.Empty
                        });
                    }
                    else if (string.IsNullOrWhiteSpace(explicitParent.Slot))
                    {
                        explicitParent.Slot = humanObject.Socket ?? string.Empty;
                    }
                }

                FlattenHierarchy(humanObject.Children, humanObject, flat, nameToId, unused);
            }
        }

        private static List<PropertyDesc> CloneProperties(List<PropertyDesc> source)
        {
            if (source == null) return new List<PropertyDesc>();
            return source.Where(p => p != null).Select(p => new PropertyDesc
            {
                Name = p.Name ?? string.Empty,
                Type = p.Type ?? string.Empty,
                Value = p.Value != null ? p.Value.DeepClone() : null
            }).ToList();
        }

        private static List<Relationship> CloneRelationships(List<Relationship> source)
        {
            if (source == null) return new List<Relationship>();
            return source.Where(r => r != null).Select(r => new Relationship
            {
                Type = r.Type ?? string.Empty,
                Target = r.Target ?? string.Empty,
                Slot = r.Slot ?? string.Empty
            }).ToList();
        }

        private static List<AssetRef> CloneAssetRefs(List<AssetRef> source)
        {
            if (source == null) return new List<AssetRef>();
            return source.Where(a => a != null).Select(a => new AssetRef
            {
                AssetId = a.AssetId ?? string.Empty
            }).ToList();
        }

        private static Dictionary<string, GameAsset> CloneAssets(Dictionary<string, GameAsset> source)
        {
            var result = new Dictionary<string, GameAsset>(StringComparer.OrdinalIgnoreCase);
            if (source == null) return result;

            foreach (var pair in source)
            {
                GameAsset a = pair.Value;
                if (a == null) continue;
                result[pair.Key] = new GameAsset
                {
                    Id = a.Id ?? string.Empty,
                    Name = a.Name ?? string.Empty,
                    Source = a.Source ?? string.Empty,
                    ImportPath = a.ImportPath ?? string.Empty,
                    LoadPath = a.LoadPath ?? string.Empty,
                    Type = a.Type ?? string.Empty
                };
            }
            return result;
        }

        private static List<Component> CloneComponents(List<Component> source)
        {
            if (source == null) return new List<Component>();
            return source.Where(c => c != null).Select(c => new Component
            {
                Type = c.Type ?? string.Empty,
                SourceType = c.SourceType,
                Description = c.Description ?? string.Empty,
                Properties = CloneProperties(c.Properties),
                Assets = CloneAssetRefs(c.Assets)
            }).ToList();
        }
    }
}
