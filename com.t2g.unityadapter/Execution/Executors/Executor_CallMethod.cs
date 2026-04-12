#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace T2G
{
    [Executor(Actions.call_method)]
    public class Executor_CallMethod : ExecutorBase
    {
        public override async Task<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            string objName = instruction.parameters.GetString("objName");
            string methodFullName = instruction.parameters.GetString("method");

            if (string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(methodFullName))
            {
                return (false, "Missing objName or method parameter.", null);
            }

            var obj = Utils.FindObjectByName(objName);
            if (obj == null)
            {
                return (false, $"Couldn't find GameObject '{objName}'!", null);
            }

            string componentTypeName = null;
            string methodName = null;

            if (methodFullName.Contains("."))
            {
                var parts = methodFullName.Split('.');
                if (parts.Length >= 2)
                {
                    componentTypeName = parts[0];
                    methodName = string.Join(".", parts.Skip(1));
                }
            }
            else
            {
                methodName = methodFullName;
            }

            var parameters = new Dictionary<string, string>();
            foreach (var param in instruction.parameters)
            {
                if (param.name != "objName" && param.name != "method")
                {
                    parameters[param.name] = param.value?.ToString();
                }
            }

            if (!string.IsNullOrEmpty(componentTypeName))
            {
                return CallMethodOnComponent(obj, componentTypeName, methodName, parameters);
            }
            else
            {
                return CallMethodOnAllComponents(obj, methodName, parameters);
            }
        }

        private (bool succeeded, string message, List<Instruction> additionalInstructions) CallMethodOnComponent(
            GameObject obj, string componentTypeName, string methodName, Dictionary<string, string> parameters)
        {
            var componentType = ComponentResolver.GetComponentType(componentTypeName);
            if (componentType == null)
            {
                return (false, $"Component type '{componentTypeName}' not found in project.", null);
            }

            var component = obj.GetComponent(componentType);
            if (component == null)
            {
                return (false, $"Component '{componentTypeName}' not found on GameObject '{obj.name}'.", null);
            }

            return InvokeMethod(component, methodName, parameters, $"Component '{componentTypeName}'");
        }

        private (bool succeeded, string message, List<Instruction> additionalInstructions) CallMethodOnAllComponents(
            GameObject obj, string methodName, Dictionary<string, string> parameters)
        {
            var components = obj.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;

                var type = component.GetType();
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (method != null)
                {
                    return InvokeMethod(component, methodName, parameters, $"Component '{type.Name}'");
                }
            }

            return (false, $"Method '{methodName}' not found on any component of GameObject '{obj.name}'.", null);
        }

        private (bool succeeded, string message, List<Instruction> additionalInstructions) InvokeMethod(
            object target, string methodName, Dictionary<string, string> parameters, string sourceDescription)
        {
            var type = target.GetType();
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (method == null)
            {
                return (false, $"Method '{methodName}' not found on {sourceDescription}.", null);
            }

            var methodParams = method.GetParameters();
            if (methodParams.Length == 0 && parameters.Count > 0)
            {
                return (false, $"Method '{methodName}' on {sourceDescription} takes no parameters, but parameters were provided.", null);
            }

            if (methodParams.Length > 0 && parameters.Count == 0)
            {
                return (false, $"Method '{methodName}' on {sourceDescription} requires {methodParams.Length} parameter(s): {string.Join(", ", methodParams.Select(p => p.Name))}.", null);
            }

            object[] args = new object[methodParams.Length];
            for (int i = 0; i < methodParams.Length; i++)
            {
                var paramInfo = methodParams[i];
                if (parameters.TryGetValue(paramInfo.Name, out string valueStr))
                {
                    try
                    {
                        args[i] = ConvertParameterValue(valueStr, paramInfo.ParameterType);
                    }
                    catch (Exception ex)
                    {
                        return (false, $"Cannot convert parameter '{paramInfo.Name}' with value '{valueStr}': {ex.Message}", null);
                    }
                }
                else
                {
                    return (false, $"Missing required parameter '{paramInfo.Name}' for method '{methodName}'.", null);
                }
            }

            try
            {
                method.Invoke(target, args);
                return (true, $"Successfully called {sourceDescription}.{methodName}()", null);
            }
            catch (Exception ex)
            {
                return (false, $"Error invoking method '{methodName}': {ex.InnerException?.Message ?? ex.Message}", null);
            }
        }

        private object ConvertParameterValue(string valueStr, Type targetType)
        {
            if (string.IsNullOrEmpty(valueStr))
            {
                return null;
            }

            valueStr = valueStr.Trim();

            if (targetType == typeof(string))
            {
                return valueStr;
            }
            else if (targetType == typeof(int))
            {
                return int.Parse(valueStr);
            }
            else if (targetType == typeof(float))
            {
                return float.Parse(valueStr);
            }
            else if (targetType == typeof(double))
            {
                return double.Parse(valueStr);
            }
            else if (targetType == typeof(bool))
            {
                return bool.Parse(valueStr);
            }
            else if (targetType == typeof(Vector2))
            {
                return ParseVector2(valueStr);
            }
            else if (targetType == typeof(Vector3))
            {
                return ParseVector3(valueStr);
            }
            else if (targetType == typeof(Vector4))
            {
                return ParseVector4(valueStr);
            }
            else if (targetType == typeof(Color))
            {
                return ParseColor(valueStr);
            }

            return Convert.ChangeType(valueStr, targetType);
        }

        private Vector2 ParseVector2(string valueStr)
        {
            valueStr = valueStr.Trim('(', ')');
            var parts = valueStr.Split(',');
            if (parts.Length >= 2)
            {
                return new Vector2(float.Parse(parts[0].Trim()), float.Parse(parts[1].Trim()));
            }
            throw new FormatException($"Cannot parse '{valueStr}' as Vector2");
        }

        private Vector3 ParseVector3(string valueStr)
        {
            valueStr = valueStr.Trim('(', ')');
            var parts = valueStr.Split(',');
            if (parts.Length >= 3)
            {
                return new Vector3(float.Parse(parts[0].Trim()), float.Parse(parts[1].Trim()), float.Parse(parts[2].Trim()));
            }
            throw new FormatException($"Cannot parse '{valueStr}' as Vector3");
        }

        private Vector4 ParseVector4(string valueStr)
        {
            valueStr = valueStr.Trim('(', ')');
            var parts = valueStr.Split(',');
            if (parts.Length >= 4)
            {
                return new Vector4(float.Parse(parts[0].Trim()), float.Parse(parts[1].Trim()), float.Parse(parts[2].Trim()), float.Parse(parts[3].Trim()));
            }
            throw new FormatException($"Cannot parse '{valueStr}' as Vector4");
        }

        private Color ParseColor(string valueStr)
        {
            valueStr = valueStr.Trim('(', ')');
            var parts = valueStr.Split(',');
            if (parts.Length >= 3)
            {
                float r = float.Parse(parts[0].Trim());
                float g = float.Parse(parts[1].Trim());
                float b = float.Parse(parts[2].Trim());
                float a = parts.Length >= 4 ? float.Parse(parts[3].Trim()) : 1f;
                return new Color(r, g, b, a);
            }
            throw new FormatException($"Cannot parse '{valueStr}' as Color");
        }
    }
}

#endif