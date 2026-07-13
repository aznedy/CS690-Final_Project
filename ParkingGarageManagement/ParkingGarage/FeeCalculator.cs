namespace ParkingGarage;

public class FeeCalculator {

    //TODO: Add a 15 minute grace period, in case the customer entered the garage in error. 
        public static FeeBreakdown CalculateBreakdown(DateTime entryTime, DateTime exitTime, RateSchedule rateSchedule)
    {
        TimeSpan duration = exitTime - entryTime;
        int billableHours = (int)Math.Ceiling(duration.TotalHours);
        if(billableHours < 0) {billableHours = 0; }

        int baseHours = Math.Min(billableHours, rateSchedule.AllowedHours); 
        int overtimeHours = billableHours - baseHours;
        decimal baseFee = baseHours * rateSchedule.BaseRatePerHour; 
        decimal overtimeFee = overtimeHours * rateSchedule.OvertimeRatePerHour;

        return new FeeBreakdown{
            Duration = duration, 
            BillableHours = billableHours, 
            BaseHours = baseHours, 
            BaseRatePerHour = rateSchedule.BaseRatePerHour, 
            BaseFee = baseFee,
            OvertimeHours = overtimeHours, 
            OvertimeRatePerHour = rateSchedule.OvertimeRatePerHour, 
            OvertimeFee = overtimeFee,
            Total = baseFee + overtimeFee
        };
    }

    public static decimal CalculateFee(DateTime entryTime, DateTime exitTime, RateSchedule rateSchedule) {
    return CalculateBreakdown(entryTime, exitTime, rateSchedule).Total;
    }   
}

