using System.ComponentModel.Design;
using System.Diagnostics.Tracing;
using System.IO.Pipes;
using System.Text;

namespace SingleInstanceProgramNS
{
    class SingleInstanceProgram
    {
        private readonly string _instanceId;
        private Mutex? mutex = null;
        private static SingleInstanceProgram? instance = null;
        private string[] myArgs;

        /// <summary>
        /// Creates initial instance of the class. instanceId takes a unique id to be used in named mutex and pipe server. args takes the data to be sent from additional instances of the program to the initial one.
        /// </summary>
        /// <param name="instanceId"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static SingleInstanceProgram GetInstance(string instanceId, string[] args)
        {
            if(instance != null)
            {
                throw new InvalidOperationException("Singleton already created - use getinstance()");
            }
            instance = new SingleInstanceProgram(instanceId, args);
            return instance;
        }

        /// <summary>
        /// Returns the previously created instance of the class.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
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
            myArgs = args;
        }

        /// <summary>
        /// Starts the single instance thread and calls functions that use events, event should be subscribed to before calling this method.
        /// </summary>
        public void Start()
        {
            mutex = new Mutex(false, $"Local\\{_instanceId}");
            if (!mutex.WaitOne(0, false))
            {
                SendToFirstInstance(myArgs);
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
                using (var server = new NamedPipeServerStream(_instanceId, PipeDirection.InOut))
                {
                    server.WaitForConnection();
                    using (var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true))
                    {
                        string? message = reader.ReadLine();
                        if (message != null)
                        {
                            MessageReceivedEventArgs eventArgs = new MessageReceivedEventArgs();
                            eventArgs.Message = message.Split();
                            Action<string[]> s = (string[] args) => 
                            {
                                using (var writer = new StreamWriter(server))
                                {
                                    writer.WriteLine(string.Join(" ", args));
                                    writer.Flush();
                                }
                            };
                            eventArgs.RespondToOtherSender = s;
                            OnMessageReceivedFromOtherInstance(eventArgs);
                        }
                    }
                }
            }
        }

        private void SendToFirstInstance(string[] args)
        {
            if (args.Length > 0)
            {
                using (var client = new NamedPipeClientStream(".",_instanceId,PipeDirection.InOut))
                {
                    client.Connect(200);
                    using (var writer = new StreamWriter(client, leaveOpen: true))
                    {
                        writer.WriteLine(string.Join(" ", args));
                        writer.Flush();
                    }
                    using (var reader = new StreamReader(client, Encoding.UTF8))
                    {
                        string? message = reader.ReadLine();
                        if (message != null)
                        {
                            MessageReceivedEventArgs eventArgs = new MessageReceivedEventArgs();
                            eventArgs.Message = message.Split();
                            OnMessageReceivedFromFirstInstance(eventArgs);
                        }
                    }
                }
            }
        }

        protected virtual void OnMessageReceivedFromOtherInstance(MessageReceivedEventArgs e)
        {
            MessageReceivedFromOtherInstance?.Invoke(this, e);
        }

        protected virtual void OnMessageReceivedFromFirstInstance(MessageReceivedEventArgs e)
        {
            MessageFromFirstInstanceReceived?.Invoke(this, e);
        }

        public event EventHandler<MessageReceivedEventArgs>? MessageReceivedFromOtherInstance;

        public event EventHandler<MessageReceivedEventArgs>? MessageFromFirstInstanceReceived;
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public string[]? Message;
        public Action<string[]>? RespondToOtherSender;
    }
}
