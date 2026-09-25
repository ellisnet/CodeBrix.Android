using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The command a scenario binds a button to: it says whether it can execute, it counts how
/// often it was executed, and it raises <see cref="CanExecuteChanged"/> the moment a scenario
/// flips that answer - which is the whole of what a requirement about Command and CanExecute
/// needs to observe.
/// </summary>
public sealed class TestCommand : ICommand
{
	private bool _canExecute = true;

	/// <summary>Builds a command under the name a feature file refers to it by.</summary>
	/// <param name="name">The Gherkin name of the command.</param>
	public TestCommand(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);
		Name = name;
	}

	/// <summary>The name a feature file refers to this command by.</summary>
	public string Name { get; }

	/// <summary>How often the command has been executed this scenario.</summary>
	public int ExecutionCount { get; private set; }

	/// <summary>The parameter the most recent execution carried.</summary>
	public object? LastParameter { get; private set; }

	/// <summary>
	/// Whether the command currently says it can execute. Setting this raises
	/// <see cref="CanExecuteChanged"/>, which is what a bound button listens to.
	/// </summary>
	public bool IsExecutable
	{
		get => _canExecute;
		set
		{
			if (_canExecute == value)
			{
				return;
			}

			_canExecute = value;
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	/// <summary>Raised when <see cref="IsExecutable"/> changes.</summary>
	public event EventHandler? CanExecuteChanged;

	/// <summary>Whether the command can execute right now.</summary>
	/// <param name="parameter">The parameter a bound control passes; it is not consulted.</param>
	/// <returns><c>true</c> when the command is executable.</returns>
	public bool CanExecute(object? parameter) => _canExecute;

	/// <summary>Executes the command, which only counts the execution.</summary>
	/// <param name="parameter">The parameter a bound control passes.</param>
	public void Execute(object? parameter)
	{
		ExecutionCount++;
		LastParameter = parameter;
	}

	/// <summary>The command as "name, executable, executed n times".</summary>
	/// <returns>The description.</returns>
	public override string ToString() => string.Create(CultureInfo.InvariantCulture,
		$"\"{Name}\" ({(_canExecute ? "executable" : "not executable")}, executed {ExecutionCount} times)");
}

/// <summary>
/// The commands the current scenario has named. They live for one scenario only, so a count
/// one scenario made can never be read by the next one.
/// </summary>
public static class TestCommands
{
	private static readonly Dictionary<string, TestCommand> Commands = new(StringComparer.Ordinal);

	/// <summary>Every command the current scenario has named, with its state.</summary>
	public static IReadOnlyCollection<string> Described =>
		Commands.Values.Select(command => command.ToString()).ToArray();

	/// <summary>The command of that name, created on first mention.</summary>
	/// <param name="name">The Gherkin name of the command.</param>
	/// <returns>The command.</returns>
	public static TestCommand Declare(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		if (!Commands.TryGetValue(name, out var command))
		{
			command = new TestCommand(name);
			Commands[name] = command;
		}

		return command;
	}

	/// <summary>The command of that name, which the scenario must already have named.</summary>
	/// <param name="name">The Gherkin name of the command.</param>
	/// <returns>The command.</returns>
	/// <exception cref="InvalidOperationException">The scenario never named such a command.</exception>
	public static TestCommand Resolve(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		if (Commands.TryGetValue(name, out var command))
		{
			return command;
		}

		var known = Commands.Count == 0
			? "the scenario has named no command"
			: "the scenario has named: " + string.Join(", ", Described);
		throw new InvalidOperationException($"No command named \"{name}\" exists ({known}).");
	}

	/// <summary>Forgets every command. The button steps call this per scenario.</summary>
	public static void Clear() => Commands.Clear();
}
