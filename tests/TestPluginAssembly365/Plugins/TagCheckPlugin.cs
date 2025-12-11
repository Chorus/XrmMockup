using System;
using Microsoft.Xrm.Sdk;

namespace TestPluginAssembly365.Plugins
{
    public class TagCheckPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            Console.WriteLine($"[TagCheckPlugin] Executing. Stage: {context.Stage}, Message: {context.MessageName}");

            string tag = null;
            var current = context;
            int depth = 0;
            while (current != null)
            {
                Console.WriteLine($"[TagCheckPlugin] Checking context depth {depth}. SharedVariables keys: {string.Join(", ", current.SharedVariables.Keys)}");
                if (current.SharedVariables.ContainsKey("tag"))
                {
                    tag = current.SharedVariables["tag"] as string;
                    Console.WriteLine($"[TagCheckPlugin] Found tag in SharedVariables at depth {depth}: {tag}");
                    break;
                }
                current = current.ParentContext;
                depth++;
            }

            if (tag == null)
            {
                 Console.WriteLine($"[TagCheckPlugin] Tag not found in SharedVariables. Checking InputParameters. Keys: {string.Join(", ", context.InputParameters.Keys)}");
                 if (context.InputParameters.ContainsKey("tag"))
                 {
                    tag = context.InputParameters["tag"] as string;
                    Console.WriteLine($"[TagCheckPlugin] Found tag in InputParameters: {tag}");
                 }
            }

            if (tag != null)
            {
                if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target)
                {
                    // Write the tag value to a field so we can verify it
                    // Using 'description' field for this purpose
                    target["description"] = "TagFound: " + tag;
                    Console.WriteLine($"[TagCheckPlugin] Updated description to: {target["description"]}");
                }
            }
            else
            {
                Console.WriteLine("[TagCheckPlugin] Tag NOT found.");
            }
        }
    }
}
