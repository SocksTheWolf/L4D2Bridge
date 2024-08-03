using System;
using RulesEngine;
using System.IO;
using Newtonsoft.Json;
using RulesEngine.Models;
using L4D2Bridge.Types;
using System.Collections.Generic;
using System.Threading.Tasks;
using RulesEngine.Extensions;

namespace L4D2Bridge.Models
{
    using L4D2Actions = List<L4D2Action>;
    using RuleResults = List<RuleResultTree>;
    using ActionDictionary = Dictionary<string, List<L4D2Action>>;

    public class RulesService : BaseService
    {
        const string rulesFile = "rules.json";
        RulesEngine.RulesEngine? engine;
        ActionDictionary Actions = new ActionDictionary();

        public RulesService(ref ActionDictionary? InActions) 
        {
            if (InActions == null)
            {
                PrintMessage("Missing actions definitions from config!");
                return;
            }

            if (!File.Exists(rulesFile))
            {
                PrintMessage("Missing rules information! Cannot handle rules engine");
                return;
            }

            // Migrate actions from config
            Actions = InActions;
        }
        public override void Start()
        {
            PrintMessage($"Using {Actions.Count} actions to the executor");

            string rulesJson = File.ReadAllText(rulesFile);
            var workflowData = JsonConvert.DeserializeObject<Workflow[]>(rulesJson);
            ReSettings settings = new ReSettings
            {
                CustomTypes = new Type[] { typeof(EventType), typeof(SourceEvent) }
            };
            engine = new RulesEngine.RulesEngine(workflowData, settings);
            PrintMessage($"Rules engine started with {workflowData?.Length} workflow");
        }

        public override ConsoleSources GetSource() => ConsoleSources.RulesEngine;

        public async Task<L4D2Actions> ExecuteAsync(string WorkflowName, SourceEvent data)
        {
            // If we don't have an engine, then we don't process anything and return an empty list.
            if (engine == null)
                return new L4D2Actions();

            PrintMessage($"Running ruleset engine against {WorkflowName} with data {data}");
            RuleResults results = await engine.ExecuteAllRulesAsync(WorkflowName, data);
            return ParseRuleResults(results);
        }

        private L4D2Actions ParseRuleResults(RuleResults Results)
        {
            L4D2Actions output = new L4D2Actions();
            if (Actions == null)
                return output;

            L4D2Actions? ActionList = null;
            foreach (RuleResultTree rule in Results)
            {
                if (!rule.IsSuccess)
                    continue;

                string RuleName = rule.Rule.RuleName.ToLower();
                // Attempt to find the actions for the given rule information
                if (!Actions.TryGetValue(RuleName, out ActionList))
                {
                    // If the rule name doesn't explicitly match, check the success event as well.
                    if (!Actions.TryGetValue(rule.Rule.SuccessEvent.ToLower(), out ActionList))
                    {
                        PrintMessage($"Could not find any actions tied to rule {RuleName}");
                        continue;
                    }
                }

                if (ActionList != null)
                {
                    PrintMessage($"Matched with rule {RuleName}");
                    output.AddRange(ActionList);
                    ActionList = null;
                }
            }
            return output;
        }
    }
}
