using Xunit.Sdk;
using Xunit.v3;

// One test at a time, as the Platform suite runs (its AssemblyInfo.cs): the GPU tests share one GPU, the latch tests flip
// an application-wide switch, and the frame-rate tests count frames against a clock.
[assembly: Parallelization(Mode = ParallelMode.None)]
