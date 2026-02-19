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
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class CommandTranslatorAttribute : Attribute
    {
        public string Action { get; private set; }

        public CommandTranslatorAttribute(string action)
        {
            Action = action;
        }
    }

    public class CommandTranslator : TranslatorBase
    {
        //Command examples:
        //create project c:\UnityGames Shooter (or create project c:\UnityGames\Shooter)
        //init project c:\UnityGames Shooter (or initialize project c:\UnityGames\Shooter)
        //open project c:\UnityGames Shooter (or open project c:\UnityGames\Shooter)
        //connect
        //disconnect

        //Rule Based Process (RBP) key map
        private (string pattern, string key)[] _RBP_Rules = 
        {
            // Pattern that handles both cases
            (@"^create\s+project\s+(?<path>[A-Za-z]:\\[^\s]+|\\\\[^\s]+)(?:\s+(?<name>[^\s]+))?$", "create_project"),
            (@"^(init|initialize)\s+project\s+(?<path>[A-Za-z]:\\[^\s]+|\\\\[^\s]+)(?:\s+(?<name>[^\s]+))?$", "init_project"),
            (@"^open\s+project\s+(?<path>[A-Za-z]:\\[^\s]+|\\\\[^\s]+)(?:\s+(?<name>[^\s]+))?$", "open_project"),
            (@"^connect$", "connect"),
            (@"^disconnect$", "disconnect"),
            (@"^create\s+space\s+(?<name>[^\s]+)$", "create_space"),
            (@"^(goto|enter|open)\s+space\s+(?<name>[^\s]+)$", "goto_space"),
            (@"^save space$", "save_space"),

            (@"^generate\s+(?:game\s+)?(?:from\s+)?(?<path>[a-zA-Z]:[\\/][^\s]+(?:[\\/][^\s]+)*)?(?:\.)?$", "generate_game"),

            (@"^(?:\w+\s+)?(create|add)\s+(?:a\s+|an\s+)?(?<type>[\w\s\-]+?)\s+(?:called|named|with\s+the\s+name)\s+(?<name>""[^""]+""|[\w\s\-]+)(?:\.)?$", "create_object"),
            (@"^(?:\w+\s+)?(select)(?:\s+(?:object))?\s+(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?)(?:\.)?$", "select_object"),
            (@"^(?:\w+\s+)?(delete)(?:\s+(?:object))?\s+(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?)(?:\.)?$", "delete_object"),
            (@"^(?:place|put)\s+(?<objectName>""[^""]+""|'[^']+'|[\w\-\s]+?)\s+(?:at|on)\s+(?<spawnpointNames>(?:\s*(""[^""]+""|'[^']+'|[\w\-]+)\s*,?)+)\s*(?:spawn\s+point)?(?:\.)?$", "place_at_spawnpoint"),
            (@"^set\s+(?<objName>""[^""]+""|'[^']+'|[\w\-\s]+?)(?:\s+(property|attribute))\s+(?<property>\w+)\s+(to|to be)\s+(?<value>(""[^""]+""|'[^']+'|[\w\.\-/]+|\(\s*-?\d+(?:\.\d+)?(?:\s*,\s*-?\d+(?:\.\d+)?)*\s*\)))(?:\s+for\s+(?<script>\w+))?\s*$", "set_value"),
            (@"^(?:\w+\s+)?set(?:\s+object)?\s+(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?)\s+(position|location)\s+(?:at\s+)?(?<position>\(?\s*-?\d+(?:\.\d+)?\s*,\s*-?\d+(?:\.\d+)?\s*,\s*-?\d+(?:\.\d+)?\s*\)?)\.?\s*$", "set_position"),
            (@"^(?:\w+\s+)?set(?:\s+object)?\s+(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?)\s+rotation\s+(?<eulerAngles>\(?\s*-?\d+(?:\.\d+)?\s*,\s*-?\d+(?:\.\d+)?\s*,\s*-?\d+(?:\.\d+)?\s*\)?)\.?\s*$", "set_rotation"),
            (@"^(?:\w+\s+)?set(?:\s+object)?\s+(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?)\s+scale\s+((to|to be)\s+)?(?<scale>\(?\s*\d+(?:\.\d+)?\s*,\s*\d+(?:\.\d+)?\s*,\s*\d+(?:\.\d+)?\s*\)?)\.?\s*$", "set_scale"),
            
            (@"^spin(?:\s+(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?))?(?:\s+(?<speed>[+-]?\d+(?:\.\d+)?))?$", "spin_object"),
            (@"^add\s+script\s+(?<filepath>[a-zA-Z]:[\\/][^\s]+(?:[\\/][^\s]+)*)?(?:\s+to\s+(?<object>""[^""]+""|'[^']+'|[\w\-\s]+))?\s*$", "add_script"),
            (@"^(add|modify)\s+(behavior|behaviour|component)\s+(?<name>[^\s]+)\s+to\s+(?<object>""[^""]+""|'[^']+'|[\w\- ]+)\s*$", "add_behavior"),
            (@"^remove\s+(behavior|behaviour|script)\s+(?<name>[^\s]+)\s+from\s+(?<object>""[^""]+""|'[^']+'|[\w\- ]+)\s*$", "remove_behavior"),
            (@"^(print|display|write)\s+(?<text>(""[^""]+"")|('[^']+')|([\w\-_]+(?:\s+[\w\-_]+)*))\s+at\s+(?<position>center|top[-\s]?(left|mid|right)|bottom[-\s]?(left|mid|right)|\(\s*-?\d+(?:\.\d+)?\s*,\s*-?\d+(?:\.\d+)?\s*\))\s*$", "print_text"),
            (@"^font\s+(?<attrib>\w+)\s+(?<value>-?\d+(?:\.\d+)?|#[0-9a-fA-F]{3,6}|\w+)\s*$", "set_font"),
            (@"^build\s+(?<shape>circle|square|rectangle)\s+(?<structure>wall)\s+with\s+(?<element>\w+)\s*(?:named|with the name\s+)?(?<name>.+?)(?:\.)?$", "build_structure"),
            (@"^attach\s+(?<source>""[^""]+""|'[^']+'|[\w\-\s]+?)\s+to\s+(?<target>""[^""]+""|'[^']+'|[\w\-\s]+?)\s*$", "attach_to"),
            (@"^detach\s+(?<source>""[^""]+""|'[^']+'|[\w\-\s]+?)\s+from\s+(?<target>""[^""]+""|'[^']+'|[\w\-\s]+?)\s*$", "detach_from"),
            (@"^(make|create)\s+prefab\s+from\s+(?<name>""[^""]+""|'[^']+'|[\w\-\s]+?)\s*$", "make_prefab")
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

        public CommandTranslator()
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

            for (int i = 0; i < _RBP_Rules.Length; ++i)  //TODO: Consider parallel process when the array length is large
            {
                var pattern = _RBP_Rules[i].pattern;
                var match = Regex.Match(command, pattern, RegexOptions.IgnoreCase);
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