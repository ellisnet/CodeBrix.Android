namespace Reqnroll.UnitTestProvider;

/// <summary>The runtime services of the unit-test framework (on the device: skip = tell the host).</summary>
public interface IUnitTestRuntimeProvider
{
    /// <summary>Skips the scenario with a reason.</summary>
    void TestIgnore(string message);

    /// <summary>Marks the scenario pending.</summary>
    void TestPending(string message);

    /// <summary>Marks the scenario inconclusive.</summary>
    void TestInconclusive(string message);
}
