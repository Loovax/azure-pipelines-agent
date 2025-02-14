using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agent.Sdk;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.PipelineCache.WebApi;

namespace Agent.Plugins.PipelineCache
{
    public class SavePipelineCacheV0 : PipelineCacheTaskPluginBase
    {
        public override string Stage => "post";

        /* To mitigate the issue - https://github.com/microsoft/azure-pipelines-tasks/issues/10907, we need to check the restore condition logic, before creating the fingerprint.
           Hence we are overriding the RunAsync function to include that logic. */
        public override async Task RunAsync(AgentTaskPluginExecutionContext context, CancellationToken token)
        {
            bool successSoFar = false;
            if (context.Variables.TryGetValue("agent.jobstatus", out VariableValue jobStatusVar))
            {
                if (Enum.TryParse<TaskResult>(jobStatusVar?.Value ?? string.Empty, true, out TaskResult jobStatus))
                {
                    if (jobStatus == TaskResult.Succeeded)
                    {
                        successSoFar = true;
                    }
                }
            }

            if (!successSoFar)
            {
                context.Warning($"Skipping because the job status was not 'Succeeded'.");
                return;
            }

            bool restoreStepRan = false;
            if (context.TaskVariables.TryGetValue(RestoreStepRanVariableName, out VariableValue ran))
            {
                if (ran != null && ran.Value != null && ran.Value.Equals(RestoreStepRanVariableValue, StringComparison.Ordinal))
                {
                    restoreStepRan = true;
                }
            }

            if (!restoreStepRan)
            {
                context.Warning($"Skipping because restore step did not run.");
                return;
            }

            await base.RunAsync(context, token);
        }

        protected override async Task ProcessCommandInternalAsync(
            AgentTaskPluginExecutionContext context,
            Fingerprint fingerprint,
            Func<Fingerprint[]> restoreKeysGenerator,
            string path,
            CancellationToken token)
        {
            VariableValue contentFormatVar = context.Variables.GetValueOrDefault(ContentFormatVariableName);
            string contentFormatValue = contentFormatVar.Value ?? string.Empty;
            contentFormatValue = contentFormatValue.Trim();

            ContentFormat contentFormat;
            if (string.IsNullOrWhiteSpace(contentFormatValue))
            {
                contentFormat = ContentFormat.Files;
            }
            else
            {
                contentFormat = Enum.Parse<ContentFormat>(contentFormatValue, ignoreCase: true);
            }

            PipelineCacheServer server = new PipelineCacheServer();
            await server.UploadAsync(
                context,
                fingerprint, 
                path,
                token,
                contentFormat);
        }
    }
}