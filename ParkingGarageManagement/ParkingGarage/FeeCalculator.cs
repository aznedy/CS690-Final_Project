namespace ParkingGarage;

public class FeeCalculator {
    public static decimal CalculateFee(Vehicle vehicle, DateTime exitTime, RateSchedule rateSchedule) {
        // FR-07: calculate the parking fee from the entry and exit timestamps
        TimeSpan duration = exitTime - vehicle.EntryTime;
        int billableHours = (int)Math.Ceiling(duration.TotalHours);

        if(billableHours < 0) {
            billableHours = 0;
        }

        // FR-08: apply the overtime rate when the allowed duration is exceeded
        if(billableHours <= rateSchedule.AllowedHours) {
            return billableHours * rateSchedule.BaseRatePerHour;
        } else {
            int overtimeHours = billableHours - rateSchedule.AllowedHours;
            decimal baseFee = rateSchedule.AllowedHours * rateSchedule.BaseRatePerHour;
            decimal overtimeFee = overtimeHours * rateSchedule.OvertimeRatePerHour;
            return baseFee + overtimeFee;
        }
    }
}
