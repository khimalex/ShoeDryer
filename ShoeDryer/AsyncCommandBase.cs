using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ShoeDryer
{
	public interface IAsyncCommand : ICommand
	{
		Task ExecuteAsync(object parameter);
	}

	public abstract class AsyncCommandBase : IAsyncCommand
	{
		public abstract bool CanExecute(object parameter);

		public abstract Task ExecuteAsync(object parameter);

		public async void Execute(object parameter)
		{
			await ExecuteAsync(parameter);
		}

		public event EventHandler CanExecuteChanged
		{
			add { CommandManager.RequerySuggested += value; }
			remove { CommandManager.RequerySuggested -= value; }
		}

		protected void RaiseCanExecuteChanged()
		{
			CommandManager.InvalidateRequerySuggested();
		}
	}

	public class AsyncCommand<TResult> : AsyncCommandBase, INotifyPropertyChanged
	{
		private readonly Func<CancellationToken, Task<TResult>> _command;
		private readonly Func<object, CancellationToken, Task<TResult>> _parametrizedCommand;
		private readonly CancelAsyncCommand _cancelCommand;
		private NotifyTaskCompletion<TResult> _execution;
		private Predicate<object> _CanExecute;


		public AsyncCommand(Func<CancellationToken, Task<TResult>> command)
		{
			_command = command;
			_cancelCommand = new CancelAsyncCommand();
		}
		public AsyncCommand(Func<CancellationToken, Task<TResult>> command, Predicate<object> canExecute) : this(command)
		{
			//_command = command;
			//_cancelCommand = new CancelAsyncCommand();
			_CanExecute = canExecute;
		}

		public AsyncCommand(Func<object, CancellationToken, Task<TResult>> parametrizedCommand)
		{
			_parametrizedCommand = parametrizedCommand;
			_cancelCommand = new CancelAsyncCommand();
		}

		public AsyncCommand(Func<object, CancellationToken, Task<TResult>> parametrizedCommand, Predicate<object> canExecute) : this(parametrizedCommand)
		{
			_CanExecute = canExecute;
		}

		public override bool CanExecute(object parameter)
		{
			var canExecute = _CanExecute?.Invoke(parameter);
			var predicateResult = canExecute.HasValue ? canExecute.Value : true;
			return (Execution == null || Execution.IsCompleted) && predicateResult;
		}

		public override async Task ExecuteAsync(object parameter)
		{
			_cancelCommand.NotifyCommandStarting();
			if (_command != null)
			{
				Execution = new NotifyTaskCompletion<TResult>(_command(_cancelCommand.Token));
			}
			else
			{
				Execution = new NotifyTaskCompletion<TResult>(_parametrizedCommand(parameter, _cancelCommand.Token));
			}
			RaiseCanExecuteChanged();
			await Execution.TaskCompletion;
			OnPropertyChanged(nameof(Execution)); //Говорим ,Что св-о изменилось, Чтобы подписанные на него объекты смогли обработаться правильно.
			_cancelCommand.NotifyCommandFinished();
			RaiseCanExecuteChanged();
		}

		public ICommand CancelCommand
		{
			get { return _cancelCommand; }
		}

		public NotifyTaskCompletion<TResult> Execution
		{
			get { return _execution; }
			private set
			{
				_execution = value;
				OnPropertyChanged();
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private sealed class CancelAsyncCommand : ICommand
		{
			private CancellationTokenSource _cts = new CancellationTokenSource();
			private bool _commandExecuting;

			public CancellationToken Token { get { return _cts.Token; } }

			public void NotifyCommandStarting()
			{
				_commandExecuting = true;
				if (!_cts.IsCancellationRequested)
					return;
				_cts = new CancellationTokenSource();
				RaiseCanExecuteChanged();
			}

			public void NotifyCommandFinished()
			{
				_commandExecuting = false;
				RaiseCanExecuteChanged();
			}

			bool ICommand.CanExecute(object parameter)
			{
				return _commandExecuting && !_cts.IsCancellationRequested;
			}

			void ICommand.Execute(object parameter)
			{
				_cts.Cancel();
				RaiseCanExecuteChanged();
			}

			public event EventHandler CanExecuteChanged
			{
				add { CommandManager.RequerySuggested += value; }
				remove { CommandManager.RequerySuggested -= value; }
			}

			private void RaiseCanExecuteChanged()
			{
				CommandManager.InvalidateRequerySuggested();
			}
		}
	}

	public static class AsyncCommandFactory
	{
		public static AsyncCommand<object> Create(Func<Task> command)
		{
			return new AsyncCommand<object>(async _ => { await command(); return null; });
		}
		public static AsyncCommand<object> Create(Func<object, Task> parametrizedCommand)
		{
			return new AsyncCommand<object>(async (param, _) => { await parametrizedCommand(param); return null; });
		}

		public static AsyncCommand<object> Create(Func<Task> command, Predicate<object> canExecute)
		{
			return new AsyncCommand<object>(async _ => { await command(); return null; }, canExecute);
		}
		public static AsyncCommand<object> Create(Func<object, Task> parametrizedCommand, Predicate<object> canExecute)
		{
			return new AsyncCommand<object>(async (param, _) => { await parametrizedCommand(param); return null; }, canExecute);
		}

		public static AsyncCommand<TResult> Create<TResult>(Func<Task<TResult>> command)
		{
			return new AsyncCommand<TResult>(_ => command());
		}
		public static AsyncCommand<TResult> Create<TResult>(Func<object, Task<TResult>> parametrizedCommand)
		{
			return new AsyncCommand<TResult>((param, _) => parametrizedCommand(param));
		}

		public static AsyncCommand<TResult> Create<TResult>(Func<Task<TResult>> command, Predicate<object> canExecute)
		{
			return new AsyncCommand<TResult>(_ => command(), canExecute);
		}
		public static AsyncCommand<TResult> Create<TResult>(Func<object, Task<TResult>> parametrizedCommand, Predicate<object> canExecute)
		{
			return new AsyncCommand<TResult>((param, _) => parametrizedCommand(param), canExecute);
		}

		public static AsyncCommand<object> Create(Func<CancellationToken, Task> command)
		{
			return new AsyncCommand<object>(async token => { await command(token); return null; });
		}
		public static AsyncCommand<object> Create(Func<object, CancellationToken, Task> parametrizedCommand)
		{
			return new AsyncCommand<object>(async (param, token) => { await parametrizedCommand(param, token); return null; });
		}

		public static AsyncCommand<object> Create(Func<CancellationToken, Task> command, Predicate<object> canExecute)
		{
			return new AsyncCommand<object>(async token => { await command(token); return null; }, canExecute);
		}
		public static AsyncCommand<object> Create(Func<object, CancellationToken, Task> parametrizedCommand, Predicate<object> canExecute)
		{
			return new AsyncCommand<object>(async (param, token) => { await parametrizedCommand(param, token); return null; }, canExecute);
		}

		public static AsyncCommand<TResult> Create<TResult>(Func<CancellationToken, Task<TResult>> command)
		{
			return new AsyncCommand<TResult>(command);
		}
		public static AsyncCommand<TResult> Create<TResult>(Func<object, CancellationToken, Task<TResult>> parametrizedCommand)
		{
			return new AsyncCommand<TResult>(parametrizedCommand);
		}

		public static AsyncCommand<TResult> Create<TResult>(Func<CancellationToken, Task<TResult>> command, Predicate<object> canExecute)
		{
			return new AsyncCommand<TResult>(command, canExecute);
		}
		public static AsyncCommand<TResult> Create<TResult>(Func<object, CancellationToken, Task<TResult>> parametrizedCommand, Predicate<object> canExecute)
		{
			return new AsyncCommand<TResult>(parametrizedCommand, canExecute);
		}

	}

}
