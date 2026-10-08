using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace T2G
{
    /// <summary>
    /// Structured result of executing one Instruction.
    /// Instruction expresses intended operation; Response reports actual result.
    /// </summary>
    [Serializable]
    public class Response
    {
        public string InstructionId = string.Empty;
        public bool Succeeded = true;
        public string Message = string.Empty;
        public List<Result> Results = new List<Result>();

        public Response()
        {
        }

        public Response(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public Response(string instructionId, bool succeeded, string message)
        {
            InstructionId = instructionId ?? string.Empty;
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        [Serializable]
        public class Result
        {
            public string name = string.Empty;
            public string type = string.Empty;
            public JToken value;

            public Result()
            {
            }

            public Result(string name, string type, JToken value)
            {
                this.name = name ?? string.Empty;
                this.type = type ?? string.Empty;
                this.value = value;
            }

            public Result(string name, JToken value)
            {
                this.name = name ?? string.Empty;
                this.value = value;
                type = value != null ? value.Type.ToString() : "Null";
            }
        }

        public bool TryGetResult(string name, out Result result)
        {
            result = null;

            if (string.IsNullOrWhiteSpace(name) || Results == null)
                return false;

            foreach (Result item in Results)
            {
                if (item == null)
                    continue;

                if (string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    result = item;
                    return true;
                }
            }

            return false;
        }

        public Result GetResultOrNull(string name)
        {
            Result result;
            return TryGetResult(name, out result) ? result : null;
        }

        public T GetValue<T>(string name, T defaultValue = default(T))
        {
            Result result = GetResultOrNull(name);

            if (result == null || result.value == null ||
                result.value.Type == JTokenType.Null)
                return defaultValue;

            try
            {
                return result.value.ToObject<T>();
            }
            catch
            {
                return defaultValue;
            }
        }

        public void AddResult(string name, string type, object value)
        {
            if (Results == null)
                Results = new List<Result>();

            Results.Add(new Result(
                name,
                type,
                value != null ? JToken.FromObject(value) : JValue.CreateNull()));
        }

        public void AddResult(string name, JToken value)
        {
            if (Results == null)
                Results = new List<Result>();

            Results.Add(new Result(name, value));
        }
    }
}
