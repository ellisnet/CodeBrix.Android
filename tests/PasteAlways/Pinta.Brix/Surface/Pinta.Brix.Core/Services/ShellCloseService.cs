using System;
using System.Threading.Tasks;

namespace Pinta.Brix.Services;

/// <summary>
/// The single instance of <see cref="IShellCloseService"/> the application
/// registers: it holds whatever prompt loop the shell installed and nothing
/// else, so it is safe to resolve before any window or page exists.
/// </summary>
public sealed class ShellCloseService : IShellCloseService
{
    /// <inheritdoc />
    public Func<Task<bool>> ConfirmCloseApplicationAsync { get; set; }

    /// <inheritdoc />
    public async Task<bool> ConfirmCloseAsync()
    {
        //Read once: the shell can replace the loop while a close is in flight.
        Func<Task<bool>> prompt = ConfirmCloseApplicationAsync;

        if (prompt == null) { return true; }

        return await prompt();
    }
}
