using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using T2G;
using UnityEngine;

namespace T2G.Assistant
{
    /// <summary>
    /// Maintains the persistent GameDesc.
    ///
    /// Instruction = intended operation.
    /// Response    = authoritative execution result.
    /// GameDesc    = established game state.
    ///
    /// Failed execution is recorded but never mutates persistent state.
    /// </summary>
    public partial class GameDescManager
    {
        #region Singleton

        private static GameDescManager _instance;

        public static GameDescManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GameDescManager();

                return _instance;
            }
        }

        private GameDescManager()
        {
        }

        #endregion

        public GameDesc CurrentGameDesc { get; private set; }

        // Runtime context only; not persisted in GameDesc.
        public Space CurrentSpace { get; private set; }
        public Object CurrentObject { get; private set; }


        public void SetGameDesc(GameDesc gameDesc)
        {
            CurrentGameDesc = gameDesc;
            CurrentSpace = null;
            CurrentObject = null;

            if (CurrentGameDesc == null)
                return;

            CurrentGameDesc.Spaces = CurrentGameDesc.Spaces ?? new List<Space>();
            CurrentGameDesc.InstructionHistory =
                CurrentGameDesc.InstructionHistory ?? new List<InstructionRecord>();

            foreach (Space space in CurrentGameDesc.Spaces)
                NormalizeSpace(space);
        }

        public GameDesc CreateGameDesc(string projectName, string title = "")
        {
            CurrentGameDesc = new GameDesc
            {
                ProjectName = projectName ?? string.Empty,
                Title = title ?? string.Empty
            };

            CurrentSpace = null;
            CurrentObject = null;
            return CurrentGameDesc;
        }

        /// <summary>
        /// Compatibility wrapper retained for existing Assistant code.
        /// New code may call CreateGameDesc directly.
        /// </summary>
        public GameDesc CreateGameDescProject(string projectName, string title = "")
        {
            return CreateGameDesc(projectName, title);
        }

        /// <summary>
        /// Opens the persisted GameDesc for a project when it exists; otherwise
        /// creates a new authoritative GameDesc for that project.
        /// </summary>
        public GameDesc OpenOrCreateGameDesc(string projectName, string title = "")
        {
            if (string.IsNullOrWhiteSpace(projectName))
                throw new ArgumentException("projectName is empty.", nameof(projectName));

            string filePath = GetGameDescFilePath(projectName);
            GameDesc gameDesc;

            if (File.Exists(filePath))
            {
                gameDesc = DeserializeGameDescFile(filePath);
                if (gameDesc == null)
                    throw new InvalidOperationException(
                        "Failed to load GameDesc for project '" + projectName + "'.");

                if (string.IsNullOrWhiteSpace(gameDesc.ProjectName))
                    gameDesc.ProjectName = projectName;

                if (string.IsNullOrWhiteSpace(gameDesc.Title) &&
                    !string.IsNullOrWhiteSpace(title))
                    gameDesc.Title = title;
            }
            else
            {
                gameDesc = new GameDesc
                {
                    ProjectName = projectName,
                    Title = title ?? string.Empty
                };
            }

            SetGameDesc(gameDesc);
            return CurrentGameDesc;
        }

        /// <summary>
        /// Saves the authoritative flat GameDesc used internally by T2G.
        /// </summary>
        public void SaveGameDesc(string projectName = null)
        {
            if (CurrentGameDesc == null)
                return;

            if (!string.IsNullOrWhiteSpace(projectName))
                CurrentGameDesc.ProjectName = projectName;

            if (string.IsNullOrWhiteSpace(CurrentGameDesc.ProjectName))
                throw new InvalidOperationException(
                    "Cannot save GameDesc because ProjectName is empty.");

            string filePath = GetGameDescFilePath(CurrentGameDesc.ProjectName);
            string json = SerializeGameDesc(CurrentGameDesc, false);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Exports a hierarchical, human-oriented representation without replacing
        /// or mutating CurrentGameDesc.
        /// </summary>
        public void SaveHumanGameDesc(string filePath)
        {
            if (CurrentGameDesc == null)
                throw new InvalidOperationException(
                    "CurrentGameDesc has not been initialized.");
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("filePath is empty.", nameof(filePath));

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string json = SerializeGameDesc(CurrentGameDesc, true);
            File.WriteAllText(filePath, json);
        }

        private static string GetGameDescFilePath(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                throw new ArgumentException("projectName is empty.", nameof(projectName));

            string directory = Path.Combine(
                Application.persistentDataPath,
                "GameDesc");

            Directory.CreateDirectory(directory);
            return Path.Combine(directory, projectName + ".json");
        }

        /// <summary>
        /// Reconciles a successfully/unsuccessfully executed instruction.
        /// History is always recorded. Persistent state changes only on success.
        /// </summary>
        public bool Apply(Instruction instruction, Response response)
        {
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            EnsureGameDesc();
            RecordHistory(instruction, response);

            if (!response.Succeeded)
                return false;

            switch (instruction.action)
            {
                case Actions.no_action:
                    break;

                case Actions.create_project:
                    ApplyCreateProject(instruction, response);
                    break;

                case Actions.create_from:
                    // Orchestration action. Generated elemental instructions
                    // reconstruct the persistent state.
                    break;

                case Actions.init_project:
                    ApplyInitProject(instruction, response);
                    break;

                case Actions.open_project:
                    // Existing GameDesc should be loaded and passed to SetGameDesc().
                    break;

                case Actions.connect:
                case Actions.disconnect:
                case Actions.clear:
                    break;

                case Actions.import_assets:
                    ApplyImportAssets(instruction, response);
                    break;

                case Actions.create_space:
                    ApplyCreateSpace(instruction, response);
                    break;

                case Actions.goto_space:
                    ApplyGotoSpace(instruction);
                    break;

                case Actions.save_space:
                    break;

                case Actions.rename_space:
                    ApplyRenameSpace(instruction, response);
                    break;

                case Actions.create_object:
                    ApplyCreateObject(instruction, response);
                    break;

                case Actions.select_object:
                    ApplySelectObject(instruction);
                    break;

                case Actions.delete_object:
                    ApplyDeleteObject(instruction, response);
                    break;

                case Actions.place_on:
                    ApplyPlaceOn(instruction, response);
                    break;

                case Actions.attach_to:
                    ApplyAttachTo(instruction, response);
                    break;

                case Actions.detach_from:
                    ApplyDetachFrom(instruction, response);
                    break;

                case Actions.set_property:
                    ApplySetProperty(instruction, response);
                    break;

                case Actions.set_relationship:
                    ApplySetRelationship(instruction, response);
                    break;

                case Actions.add_component:
                    ApplyAddComponent(instruction, response);
                    break;

                case Actions.remove_component:
                    ApplyRemoveComponent(instruction, response);
                    break;

                case Actions.call_method:
                    // Do not infer arbitrary persistent changes from a method call.
                    break;

                default:
                    throw new InvalidOperationException(
                        "Unsupported GameDesc action: '" + instruction.action + "'.");
            }

            return true;
        }

        private void ApplyCreateProject(Instruction instruction, Response response)
        {
            string projectName = GetParameterString(instruction, "ProjectName");
            CurrentGameDesc.ProjectName =
                response.GetValue<string>("ProjectName", projectName) ?? string.Empty;
        }

        private void ApplyInitProject(Instruction instruction, Response response)
        {
            string projectName = response.GetValue<string>(
                "ProjectName",
                GetParameterString(instruction, "ProjectName"));

            if (!string.IsNullOrWhiteSpace(projectName))
                CurrentGameDesc.ProjectName = projectName;
        }

        private void ApplyCreateSpace(Instruction instruction, Response response)
        {
            string name = response.GetValue<string>(
                "SpaceName",
                GetParameterString(instruction, "SpaceName"));

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("create_space requires SpaceName.");

            Space existing = FindSpaceByName(name);
            if (existing != null)
            {
                CurrentSpace = existing;
                CurrentObject = null;
                return;
            }

            string id = response.GetValue<string>("SpaceId", string.Empty);
            if (string.IsNullOrWhiteSpace(id))
                id = Guid.NewGuid().ToString();

            Space space = new Space
            {
                Id = id,
                Name = name
            };

            CurrentGameDesc.Spaces.Add(space);
            CurrentSpace = space;
            CurrentObject = null;
        }

        private void ApplyGotoSpace(Instruction instruction)
        {
            string name = GetParameterString(instruction, "SpaceName");
            Space space = FindSpaceByName(name);

            if (space == null)
                throw new InvalidOperationException("Space '" + name + "' does not exist.");

            CurrentSpace = space;
            CurrentObject = null;
        }

        private void ApplyRenameSpace(Instruction instruction, Response response)
        {
            RequireCurrentSpace();

            string newName = response.GetValue<string>(
                "SpaceName",
                GetParameterString(instruction, "SpaceName"));

            if (string.IsNullOrWhiteSpace(newName))
                throw new InvalidOperationException("rename_space requires SpaceName.");

            CurrentSpace.Name = newName;
        }

        private void ApplyCreateObject(Instruction instruction, Response response)
        {
            RequireCurrentSpace();

            string name = response.GetValue<string>(
                "Name",
                GetParameterString(instruction, "Name"));

            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("create_object requires Name.");

            Object existing;
            if (CurrentSpace.TryGetObjectByName(name, out existing))
            {
                CurrentObject = existing;
                return;
            }

            string id = response.GetValue<string>("ObjectId", string.Empty);
            if (string.IsNullOrWhiteSpace(id))
                id = Guid.NewGuid().ToString();

            Object obj = new Object
            {
                Id = id,
                Name = name,
                Desc = instruction.desc ?? string.Empty
            };

            // Optional semantic metadata emitted by GameDescParser during regeneration.
            obj.Tags = ParseStringListParameter(instruction, "Tags");
            obj.Roles = ParseStringListParameter(instruction, "Roles");

            Instruction.Parameter position = GetParameter(instruction, "Position");
            if (position != null)
            {
                JToken actualPosition = GetResponseValue(
                    response, "Position", position.value);

                SetProperty(
                    obj.Properties,
                    "Position",
                    NormalizeType(position.type, actualPosition),
                    CloneToken(actualPosition));
            }

            RegisterInstructionAssets(instruction, response, obj);

            CurrentSpace.Objects[id] = obj;
            CurrentSpace.RebuildNameIndex();
            obj.RebuildPropertyMap();

            CurrentObject = obj;
        }

        private void ApplySelectObject(Instruction instruction)
        {
            RequireCurrentSpace();

            string name = GetParameterString(instruction, "Name");
            Object obj = FindObjectByName(name);

            if (obj == null)
                throw new InvalidOperationException(
                    "Object '" + name + "' does not exist in space '" +
                    CurrentSpace.Name + "'.");

            CurrentObject = obj;
        }

        private void ApplyDeleteObject(Instruction instruction, Response response)
        {
            RequireCurrentSpace();

            string name = GetParameterString(instruction, "Name");
            string objectId = response.GetValue<string>("ObjectId", string.Empty);

            Object obj = null;

            if (!string.IsNullOrWhiteSpace(objectId))
                CurrentSpace.TryGetObjectById(objectId, out obj);

            if (obj == null)
                obj = FindObjectByName(name);

            if (obj == null)
                return;

            string removedId = obj.Id;
            CurrentSpace.Objects.Remove(removedId);

            foreach (Object remaining in CurrentSpace.Objects.Values)
            {
                if (remaining == null || remaining.Relationships == null)
                    continue;

                remaining.Relationships.RemoveAll(
                    r => r != null &&
                         string.Equals(
                             r.Target,
                             removedId,
                             StringComparison.OrdinalIgnoreCase));
            }

            CurrentSpace.RebuildNameIndex();

            if (CurrentObject != null &&
                string.Equals(CurrentObject.Id, removedId, StringComparison.OrdinalIgnoreCase))
                CurrentObject = null;
        }

        private void ApplyPlaceOn(Instruction instruction, Response response)
        {
            RequireCurrentSpace();

            string name = GetParameterString(instruction, "Name");
            Object obj = FindObjectByName(name);

            if (obj == null)
                throw new InvalidOperationException("Object '" + name + "' does not exist.");

            ApplyResponsePropertyIfPresent(response, obj.Properties, "Position", "Vector3");
            ApplyResponsePropertyIfPresent(response, obj.Properties, "Rotation", "Vector3");
            ApplyResponsePropertyIfPresent(response, obj.Properties, "Scale", "Vector3");

            obj.RebuildPropertyMap();
        }

        private void ApplyAttachTo(Instruction instruction, Response response)
        {
            RequireCurrentSpace();

            Object source = ResolveResponseOrNamedObject(
                response,
                "SourceObjectId",
                GetParameterString(instruction, "Source"));

            Object target = ResolveResponseOrNamedObject(
                response,
                "TargetObjectId",
                GetParameterString(instruction, "Target"));

            if (source == null || target == null)
                throw new InvalidOperationException(
                    "attach_to source or target could not be resolved.");

            AddOrUpdateRelationship(
                source,
                GameDescRelationTypes.AttachedTo,
                target.Id,
                string.Empty);
        }

        private void ApplyDetachFrom(Instruction instruction, Response response)
        {
            RequireCurrentSpace();

            Object source = ResolveResponseOrNamedObject(
                response,
                "SourceObjectId",
                GetParameterString(instruction, "Name"));

            if (source == null || source.Relationships == null)
                return;

            string targetId = response.GetValue<string>("TargetObjectId", string.Empty);

            source.Relationships.RemoveAll(
                r =>
                {
                    if (r == null ||
                        !string.Equals(
                            r.Type,
                            GameDescRelationTypes.AttachedTo,
                            StringComparison.OrdinalIgnoreCase))
                        return false;

                    return string.IsNullOrWhiteSpace(targetId) ||
                           string.Equals(
                               r.Target,
                               targetId,
                               StringComparison.OrdinalIgnoreCase);
                });
        }

        private void ApplySetRelationship(Instruction instruction, Response response)
        {
            RequireCurrentSpace();

            Object source = ResolveResponseOrNamedObject(
                response,
                "SourceObjectId",
                GetParameterString(instruction, "Source"));

            Object target = ResolveResponseOrNamedObject(
                response,
                "TargetObjectId",
                GetParameterString(instruction, "Target"));

            if (source == null || target == null)
                throw new InvalidOperationException(
                    "set_relationship source or target could not be resolved.");

            string type = response.GetValue<string>(
                "RelationshipType",
                GetParameterString(instruction, "Type"));

            string slot = response.GetValue<string>(
                "Slot",
                GetParameterString(instruction, "Slot"));

            AddOrUpdateRelationship(source, type, target.Id, slot);
        }

        private void ApplySetProperty(Instruction instruction, Response response)
        {
            RequireCurrentSpace();

            string ownerName = GetParameterString(instruction, "ObjName");
            string propertyName = GetParameterString(instruction, "Property");
            Instruction.Parameter valueParameter = GetParameter(instruction, "Value");

            if (valueParameter == null)
                throw new InvalidOperationException("set_property requires Value.");

            JToken actualValue = GetResponseValue(response, "Value", valueParameter.value);
            string type = response.GetValue<string>(
                "ValueType",
                NormalizeType(valueParameter.type, actualValue));

            // Regeneration uses ComponentType.PropertyName to address component
            // properties without changing the elemental set_property schema.
            string componentType;
            string componentProperty;
            bool isComponentProperty = TrySplitComponentProperty(
                propertyName, out componentType, out componentProperty);

            if (string.Equals(CurrentSpace.Name, ownerName, StringComparison.OrdinalIgnoreCase))
            {
                if (isComponentProperty)
                {
                    Component component = FindComponentByType(CurrentSpace.Components, componentType);
                    if (component == null)
                        throw new InvalidOperationException(
                            "Component '" + componentType + "' does not exist on space '" + ownerName + "'.");

                    SetProperty(component.Properties, componentProperty, type, CloneToken(actualValue));
                    component.RebuildPropertyMap();
                }
                else
                {
                    SetProperty(CurrentSpace.Properties, propertyName, type, CloneToken(actualValue));
                    CurrentSpace.RebuildPropertyMap();
                }
                return;
            }

            Object obj = FindObjectByName(ownerName);
            if (obj == null)
                throw new InvalidOperationException(
                    "Property owner '" + ownerName + "' could not be resolved.");

            if (isComponentProperty)
            {
                Component component = FindComponentByType(obj.Components, componentType);
                if (component == null)
                    throw new InvalidOperationException(
                        "Component '" + componentType + "' does not exist on object '" + ownerName + "'.");

                SetProperty(component.Properties, componentProperty, type, CloneToken(actualValue));
                component.RebuildPropertyMap();
            }
            else
            {
                SetProperty(obj.Properties, propertyName, type, CloneToken(actualValue));
                obj.RebuildPropertyMap();
            }
        }

        private void ApplyAddComponent(Instruction instruction, Response response)
        {
            RequireCurrentSpace();

            string ownerName = GetParameterString(instruction, "ObjName");
            string componentType = response.GetValue<string>(
                "ComponentType",
                GetParameterString(instruction, "Type"));

            if (string.IsNullOrWhiteSpace(componentType))
                throw new InvalidOperationException("add_component requires Type.");

            Component component = CreateComponentFromInstruction(
                instruction, response, componentType);

            if (string.Equals(
                CurrentSpace.Name,
                ownerName,
                StringComparison.OrdinalIgnoreCase))
            {
                RemoveComponentByType(CurrentSpace.Components, componentType);
                CurrentSpace.Components.Add(component);
                return;
            }

            Object obj = FindObjectByName(ownerName);
            if (obj == null)
                throw new InvalidOperationException(
                    "Component owner '" + ownerName + "' could not be resolved.");

            RemoveComponentByType(obj.Components, componentType);
            obj.Components.Add(component);
        }

        private void ApplyRemoveComponent(Instruction instruction, Response response)
        {
            RequireCurrentSpace();

            string ownerName = GetParameterString(instruction, "ObjName");
            string componentType = response.GetValue<string>(
                "ComponentType",
                GetParameterString(instruction, "ComponentType"));

            if (string.Equals(
                CurrentSpace.Name,
                ownerName,
                StringComparison.OrdinalIgnoreCase))
            {
                RemoveComponentByType(CurrentSpace.Components, componentType);
                return;
            }

            Object obj = FindObjectByName(ownerName);
            if (obj != null)
                RemoveComponentByType(obj.Components, componentType);
        }

        private void ApplyImportAssets(Instruction instruction, Response response)
        {
            RequireCurrentSpace();
            RegisterInstructionAssets(instruction, response, null);
        }

        private void RecordHistory(Instruction instruction, Response response)
        {
            EnsureGameDesc();

            CurrentGameDesc.InstructionHistory.Add(
                new InstructionRecord
                {
                    InstructionJson = JsonConvert.SerializeObject(instruction),
                    ResponseJson = JsonConvert.SerializeObject(response),
                    ExecutedUtc = DateTime.UtcNow,
                    Succeeded = response.Succeeded
                });
        }

        public Space FindSpaceByName(string name)
        {
            if (CurrentGameDesc == null ||
                CurrentGameDesc.Spaces == null ||
                string.IsNullOrWhiteSpace(name))
                return null;

            foreach (Space space in CurrentGameDesc.Spaces)
            {
                if (space != null &&
                    string.Equals(space.Name, name, StringComparison.OrdinalIgnoreCase))
                    return space;
            }

            return null;
        }

        public Object FindObjectByName(string name)
        {
            if (CurrentSpace == null || string.IsNullOrWhiteSpace(name))
                return null;

            Object obj;
            return CurrentSpace.TryGetObjectByName(name, out obj) ? obj : null;
        }

        private Object ResolveResponseOrNamedObject(
            Response response,
            string responseIdName,
            string fallbackName)
        {
            string id = response.GetValue<string>(responseIdName, string.Empty);

            if (!string.IsNullOrWhiteSpace(id))
            {
                Object byId;
                if (CurrentSpace.TryGetObjectById(id, out byId))
                    return byId;
            }

            return FindObjectByName(fallbackName);
        }

        private static Instruction.Parameter GetParameter(
            Instruction instruction,
            string name)
        {
            if (instruction == null || instruction.parameters == null)
                return null;

            foreach (Instruction.Parameter parameter in instruction.parameters)
            {
                if (parameter != null &&
                    string.Equals(parameter.name, name, StringComparison.OrdinalIgnoreCase))
                    return parameter;
            }

            return null;
        }

        private static string GetParameterString(
            Instruction instruction,
            string name)
        {
            Instruction.Parameter parameter = GetParameter(instruction, name);

            if (parameter == null || parameter.value == null)
                return string.Empty;

            try
            {
                return parameter.value.ToObject<string>() ?? string.Empty;
            }
            catch
            {
                return parameter.value.ToString();
            }
        }

        private static JToken GetResponseValue(
            Response response,
            string name,
            JToken fallback)
        {
            Response.Result result = response.GetResultOrNull(name);
            return result != null && result.value != null ? result.value : fallback;
        }

        private static void SetProperty(
            List<PropertyDesc> properties,
            string name,
            string type,
            JToken value)
        {
            if (properties == null)
                throw new ArgumentNullException(nameof(properties));

            PropertyDesc existing = null;

            foreach (PropertyDesc property in properties)
            {
                if (property != null &&
                    string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    existing = property;
                    break;
                }
            }

            if (existing == null)
            {
                existing = new PropertyDesc { Name = name ?? string.Empty };
                properties.Add(existing);
            }

            existing.Type = type ?? string.Empty;
            existing.Value = value;
        }

        private static void ApplyResponsePropertyIfPresent(
            Response response,
            List<PropertyDesc> properties,
            string resultName,
            string defaultType)
        {
            Response.Result result = response.GetResultOrNull(resultName);

            if (result == null || result.value == null)
                return;

            SetProperty(
                properties,
                resultName,
                !string.IsNullOrWhiteSpace(result.type) ? result.type : defaultType,
                CloneToken(result.value));
        }

        private static void AddOrUpdateRelationship(
            Object source,
            string type,
            string targetId,
            string slot)
        {
            if (source.Relationships == null)
                source.Relationships = new List<Relationship>();

            foreach (Relationship relationship in source.Relationships)
            {
                if (relationship == null)
                    continue;

                if (string.Equals(
                        relationship.Type,
                        type,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        relationship.Target,
                        targetId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        relationship.Slot ?? string.Empty,
                        slot ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase))
                    return;
            }

            source.Relationships.Add(
                new Relationship
                {
                    Type = type ?? string.Empty,
                    Target = targetId ?? string.Empty,
                    Slot = slot ?? string.Empty
                });
        }

        private Component CreateComponentFromInstruction(
            Instruction instruction,
            Response response,
            string componentType)
        {
            Component component = new Component
            {
                Type = componentType,
                SourceType = DetermineComponentSourceType(instruction),
                Description = instruction.desc ?? string.Empty
            };

            RegisterComponentAssets(instruction, response, component);
            return component;
        }

        private static ComponentSourceType DetermineComponentSourceType(
            Instruction instruction)
        {
            if (instruction != null && instruction.assets != null)
            {
                foreach (Instruction.Asset asset in instruction.assets)
                {
                    if (asset == null)
                        continue;

                    if (asset.type == AssetType.ScriptFile)
                        return ComponentSourceType.Script;

                    if (asset.type == AssetType.Identifier)
                        return ComponentSourceType.Engine;
                }
            }

            return ComponentSourceType.Unknown;
        }

        private static void RemoveComponentByType(
            List<Component> components,
            string componentType)
        {
            if (components == null || string.IsNullOrWhiteSpace(componentType))
                return;

            components.RemoveAll(
                c => c != null &&
                     string.Equals(
                         c.Type,
                         componentType,
                         StringComparison.OrdinalIgnoreCase));
        }

        private void RegisterInstructionAssets(
            Instruction instruction,
            Response response,
            Object owner)
        {
            if (instruction.assets == null)
                return;

            foreach (Instruction.Asset asset in instruction.assets)
            {
                GameAsset gameAsset = GetOrCreateGameAsset(asset, response);
                if (gameAsset == null || owner == null)
                    continue;

                if (!HasAssetRef(owner.Assets, gameAsset.Id))
                    owner.Assets.Add(new AssetRef { AssetId = gameAsset.Id });
            }
        }

        private void RegisterComponentAssets(
            Instruction instruction,
            Response response,
            Component component)
        {
            if (instruction.assets == null)
                return;

            foreach (Instruction.Asset asset in instruction.assets)
            {
                GameAsset gameAsset = GetOrCreateGameAsset(asset, response);
                if (gameAsset == null)
                    continue;

                if (!HasAssetRef(component.Assets, gameAsset.Id))
                    component.Assets.Add(new AssetRef { AssetId = gameAsset.Id });
            }
        }

        private GameAsset GetOrCreateGameAsset(
            Instruction.Asset asset,
            Response response)
        {
            if (asset == null || string.IsNullOrWhiteSpace(asset.source))
                return null;

            GameAsset gameAsset = FindGameAssetBySource(asset.source);

            if (gameAsset == null)
            {
                gameAsset = new GameAsset
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = asset.desc ?? string.Empty,
                    Source = asset.source,
                    Type = asset.type.ToString(),
                    ImportPath = asset.source,
                    LoadPath = string.Empty
                };

                CurrentSpace.Assets[gameAsset.Id] = gameAsset;
            }

            // For single-asset operations, the Executor may return authoritative
            // import/load paths. Multi-asset response conventions can be added later.
            string importPath = response.GetValue<string>("ImportPath", string.Empty);
            string loadPath = response.GetValue<string>("LoadPath", string.Empty);

            if (!string.IsNullOrWhiteSpace(importPath))
                gameAsset.ImportPath = importPath;
            if (!string.IsNullOrWhiteSpace(loadPath))
                gameAsset.LoadPath = loadPath;

            return gameAsset;
        }

        private GameAsset FindGameAssetBySource(string source)
        {
            if (CurrentSpace == null ||
                CurrentSpace.Assets == null ||
                string.IsNullOrWhiteSpace(source))
                return null;

            foreach (GameAsset asset in CurrentSpace.Assets.Values)
            {
                if (asset != null &&
                    string.Equals(asset.Source, source, StringComparison.OrdinalIgnoreCase))
                    return asset;
            }

            return null;
        }

        private static bool HasAssetRef(List<AssetRef> references, string assetId)
        {
            if (references == null)
                return false;

            foreach (AssetRef reference in references)
            {
                if (reference != null &&
                    string.Equals(
                        reference.AssetId,
                        assetId,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool TrySplitComponentProperty(
            string propertyName,
            out string componentType,
            out string componentProperty)
        {
            componentType = string.Empty;
            componentProperty = string.Empty;

            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            int separator = propertyName.IndexOf('.');
            if (separator <= 0 || separator >= propertyName.Length - 1)
                return false;

            componentType = propertyName.Substring(0, separator).Trim();
            componentProperty = propertyName.Substring(separator + 1).Trim();
            return !string.IsNullOrWhiteSpace(componentType) &&
                   !string.IsNullOrWhiteSpace(componentProperty);
        }

        private static Component FindComponentByType(
            List<Component> components,
            string componentType)
        {
            if (components == null || string.IsNullOrWhiteSpace(componentType))
                return null;

            foreach (Component component in components)
            {
                if (component != null &&
                    string.Equals(component.Type, componentType, StringComparison.OrdinalIgnoreCase))
                    return component;
            }
            return null;
        }

        private static List<string> ParseStringListParameter(
            Instruction instruction,
            string parameterName)
        {
            string value = GetParameterString(instruction, parameterName);
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
                return result;

            string[] parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string item = part.Trim();
                if (!string.IsNullOrWhiteSpace(item))
                    result.Add(item);
            }
            return result;
        }

        private void EnsureGameDesc()
        {
            if (CurrentGameDesc == null)
                CurrentGameDesc = new GameDesc();

            CurrentGameDesc.Spaces =
                CurrentGameDesc.Spaces ?? new List<Space>();

            CurrentGameDesc.InstructionHistory =
                CurrentGameDesc.InstructionHistory ?? new List<InstructionRecord>();
        }

        private void RequireCurrentSpace()
        {
            if (CurrentSpace == null)
                throw new InvalidOperationException("No current Space has been selected.");
        }

        private static void NormalizeSpace(Space space)
        {
            if (space == null)
                return;

            if (string.IsNullOrWhiteSpace(space.Id))
                space.Id = Guid.NewGuid().ToString();

            space.Properties = space.Properties ?? new List<PropertyDesc>();
            space.Components = space.Components ?? new List<Component>();
            space.Objects = space.Objects ??
                new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
            space.Assets = space.Assets ??
                new Dictionary<string, GameAsset>(StringComparer.OrdinalIgnoreCase);

            foreach (Object obj in space.Objects.Values)
            {
                if (obj == null)
                    continue;

                if (string.IsNullOrWhiteSpace(obj.Id))
                    obj.Id = Guid.NewGuid().ToString();

                obj.Properties = obj.Properties ?? new List<PropertyDesc>();
                obj.Components = obj.Components ?? new List<Component>();
                obj.Assets = obj.Assets ?? new List<AssetRef>();
                obj.Tags = obj.Tags ?? new List<string>();
                obj.Roles = obj.Roles ?? new List<string>();
                obj.Relationships = obj.Relationships ?? new List<Relationship>();

                obj.RebuildPropertyMap();

                foreach (Component component in obj.Components)
                {
                    if (component != null)
                        component.RebuildPropertyMap();
                }
            }

            foreach (Component component in space.Components)
            {
                if (component != null)
                    component.RebuildPropertyMap();
            }

            space.RebuildNameIndex();
            space.RebuildPropertyMap();
        }

        private static string NormalizeType(string declaredType, JToken value)
        {
            if (!string.IsNullOrWhiteSpace(declaredType))
                return declaredType;

            return value != null ? value.Type.ToString() : "Null";
        }

        private static JToken CloneToken(JToken token)
        {
            return token != null ? token.DeepClone() : null;
        }
    }
}
