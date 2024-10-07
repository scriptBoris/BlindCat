using System.Reflection.Metadata;

namespace BlindCatCore.Core
{
    public class Cmd : IAsyncCommand
    {
        private readonly object? _action;
        private bool isBusy;

        public event EventHandler? CanExecuteChanged;

        public bool IsRunning => isBusy;

        protected Cmd() { }

        public Cmd(Action action)
        {
            _action = action;
        }

        public Cmd(Func<Task> action)
        {
            _action = action;
        }

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public async void Execute(object? parameter)
        {
            if (isBusy)
                return;

            switch (_action)
            {
                case Action a:
                    a();
                    break;
                case Func<Task> ft:
                    await ft();
                    break;
                default: 
                    await Invoke(parameter);
                    break;
            }

            isBusy = false;
        }

        public async Task ExecuteAsync(object? parameter)
        {
            if (isBusy)
                return;

            switch (_action)
            {
                case Action a:
                    a();
                    break;
                case Func<Task> ft:
                    await ft();
                    break;
                default:
                    await Invoke(parameter);
                    break;
            }

            isBusy = false;
        }

        protected virtual Task Invoke(object? parameter)
        {
            return Task.CompletedTask;
        }
    }
}
