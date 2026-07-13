namespace ParkingGarage;

using System.Text.RegularExpressions;

public class VehicleTagValidator {
    // 3-9 characters, letters and digits only (no spaces or punctuation)
    static readonly Regex Pattern = new Regex("^[A-Za-z0-9]{3,9}$");

    public static bool IsValid(string tag) {
        return !string.IsNullOrEmpty(tag) && Pattern.IsMatch(tag);
    }
}
