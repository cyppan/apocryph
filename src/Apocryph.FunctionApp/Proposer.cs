using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Apocryph.FunctionApp.Model;
using Microsoft.Azure.WebJobs;
using Perper.WebJobs.Extensions.Config;
using Perper.WebJobs.Extensions.Model;

namespace Apocryph.FunctionApp
{
    public static class Proposer
    {
        private class State
        {
            public Dictionary<Hash, Dictionary<ValidatorKey, ValidatorSignature>> Commits { get; set; }
        }

        [FunctionName("Proposer")]
        public static async Task Run([PerperStreamTrigger] PerperStreamContext context,
            [PerperStream("commitsStream")] IAsyncEnumerable<Signed<Commit>> commitsStream,
            [PerperStream("runtimeStream")] IAsyncEnumerable<IAgentStep> runtimeStream,
            [PerperStream("outputStream")] IAsyncCollector<IAgentStep> outputStream)
        {
            var state = await context.FetchStateAsync<State>();

            await Task.WhenAll(
                commitsStream.ForEachAsync(async commit =>
                {
                    state.Commits[commit.Value.For].Add(commit.Signer, commit.Signature);
                    await context.UpdateStateAsync(state);
                }, CancellationToken.None),

                runtimeStream.ForEachAsync(async step =>
                {
                    // FIXME: Should probably block until there are enough signatures (as we cannot be sure that the other stream would collect them in time)
                    step.CommitSignatures = state.Commits[step.Previous];

                    await outputStream.AddAsync(step);
                }, CancellationToken.None));
        }
    }
}