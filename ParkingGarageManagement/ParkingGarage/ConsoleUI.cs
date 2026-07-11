namespace ParkingGarage;

using Spectre.Console;


public class ConsoleUI {
    DataManager dataManager;

    public ConsoleUI() {
        dataManager = new DataManager();
    }

    public void Show() {

        Console.WriteLine("Welcome to the Parking Garage Management System");

        string command;
        do {

            command = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("What do you want to do?")
                                .AddChoices(new[] {
                                    "record entry","record exit","check availability","end"
                                }));

            if(command=="record entry") {
                Console.WriteLine("Available spots: "+dataManager.AvailableSpots());
                if(dataManager.IsFull()) {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("The garage is full! Entry is blocked.");
                    Console.ResetColor();
                } else {
                    var vehicleTag = AnsiConsole.Prompt(new TextPrompt<string>("Enter vehicle tag:"));
                    Vehicle vehicle = new Vehicle(vehicleTag, DateTime.Now);
                    dataManager.RecordEntry(vehicle);
                    Console.WriteLine("Vehicle "+vehicle.VehicleTag+" entered at "+vehicle.EntryTime);
                    Console.WriteLine("Available spots: "+dataManager.AvailableSpots());
                }

            } else if(command=="record exit") {

                if(dataManager.ParkedVehicles.Count==0) {
                    AnsiConsole.Clear();
                    Console.WriteLine("There are no vehicles currently parked.");
                } else {
                    AnsiConsole.Clear();
                    Vehicle selectedVehicle = AnsiConsole.Prompt(
                            new SelectionPrompt<Vehicle>()
                                .Title("Select a vehicle to exit")
                                .AddChoices(dataManager.ParkedVehicles));

                    DateTime exitTime = DateTime.Now;
                    decimal fee = FeeCalculator.CalculateFee(selectedVehicle, exitTime, dataManager.RateSchedule);
                    dataManager.RemoveVehicle(selectedVehicle);

                    Console.WriteLine("Vehicle "+selectedVehicle.VehicleTag+" exited at "+exitTime);
                    Console.WriteLine("Parking fee: $"+fee);
                    Console.WriteLine("Available spots: "+dataManager.AvailableSpots());
                }

            } else if(command=="check availability") {
                AnsiConsole.Clear();
                Console.WriteLine("Available spots: "+dataManager.AvailableSpots()+" / "+dataManager.Capacity);
            }


        } while(command!="end");
    }

    public static string AskForInput(string message) {
        Console.Write(message);
        return Console.ReadLine();
    }
}
