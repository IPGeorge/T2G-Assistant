using System;
using System.Collections.Generic;
using UnityEngine;
using T2G;

namespace T2G.Assistant
{
    /// <summary>
    /// Resolution module.
    ///
    /// Converts Raw instructions into Resolved instructions by resolving
    /// semantic asset requirements into concrete resources that can later
    /// be used by the Executor.
    ///
    /// Resolution does not reinterpret the instruction intent. The action
    /// and parameters have already been established by Interpretation.
    /// </summary>
    public class Resolution
    {
        private static Resolution _instance = null;

        public static Resolution Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Resolution();
                }

                return _instance;
            }
        }

        private readonly AssetResolver _assetResolver;

        private Resolution()
        {
            _assetResolver = new AssetResolver();
        }

        /// <summary>
        /// Resolves a list of instructions.
        ///
        /// Each instruction is resolved independently. Instructions that
        /// fail resolution remain in the Raw state.
        /// </summary>
        public async Awaitable<List<Instruction>> Resolve(
            List<Instruction> instructions)
        {
            if (instructions == null)
            {
                Debug.LogWarning(
                    "[Resolution] Instruction list is null.");

                return null;
            }

            List<Instruction> resolvedInstructions =
                new List<Instruction>();

            foreach (Instruction instruction in instructions)
            {
                Instruction resolved = await Resolve(instruction);

                if (resolved != null)
                {
                    resolvedInstructions.Add(resolved);
                }
            }

            return resolvedInstructions;
        }

        /// <summary>
        /// Resolves a single instruction.
        ///
        /// Init:
        ///     Resolution cannot process the instruction.
        ///
        /// Raw:
        ///     Resolve all required assets.
        ///
        /// Resolved:
        ///     No further work is required.
        /// </summary>
        public async Awaitable<Instruction> Resolve(
            Instruction instruction)
        {
            if (instruction == null)
            {
                Debug.LogWarning(
                    "[Resolution] Instruction is null.");

                return null;
            }

            switch (instruction.state)
            {
                case InstructionState.Init:
                    {
                        Debug.LogWarning(
                            $"[Resolution] Instruction '{instruction.id}' " +
                            "is still in Init state and cannot be resolved.");

                        return instruction;
                    }

                case InstructionState.Resolved:
                    {
                        // Already resolved.
                        return instruction;
                    }

                case InstructionState.Raw:
                    {
                        return await ResolveRawInstruction(instruction);
                    }

                default:
                    {
                        Debug.LogWarning(
                            $"[Resolution] Unsupported instruction state: " +
                            $"{instruction.state}");

                        return instruction;
                    }
            }
        }

        /// <summary>
        /// Resolves a Raw instruction.
        ///
        /// The instruction becomes Resolved only if every required asset
        /// has been successfully resolved.
        /// </summary>
        private async Awaitable<Instruction> ResolveRawInstruction(
            Instruction instruction)
        {
            // An instruction without asset requirements does not require
            // resource resolution.
            if (instruction.assets == null ||
                instruction.assets.Count == 0)
            {
                instruction.state = InstructionState.Resolved;

                Debug.Log(
                    $"[Resolution] Instruction '{instruction.id}' " +
                    $"({instruction.action}) contains no asset " +
                    "requirements. Marked Resolved.");

                return instruction;
            }

            for (int i = 0; i < instruction.assets.Count; ++i)
            {
                Instruction.Asset asset = instruction.assets[i];

                if (asset == null)
                {
                    Debug.LogWarning(
                        $"[Resolution] Instruction '{instruction.id}' " +
                        $"contains a null asset requirement at index {i}.");

                    // Do not mark the instruction Resolved.
                    return instruction;
                }

                bool succeeded =
                    await _assetResolver.Resolve(asset);

                if (!succeeded)
                {
                    Debug.LogWarning(
                        $"[Resolution] Failed to resolve asset " +
                        $"'{asset.desc}' for instruction " +
                        $"'{instruction.id}' ({instruction.action}).");

                    // Keep the instruction Raw.
                    return instruction;
                }
            }

            // Every required asset has been successfully resolved.
            instruction.state = InstructionState.Resolved;

            Debug.Log(
                $"[Resolution] Instruction '{instruction.id}' " +
                $"({instruction.action}) successfully resolved.");

            return instruction;
        }
    }


    /// <summary>
    /// Resolves an individual Instruction.Asset.
    ///
    /// Asset types:
    ///
    /// Identifier:
    ///     Engine/system identifier. No Asset Library lookup is required.
    ///
    /// ScriptFile:
    ///     Local script resource. No Asset Library lookup is currently
    ///     required.
    ///
    /// SourceAsset:
    ///     Asset already resolved to an Asset Library source.
    ///
    /// Unknown:
    ///     Semantic asset requirement that must be resolved through the
    ///     Asset Library Service.
    /// </summary>
    public class AssetResolver
    {
        public async Awaitable<bool> Resolve(
            Instruction.Asset asset)
        {
            if (asset == null)
            {
                return false;
            }

            switch (asset.type)
            {
                case AssetType.Identifier:
                    {
                        return ResolveIdentifier(asset);
                    }

                case AssetType.ScriptFile:
                    {
                        return ResolveScriptFile(asset);
                    }

                case AssetType.SourceAsset:
                    {
                        return ValidateSourceAsset(asset);
                    }

                case AssetType.Unknown:
                    {
                        return await ResolveUnknownAsset(asset);
                    }

                default:
                    {
                        Debug.LogWarning(
                            $"[AssetResolver] Unsupported AssetType: " +
                            $"{asset.type}");

                        return false;
                    }
            }
        }

        /// <summary>
        /// Identifier assets represent engine/system resources such as
        /// built-in components. They do not require Asset Library lookup.
        /// </summary>
        private bool ResolveIdentifier(
            Instruction.Asset asset)
        {
            if (string.IsNullOrWhiteSpace(asset.source))
            {
                // If Interpretation supplied only the semantic description,
                // use it as the identifier.
                if (!string.IsNullOrWhiteSpace(asset.desc))
                {
                    asset.source = asset.desc;
                }
            }

            if (string.IsNullOrWhiteSpace(asset.source))
            {
                Debug.LogWarning(
                    "[AssetResolver] Identifier asset contains neither " +
                    "a source nor a description.");

                return false;
            }

            return true;
        }

        /// <summary>
        /// ScriptFile currently represents a local script file.
        /// Resolution does not search the Asset Library for scripts.
        /// </summary>
        private bool ResolveScriptFile(
            Instruction.Asset asset)
        {
            if (string.IsNullOrWhiteSpace(asset.source))
            {
                // A script may currently be represented by its description
                // when no explicit source has been assigned.
                if (!string.IsNullOrWhiteSpace(asset.desc))
                {
                    asset.source = asset.desc;
                }
            }

            if (string.IsNullOrWhiteSpace(asset.source))
            {
                Debug.LogWarning(
                    "[AssetResolver] ScriptFile asset contains neither " +
                    "a source nor a description.");

                return false;
            }

            return true;
        }

        /// <summary>
        /// A SourceAsset should already contain its resolved source.
        /// </summary>
        private bool ValidateSourceAsset(
            Instruction.Asset asset)
        {
            if (string.IsNullOrWhiteSpace(asset.source))
            {
                Debug.LogWarning(
                    $"[AssetResolver] SourceAsset '{asset.desc}' " +
                    "does not contain a resolved source.");

                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves an Unknown semantic asset requirement through the
        /// Asset Library Service.
        /// </summary>
        private async Awaitable<bool> ResolveUnknownAsset(
            Instruction.Asset asset)
        {
            if (string.IsNullOrWhiteSpace(asset.desc))
            {
                Debug.LogWarning(
                    "[AssetResolver] Unknown asset has no semantic " +
                    "description.");

                return false;
            }

            Debug.Log(
                $"[AssetResolver] Searching Asset Library for " +
                $"'{asset.desc}'.");

            var response =
                await AssetSearchClient.SearchAssets(asset.desc);

            if (!response.succeeded ||
                response.assetPaths == null ||
                response.assetPaths.Length == 0)
            {
                Debug.LogWarning(
                    $"[AssetResolver] No asset found for " +
                    $"'{asset.desc}'.");

                return false;
            }

            // AssetSearchClient returns the search results in relevance
            // order. The first result is therefore selected as the best
            // match.
            string source = response.assetPaths[0];

            if (string.IsNullOrWhiteSpace(source))
            {
                Debug.LogWarning(
                    $"[AssetResolver] Asset Library returned an empty " +
                    $"source for '{asset.desc}'.");

                return false;
            }

            asset.source = source;
            asset.type = AssetType.SourceAsset;

            Debug.Log(
                $"[AssetResolver] Resolved '{asset.desc}' -> " +
                $"'{asset.source}'.");

            return true;
        }
    }
}