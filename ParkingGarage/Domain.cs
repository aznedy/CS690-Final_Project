namespace ParkingGarage;

public class Vehicle {
    public string VehicleTag { get; }
    public DateTime EntryTime { get; }

    public Vehicle(string vehicleTag, DateTime entryTime) {
        this.VehicleTag = vehicleTag;
        this.EntryTime = entryTime;
    }

    public override string ToString() {
        return this.VehicleTag;
    }
}

public class RateSchedule {
    public decimal BaseRatePerHour { get; }
    public decimal OvertimeRatePerHour { get; }
    public int AllowedHours { get; }

    public RateSchedule(decimal baseRatePerHour, decimal overtimeRatePerHour, int allowedHours) {
        this.BaseRatePerHour = baseRatePerHour;
        this.OvertimeRatePerHour = overtimeRatePerHour;
        this.AllowedHours = allowedHours;
    }
}
