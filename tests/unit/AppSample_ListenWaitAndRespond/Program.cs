using SingleInstanceProgramNS;

//event handler
void s_MessageReceivedFromOtherInstance(object? sender, MessageReceivedEventArgs e)
{
    //process the string array(commands sent by the other instance)
    if (e.Message != null)
    {
        string[] toSend = ProcessArgsFirstInstance(e.Message).ToArray();
        //Respond to the sender instance
        Thread.Sleep(2000);
        e.RespondToOtherSender?.Invoke(toSend);
    }
}

void s_MessageReceivedFromFirstInstance(object? sender, MessageReceivedEventArgs e)
{
    if (e.Message != null)
    {
        ProcessArgsSecondaryInstance(e.Message);
    }
}

List<string> ProcessArgsFirstInstance(string[] strings)
{
    List<string> result = new List<string>();
    foreach (string s in strings)
    {
        result.Add(s + " processed by first");
        Console.WriteLine(s + " processed by first");
    }
    return result;
}

void ProcessArgsSecondaryInstance(string[] strings)
{
    foreach(string s in strings)
    {
        Console.WriteLine(s + " processed by second");
    }
}

SingleInstanceProgram s = SingleInstanceProgram.Create(AppDomain.CurrentDomain.FriendlyName, args);


//Subscribe to the events before starting the single instance thread and calling functions that use events
s.MessageReceivedFromFirstInstance += s_MessageReceivedFromFirstInstance;
s.MessageReceivedFromOtherInstance += s_MessageReceivedFromOtherInstance;

//Starts the single instance thread and can call functions that use events
s.Start();

//Since the initial instance of the program won't trigger MessageReceived event handler, run the function for processing args manually.
ProcessArgsFirstInstance(args);

//First instance waits before quitting for testing
Console.ReadLine();

s.Stop();