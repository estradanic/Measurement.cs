namespace EstradaNic.Measurement
{

    internal abstract class UnitType
    {
        public UnitType();

        public List<string> ValidUnits { get; protected set; }
        
        public bool CanConvert<T>() where T : UnitType;

        public double GetUnitMultiplier<T>(string unit) where T : UnitType;

        public double GetUnitMultiplier(string unit, Type type);

        public List<Dictionary<UnitType, double>> Constructions { get; protected set; }

        public static Type TypeOf()

    }

}