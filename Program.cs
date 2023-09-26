// See https://aka.ms/new-console-template for more information

using System.Collections;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
bool administrativeMode = principal.IsInRole(WindowsBuiltInRole.Administrator);

//if (!administrativeMode)
//{
//    Console.WriteLine("Run As Admin or change permissions on the ");
//}
int i = 1;
try
{
    bool isModded = await IsAllModded();

    var title = "EAC for Star Citizen is Enabled: " + isModded;
    Console.WriteLine(title);
    Console.WriteLine(new string('=',title.Length));
    Console.WriteLine();
    var enable = await Prompt("Eac Bypass?", "Enable", "Disable");
    
    i += await EditHosts(enable == 0);
    i += DeleteEAC(enable == 0);
    i += await EditEACSettings(enable == 0);

    Console.WriteLine("Done. Press any key to quit");
    Console.ReadKey();

}
catch (Exception ex)
{
    var prev = Console.ForegroundColor;

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(ex.ToString());
    Console.ForegroundColor = prev;
    
    if (i > 0)
    {
        Console.WriteLine("Rollback");

    }
    
}

static async Task<bool> IsAllModded()
{
    return await IsHostsModified() && await IsEACFolderModded() && await IsEacSettingsModified();
}

static async Task<bool> IsHostsModified()
{
    bool yes = false;

    var h = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers\\etc\\hosts");
    if (File.Exists(h))
    {
        var text = await File.ReadAllLinesAsync(h);
        var comment = "#Star Citizen EAC workaround";
        yes = text.Any(l => l == comment);
    }



    return yes;

}


static async Task<int> EditHosts(bool enable = true)
{

    var hosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers\\etc\\hosts");
    if (File.Exists(hosts))
    {
        var text = await File.ReadAllLinesAsync(hosts);
        var comment = "#Star Citizen EAC workaround";
        if (enable && text.All(l => l != comment))
        {
            File.AppendAllText(hosts, @$"
{comment}
127.0.0.1        modules-cdn.eac-prod.on.epicgames.com");

            Console.WriteLine("hosts modded");
        }
        else if (!enable && text.Any(t => t == comment))
        {
            var txt = text.Except(text.Where(t =>
                t == comment || Regex.Match(t, @"127\.0\.0\.1\s+modules-cdn\.eac-prod\.on\.epicgames\.com").Success)).ToList();

            if (text.Length > txt!.Count)
            {
                File.WriteAllLines(hosts,txt,Encoding.ASCII);
            }

            Console.WriteLine("hosts un-modded");
        }

    }

    return 1;
}

static async Task<bool> IsEACFolderModded()
{
    var eacFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyAntiCheat");

    bool yes = !Directory.Exists(eacFolder) && Directory.Exists(eacFolder + "_bak");

    return yes;

}
static int DeleteEAC(bool enable)
{
    var eacFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EasyAntiCheat");
    if (enable && Directory.Exists(eacFolder))
    {
        
        Directory.Move(eacFolder,eacFolder+"_bak");
        Console.WriteLine("eac folder moved");
    }
    else if (!enable && Directory.Exists(eacFolder + "_bak"))
    {
        Directory.Move(eacFolder + "_bak", eacFolder);
        Console.WriteLine("eac folder restored");
    }

    return 1;
}

static async Task<bool> IsEacSettingsModified()
{
    var scLivefolder = @"Z:\Games\StarCitizen\LIVE\";

    if (scLivefolder == null || !Directory.Exists(scLivefolder))
        scLivefolder = await Question("Path to \"StarCitizen\\LIVE\" folder:");

    if (string.IsNullOrWhiteSpace(scLivefolder) || !Directory.Exists(scLivefolder))
    {
        throw new Exception("Starcitizen folder is invalid");
    }
    var settingsfile = Path.Combine(scLivefolder, "EasyAntiCheat\\Settings.json");

    if (File.Exists(settingsfile))
    {
        var txt = File.ReadAllText(settingsfile);
        return txt.Contains("vorpx-eac-workaround");
    }

    return false;
}
static async Task<int> EditEACSettings(bool enable)
{
    var scLivefolder = @"Z:\Games\StarCitizen\LIVE\";

    if(scLivefolder == null || !Directory.Exists(scLivefolder))
        scLivefolder = await Question("Path to \"StarCitizen\\LIVE\" folder:");

    if (string.IsNullOrWhiteSpace(scLivefolder) || !Directory.Exists(scLivefolder))
    {
        throw new Exception("Starcitizen folder is invalid");
    }
    var settingsfile = Path.Combine(scLivefolder, "EasyAntiCheat\\Settings.json");

    if (!File.Exists(settingsfile))
    {
        throw new Exception("Star citizen folder is invalid, no EasyAntiCheat/Settings.json was inside");
    }
    if (enable)
    {

        if (!File.Exists(settingsfile + ".bak"))
        {
            Console.WriteLine("backing up " + Path.GetFileName(settingsfile) );
            File.Copy(settingsfile, settingsfile + ".bak");
        }

        var node = JObject.Parse(await File.ReadAllTextAsync(settingsfile));

        foreach (var n in new[] {"productid", "sandboxid", "clientid", "deploymentid"})
        {
            node[n] = "vorpx-eac-workaround";
        }
        
        var newJson = node.ToString();

        if (File.Exists(settingsfile + ".bak"))
        {
            Console.WriteLine("backup exists modifying " + Path.GetFileName(settingsfile));
            await File.WriteAllTextAsync(settingsfile, newJson.ToString());
        }
        else
        {
           throw new Exception("Backup of Settings.json does not exist! will not update.");
        }
    }
    else if (!enable)
    {
        if (File.Exists(settingsfile + ".bak"))
        {
            Console.WriteLine("Restoring " + settingsfile);
            File.Move(settingsfile+".bak", settingsfile, true);
        }
        else
        {
            throw new Exception("settings backup did not exist!");
        }
    }

    return 1;
}

static Task<string?> Question(string question)
{
    Console.WriteLine(question);

    return Task.Run(Console.ReadLine);

}

static async Task<int> Prompt(string question, params string[] choices)
{
    Console.WriteLine(question);
    for (var index = 0; index < choices.Length; index++)
    {
        var choice = choices[index];
        Console.WriteLine($"{(index+1)}) {choice}");
    }

    var answer = await Task.Run<int>(() =>
    {
        tryagain:

        if (choices.Length == 0)
        {
            Console.ReadLine();
            return -1;
        }

        var chr = Console.ReadKey().KeyChar.ToString();

        int k = Convert.ToInt32(chr ) -1;

        if (k >= 0 && k < choices.Length)
        {
            return k;
        }
        else
        {
            Console.WriteLine("Invalid Choice");
            goto tryagain;
        }
        return -1;
    });

    return answer;
}
