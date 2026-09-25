using Xunit.Sdk;
using Xunit.v3;

// One emulator, one app, one panel: scenarios run one at a time.
[assembly: Parallelization(Mode = ParallelMode.None)]
