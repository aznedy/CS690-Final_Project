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
                                    "Record Entry","Record Exit","Spot Status","Daily Summary","Manage Subscribers","End"
                                }));

            if(command=="Record Entry") {
                RecordEntry();
            } else if(command=="Record Exit") {
                RecordExit();
            } else if(command=="Spot Status") {
                ShowSpotStatus();
            } else if(command=="Daily Summary") {
                ShowDailySummary();
            } else if(command=="Manage Subscribers") {
                ManageSubscribers();
            }

        } while(command!="End");

        AnsiConsole.MarkupLine("[grey]Goodbye![/]");
    }

    void ShowBanner() {
        AnsiConsole.Write(new FigletText("Parking Garage").LeftJustified().Color(Color.Aqua));
        AnsiConsole.Write(new Markup("[grey italic]Management...[/]"));
        AnsiConsole.WriteLine();
    }

    // -------- Record Entry ---------
    // It may be worthwhile to add the vehicleTag state 
    // in the case that two matching license plates from different states exist. 
    void RecordEntry() {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[green]Record Entry[/]").LeftJustified().RuleStyle("green"));

        // TODO: present an option to select from subscriber plates, instead of manual entry. 
        // The potential to incorrectly enter the vehicleTag could cause data integrity issues.
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

        // Reminder for operator for when the driver was placed in a reserved overflow spot
        var overflowNote = dataManager.IsReserved(spot!)
            ? "\n[yellow]Note: general area full - assigned a reserved overflow spot.[/]"
            : "";

        AnsiConsole.Write(new Panel(new Markup(
                $"Vehicle [aqua]{Markup.Escape(vehicleTag)}[/] entered.\n" +
                $"Assigned spot: [bold green]{spot}[/]\n" +
                $"Entry time: {Pretty(session.EntryTime)}\n" +
                $"Available spots: [green]{dataManager.AvailableSpots()}[/] / {dataManager.GeneralSpots().Count}" +
                overflowNote))
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
        var breakdown = FeeCalculator.CalculateBreakdown(selectedSession.EntryDateTime(), exitTime, dataManager.RateSchedule);
        // Record the payment, do not release spot until full fee satisfied.
        dataManager.SetAmountCharged(selectedSession, breakdown.Total);

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        table.AddColumn("Item");
        table.AddColumn(new TableColumn("Detail").RightAligned());
        table.AddColumn(new TableColumn("Amount").RightAligned());
        table.AddRow("Vehicle", Markup.Escape(selectedSession.VehicleTag), "");
        table.AddRow("Spot", selectedSession.AssignedSpace, "");
        table.AddRow("Entry", Pretty(selectedSession.EntryTime), "");
        table.AddRow("Exit", Pretty(exitTime.ToString("yyyyMMddHHmmss")), "");
        table.AddRow("Duration", $"{(int)breakdown.Duration.TotalHours}h {breakdown.Duration.Minutes}m", "");
        table.AddRow($"Base ({breakdown.BaseHours}h @ ${breakdown.BaseRatePerHour}/h)", "", $"${breakdown.BaseFee}");
        if(breakdown.OvertimeHours > 0) {
            table.AddRow($"[yellow]Overtime ({breakdown.OvertimeHours}h @ ${breakdown.OvertimeRatePerHour}/h)[/]", "", $"[yellow]${breakdown.OvertimeFee}[/]");
        }
        table.AddRow("[bold]Total due[/]", "", $"[bold green]${breakdown.Total}[/]");
        AnsiConsole.Write(table);

        // Collect payment loop
        CollectPayment(selectedSession);

        // The balance is cleared when all payment sum to total, free spot
        dataManager.RecordExit(selectedSession, exitTime, breakdown.Total);

        ShowReceipt(selectedSession);

        AnsiConsole.MarkupLine($"Available spots: [green]{dataManager.AvailableSpots()}[/] / {dataManager.GeneralSpots().Count}");
        Pause();
    }

    void CollectPayment(ParkingSession session) {
        while(session.BalanceDue() > 0m) {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Balance due: [bold yellow]${session.BalanceDue():0.00}[/]");

            var method = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Payment [green]method[/]")
                        .HighlightStyle(new Style(foreground: Color.Green))
                        .AddChoices(new[] { "Cash", "Card" }));

            decimal balance = session.BalanceDue();
            decimal tendered = AnsiConsole.Prompt(
                    new TextPrompt<decimal>($"Enter [green]{method}[/] amount:")
                        .PromptStyle("aqua")
                        .Validate(amount => amount > 0m
                                    ? ValidationResult.Success()
                                    : ValidationResult.Error("[red]Amount must be greater than zero.[/]")));

            if(method == "Cash" && tendered > balance) {
                // Overpayment: credit only the balance, return the rest as change.
                decimal change = tendered - balance;
                dataManager.AddPayment(session, balance, method);
                AnsiConsole.MarkupLine($"[green]Paid ${balance:0.00} cash.[/] Change due: [bold]${change:0.00}[/]");
            } else {
                // Card is charged exactly; cash at/under balance is applied as-is.
                decimal applied = method == "Card" ? Math.Min(tendered, balance) : tendered;
                dataManager.AddPayment(session, applied, method);
                if(session.BalanceDue() > 0m) {
                    AnsiConsole.MarkupLine($"[green]Paid ${applied:0.00} {method.ToLower()}.[/] Remaining: [yellow]${session.BalanceDue():0.00}[/]");
                } else {
                    AnsiConsole.MarkupLine($"[green]Paid ${applied:0.00} {method.ToLower()}.[/]");
                }
            }
        }
        AnsiConsole.MarkupLine("[bold green]Payment complete.[/]");
    }

    // FR-12: show the itemized payment log for the session.
    void ShowReceipt(ParkingSession session) {
        var receipt = new Table().Border(TableBorder.Rounded).BorderColor(Color.Green);
        receipt.AddColumn("Payment");
        receipt.AddColumn(new TableColumn("Method").Centered());
        receipt.AddColumn(new TableColumn("Time").RightAligned());
        receipt.AddColumn(new TableColumn("Amount").RightAligned());
        int index = 1;
        foreach(var payment in session.Payments) {
            receipt.AddRow($"#{index}", Markup.Escape(payment.Method), Pretty(payment.Timestamp), $"${payment.Amount:0.00}");
            index++;
        }
        receipt.AddRow("[bold]Total paid[/]", "", "", $"[bold green]${session.TotalPaid():0.00}[/]");
        AnsiConsole.Write(new Panel(receipt).Header("[green]Payment Receipt[/]").BorderColor(Color.Green));
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

    // A subscribed reserved spot always shows [R]. An unsubscribed reserved spot
    // shows [U] when empty, but [O] once an ad hoc guest is parked there via the
    string RenderSpot(string spot) {
        if(dataManager.IsReserved(spot)) {
            if(dataManager.ActiveSubscriberForSpot(spot) != null) {
                return $"[blue]{spot} [[R]][/]";
            }
            if(dataManager.IsSpotOccupied(spot)) {
                return $"[red]{spot} [[O]][/]";
            }
            return $"[purple]{spot} [[U]][/]";
        }
        if(dataManager.IsSpotOccupied(spot)) {
            return $"[red]{spot} [[O]][/]";
        }
        return $"[green]{spot} [[A]][/]";
    }

    // ---------------- Daily Activity Summary ----------------

    void ShowDailySummary() {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[aqua]Daily Activity Summary[/]").LeftJustified().RuleStyle("aqua"));

        // Offer the last 7 calendar days (today back through 6 days ago).
        var days = new List<DateTime>();
        for(int i = 0; i < 7; i++) {
            days.Add(DateTime.Today.AddDays(-i));
        }

        var selectedDay = AnsiConsole.Prompt(
                new SelectionPrompt<DateTime>()
                    .Title("Select a [aqua]day[/] to report")
                    .HighlightStyle(new Style(foreground: Color.Aqua))
                    .UseConverter(day => day == DateTime.Today
                        ? $"{day:dddd, MMM dd yyyy} (today)"
                        : day.ToString("dddd, MMM dd yyyy"))
                    .AddChoices(days));

        // A day spans 00:00:00 through 23:59:59 of the selected date.
        int entries = dataManager.Sessions.Count(s => s.EntryDateTime().Date == selectedDay.Date);
        int exits = dataManager.Sessions.Count(s => s.ExitDateTime()?.Date == selectedDay.Date);
        decimal revenue = dataManager.PaymentsOn(selectedDay).Sum(p => p.Amount);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Markup(
                $"Date: [bold]{selectedDay:dddd, MMMM dd, yyyy}[/]\n" +
                $"Total entries: [green]{entries}[/]\n" +
                $"Total exits: [yellow]{exits}[/]\n" +
                $"Revenue collected: [bold green]${revenue:0.00}[/]"))
            .Header("[aqua]Summary[/]").BorderColor(Color.Aqua));

        // Entries per hour, 12 AM through 11 PM.
        var perHour = new int[24];
        foreach(var session in dataManager.Sessions) {
            var entry = session.EntryDateTime();
            if(entry.Date == selectedDay.Date) {
                perHour[entry.Hour]++;
            }
        }

        AnsiConsole.WriteLine();
        var amTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        amTable.AddColumn("Hour");
        amTable.AddColumn(new TableColumn("Entries").RightAligned());
        amTable.AddColumn("");
        var pmTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
        pmTable.AddColumn("Hour");
        pmTable.AddColumn(new TableColumn("Entries").RightAligned());
        pmTable.AddColumn("");
        for(int h = 0; h < 12; h++) {
            string amLabel = new DateTime(2000, 1, 1, h, 0, 0).ToString("h tt");
            string amBar = perHour[h] > 0 ? new string('█', perHour[h]) : "";
            amTable.AddRow(amLabel, perHour[h].ToString(), $"[aqua]{amBar}[/]");

            int pmHour = h + 12;
            string pmLabel = new DateTime(2000, 1, 1, pmHour, 0, 0).ToString("h tt");
            string pmBar = perHour[pmHour] > 0 ? new string('█', perHour[pmHour]) : "";
            pmTable.AddRow(pmLabel, perHour[pmHour].ToString(), $"[aqua]{pmBar}[/]");
        }
        AnsiConsole.Write(new Panel(new Columns(amTable, pmTable).Collapse()).Header("[aqua]Entries per Hour[/]").BorderColor(Color.Aqua));

        Pause();
    }

    // ---------------- Manage Subscribers ----------------

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
