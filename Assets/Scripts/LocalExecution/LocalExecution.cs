using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace T2G.Assistant
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class LocalExecutorAttribute : Attribute
    {
        public string Action { get; private set; }

        public LocalExecutorAttribute(string instructionAction)
        {
            Action = instructionAction;
        }
    }

    public class LocalExecution
    {
        public static LocalExecution _instance = null;
        public static LocalExecution Instance
        {
            get
            {
                if(_instance == null)
                {
                    _instance = new LocalExecution();
                }
                return _instance;
            }
        }

        Dictionary<string, ExecutorBase> _executorMap = new Dictionary<string, ExecutorBase>();

        void Register_Executors()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var executorClasses = assembly.GetTypes()
                .Where(type => type.IsClass && type.GetCustomAttributes(typeof(LocalExecutorAttribute), false).Any());
            foreach (var executorClass in executorClasses)
            {
                var attribute = executorClass.GetCustomAttribute<LocalExecutorAttribute>();
                var executor = (ExecutorBase)(Activator.CreateInstance(executorClass));
              
                _executorMap.Add(attribute.Action, executor);
            }
        }

        public LocalExecution()
        {
            Register_Executors();
        }

        public async Awaitable<(bool succeeded, string message, List<Instruction> additionalInstructions)> Execute(Instruction instruction)
        {
            if(_executorMap.TryGetValue(instruction.action, out var executor))
            {
                return await executor.Execute(instruction);
            }
            return (false, null, null);
        }
    }
}
