﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShoeDryer
{
	public class MainWindowVM: INotifyPropertyChanged
	{

		public MainWindowVM()
		{
			SetCoreThreads();
			StartCommand = AsyncCommandFactory.Create(StartCommandAsync, t => CoresForWorking > 0 && TasksContinues.Count == 0);
			StopCommand = AsyncCommandFactory.Create(StopCommandAsync, t => Cancel != null);

		}

		public ObservableCollection<Int32> CoresDropDown
		{
			get; set;
		} = new ObservableCollection<int>();

		private Int32 _CoresForExecuting = 1;
		public Int32 CoresForWorking
		{
			get
			{
				return _CoresForExecuting;
			}
			set
			{
				SetField(ref _CoresForExecuting, value);
			}
		}
		private List<Task> _Tasks;
		private List<Task> TasksContinues => _Tasks ?? (_Tasks = new List<Task>());
		public IAsyncCommand StartCommand
		{
			get; set;
		}
		private CancellationTokenSource Cancel
		{
			get; set;
		}
		private async Task StartCommandAsync()
		{
			if (Cancel != null && TasksContinues.Count != 0)
			{
				await StopCommand.ExecuteAsync(null);
			}
			Cancel = new CancellationTokenSource();
			for (int i = 0; i < CoresForWorking; i++)
			{
				//var task = WorkTask(Cancel.Token);
				//try/catch instead - using continuewith OnlyOnCancelled
				var taskContinue = WorkTask(Cancel.Token).ContinueWith(t => TasksContinues.Clear(), TaskContinuationOptions.OnlyOnCanceled);
				TasksContinues.Add(taskContinue);
			}
			await Task.WhenAll(TasksContinues).ConfigureAwait(false);
		}
		public IAsyncCommand StopCommand
		{
			get; set;
		}
		private async Task StopCommandAsync()
		{
			Cancel?.Cancel();
			Cancel = null;
		}

		private Task WorkTask(CancellationToken token)
		{
			return Task.Run(() =>
			{
				var random = new Random();
				while (true)
				{
					random.Next(Int32.MinValue, Int32.MaxValue);
					//In any cases even if i call "throw new OperationCancelException" i'll get an task status == cancelled.
					//Special exception type are important.
					//But we'll use a classic approach. When we didn't have async/await keywords.
					if (token.IsCancellationRequested)
						token.ThrowIfCancellationRequested();
				}
			}, token);
		}

		private void SetCoreThreads()
		{
			for (int i = 1; i <= Environment.ProcessorCount; i++)
			{
				CoresDropDown.Add(i);
			}
		}
		#region INotifyPropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
			{
				return false;
			}
			field = value;
			OnPropertyChanged(propertyName);
			return true;
		}
		#endregion
	}
}
