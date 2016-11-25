using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShoeDryer
{
	public class MainWindowVM : INotifyPropertyChanged
	{

		public MainWindowVM()
		{
			StartCommand = AsyncCommandFactory.Create(StartCommandAsync, t => Threads > 0 && Tasks.Count == 0);
			StopCommand = AsyncCommandFactory.Create(StopCommandAsync, t => Cancel != null);
		}
		public Int32 Threads { get; set; }
		private List<Task> _Tasks;
		private List<Task> Tasks => _Tasks ?? (_Tasks = new List<Task>());

		public IAsyncCommand StartCommand { get; set; }
		private CancellationTokenSource Cancel { get; set; }
		private async Task StartCommandAsync()
		{
			if (Cancel != null && Tasks.Count != 0)
			{
				await StopCommand.ExecuteAsync(null);
			}
			Cancel = new CancellationTokenSource();
			for (int i = 0; i < Threads; i++)
			{
				var task = WorkTask();
				Tasks.Add(task);
			}
			await Task.WhenAll(Tasks);
		}
		public IAsyncCommand StopCommand { get; set; }
		private async Task StopCommandAsync()
		{
			Cancel?.Cancel();
			Cancel = null;
			Tasks.Clear();
		}

		private async Task WorkTask()
		{
			await Task.Run(() =>
			{
				var random = new Random();
				while ((Cancel?.Token.IsCancellationRequested).HasValue && !Cancel.Token.IsCancellationRequested)
				{
					random.Next(Int32.MinValue, Int32.MaxValue);
				}
				Cancel?.Token.ThrowIfCancellationRequested();
			}, Cancel.Token);
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
