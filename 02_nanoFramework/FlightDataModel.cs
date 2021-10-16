using System;
using System.Collections.Generic;
using System.Text;

namespace TechDays2021
{
    public class FlightDataModel
    {
        // ID of datapoint
        public int ID { get; set; }
        // FLight Callsign with be AAW which stands for Azure AirWays ;-)
        public string CallSign { get; set; }
        // Date formatted as DD/MM/YYYY
        public string Date { get; set; }
        // Tiem formatted as HH:mm:ss
        public string Time { get; set; }
        // Flight Position Latitude formated to 4 decimal places.
        public double Latitude { get; set; }
        // Flight Position Longitude formatted to 4 decimal places
        public double Longitude { get; set; }
        // Aircraft Alititude in Feet
        public int Altitude { get; set; }
        // Aircraft Vertical Speed in Feet Per Minute
        public int VSFPM { get; set; }
        // Number of Seconds before the next Datapoint (Used in this demo to time the data being sent to IoTHUB)
        public int SecondsNextReport { get; set; }
        // Aircraft speed in Knots so Nautical Miles Per Hour. 1 Knot (KT) = 1.15 Miles Per Hour (MPH)
        public int Speed { get; set; }
        // Aicraft flight direction in degree's (direction of travel over the ground)
        public int Direction { get; set; }
        // Outside Temperature at the Aircrafts Altitude.  Measured in Degrees Celsius.
        public double OutSideAirTemp { get; set; }
        // Direction the wind is blowing from at the height of the Aircraft.
        public int WindDirection { get; set; }
        // Speed of the wind at the height of the aircraft.
        public int WindSpeed { get; set; }
    }
}
