using System;
using RulesEngine;
using System.IO;
using Newtonsoft.Json;
using RulesEngine.Models;
using L4D2Bridge.Types;
using System.Collections.Generic;
using System.Threading.Tasks;
using L4D2Bridge.Utils;

namespace L4D2Bridge.Models
{
    using L4D2Actions = List<L4D2Action>;
    using RuleResults = List<RuleResultTree>;
    using ActionDictionary = Dictionary<string, List<L4D2Action>>;

    public class RulesService : BaseService
    {
        private const string rulesFile = "rules.json";
        private RulesEngine.RulesEngine? engine;
        private ActionDictionary Actions = [];

        public void LoadActions(ref readonly ActionDictionary InActions)
        {
            if (InActions == null)
            {
                PrintMessage("Missing actions definitions from config!");
                return;
            }

            // Migrate actions from config
            Actions = InActions;
        }

        protected override bool Internal_Start()
        {
            if (!File.Exists(rulesFile))
            {
                PrintMessage("Missing rules information! Please create rules and reload config.");
                // Create an empty file
                File.Create(rulesFile).Close();
                return false;
            }
            PrintMessage($"Using {Actions.Count} actions to the executor");

            string rulesJson = File.ReadAllText(rulesFile);
            try
            {
                var workflowData = JsonConvert.DeserializeObject<Workflow[]>(rulesJson);
                if (workflowData == null) 
                {
                    PrintMessage("Workflow data for rules engine is invalid! Cannot run rules engine.");
                    return false;
                }
                ReSettings settings = new ReSettings
                {
                    CustomTypes = new Type[] { typeof(SourceEventType), typeof(SourceEvent), typeof(REUtils) }
                };
                engine = new RulesEngine.RulesEngine(workflowData, settings);
                PrintMessage($"Rules engine started with {workflowData?.Length} workflow");
            }
            catch (Exception ex)
            {
                PrintMessage(ex.ToString());
                return false;
            }

            return true;
        }

        public override ConsoleSources GetSource() => ConsoleSources.RulesEngine;

        public async Task<L4D2Actions> ExecuteAsync(string WorkflowName, SourceEvent data)
        {
            // If we don't have an engine, then we don't process anything and return an empty list.
            if (engine == null)
                return [];

            PrintMessage($"Running ruleset engine against {WorkflowName} with data {data}");
            // NOTE: it's untested what happens if you change the rulesengine while it's being executed
            // Technically, it should produce results on the old engine while new executes will run on the newer one
            // until all older rules are done running.
            RuleResults results = await engine.ExecuteAllRulesAsync(WorkflowName, data);
            return ParseRuleResults(results);
        }

        private L4D2Actions ParseRuleResults(RuleResults Results)
        {
            L4D2Actions output = [];
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
                    PrintMessage($"Matched successfully with rule {RuleName}");
                    output.AddRange(ActionList);
                    ActionList = null;
                }
            }
            return output;
        }

        // This transforms any actions into a human readable string.
        public static string ResultActionsToString(ref readonly L4D2Actions actions)
        {
            if (actions.Count <= 0)
                return "";

            string output = "Spawning ";
            Dictionary<L4D2Action, int> appearances = [];
            actions.ForEach(action =>
            {
                if (appearances.ContainsKey(action))
                    appearances[action] += 1;
                else
                    appearances[action] = 1;
            });

            foreach (var actionData in appearances)
            {
                string amount = (actionData.Value > 1) ? $" (x{actionData.Value})" : "";
                string? readableName = actionData.Key.GetReadableName();
                if (readableName != null)
                    output += $"{actionData.Key.GetReadableName()}{amount}, ";
            }

            return output.Remove(output.Length - 2);
        }
    }
}
