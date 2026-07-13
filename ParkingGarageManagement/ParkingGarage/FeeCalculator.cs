namespace ParkingGarage;

public class FeeCalculator {

    //TODO: Add a 15 minute grace period, in case the customer entered the garage in error. 
    public static decimal CalculateFee(DateTime entryTime, DateTime exitTime, RateSchedule rateSchedule) {
        // FR-07: calculate the parking fee from the entry and exit timestamps
        TimeSpan duration = exitTime - entryTime;
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
