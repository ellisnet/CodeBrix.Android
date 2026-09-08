using System;
using System.Threading.Tasks;
using Android.App;
using Android.OS;
using Android.Util;
using Android.Widget;

namespace SimpleDebugApp;

/// <summary>
/// A deliberately small activity whose every button is a debugger exercise:
/// a synchronous click, a loop with locals, an awaited continuation, a thread-pool hop,
/// and a thrown-and-caught exception. Set breakpoints in the On*Clicked methods here and
/// in <see cref="Calculator"/>.
/// </summary>
[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true, Name = "com.codebrix.simpledebugapp_net11.MainActivity")]
public class MainActivity : Activity
{
	private const string Tag = "SimpleDebugApp";

	private int _count;
	private TextView _statusText;
	private TextView _counterText;

	protected override void OnCreate(Bundle savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		SetContentView(Resource.Layout.activity_main);

		_statusText = FindViewById<TextView>(Resource.Id.status_text);
		_counterText = FindViewById<TextView>(Resource.Id.counter_text);

		FindViewById<Button>(Resource.Id.count_button).Click += (sender, args) => OnCountClicked();
		FindViewById<Button>(Resource.Id.compute_button).Click += (sender, args) => OnComputeClicked();
		FindViewById<Button>(Resource.Id.async_button).Click += async (sender, args) => await OnAsyncClickedAsync();
		FindViewById<Button>(Resource.Id.background_button).Click += (sender, args) => OnBackgroundClicked();
		FindViewById<Button>(Resource.Id.throw_button).Click += (sender, args) => OnThrowClicked();

		ShowCount();
		Log.Info(Tag, "OnCreate finished");
	}

	/// <summary>Plain synchronous handler. Breakpoint here stops on the UI thread.</summary>
	private void OnCountClicked()
	{
		_count++;
		string description = Calculator.DescribeCount(_count);
		ShowCount();
		SetStatus(description);
		Log.Info(Tag, description);
	}

	/// <summary>Calls a loop with locals worth inspecting; step into PrimesBelow.</summary>
	private void OnComputeClicked()
	{
		int limit = 200 + (_count * 10);
		int[] primes = Calculator.PrimesBelow(limit);
		int last = primes.Length > 0 ? primes[primes.Length - 1] : 0;
		SetStatus($"{primes.Length} primes below {limit}; the largest is {last}");
	}

	/// <summary>Awaits a delay; a breakpoint after the await lands in the continuation.</summary>
	private async Task OnAsyncClickedAsync()
	{
		SetStatus("Waiting 750 ms...");
		await Task.Delay(750);
		string threadName = System.Threading.Thread.CurrentThread.Name ?? "(UI thread)";
		SetStatus($"Async continuation ran on: {threadName}");
	}

	/// <summary>Runs work on a thread-pool thread, then marshals the result back to the UI thread.</summary>
	private void OnBackgroundClicked()
	{
		SetStatus("Working in the background...");
		int n = 100_000 + _count;
		Task.Run(() =>
		{
			long total = Calculator.SumOfSquares(n);
			int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
			RunOnUiThread(() => SetStatus($"Sum of squares to {n} is {total} (worker thread {threadId})"));
		});
	}

	/// <summary>Throws and catches; useful for first-chance exception stops.</summary>
	private void OnThrowClicked()
	{
		try
		{
			Calculator.ThrowForDemo(_count);
			SetStatus("No exception was thrown (unexpected)");
		}
		catch (InvalidOperationException ex)
		{
			SetStatus($"Caught: {ex.Message}");
			Log.Warn(Tag, ex.Message);
		}
	}

	private void ShowCount()
	{
		_counterText.Text = _count.ToString();
	}

	private void SetStatus(string message)
	{
		_statusText.Text = message;
	}
}
