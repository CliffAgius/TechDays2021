namespace TechDays2021
{
    public class FlightDataModel
    {
        // ID of datapoint
        public int ID { get; set; }
        // FLight Callsign with be AAW which stands for Azure AirWays ;-)
        public string callSign { get; set; }
        // Date formatted as DD/MM/YYYY
        public string date { get; set; }
        // Tiem formatted as HH:mm:ss
        public string time { get; set; }
        // Flight Position Latitude formated to 4 decimal places.
        public double latitude { get; set; }
        // Flight Position Longitude formatted to 4 decimal places
        public double longitude { get; set; }
        // Aircraft Alititude in Feet
        public int altitude { get; set; }
        // Aircraft Vertical Speed in Feet Per Minute
        public int vSFPM { get; set; }
        // Number of Seconds before the next Datapoint (Used in this demo to time the data being sent to IoTHUB)
        public int secondsNextReport { get; set; }
        // Aircraft speed in Knots so Nautical Miles Per Hour. 1 Knot (KT) = 1.15 Miles Per Hour (MPH)
        public int speed { get; set; }
        // Aicraft flight direction in degree's (direction of travel over the ground)
        public int direction { get; set; }
        // Aircraft flight direction converted to a compass heading string (N,S,E,W etc)
        public string directionString { get; set; }
        // Outside Temperature at the Aircrafts Altitude.  Measured in Degrees Celsius.
        public object outSideAirTemp { get; set; }
        // Direction the wind is blowing from at the height of the Aircraft.
        public int windDirection { get; set; }
        // DIrection the wind is blowing converted to a compass heading string (N,S,E,W etc)
        public string windDirectionString { get; set; }
        // Speed of the wind at the height of the aircraft.
        public int windSpeed { get; set; }
    }
}
