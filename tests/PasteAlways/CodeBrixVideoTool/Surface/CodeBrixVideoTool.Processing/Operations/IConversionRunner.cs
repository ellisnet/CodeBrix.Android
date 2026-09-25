using CodeBrixVideoTool.Processing.Planning;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrixVideoTool.Processing.Operations;

/// <summary>Carries out one settled conversion.</summary>
public interface IConversionRunner
{
    /// <summary>Runs a conversion to completion, or until it is stopped.</summary>
    /// <param name="plan">What to do.</param>
    /// <param name="progress">Where to report how far it has got, or null to report nothing.</param>
    /// <param name="cancellationToken">Stops the conversion.</param>
    /// <returns>What happened.</returns>
    Task<ConversionOutcome> RunAsync(
        ConversionPlan plan, IProgress<ConversionProgress> progress, CancellationToken cancellationToken);
}
