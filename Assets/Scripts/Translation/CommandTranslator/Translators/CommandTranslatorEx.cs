using UnityEngine;
using T2G;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using System.Reflection;
using System.Linq;

namespace T2G.Assistant
{
    public class CommandTranslatorEx : TranslatorBase
    {
        private (string pattern, string key)[] _RBP_Rules = 
        {
            // Project commands - expanded variations
            (@"^(?:create\s+new\s+)?project(?:\s+(?:at|in|to)\s+(?<path>[A-Za-z]:\\[^\s]+|\\\\[^\s]+))?(?:\s+(?:named?|called|as)\s+(?<name>[^\s]+))?$", T2G.Actions.create_project),
            (@"^(?:init|initialize|setup)\s+(?:the\s+)?project(?:\s+(?:at|in)\s+(?<path>[A-Za-z]:\\[^\s]+|\\\\[^\s]+))?(?:\s+(?:named?|called|as)\s+(?<name>[^\s]+))?$", T2G.Actions.init_project),
            (@"^(?:open|load)\s+(?:the\s+)?project(?:\s+(?:at|in)\s+(?<path>[A-Za-z]:\\[^\s]+|\\\\[^\s]+))?(?:\s+(?:named?|called|as)\s+(?<name>[^\s]+))?$", T2G.Actions.open_project),

            // Connection - expanded
            (@"^(?:connect\s+(?:to\s+)?(?:the\s+)?server|start\s+(?:the\s+)?connection)$", T2G.Actions.connect),
            (@"^(?:disconnect\s+(?:from\s+)?(?:the\s+)?server|stop\s+(?:the\s+)?connection|end\s+(?:the\s+)?session)$", T2G.Actions.disconnect),

            // Clear commands - expanded
            (@"^(?:clear|clean|reset)(?:\s+(?:the\s+)?(?:history|chat|console|scene))?$", T2G.Actions.clear),

            // Space commands - expanded natural language
            (@"^(?:create\s+(?:a\s+)?(?:new\s+)?(?:space|scene|level|world)(?:\s+(?:called|named|as)\s+(?<name>[^\s]+))?)$", T2G.Actions.create_space),
            (@"^(?:go\s+to|go\s+into|enter|switch\s+to|load\s+(?:the\s+)?|open)\s+(?:the\s+)?(?:space|scene|level|world)\s+(?<name>[^\s]+)$", T2G.Actions.goto_space),
            (@"^(?:save\s+(?:the\s+)?|store)\s+(?:the\s+)?(?:space|scene|level)$", T2G.Actions.save_space),

            // Object creation - expanded natural language
            (@"^(?:create|make|add|spawn|instantiate)(?:\s+(?:a\s+|an\s+)?(?:new\s+)?(?<desc>cube|sphere|plane|capsule|cylinder|box|primitive)(?:\s+(?:called|named|as)\s+(?<name>""[^""]+""|[\w\s\-]+))?)$", T2G.Actions.create_object),
            (@"^(?:create|make|add|spawn|instantiate)(?:\s+(?:a\s+|an\s+)?(?:new\s+)?(?<desc>camera|main\s*camera)(?:\s+(?:called|named|as)\s+(?<name>""[^""]+""|[\w\s\-]+))?)$", T2G.Actions.create_object),
            (@"^(?:create|make|add|spawn|instantiate)(?:\s+(?:a\s+|an\s+)?(?:new\s+)?(?<desc>light|directional\s*light|point\s*light|spot\s*light)(?:\s+(?:called|named|as)\s+(?<name>""[^""]+""|[\w\s\-]+))?)$", T2G.Actions.create_object),
            (@"^(?:create|make|add|spawn|instantiate)(?:\s+(?:a\s+|an\s+)?(?:new\s+)?(?<desc>[\w\s\-]+)(?:\s+(?:called|named|as)\s+(?<name>""[^""]+""|[\w\s\-]+))?)$", T2G.Actions.create_object),

            // Object selection - expanded
            (@"^(?:select|choose|pick|highlight)(?:\s+(?:the\s+)?(?:object\s+)?(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?))?$", T2G.Actions.select_object),
            (@"^(?:deselect|unselect|clear\s+(?:selection\s+)?of)(?:\s+(?:the\s+)?(?:object\s+)?(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?))?$", T2G.Actions.select_object),

            // Object deletion - expanded
            (@"^(?:delete|remove|destroy|kill)(?:\s+(?:the\s+)?(?:object\s+)?(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?))?$", T2G.Actions.delete_object),

            // Object placement - expanded
            (@"^(?:place|put|drop|position)(?:\s+(?:the\s+)?(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?))(?:\s+(?:on|onto|to)\s+(?:the\s+)?(?:ground|floor|surface))?\.?$", T2G.Actions.place_on_ground),

            // Attach/Detach - expanded
            (@"^(?:attach|parent|link)(?:\s+(?:the\s+)?(?<source>""[^""]+""|'[^']+'|[\w\-\s]+?))\s+(?:to|under)\s+(?:the\s+)?(?<target>""[^""]+""|'[^']+'|[\w\-\s]+?)(?:\s+(?:at|on)\s+(?<bone>[\w\-]+))?$", "attach_to"),
            (@"^(?:detach|unparent|unlink)(?:\s+(?:the\s+)?(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?))(?:\s+(?:from|to))?$", "detach"),

            // Property setting - expanded
            (@"^(?:set|change|update|modify)(?:\s+(?:the\s+)?(?<objName>""[^""]+""|'[^']+'|[\w\-]+)(?:\s+(?:property|attribute))?\s+(?<property>.+?)\s*(?:=)?\s*(?<value>(?:\([\d,\.\-]+\)|[0-9][^\s]*|""[^""]*""|'[^']*'))(?:\s+for\s+(?<script>\w+))?$", "set_property"),

            // Component operations - expanded
            (@"^(?:add|attach|enable)(?:\s+(?:a\s+)?(?:new\s+)?(?:behavior|behaviour|script|component)\s+(?<component>""[^""]+""|'[^']+'|[\w\\\.:\- ]+?)\s+to\s+(?:the\s+)?(?<objName>""[^""]+""|'[^']+'|[\w\- ]+)$", "add_component"),
            (@"^(?:remove|detach|disable|delete)(?:\s+(?:the\s+)?(?:behavior|behaviour|script|component)\s+(?<componentType>[^\s]+)\s+from\s+(?:the\s+)?(?<objName>""[^""]+""|'[^']+'|[\w\- ]+)$", "remove_component"),
            (@"^(?:update|replace|swap|change)(?:\s+(?:the\s+)?(?:behavior|behaviour|script|component)\s+(?<component>[^\s]+)\s+(?:with|to|using)\s+(?<newComponent>""[^""]+""|'[^']+'|[\w\\\.:\- ]+?)(?:\s+on\s+(?:the\s+)?(?<objName>""[^""]+""|'[^']+'|[\w\- ]+))?$", "update_component"),

            // Create from - expanded
            (@"^(?:create|load|build)\s+(?:from|using)\s+(?:a\s+)?(?:game\s+)?description\s+(?:file\s+)?(?<path>[a-zA-Z]:[\\/][^\s]+(?:[\\/][^\s]+)*)?$", "create_from"),

            // Transform operations - expanded
            (@"^(?:move|shift|translate)(?:\s+(?:the\s+)?(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?))(?:\s+to\s+(?<position>\(\s*-?\d+(?:\.\d+)?\s*,\s*-?\d+(?:\.\d+)?\s*(?:,\s*-?\d+(?:\.\d+)?)?\s*\)))?$", "set_object_position"),
            (@"^(?:rotate|spin|turn)(?:\s+(?:the\s+)?(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?))(?:\s+by\s+(?<rotation>\d+))?$", "set_object_rotation"),
            (@"^(?:scale|resize)(?:\s+(?:the\s+)?(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?))(?:\s+(?:to\s+|by\s+)?(?<scale>\d+(?:\.\d+)?))?$", "set_object_scale"),

            // Save/Build operations
            (@"^(?:save|export|build)(?:\s+(?:the\s+)?project)?$", "save_space"),
        };

        Dictionary<string, CmdTranslatorBase> _translatorMap = new Dictionary<string, CmdTranslatorBase>();

        void Register_Executors()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var translatorClasses = assembly.GetTypes()
                .Where(type => type.IsClass && type.GetCustomAttributes(typeof(CommandTranslatorAttribute), false).Any());
            foreach (var translatorClass in translatorClasses)
            {
                var attribute = translatorClass.GetCustomAttribute<CommandTranslatorAttribute>();
                var translator = (CmdTranslatorBase)(Activator.CreateInstance(translatorClass));
                _translatorMap.Add(attribute.Action, translator);
            }
        }

        public CommandTranslatorEx()
        {
            Register_Executors();
        }

        public override async Task<(bool succeeded, List<Instruction> instructions)> Translate(string command)
        {
            if(MatchAction(command, out var action, out var arguments) && 
                _translatorMap.ContainsKey(action))
            {
                List<Instruction> instructions = new List<Instruction>();
                var result = _translatorMap[action].Translate(arguments);
                if (result.succeeded)
                {
                    instructions.AddRange(result.instructions);
                    await Task.Yield();
                    return (true, instructions);
                }
                else
                {
                    return (false, null);
                }
            }

            return (false, null);
        }

        bool MatchAction(string command, out string key, out (string name, string value)[] arguments)
        {
            List<(string, string)> args = new List<(string, string)>();

            for (int i = 0; i < _RBP_Rules.Length; ++i)
            {
                var pattern = _RBP_Rules[i].pattern;
                var match = Regex.Match(command.Trim(), pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    key = _RBP_Rules[i].key;
                    for(int j = 0; j < match.Groups.Count; ++ j)
                    {
                        args.Add((match.Groups[j].Name, match.Groups[j].Value));
                    }
                    arguments = args.ToArray();
                    return true;
                }
            }

            key = string.Empty;
            arguments = null;
            return false;
        }
    }
}
