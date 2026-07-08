namespace ParkingGarage;


public class DataManager {

    FileSaver fileSaver;

    public int Capacity { get; }
    public RateSchedule RateSchedule { get; }
    public List<Vehicle> ParkedVehicles { get; }

    public DataManager() {

        fileSaver = new FileSaver("parked-vehicles.txt");

        ParkedVehicles = new List<Vehicle>();

        Capacity = 50;
        decimal baseRatePerHour = 5.0m;
        decimal overtimeRatePerHour = 6.0m;
        int allowedHours = 4;

        RateSchedule = new RateSchedule(baseRatePerHour, overtimeRatePerHour, allowedHours);

        if(File.Exists("parked-vehicles.txt")) {
            var parkedFileContent = File.ReadAllLines("parked-vehicles.txt");
            foreach(var line in parkedFileContent) {
                if(string.IsNullOrWhiteSpace(line)) {
                    continue;
                }
                var splitted = line.Split(":",StringSplitOptions.RemoveEmptyEntries);
                var vehicleTag = splitted[0];
                var entryTime = DateTime.ParseExact(splitted[1], "yyyyMMddHHmmss", null);

                ParkedVehicles.Add(new Vehicle(vehicleTag, entryTime));
            }
        }
    }

    public int AvailableSpots() {
        return Capacity - ParkedVehicles.Count;
    }

    public bool IsFull() {
        return ParkedVehicles.Count >= Capacity;
    }

    public void RecordEntry(Vehicle vehicle) {
        this.ParkedVehicles.Add(vehicle);
        this.fileSaver.AppendData(vehicle);
    }

    public Vehicle? FindVehicle(string vehicleTag) {
        foreach(var vehicle in ParkedVehicles) {
            if(vehicle.VehicleTag == vehicleTag) {
                return vehicle;
            }
        }
        return null;
    }

    public void SynchronizeParkedVehicles() {
        File.Delete("parked-vehicles.txt");
        foreach(var vehicle in ParkedVehicles) {
            File.AppendAllText("parked-vehicles.txt", vehicle.VehicleTag + ":" + vehicle.EntryTime.ToString("yyyyMMddHHmmss") + Environment.NewLine);
        }
    }

    public void RemoveVehicle(Vehicle vehicle) {
        ParkedVehicles.Remove(vehicle);
        SynchronizeParkedVehicles();
    }
}
