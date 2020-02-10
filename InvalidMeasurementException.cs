namespace EstradaNic.Measurement
{
    public class InvalidMeasurementException : Exception
    {
        public InvalidMeasurementException() { }
        public InvalidMeasurementException(string msg) : base(msg) { }
    }

}