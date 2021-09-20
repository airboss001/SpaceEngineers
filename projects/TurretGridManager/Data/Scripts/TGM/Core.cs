//This is a script provided by Cheerkin that wasn't working in the lastest build at the time of this posting (9/2021).
//This is the second update (first was 12/2018) of the script to make it work with current SE changes.
//
//I take no credit for the creation of this module.
//
//Here is a link to the script I started at:
//https://steamcommunity.com/sharedfiles/filedetails/?id=620458663&searchtext=turret+grid+manager

public static class Config
{
    public static string DefaultTextPanelName = "TurretPanel";
    public static string Title = "\n===== RobCo Turret Control {0} =====\n";

    public static class TextFormatting
    {
        public static string PaddingLeft = "    ";
        public const int MaxCharsPerLine = 75;
    }

    public static class AmmoManagement
    {
        public static bool RefillEnabled = true;
        public static int OptimalAmount = 5;
        public static string FeedContainerName = "Cargo Container Ammo";
        public static string NoFeedContainerFound = "No Feed Container was found. Check that at least one container is set to: " 
        + FeedContainerName
        + " and re-run script.";
        public static string MultipleFeedContainersFound = "Multiple Feed Containers were found. First returned will be used. (indeterminate)";
    }

    public static class TextMessages
    {
        public static string DryNoAmmoContainers = "{0}: DRY (no ammo containers).";
        public static string WarningTurretIsDamagedHealthLevel = "{0}: Warning! Turret is damaged, health level: {1}.";
        public static string NoTurretsDetected = "No turrets detected.";
        public static string LowOnAmmoOfAmmoContainers = "{0}: Low on ammo ({1} of {2} ammo containers).";
        public static string FailedToFindAmmo = "Failed to find ammo. Please check storage \"{0}\".";
        public const string FailedToSend = "Failed to send {0} units of {1}. Please check conveyor connection.";
        public const string SendingUnits = "Sending {0} units of {1}.";
        public const string ConveyerIsEnabled = "{0}: Warning! Configured to use conveyor in auto mode.";
        public const string AmmoSpentToday = "\nToday the grid has chewed through {0} ammo containers:";
        public const string AmmoSpentByTurret = "Unit \"{0}\" has received {1} ammo so far";
    }
}

// We'll need this every tick, static global variables lifetime is per world reload 
public IMyTextPanel OutputPanel
{
    get
    {
        return GridTerminalSystem.GetBlockWithName(Config.DefaultTextPanelName) as IMyTextPanel;
    }
}

private IMyInventoryOwner _ammoFeedInventory;
public IMyInventoryOwner AmmoFeedInventory
{
    get
    {
        return _ammoFeedInventory;
    }
}

private Dictionary<string, IMyTextPanel> _pluginPanels = new Dictionary<string, IMyTextPanel>();
public IMyTextPanel GetPluginPanel(string customName)
{
    if (!_pluginPanels.ContainsKey(customName)) // can be killed though, in this case there would be a NRE 
        _pluginPanels.Add(customName, (IMyTextPanel)GridTerminalSystem.GetBlockWithName(customName));

    return _pluginPanels[customName];
}

public class TurretConsumptionStat
{
    public IMyInventory Turret { get; set; }
    public int Amount { get; set; }
}

public List<TurretConsumptionStat> TotalItemsReceived = new List<TurretConsumptionStat>();
public void AddReceivedCount(IMyInventory key, int count)
{
    for (int i = 0; i < TotalItemsReceived.Count; i++)
    {
        if (TotalItemsReceived[i].Turret == key)
        {
            TotalItemsReceived[i].Amount += count;
            return;
        }
    }
    TotalItemsReceived.Add(new TurretConsumptionStat { Turret = key, Amount = count });
}

public Program()
{
    // No point in getting this every tick 
    List<IMyTerminalBlock> mioList = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(mioList, x => x.CustomName.Contains(Config.AmmoManagement.FeedContainerName));
    
    if(mioList.Count > 1)
    {
        AppendPanelNewlineText(OutputPanel, Config.AmmoManagement.MultipleFeedContainersFound, true);
    }
    
    if(mioList.Count > 0)
    {
        _ammoFeedInventory = (IMyInventoryOwner) mioList[0];
    }    
    else
    {
        AppendPanelNewlineText(OutputPanel, Config.AmmoManagement.NoFeedContainerFound, true);
        return;
    }

    Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Main(string argument, UpdateType updateSource)
{
    DateTime _start = DateTime.Now;

    OutputPanel.WritePublicText(String.Format(Config.Title, DateTime.Now));

    // Lifetime per script RUN - to handle grid changes 
    List<IMyTerminalBlock> turrets = new List<IMyTerminalBlock>();

    // IMyLargeTurretBase, IMyLargeGatlingTurret, IMyLargeMissileTurret, IMyLargeInteriorTurret
    GridTerminalSystem.GetBlocksOfType<IMyLargeTurretBase>(turrets);
    Echo("Turrets in current grid: " + turrets.Count.ToString());

    if (turrets.Count == 0)
    {
        AppendPanelNewlineText(OutputPanel, Config.TextMessages.NoTurretsDetected, true);
        return;
    }

    AppendPanelNewlineText(OutputPanel, TitleWithSpinner("Monitored turrets count: " + turrets.Count + " {0}", 1), true);

    for (int n = 0; n < turrets.Count; n++)
    {
        var health = GetMyTerminalBlockHealth(turrets[n]);
        if (health < 1)
        {
            AppendPanelNewlineText(OutputPanel, String.Format(Config.TextMessages.WarningTurretIsDamagedHealthLevel,
                    turrets[n].CustomName, health.ToString("F1")), true);
        }

        IMyInventory containerInventory = turrets[n].GetInventory(0);

        List<MyInventoryItem> items = new List<MyInventoryItem>();
        containerInventory.GetItems(items);

        if (items.Count == 0)
        {
            string message = String.Format(Config.TextMessages.DryNoAmmoContainers, turrets[n].CustomName);
            if (Config.AmmoManagement.RefillEnabled)
            {
                RefillAmmo(containerInventory, 0);
            }
            AppendPanelNewlineText(OutputPanel, message, true);
        }
        else
        {
            VRage.MyFixedPoint totalAmount = 0;
            for (int i = 0; i < items.Count; i++)
            {
                totalAmount += items[i].Amount;
            }
            if (totalAmount < Config.AmmoManagement.OptimalAmount)
            {
                string message = String.Format(Config.TextMessages.LowOnAmmoOfAmmoContainers,
                        turrets[n].CustomName, totalAmount, Config.AmmoManagement.OptimalAmount);
                if (Config.AmmoManagement.RefillEnabled)
                {
                    RefillAmmo(containerInventory, totalAmount);
                }
                AppendPanelNewlineText(OutputPanel, message, true);
            }
        }
    }

    if (TotalItemsReceived.Count > 0)
    {
        List<string> ammoReport = new List<string>();
        int grandTotal = 0;
        for (int n = 0; n < TotalItemsReceived.Count; n++)
        {
            if ((TotalItemsReceived[n].Turret != null) && (TotalItemsReceived[n].Turret.Owner != null))
            {
                ammoReport.Add(String.Format(Config.TextMessages.AmmoSpentByTurret,
                        ((IMyTerminalBlock)TotalItemsReceived[n].Turret.Owner).CustomName,
                        TotalItemsReceived[n].Amount));
                grandTotal += TotalItemsReceived[n].Amount;
            }
        }
        ammoReport.Add(String.Format(Config.TextMessages.AmmoSpentToday, grandTotal));
        ammoReport.Reverse();
        for (int n = 0; n < ammoReport.Count; n++)
        {
            AppendPanelNewlineText(OutputPanel, ammoReport[n], true);
        }
    }
    string elapsed = (DateTime.Now - _start).TotalMilliseconds.ToString("F5");
    Echo(elapsed);
    AppendPanelNewlineText(OutputPanel, "\nProcessed in " + elapsed + " ms", true);
}

void RefillAmmo(IMyInventory turretInventory, VRage.MyFixedPoint currentAmount)
{
    // InteriorTurret throws NotImplemented for UseConveyorSystem
    if (turretInventory.Owner is IMyLargeInteriorTurret)
        return;

    string ammoToSearch;
    bool useConveyorSystem = false;

    if (turretInventory.Owner is IMyLargeGatlingTurret)
    {
        ammoToSearch = "25x184";
        useConveyorSystem = ((IMyLargeGatlingTurret)turretInventory.Owner).UseConveyorSystem;
    }
    else if (turretInventory.Owner is IMyLargeMissileTurret)
    {
        ammoToSearch = "Missile200mm";
        useConveyorSystem = ((IMyLargeMissileTurret)turretInventory.Owner).UseConveyorSystem;
    }
    else
    {
        throw new Exception("Cant infer ammo type for" + ((IMyTerminalBlock)turretInventory.Owner).CustomName);
    }

    if (useConveyorSystem)
    {
        AppendPanelNewlineText(OutputPanel, String.Format(Config.TextMessages.ConveyerIsEnabled,
                    ((IMyTerminalBlock)turretInventory.Owner).CustomName), true);
    }

    IMyInventory feedInventory = this.AmmoFeedInventory.GetInventory(0);
    List<MyInventoryItem> items = new List<MyInventoryItem>();
    feedInventory.GetItems(items);

    for (int i = 0; i < items.Count; i++)
    {
        MyDefinitionId itemType = items[i].Type; // MyDefinitionId.FromContent((items[i].Content));
        string sn = itemType.SubtypeName;
        if (sn.Contains(ammoToSearch))
        {
            VRage.MyFixedPoint amountToSend = Config.AmmoManagement.OptimalAmount - currentAmount;
            if (amountToSend <= 0)
                return;
            bool result = feedInventory
                    .TransferItemTo(turretInventory, i, stackIfPossible: true, amount: amountToSend);
            if (result)
            {
                AppendPanelNewlineText(OutputPanel, String.Format(Config.TextMessages.SendingUnits,
                        amountToSend, ammoToSearch), true);
                // in reality can be less (returns true if sends 1 of 20 for example) but normally turret requests 1
                // ammo per tick
                AddReceivedCount(turretInventory, (int)amountToSend);
            }
            else
            {
                AppendPanelNewlineText(OutputPanel, String.Format(Config.TextMessages.FailedToSend,
                        amountToSend, ammoToSearch), true);
            }
            return;
        }
    }

    AppendPanelNewlineText(OutputPanel,
            String.Format(Config.TextMessages.FailedToFindAmmo,
                    ((IMyTerminalBlock)this.AmmoFeedInventory).CustomName)
                        , true);
}

float GetMyTerminalBlockHealth(IMyTerminalBlock block)
{
    IMySlimBlock slimblock = block.CubeGrid.GetCubeBlock(block.Position);
    return slimblock.BuildIntegrity / slimblock.MaxIntegrity;
}

private void AppendPanelNewlineText(IMyTextPanel panel, string text, bool wordWrap)
{
    /*   ///////////// 
    var otherPb = GridTerminalSystem.GetBlockWithName("Programmable block 2") as IMyProgrammableBlock;   
      Echo ( otherPb.TryRun(text).ToString() ) ;  
       return; 
   ///////////////*/
    var multicastPanels = new List<IMyTextPanel>();
    if (text.Contains("###"))
    {
        // notation: ###my-awesome-panel-name,another-multicast-panel?message 
        string pluginPanelName = text.Split('?')[0].Substring(3);
        string[] panelNames = pluginPanelName.Split(',');
        text = text.Substring(text.IndexOf('?') + 1);
        for (int i = 0; i < panelNames.Length; i++)
        {
            multicastPanels.Add(GetPluginPanel(panelNames[i]));
        }
    }
    if (text.Length > Config.TextFormatting.MaxCharsPerLine)
    {
        string[] linesToJoin = Wrap(text, Config.TextFormatting.MaxCharsPerLine).ToArray();
        text = String.Join("\n" + Config.TextFormatting.PaddingLeft, linesToJoin);
    }

    text = "\n" + Config.TextFormatting.PaddingLeft + text;
    if (multicastPanels.Count > 0)
    {
        for (int i = 0; i < multicastPanels.Count; i++)
        {
            multicastPanels[i].WritePublicText(text, true);
            RefreshWorkAroundSplint(multicastPanels[i]);
        }
    }
    else
    {
        panel.WritePublicText(text, true);
        RefreshWorkAroundSplint(panel);
    }
}

private void RefreshWorkAroundSplint(IMyTextPanel glitchedPanel)
{
    glitchedPanel.ShowPrivateTextOnScreen();
    glitchedPanel.ShowPublicTextOnScreen();
}

// string wrapping by Bryan Reynolds  
public static List<String> Wrap(string text, int maxLength)
{
    if (text.Length == 0) return new List<string>();
    var words = text.Split(' ');
    var lines = new List<string>();
    var currentLine = "";
    foreach (var currentWord in words)
    {
        if ((currentLine.Length > maxLength) || ((currentLine.Length + currentWord.Length) > maxLength))
        {
            lines.Add(currentLine);
            currentLine = "";
        }
        if (currentLine.Length > 0)
            currentLine += " " + currentWord;
        else
            currentLine += currentWord;
    }
    if (currentLine.Length > 0)
        lines.Add(currentLine);
    return lines;
}

private string TitleWithSpinner(string formatter, int spinningCharsCount)
{
    string[] frames = new string[] { "\\", "|", "/", "--" };
    int rnd = new Random().Next(4);
    string current = frames[rnd];

    StringBuilder sb = new StringBuilder();
    for (int i = 0; i < spinningCharsCount; i++)
    {
        sb.Append(current);
    }

    return String.Format(formatter, sb.ToString());
}
