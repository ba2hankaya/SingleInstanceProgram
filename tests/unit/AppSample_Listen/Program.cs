using SingleInstanceProgramNS;

//event handler
void s_MessageReceivedFromOtherInstance(object? sender, MessageReceivedEventArgs e)
{
    //process the string array(commands sent by the other instance)
    if (e.Message != null)
    {
        ProcessArgsFirstInstance(e.Message);
    }
}

void ProcessArgsFirstInstance(string[] strings)
{
    foreach (string s in strings)
    {
        Console.WriteLine(s + " processed by first");
    }
}

string firstInstanceIO = GenerateArgumentString("arg", 500, 0);
SingleInstanceProgram s = SingleInstanceProgram.Create(AppDomain.CurrentDomain.FriendlyName, args);


//Subscribe to the events before starting the single instance thread and calling functions that use events
s.MessageReceivedFromOtherInstance += s_MessageReceivedFromOtherInstance;

//Starts the single instance thread and can call functions that use events
s.Start();

//Since the initial instance of the program won't trigger MessageReceived event handler, run the function for processing args manually.
Console.WriteLine("This is the first instance of the application.");
//ProcessArgsFirstInstance(firstInstanceIO.Split());
ProcessArgsFirstInstance(args);

//First instance waits before quitting for testing
Console.ReadLine();

s.Stop();

static string GenerateArgumentString(string argName, int argCount, int instanceNumber)
{
    List<string> result = new();
    for (int i = 0; i < argCount; i++)
    {
        result.Add($"I{instanceNumber}_{argName}{i.ToString()}");
    }

    return String.Join(" ", result);
}