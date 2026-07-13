namespace ParkingGarage;

using Spectre.Console;


public class ConsoleUI {
    DataManager dataManager;

    public ConsoleUI() {
        dataManager = new DataManager();
    }

    public void Show() {

        string command;
        do {
            AnsiConsole.Clear();
            ShowBanner();

            command = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("[bold]What do you want to do?[/]")
                                .HighlightStyle(new Style(foreground: Color.Yellow))
                                .AddChoices(new[] {
                                    "record entry","record exit","spot status","manage subscribers","end"
                                }));

            if(command=="record entry") {
                RecordEntry();
            } else if(command=="record exit") {
                RecordExit();
            } else if(command=="spot status") {
                ShowSpotStatus();
            } else if(command=="manage subscribers") {
                ManageSubscribers();
            }

        } while(command!="end");

        AnsiConsole.MarkupLine("[grey]Goodbye![/]");
    }

    void ShowBanner() {
        AnsiConsole.Write(new FigletText("Parking Garage").LeftJustified().Color(Color.Aqua));
        AnsiConsole.Write(new Markup("[grey italic]Management...[/]"));
        AnsiConsole.WriteLine();
    }

    // -------- Record Entry ---------

    void RecordEntry() {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[green]Record Entry[/]").LeftJustified().RuleStyle("green"));

        var vehicleTag = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter [green]vehicle tag[/]:")
                    .PromptStyle("aqua")
                    .Validate(tag => VehicleTagValidator.IsValid(tag),
                              "[red]Tag must be 3-9 letters or digits, no spaces or punctuation.[/]"));

        // A subscriber already has a reserved spot in subscribers.json, so no
        // spot is assigned to them. Only ad hoc guests get a general spot.
        var subscriber = dataManager.FindSubscriberByTag(vehicleTag);
        if(subscriber != null && subscriber.IsActive()) {
            AnsiConsole.Write(new Panel(new Markup(
                    $"[blue]Welcome back, {Markup.Escape(subscriber.Name)}![/]\n" +
                    $"Your reserved spot is [bold]{Markup.Escape(subscriber.AssignedSpot)}[/]. No ad hoc spot is assigned."))
                .Header("[blue]Subscriber[/]").BorderColor(Color.Blue));
            Pause();
            return;
        }

        if(dataManager.IsFull()) {
            AnsiConsole.Write(new Panel(new Markup("[red]The garage is full! Entry is blocked.[/]"))
                .Header("[red]Full[/]").BorderColor(Color.Red));
            Pause();
            return;
        }

        var spot = dataManager.FindAvailableSpot();
        var session = dataManager.RecordEntry(vehicleTag, spot!);

        AnsiConsole.Write(new Panel(new Markup(
                $"Vehicle [aqua]{Markup.Escape(vehicleTag)}[/] entered.\n" +
                $"Assigned spot: [bold green]{spot}[/]\n" +
                $"Entry time: {Pretty(session.EntryTime)}\n" +
                $"Available spots: [green]{dataManager.AvailableSpots()}[/] / {dataManager.GeneralSpots().Count}"))
            .Header("[green]Entry Recorded[/]").BorderColor(Color.Green));
        Pause();
    }

    // --------Record Exit -------

    void RecordExit() {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[yellow]Record Exit[/]").LeftJustified().RuleStyle("yellow"));

        var active = dataManager.ActiveSessions();
        if(active.Count==0) {
            AnsiConsole.MarkupLine("[grey]There are no vehicles currently parked.[/]");
            Pause();
            return;
        }

        var selectedSession = AnsiConsole.Prompt(
                new SelectionPrompt<ParkingSession>()
                    .Title("Select a vehicle to [yellow]exit[/]")
                    .HighlightStyle(new Style(foreground: Color.Yellow))
                    .UseConverter(session => Markup.Escape($"{session.VehicleTag}  (spot {session.AssignedSpace}, in {Pretty(session.EntryTime)})"))
                    .AddChoices(active));

        DateTime exitTime = DateTime.Now;
        decimal fee = FeeCalculator.CalculateFee(selectedSession.EntryDateTime(), exitTime, dataManager.RateSchedule);
        dataManager.RecordExit(selectedSession, exitTime, fee);

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.AddColumn("Item");
        table.AddColumn(new TableColumn("Detail").RightAligned());
        table.AddRow("Vehicle", Markup.Escape(selectedSession.VehicleTag));
        table.AddRow("Spot", selectedSession.AssignedSpace);
        table.AddRow("Entry", Pretty(selectedSession.EntryTime));
        table.AddRow("Exit", Pretty(selectedSession.ExitTime!));
        table.AddRow("[bold]Fee due[/]", $"[green]${fee}[/]");
        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine($"Available spots: [green]{dataManager.AvailableSpots()}[/] / {dataManager.GeneralSpots().Count}");
        Pause();
    }

    // ------------- Show Spot Status Grid -------------

    void ShowSpotStatus() {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[aqua]Spot Status[/]").LeftJustified().RuleStyle("aqua"));

        int columns = 10;
        var grid = new Grid();
        for(int c=0;c<columns;c++) {
            grid.AddColumn();
        }

        var cells = new List<string>();
        foreach(var spot in dataManager.AllSpots()) {
            cells.Add(RenderSpot(spot));
        }

        for(int i=0;i<cells.Count;i+=columns) {
            var row = new string[columns];
            for(int j=0;j<columns;j++) {
                row[j] = (i+j) < cells.Count ? cells[i+j] : "";
            }
            grid.AddRow(row);
        }
        AnsiConsole.Write(grid);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green][[A]] Available[/]    [red][[O]] Occupied[/]    [blue][[R]] Reserved, Subscribed[/]    [purple][[U]] Reserved, Unassigned[/]");
        Pause();
    }

    // Reserved designation wins: a reserved spot always shows [R]/[U] from subcribers.json
    // v3 will include assignment of U spots, changing to [O], temporarily.
    string RenderSpot(string spot) {
        if(dataManager.IsReserved(spot)) {
            if(dataManager.ActiveSubscriberForSpot(spot) != null) {
                return $"[blue]{spot} [[R]][/]";
            }
            return $"[purple]{spot} [[U]][/]";
        }
        if(dataManager.IsSpotOccupied(spot)) {
            return $"[red]{spot} [[O]][/]";
        }
        return $"[green]{spot} [[A]][/]";
    }

    // ---------------- manage subscribers ----------------

    void ManageSubscribers() {
        string action;
        do {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[fuchsia]Manage Subscribers[/]").LeftJustified().RuleStyle("fuchsia"));
            ShowSubscriberTable();

            action = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("Subscriber action")
                                .HighlightStyle(new Style(foreground: Color.Fuchsia))
                                .AddChoices(new[] {
                                    "add subscriber","modify subscriber","remove subscriber","back"
                                }));

            if(action=="add subscriber") {
                AddSubscriber();
            } else if(action=="modify subscriber") {
                ModifySubscriber();
            } else if(action=="remove subscriber") {
                RemoveSubscriber();
            }

        } while(action!="back");
    }

    void ShowSubscriberTable() {
        if(dataManager.Subscribers.Count==0) {
            AnsiConsole.MarkupLine("[grey]No subscribers yet.[/]");
            return;
        }
        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Fuchsia);
        table.AddColumn("Name");
        table.AddColumn("Spot");
        table.AddColumn("Vehicle Tag");
        table.AddColumn("Expiry");
        table.AddColumn("Active");
        foreach(var subscriber in dataManager.Subscribers) {
            var active = subscriber.IsActive() ? "[green]yes[/]" : "[red]no[/]";
            table.AddRow(Markup.Escape(subscriber.Name), Markup.Escape(subscriber.AssignedSpot),
                         Markup.Escape(subscriber.VehicleTag), Markup.Escape(subscriber.SubscriptionExpiry), active);
        }
        AnsiConsole.Write(table);
    }

    void AddSubscriber() {
        var subscriber = new Subscriber();
        if(EditSubscriberForm(subscriber)) {
            dataManager.AddSubscriber(subscriber);
            AnsiConsole.MarkupLine("[green]Subscriber added.[/]");
            Pause();
        }
    }

    void ModifySubscriber() {
        if(dataManager.Subscribers.Count==0) {
            AnsiConsole.MarkupLine("[grey]No subscribers to modify.[/]");
            Pause();
            return;
        }
        var original = AnsiConsole.Prompt(
                new SelectionPrompt<Subscriber>()
                    .Title("Select a subscriber to [yellow]modify[/]")
                    .UseConverter(s => Markup.Escape($"{s.Name}  (spot {s.AssignedSpot}, tag {s.VehicleTag})"))
                    .AddChoices(dataManager.Subscribers));

        // edit a working copy so a cancel leaves the original intact
        var working = new Subscriber {
            Name = original.Name,
            AssignedSpot = original.AssignedSpot,
            VehicleTag = original.VehicleTag,
            SubscriptionExpiry = original.SubscriptionExpiry
        };

        if(EditSubscriberForm(working)) {
            original.Name = working.Name;
            original.AssignedSpot = working.AssignedSpot;
            original.VehicleTag = working.VehicleTag;
            original.SubscriptionExpiry = working.SubscriptionExpiry;
            dataManager.SaveSubscribers();
            AnsiConsole.MarkupLine("[green]Subscriber updated.[/]");
            Pause();
        }
    }

    void RemoveSubscriber() {
        if(dataManager.Subscribers.Count==0) {
            AnsiConsole.MarkupLine("[grey]No subscribers to remove.[/]");
            Pause();
            return;
        }
        var subscriber = AnsiConsole.Prompt(
                new SelectionPrompt<Subscriber>()
                    .Title("Select a subscriber to [red]remove[/]")
                    .UseConverter(s => Markup.Escape($"{s.Name}  (spot {s.AssignedSpot}, tag {s.VehicleTag})"))
                    .AddChoices(dataManager.Subscribers));

        if(AnsiConsole.Confirm($"Remove [red]{Markup.Escape(subscriber.Name)}[/]?", false)) {
            dataManager.RemoveSubscriber(subscriber);
            AnsiConsole.MarkupLine("[green]Subscriber removed.[/]");
        }
        Pause();
    }

    //Edit Subscriber Menu
    bool EditSubscriberForm(Subscriber subscriber) {
        while(true) {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[fuchsia]Subscriber Details[/]").LeftJustified().RuleStyle("fuchsia"));

            var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a field to edit, then choose [green]Save[/]")
                        .HighlightStyle(new Style(foreground: Color.Fuchsia))
                        .UseConverter(x => Markup.Escape(x))
                        .AddChoices(new[] {
                            $"Name: {subscriber.Name}",
                            $"Assigned Spot: {subscriber.AssignedSpot}",
                            $"Vehicle Tag: {subscriber.VehicleTag}",
                            $"Expiry (yyyyMMdd): {subscriber.SubscriptionExpiry}",
                            "Save",
                            "Cancel"
                        }));

            if(choice.StartsWith("Name:")) {
                subscriber.Name = AnsiConsole.Prompt(new TextPrompt<string>("Name:").DefaultValue(subscriber.Name).AllowEmpty());
            } else if(choice.StartsWith("Assigned Spot:")) {
                subscriber.AssignedSpot = AnsiConsole.Prompt(new TextPrompt<string>("Assigned Spot:").DefaultValue(subscriber.AssignedSpot).AllowEmpty());
            } else if(choice.StartsWith("Vehicle Tag:")) {
                subscriber.VehicleTag = AnsiConsole.Prompt(
                    new TextPrompt<string>("Vehicle Tag:")
                        .DefaultValue(subscriber.VehicleTag)
                        .AllowEmpty()
                        .Validate(tag => tag == "" || VehicleTagValidator.IsValid(tag),
                                  "[red]Tag must be 3-9 letters or digits, no spaces or punctuation.[/]"));
            } else if(choice.StartsWith("Expiry")) {
                subscriber.SubscriptionExpiry = AnsiConsole.Prompt(new TextPrompt<string>("Expiry (yyyyMMdd):").DefaultValue(subscriber.SubscriptionExpiry).AllowEmpty());
            } else if(choice=="Save") {
                if(string.IsNullOrWhiteSpace(subscriber.Name)
                    || string.IsNullOrWhiteSpace(subscriber.VehicleTag)
                    || string.IsNullOrWhiteSpace(subscriber.AssignedSpot)) {
                    AnsiConsole.MarkupLine("[red]Name, Vehicle Tag and Assigned Spot are required.[/]");
                    Pause();
                    continue;
                }
                return true;
            } else if(choice=="Cancel") {
                return false;
            }
        }
    }

    // ---------------- helpers ----------------

    static string Pretty(string stamp) {
        return DateTime.ParseExact(stamp, "yyyyMMddHHmmss", null).ToString("MM-dd-yyyy HH:mm:ss");
    }

    void Pause() {
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    public static string AskForInput(string message) {
        Console.Write(message);
        return Console.ReadLine();
    }
}
