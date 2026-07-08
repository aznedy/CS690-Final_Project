namespace ParkingGarage;

using System.IO;

public class FileSaver {
    string fileName;

    public FileSaver(string fileName) {
        this.fileName = fileName;
        if(!File.Exists(this.fileName)) {
            File.Create(this.fileName).Close();
        }
    }

    public void AppendLine(string line) {
        File.AppendAllText(this.fileName, line + Environment.NewLine);
    }

    public void AppendData(Vehicle data) {
        File.AppendAllText(this.fileName, data.VehicleTag + ":" + data.EntryTime.ToString("yyyyMMddHHmmss") + Environment.NewLine);
    }
}
