using System.IO.Pipes;
using System.Text;

namespace SingleInstanceProgramNS
{
    class SingleInstanceProgram
    {
        private readonly string _instanceId;
        private Mutex mutex;
        private static SingleInstanceProgram? instance = null;


        public static SingleInstanceProgram GetInstance(string instanceId, string[] args)
        {
            if(instance != null)
            {
                throw new InvalidOperationException("Singleton already created - use getinstance()");
            }
            instance = new SingleInstanceProgram(instanceId, args);
            return instance;
        }

        public static SingleInstanceProgram GetInstance()
        {
            if (instance == null)
            {
                throw new InvalidOperationException("Singleton not created - use GetInstance(arg1, arg2)");
            }
            return instance;
        }
        private SingleInstanceProgram(string instanceId, string[] args)
        {

            _instanceId = instanceId;
            mutex = new Mutex(false, $"Local\\{instanceId}");
            if (!mutex.WaitOne(0, false))
            {
                SendToFirstInstance(args);
                Environment.Exit(0);
            }

            Console.WriteLine("This is the first instance of the application.");
            Thread listenerThread = new Thread(ListenForClients);
            listenerThread.IsBackground = true;
            listenerThread.Start();
        }

        private void ListenForClients()
        {
            while (true)
            {
                using (var server = new NamedPipeServerStream(_instanceId, PipeDirection.In))
                {
                    server.WaitForConnection();
                    using (var reader = new StreamReader(server, Encoding.UTF8))
                    {
                        string? message = reader.ReadLine();
                        if (message != null)
                        {
                            MessageReceivedEventArgs eventArgs = new MessageReceivedEventArgs();
                            eventArgs.Message = message.Split();
                            OnMessageReceived(eventArgs);
                        }
                    }
                }
            }
        }

        private void SendToFirstInstance(string[] args)
        {
            if (args.Length > 0)
            {
                using (var client = new NamedPipeClientStream(".",_instanceId,PipeDirection.Out))
                {
                    client.Connect(200);
                    using (var writer = new StreamWriter(client))
                    {
                        writer.WriteLine(string.Join(" ", args));
                        writer.Flush();
                    }
                }
            }
        }

        protected virtual void OnMessageReceived(MessageReceivedEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public string[]? Message { get; set; }
    }
}
