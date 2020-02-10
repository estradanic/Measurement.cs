namespace EstradaNic.Measurement
{

    public class Measurement<TUnitType> : IComparable<string>, IComparable<Measurement> where TUnitType : UnitType
    {
        public static Measurement<None> Zero { get => new Measurement<None>(); }

        private double value;
        public double Value
        {
            get => value;
            private set 
            {
                if (double.IsNaN(value)) { throw new InvalidMeasurementException("Measurement cannot be NaN"); }
                if (double.IsNegativeInfinity(value)) { throw new InvalidMeasurementException("Measurement cannot be Negative Infinity"); }
                if (double.IsPositiveInfinity(value)) { throw new InvalidMeasurementException("Measurement cannot be Positive Infinity"); }

                this.value = value;
            }
        } 

        /// <summary>
        /// Unit type of this Measurement (eg. Length, Area, Weight, etc.)
        /// </summary>
        public TUnitType UnitType { get; private set; }

        /// <summary>
        /// Unit of this Measurement (eg. "lb", "m^2", "in", etc.) to be used in Serialize()
        /// </summary>
        public string Unit {get; private set;}

        /// <summary>
        /// Custom Measurement display unit to be used in ToString()
        /// </summary>
        public string CustomDisplayUnit { get; set; }

        /// <summary>
        /// Constructs new Measurement using TUnitType's default unit for the UnitSystem 
        /// </summary>
        public Measurement(UnitSystem unitSystem = UnitSystem.SI, double value = 0)
        {
            Value = value;
            UnitType = new TUnitType();
            Unit = UnitType.Default(unitSystem);
        }

        public Measurement(string unit, double value = 0)
        {
            Value = value;
            UnitType = new TUnitType();
            Unit = unit;
            CheckAndSimplifyUnits();
        }

        public Measurement(string unit, double? value) : this(unit, value.Value) { }

        /// <summary>
        /// Constructs new Measurement from a serialized Measurement string
        /// <summary>
        public Measurement(string measurement)
        {
            if(string.IsNullOrWhiteSpace(measurement)){ throw new InvalidMeasurementException("measurement must be a valid serialized Measurement"); }
            
            string[] parameters = measurement.Split(' ');
            if(parameters.Length < 2){ throw new InvalidMeasurementException("measurement must be a valid serialized Measurement"); }
        
            string unit = parameters[parameters.Length - 1];
            string value = "";

            for (int i = 0; i < parameters.Length - 1; i++)
            {
                if(i > 0 && Regex.IsMatch(parameters[i], "[^0-9/. -]"))
                {
                    units = parameters[i] + "*" + units;
                }
                else
                {
                    value += parameters[i] + " ";
                }
            }

            units = ParseUnitFromDisplayFormatting(units);
            Value = ParseValueFromString(value.TrimEnd(' '));
            Units = units;
            UnitType = new TUnitType();
            CheckAndSimplifyUnits();
        }

        public Measurement(string value, string units) : this(ParseValueFromString(value), units) { }

        /// <summary>
        /// Parses a string into a double, allowing for decimals, fractions, and mixed numbers (eg. "6 1/5", "95/10", "9.623", etc.)
        /// </summary>
        private static double ParseValueFromString(string valueString)
        {
            valueString = Regex.Replace((valueString ?? ""), "[^0-9/. -]", "").Trim().TrimEnd('/');

            if(string.IsNullOrWhiteSpace(valueString)){ throw new InvalidMeasurementException("value must not be null or whitespace"); }
        
            // if valueString is a standard decimal number (eg. "1.125")
            if(!valueString.Contains('/'))
            {
                return double.Parse(valueString);
            }

            // valueString is either a fraction or a mixed number (eg. "9/16", "6 7/8", etc.)
            string[] parts = valueString.Split(new char[] {' ', '/'}, StringSplitOptions.RemoveEmptyEntries);

            // valueString is a fraction (eg. "8/5", "9.25/1000", etc.)
            if(!valueString.Contains(' ') && parts.Length == 2)
            {
                string numerator = parts[0];
                string denominator = parts[1];
                return double.Parse(numerator) / double.Parse(denominator);
            }

            // valueString is a mixed number (eg. "9 12/25", "12.9 1/10", etc.)
            if (parts.Length == 3)
            {
                string whole = parts[0];
                string numerator = parts[1];
                string denominator = parts[2];

                return double.Parse(whole) + double.Parse(numerator) / double.Parse(denominator);
            }

            // unable to parse
            return double.NaN;
        }

        /// <summary>
        /// Parses processable unit string from display unit string
        /// </summary>
        private static string ParseUnitFromDisplayFormatting(string displayUnit)
        {
            string unit = Regex.Replace(unit, "[⁻⁰¹²³⁴⁵⁶⁷⁸⁹]+", @"^$&");
            for(int i = 0; i < unit.Length; i++)
            {
                char character = unit[i];
                char replacement =
                    character == '⁻' ? '-' :
                    character == '⁰' ? '0' :
                    character == '¹' ? '1' :
                    character == '²' ? '2' :
                    character == '³' ? '3' :
                    character == '⁴' ? '4' :
                    character == '⁵' ? '5' :
                    character == '⁶' ? '6' :
                    character == '⁷' ? '7' :
                    character == '⁸' ? '8' :
                    character == '⁹' ? '9' :
                    character;
                unit = unit.ReplaceAt(i, replacement);
            }
            return unit;
        }

        private void CheckAndSimplifyUnits()
        {

        }

        public static Measurement<T> operator +(Measurement<T> addend1, Measurement<T> addend2)
        {
            addend2 = addend2.ConvertTo<T>(addend1);
            return new Measurement<T>(addend1.Unit, addend1.Value + addend2.Value);
        }

        public static Measurement<T> operator +(Measurement<T> addend1, string addend2)
        {
            return addend1 + new Measurement(addend2).ConsolidateUnitType<T>();
        }

        public static Measurement<T> operator -(Measurement<T> minuend, Measurement<T> subtrahend)
        {
            subtrahend = subtrahend.ConvertTo<T>(minuend);
            return new Measurement<T>(minuend.Unit, minuend.Value - subtrahend.Value);
        }

        public static Measurement<T> operator -(Measurement<T> minuend, string subtrahend)
        {
            return minuend - new Measurement(subtrahend).ConsolidateUnitType<T>();
        }


    }

    public class Measurement : Measurement<Complex>
    {
        public Measurement<T> ConsolidateUnitType<T>()
        {

        }
    }
}