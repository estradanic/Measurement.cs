using CCM_Util.JsonConverters;
using CCM_Util.ModelBinders;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;


namespace CCM_Util
{
    public class InvalidMeasurementException : Exception
    {
        public InvalidMeasurementException() { }
        public InvalidMeasurementException(string msg) : base(msg) { }
    }

    [JsonConverter(converterType: typeof(MeasurementJsonConverter))]
    [ModelBinder(BinderType = typeof(MeasurementModelBinder))]
    public class Measurement : IComparable, IComparable<Measurement>
    {
        public static Measurement Zero { get => new Measurement(0, "ZERO_NO_UNITS"); }

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
        public string Units { get; private set; }
        public string DisplayUnits { get; set; }
        private Dictionary<string, double> UnitsTable;

        private Measurement()
        {
            Value = 0; Units = null; UnitsTable = null; DisplayUnits = null;
        }

        public Measurement(double value, string units)
        {
            Value = value;
            Units = units;
            UnitsTable = new Dictionary<string, double>();
            FillUnitsTable();
            Units = UnitsTableToString();
            DisplayUnits = null;
        }

        public Measurement(double? value, string units)
        {
            Value = value.Value;
            Units = units;
            UnitsTable = new Dictionary<string, double>();
            FillUnitsTable();
            Units = UnitsTableToString();
            DisplayUnits = null;
        }

        public Measurement(string measurement)
        {
            if (measurement == null) { throw new InvalidMeasurementException("Measurement can't be null"); }
            string[] parameters = measurement.Split(' ');
            if (parameters.Length < 2) { throw new InvalidMeasurementException("Measurement needs at least two parameters"); }
            string units = parameters[parameters.Length - 1];
            string value = "";
            for (int i = 0; i < parameters.Length - 1; i++) {
                if (i > 0 && Regex.IsMatch(parameters[i], "[^0-9/. -]"))
                {
                    units = parameters[i] + "*" + units;
                }
                else
                {
                    value += parameters[i] + " ";
                }

            }
            units = ParseFromDisplayFormatting(units);
            Value = ParseFromString(value.TrimEnd(' '));
            Units = units;
            UnitsTable = new Dictionary<string, double>();
            FillUnitsTable();
            Units = UnitsTableToString();
            DisplayUnits = null;
        }

        public Measurement(string value, string units) : this(ParseFromString(value), units) { }

        private static double ParseFromString(string stringNum)
        {
            stringNum = Regex.Replace((stringNum ?? ""), "[^0-9/. -]", "");
            stringNum = (stringNum ?? "").Trim();
            stringNum = Regex.Replace(stringNum, "/$", string.Empty);
            if (string.IsNullOrEmpty(stringNum))
            {
                stringNum = "0";
            }

            // standard decimal number (e.g. 1.125)
            if (stringNum.IndexOf('.') != -1 || (stringNum.IndexOf(' ') == -1 && stringNum.IndexOf('/') == -1 && stringNum.IndexOf('\\') == -1))
            {
                double result;
                if (double.TryParse(stringNum, out result))
                {
                    return result;
                }
            }

            string[] parts = stringNum.Split(new[] { ' ', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

            // stand-off fractional (e.g. 7/8)
            if (stringNum.IndexOf(' ') == -1 && parts.Length == 2)
            {
                double num, den;
                if (double.TryParse(parts[0], out num) && double.TryParse(parts[1], out den))
                {
                    return num / den;
                }
            }

            // Number and fraction (e.g. 2 1/2)
            if (parts.Length == 3)
            {
                double whole, num, den;
                if (double.TryParse(parts[0], out whole) && double.TryParse(parts[1], out num) && double.TryParse(parts[2], out den))
                {
                    return whole + (num / den);
                }
            }

            // Bogus / unable to parse
            return double.NaN;
        }

        private void FillUnitsTable()
        {
            string[] numerator_arr, denominator_arr;
            string numerator, denominator;
            Regex regex = new Regex(@"[/]+(?![^(]*\))");
            string[] parts = regex.Split(Units);
            if (parts.Length == 2)
            {
                double test;
                numerator = parts[0];
                denominator = parts[1];
                if (numerator == "") { numerator = "1"; }
                if (double.TryParse(numerator, out test))
                {
                    if (numerator != "1") { throw new InvalidMeasurementException("Units cannot have scalar values"); }
                }
            }
            else if (parts.Length == 1)
            {
                numerator = Units;
                denominator = "";
            }
            else
            {
                throw new InvalidMeasurementException("Extra '/' symbols must be contained in parentheses.");
            }
            regex = new Regex(@"[*]+(?![^(]*\))");
            numerator_arr = regex.Split(numerator);
            if (numerator == "1") { numerator_arr = new string[0]; }
            denominator_arr = regex.Split(denominator);
            regex = new Regex(@"[\^]+(?![^(]*\))");
            foreach (string ele in numerator_arr)
            {
                string[] unitArr = regex.Split(ele);
                string unit = unitArr[0];
                double power = 1;
                if (unitArr.Length == 2)
                {
                    power = double.Parse(unitArr[1]);
                }
                else if (unitArr.Length > 2) { throw new InvalidMeasurementException("Extra '^' symbols must be contained in parentheses."); }
                double count = 0;
                UnitsTable.TryGetValue(unit, out count);
                if (count == 0) { UnitsTable.Add(unit, power); }
                else { UnitsTable[unit] = count + power; }
            }
            foreach (string ele in denominator_arr)
            {
                if (ele == "") { continue; }
                string[] unitArr = regex.Split(ele);
                string unit = unitArr[0];
                double power = 1;
                if (unitArr.Length == 2)
                {
                    power = double.Parse(unitArr[1]);
                }
                else if (unitArr.Length > 2) { throw new InvalidMeasurementException("Extra '^' symbols must be contained in parentheses."); }
                double count = 0;
                UnitsTable.TryGetValue(unit, out count);
                UnitsTable[unit] = count - power;
            }
            Simplify();
        }

        private string UnitsTableToString(Dictionary<string, double> dict = null)
        {
            Dictionary<string, double> dictionary;
            if (dict == null) { dictionary = UnitsTable; }
            else { dictionary = dict; }
            string numerator = "";
            string denominator = "";
            string[] unitsArr = dictionary.Keys.ToArray();
            Array.Sort(unitsArr);
            foreach (string key in unitsArr)
            {
                if (key == "" || key == null) { continue; }
                double power = 0;
                dictionary.TryGetValue(key, out power);
                if (power == 1) { numerator += "*" + key; }
                else if (power == -1) { denominator += "*" + key; }
                else if (power < 0) { denominator += "*" + key + "^" + Math.Abs(power); }
                else if (power > 0) { numerator += "*" + key + "^" + power; }
            }
            if (numerator == "" && denominator == "") { return ""; }
            numerator = numerator.Trim();
            if (numerator.StartsWith("*")) { numerator = numerator.Substring(1); }
            denominator = denominator.Trim();
            if (denominator.StartsWith("*")) { denominator = denominator.Substring(1); }
            if (denominator == "") { return numerator; }
            if (numerator == "") { numerator = "1"; }
            return numerator + "/" + denominator;
        }

        public static Measurement operator +(Measurement n1, Measurement n2)
        {
            if (!SameUnits(n1, n2)) { n2 = n2.ConvertTo(n1); }
            return new Measurement(n1.Value + n2.Value, n1.Units);
        }

        public static Measurement operator -(Measurement n1, Measurement n2)
        {
            if (!SameUnits(n1, n2)) { n2 = n2.ConvertTo(n1); }
            return new Measurement(n1.Value - n2.Value, n1.Units);
        }

        public static Measurement operator -(Measurement n1, string n2String)
        {
            Measurement n2 = new Measurement(n2String);
            return n1 - n2;
        }

        public static Measurement operator *(Measurement n1, Measurement n2)
        {
            if(n2.CanConvertTo(n1))
            {
                n2 = n2.ConvertTo(n1);
            }
            else
            {
                n2 = new Measurement(n2.Value, n2.Units); // making a copy to maintain immutability
            }
            double val = n1.Value * n2.Value;
            Dictionary<string, double> units_table = new Dictionary<string, double>();
            foreach (var n1Key in n1.UnitsTable.Keys)
            {
                double n1Val = 0, n2Val = 0;
                n1.UnitsTable.TryGetValue(n1Key, out n1Val);
                n2.UnitsTable.TryGetValue(n1Key, out n2Val);
                if(n2Val != 0) { n2.UnitsTable.Remove(n1Key); }
                List<string> removeUnits = new List<string>();
                foreach (var n2Key in n2.UnitsTable.Keys)
                {
                    if(UnitData.CanConvert(n1Key, n2Key))
                    {
                        val *= Math.Pow(UnitData.GetUnitMultiplier(n2Key, n1Key), n2.UnitsTable[n2Key]);
                        n2Val = n2.UnitsTable[n2Key];
                        removeUnits.Add(n2Key);
                    }
                }
                foreach(var key in removeUnits)
                {
                    n2.UnitsTable.Remove(key);
                }
                double power = n1Val + n2Val;
                units_table.Add(n1Key, power);
            }
            foreach (var key in n2.UnitsTable.Keys)
            {
                units_table.Add(key, n2.UnitsTable[key]);
            }
            Measurement m = new Measurement();
            string unitString = m.UnitsTableToString(units_table);
            return new Measurement(val, unitString);
        }

        public static Measurement operator *(Measurement n1, string n2String)
        {
            Measurement n2 = new Measurement(n2String);
            return n1 * n2;
        }

        public static Measurement operator /(Measurement n1, Measurement n2)
        {
            if (n2.CanConvertTo(n1))
            {
                n2 = n2.ConvertTo(n1);
            }
            else
            {
                n2 = new Measurement(n2.Value, n2.Units);
            }

            double val = n1.Value / n2.Value;
            Dictionary<string, double> units_table = new Dictionary<string, double>();
            foreach (var n1Key in n1.UnitsTable.Keys)
            {
                double n1Val = 0, n2Val = 0;
                n1.UnitsTable.TryGetValue(n1Key, out n1Val);
                n2.UnitsTable.TryGetValue(n1Key, out n2Val);
                if (n2Val != 0) { n2.UnitsTable.Remove(n1Key); }
                List<string> removeUnits = new List<string>();
                foreach (var n2Key in n2.UnitsTable.Keys)
                {
                    if (UnitData.CanConvert(n1Key, n2Key))
                    {
                        val *= Math.Pow(UnitData.GetUnitMultiplier(n2Key, n1Key), -n2.UnitsTable[n2Key]);
                        n2Val = n2.UnitsTable[n2Key];
                        removeUnits.Add(n2Key);
                    }
                }
                foreach (var key in removeUnits)
                {
                    n2.UnitsTable.Remove(key);
                }
                double power = n1Val - n2Val;
                units_table.Add(n1Key, power);
            }
            foreach (var key in n2.UnitsTable.Keys)
            {
                units_table.Add(key, -n2.UnitsTable[key]);
            }
            Measurement m = new Measurement();
            string _units = m.UnitsTableToString(units_table);
            return new Measurement(val, _units);
        }

        public static Measurement operator /(Measurement n1, string n2String)
        {
            Measurement n2 = new Measurement(n2String);
            return n1 / n2;
        }

        public static Measurement operator *(Measurement n1, double n2)
        {
            return new Measurement(n1.Value * n2, n1.Units);
        }

        public static Measurement operator /(Measurement n1, double n2)
        {
            return new Measurement(n1.Value / n2, n1.Units);
        }

        public static Measurement operator *(double n1, Measurement n2)
        {
            return new Measurement(n1 * n2.Value, n2.Units);
        }

        public static Measurement operator /(double n1, Measurement n2)
        {
            double val = n1 / n2.Value;
            Dictionary<string, double> units_table = new Dictionary<string, double>();
            foreach (string key in n2.UnitsTable.Keys)
            {
                units_table.Add(key, n2.UnitsTable[key] * -1);
            }
            Measurement m = new Measurement();
            string unitString = m.UnitsTableToString(units_table);
            return new Measurement(val, unitString);
        }

        public static Measurement operator *(Measurement n1, int n2)
        {
            return new Measurement(n1.Value * n2, n1.Units);
        }

        public static Measurement operator /(Measurement n1, int n2)
        {
            return new Measurement(n1.Value / n2, n1.Units);
        }

        public static Measurement operator *(int n1, Measurement n2)
        {
            return new Measurement(n1 * n2.Value, n2.Units);
        }

        public static Measurement operator /(int n1, Measurement n2)
        {
            double val = n1 / n2.Value;
            Dictionary<string, double> units_table = new Dictionary<string, double>();
            foreach (string key in n2.UnitsTable.Keys)
            {
                units_table.Add(key, n2.UnitsTable[key] * -1);
            }
            Measurement m = new Measurement();
            string unitString = m.UnitsTableToString(units_table);
            return new Measurement(val, unitString);
        }

        public static decimal operator *(Measurement n1, decimal n2)
        {
            return (decimal)n1.Value * n2;
        }

        public static decimal operator /(decimal n1, Measurement n2)
        {
            return n1 / (decimal)n2.Value;
        }

        public static decimal operator *(decimal n1, Measurement n2)
        {
            return n1 * (decimal)n2.Value;
        }

        // No operator is provided for Measurement / decimal because that makes no sense. Decimals are for money.

        public static bool operator ==(Measurement n1, Measurement n2)
        {
            if (ReferenceEquals(null, n1) && !ReferenceEquals(null, n2)) { return false; }
            else if (ReferenceEquals(null, n2) && !ReferenceEquals(null, n1)) { return false; }
            else if (ReferenceEquals(null, n1) && ReferenceEquals(null, n2)) { return true; }
            n2 = n2.TryConvertTo(n1);
            return (n1.Value == n2.Value && SameUnits(n1, n2));
        }

        public static bool operator ==(Measurement n1, string n2)
        {
            if(n2 == null) { return false; }
            string[] arr = n2.Split(' ');
            Measurement m = new Measurement(arr[0], arr[1]);
            return n1 == m;
        }
        
        public static bool operator !=(Measurement n1, string n2)
        {
            return !(n1 == n2);
        }

        public static bool operator !=(Measurement n1, Measurement n2)
        {
            return !(n1 == n2);
        }

        public static bool operator >(Measurement n1, Measurement n2)
        {
            n2 = n2.TryConvertTo(n1);
            if (!SameUnits(n1, n2)) { throw new InvalidOperationException("Cannot compare Measurements of different units"); }
            return n1.Value > n2.Value;
        }

        public static bool operator >(Measurement n1, string n2)
        {
            if (n2 == null) { return false; }
            string[] arr = n2.Split(' ');
            Measurement m = new Measurement(arr[0], arr[1]);
            return n1 > m;
        }

        public static bool operator <(Measurement n1, Measurement n2)
        {
            n2 = n2.TryConvertTo(n1);
            if (!SameUnits(n1, n2)) { throw new InvalidOperationException("Cannot compare Measurements of different units"); }
            return n1.Value < n2.Value;
        }

        public static bool operator <(Measurement n1, string n2)
        {
            if (n2 == null) { return false; }
            string[] arr = n2.Split(' ');
            Measurement m = new Measurement(arr[0], arr[1]);
            return n1 < m;
        }

        public static bool operator >=(Measurement n1, Measurement n2)
        {
            n2 = n2.TryConvertTo(n1);
            if (!SameUnits(n1, n2)) { throw new InvalidOperationException("Cannot compare Measurements of different units"); }
            return n1.Value >= n2.Value;
        }

        public static bool operator >=(Measurement n1, string n2)
        {
            if (n2 == null) { return false; }
            string[] arr = n2.Split(' ');
            Measurement m = new Measurement(arr[0], arr[1]);
            return n1 >= m;
        }

        public static bool operator <=(Measurement n1, Measurement n2)
        {
            n2 = n2.TryConvertTo(n1);
            if (!SameUnits(n1, n2)) { throw new InvalidOperationException("Cannot compare Measurements of different units"); }
            return n1.Value <= n2.Value;
        }

        public static bool operator <=(Measurement n1, string n2)
        {
            if (n2 == null) { return false; }
            string[] arr = n2.Split(' ');
            Measurement m = new Measurement(arr[0], arr[1]);
            return n1 <= m;
        }

        public static Measurement Pow(Measurement n1, double n2)
        {
            double val = Math.Pow(n1.Value, n2);
            Dictionary<string, double> units_table = new Dictionary<string, double>();
            foreach (string key in n1.UnitsTable.Keys)
            {
                units_table[key] = n1.UnitsTable[key] * n2;
            }
            Measurement m = new Measurement();
            string _units = m.UnitsTableToString(units_table);
            return new Measurement(val, _units);
        }

        public static Measurement Pow(Measurement n1, int n2)
        {
            double val = Math.Pow(n1.Value, n2);
            Dictionary<string, double> units_table = new Dictionary<string, double>();
            foreach (string key in n1.UnitsTable.Keys)
            {
                if (key == "" || key == null) { continue; }
                units_table.Add(key, n1.UnitsTable[key] * n2);
            }
            Measurement m = new Measurement();
            string _units = m.UnitsTableToString(units_table);
            return new Measurement(val, _units);
        }

        public static Measurement Ceil(Measurement n1)
        {
            double val = Math.Ceiling(n1.Value);
            return new Measurement(val, n1.Units);
        }

        public static Measurement Floor(Measurement n1)
        {
            double val = Math.Floor(n1.Value);
            return new Measurement(val, n1.Units);
        }

        public static Measurement Round(Measurement n1, int digits = 2, MidpointRounding mode = MidpointRounding.AwayFromZero)
        {
            double val = Math.Round(n1.Value, digits, mode);
            Measurement mOut = new Measurement(val, n1.Units);
            mOut.DisplayUnits = n1.DisplayUnits;
            return mOut;
        }

        /// <summary>
        /// Defaults to 2 decimal places
        /// </summary>
        public Measurement Round(int digits = 2, MidpointRounding mode = MidpointRounding.AwayFromZero)
        {
            return Round(this, digits, mode);
        }

        public static bool SameUnits(Measurement n1, Measurement n2)
        {
            if (n1.Units == n2.Units) { return true; }
            if (n1.UnitsTable.Count != n2.UnitsTable.Count) { return false; }
            bool? inverted = null;
            foreach (var key in n1.UnitsTable.Keys)
            {
                double n1Val = 0, n2Val = 0;
                n1.UnitsTable.TryGetValue(key, out n1Val);
                n2.UnitsTable.TryGetValue(key, out n2Val);
                if (inverted == null) { inverted = n1Val == -1 * n2Val; }
                else if ((bool)inverted) { n2Val *= -1; }
                if (n1Val != n2Val) { return false; }
            }
            return true;
        }

        public static bool SameUnits(Measurement n1, string n2)
        {
            return SameUnits(n1, new Measurement(0, n2));
        }

        public static bool SameUnits(string n1, string n2)
        {
            return SameUnits(new Measurement(0, n1), new Measurement(0, n2));
        }

        private string ParseFromDisplayFormatting(string displayUnits)
        {
            displayUnits = Regex.Replace(displayUnits, "[⁻⁰¹²³⁴⁵⁶⁷⁸⁹]+", @"^$&");
            for(int index = 0; index < displayUnits.Length; index++)
            {
                char character = displayUnits[index];
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
                displayUnits = displayUnits.ReplaceAt(index, replacement);
            }
            return displayUnits;
        }

        public override string ToString()
        {
            if(DisplayUnits != null) { return Value + " " + DisplayUnits; }
            bool isExponent = false;
            List<int> carets = new List<int>();
            string u = Units;
            for(int index = 0; index < u.Length; index++)
            {
                char character = u[index];
                char replacement;
                if (isExponent)
                {
                    replacement =
                        character == '-' ? '⁻' :
                        character == '0' ? '⁰' :
                        character == '1' ? '¹' :
                        character == '2' ? '²' :
                        character == '3' ? '³' :
                        character == '4' ? '⁴' :
                        character == '5' ? '⁵' :
                        character == '6' ? '⁶' :
                        character == '7' ? '⁷' :
                        character == '8' ? '⁸' :
                        character == '9' ? '⁹' :
                        character;
                    u = u.ReplaceAt(index, replacement);
                }

                if (character == '^') { isExponent = true; carets.Add(index); }
                else if(character == '*' || character == '/'){ isExponent = false; }
            }
            foreach(int caret in carets) { u = u.Remove(caret, 1); }
            return Value + " " + u.Replace("*", " ");
        }

        public string ToDatabaseString()
        {
            return Value + " " + Units;
        }

        public override bool Equals(object obj)
        {
            var measurement = obj as Measurement;
            return measurement == this;
        }

        public override int GetHashCode()
        {
            var hashCode = -665639642;
            hashCode = hashCode * -1521134295 + Value.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Units);
            return hashCode;
        }

        /// <summary>
        /// Returns measurement with same value but different units
        /// </summary>
        /// <param name="units"></param>
        /// <returns></returns>
        public Measurement ForceTo(string units)
        {
            return new Measurement(Value, units);
        }

        public int CompareTo(object other)
        {
            switch (other)
            {
                case null:
                    return 1;
                case Measurement measurement:
                    return CompareTo(measurement);
                case double doubleFloat:
                    if (Value < doubleFloat) { return -1; }
                    else if (Value > doubleFloat) { return 1; }
                    return 0;
                case int integer:
                    if (Value < integer) { return -1; }
                    else if (Value > integer) { return 1; }
                    return 0;
            }
            throw new ArgumentException("Cannot compare Measurement to object of type" + other.GetType());
        }

        public int CompareTo(Measurement other)
        {
            if (other == default || this > other) { return 1; }
            else if (this < other) { return -1; }
            return 0;
        }

        public static implicit operator string(Measurement v)
        {
            return v.ToString();
        }

        public bool CanConvertTo(Measurement convertTo)
        {
            Measurement converted = TryConvertTo(convertTo);
            if (!SameUnits(convertTo, converted))
            {
                return false;
            }
            return true;
        }

        public bool CanConvertTo(string convertTo)
        {
            return CanConvertTo(new Measurement(0, convertTo));
        }

        public static bool AddOrReplaceUnitMultiplier(string from, string to, double multiplier)
        {
            return UnitData.AddOrReplaceUnitMultiplier(from, to, multiplier);
        }

        public static bool AddOrReplaceUnitAdder(string from, string to, double adder)
        {
            return UnitData.AddOrReplaceUnitAdder(from, to, adder);
        }

        private Measurement TryConvertTo(Measurement convertTo, bool adder = false)
        {
            if(this.IsZero()) { return new Measurement(0, convertTo.Units); }
            Dictionary<string, double> toUnitsTable = new Dictionary<string, double>(convertTo.UnitsTable);
            Dictionary<string, double> fromUnitsTable = new Dictionary<string, double>(UnitsTable);
            double convertedValue = Value;
            if(toUnitsTable.Count != UnitsTable.Count) { return this; }
            if (UnitData.CanConvert(Units, convertTo.Units))
            {
                convertedValue *= UnitData.GetUnitMultiplier(Units, convertTo.Units);
                if (adder) { convertedValue += UnitData.GetUnitAdder(Units, convertTo.Units); }
                return new Measurement(convertedValue, convertTo.Units);
            }

            // see if units will convert when inverted (to allow for things like fin/in to mm/fin)
            Measurement inverted = convertedValue == 0 ? new Measurement(0, Pow(new Measurement(1, Units), -1).Units) : Pow(this, -1);
            if (UnitData.CanConvert(inverted.Units, convertTo.Units))
            {
                return inverted.ConvertTo(convertTo);
            }

ResetIterators1:
            foreach(string from in fromUnitsTable.Keys)
            {
                foreach(string to in toUnitsTable.Keys)
                {
                    if(UnitData.CanConvert(from, to, fromUnitsTable[from], toUnitsTable[to]))
                    {
                        convertedValue *= Math.Pow(UnitData.GetUnitMultiplier(from, to), toUnitsTable[to]);
                        toUnitsTable.Remove(to);
                        fromUnitsTable.Remove(from);
                        goto ResetIterators1;
                    }
                }
                return new Measurement(Value, Units);
            }

            Measurement converted = new Measurement();
            converted.UnitsTable = convertTo.UnitsTable;
            converted.Value = convertedValue;
            converted.Units = convertTo.UnitsTableToString();
            return converted;
        }

        public Measurement ConvertTo(Measurement convertTo, bool adder = false)
        {
            Measurement converted = TryConvertTo(convertTo, adder);
            if (!SameUnits(convertTo, converted))
            {
                throw new InvalidOperationException("Cannot convert " + Units + " to " + convertTo.Units);
            }
            return converted;
        }

        public Measurement ConvertTo(string convertTo, bool adder = false)
        {
            return ConvertTo(new Measurement(1, convertTo), adder);
        }

        private bool IsZero()
        {
            return Value == 0 && Units == "ZERO_NO_UNITS";
        }

        public Measurement StripParentheses()
        {
            Measurement toReturn = new Measurement(Value, Units);
            foreach(string unit in UnitsTable.Keys)
            {
                if (unit.Contains('('))
                {
                    toReturn.UnitsTable.Remove(unit);
                    toReturn.Units = toReturn.UnitsTableToString();
                    string u = unit.Replace(")", "");
                    u = u.Replace("(", "");
                    toReturn *= Pow(new Measurement(1, u), UnitsTable[unit]);
                }
            }
            return toReturn;
        }

        private void Simplify()
        {
ResetIterators2:
            foreach(string from in UnitsTable.Keys)
            {
                foreach(string to in UnitsTable.Keys)
                {
                    if(UnitData.CanConvert(from, to) && from != to)
                    {
                        Value *= Math.Pow(UnitData.GetUnitMultiplier(from, to), UnitsTable[to]);
                        UnitsTable[from] += UnitsTable[to];
                        UnitsTable.Remove(to);
                        goto ResetIterators2;
                    }
                }
            }
        }
    }

    internal static class UnitData
    {
        public static double GetUnitMultiplier(string from, string to)
        {
            double multiplier;
            if(from == to) { return 1; }
            if (UnitMultipliers.TryGetValue(from + "-" + to, out multiplier)) { return multiplier; }
            if (UnitMultipliers.TryGetValue(to + "-" + from, out multiplier)) { return 1/multiplier; }
            throw new InvalidConversionException("Could not find unit multiplier for " + from + " to " + to);
        }

        public static double GetUnitAdder(string from, string to)
        {
            double adder;
            if(from == to) { return 0; }
            if(UnitAdders.TryGetValue(from + "-" + to, out adder)) { return adder; }
            if(UnitAdders.TryGetValue(to + "-" + from, out adder)) { return -adder * GetUnitMultiplier(from, to); }
            return 0;
        }

        public static bool AddOrReplaceUnitMultiplier(string from, string to, double multiplier)
        {
            string key = $"{from}-{to}";
            bool exists = UnitMultipliers.ContainsKey(key);
            if(exists)
            {
                UnitMultipliers[key] = multiplier;
            }
            else
            {
                UnitMultipliers.Add(key, multiplier);
            }
            return UnitMultipliers.ContainsKey(key);
        }

        public static bool AddOrReplaceUnitAdder(string from, string to, double adder)
        {
            string key = $"{from}-{to}";
            bool exists = UnitAdders.ContainsKey(key);
            if (exists)
            {
                UnitAdders[key] = adder;
            }
            else
            {
                UnitAdders.Add(key, adder);
            }
            return UnitAdders.ContainsKey(key);
        }

        public static bool CanConvert(string from, string to, double fromPower = 1, double toPower = 1)
        {
            if(toPower != fromPower) { return false; }
            if(from == to) { return true; }
            double m;
            return UnitMultipliers.TryGetValue(from + "-" + to, out m) || UnitMultipliers.TryGetValue(to + "-" + from, out m);
        }

        private static Dictionary<string, double> UnitMultipliers = new Dictionary<string, double>
        {
            {"yd-in", 36}, {"ft-in", 12}, {"m-in", 39.37008}, {"in-cm", 2.54}, {"in-mm", 25.4},
            {"m-ft", 3.28084}, {"ft-cm", 30.48}, {"ft-mm", 304.8}, {"yd-ft", 3},
            {"m-cm", 100}, {"m-mm", 1000}, {"m-yd", 1.093613},
            {"cm-mm", 10}, {"yd-cm", 91.44},
            {"yd-mm", 914.4},
            {"kg-lb", 2.204623}, {"lb-g", 453.5924},
            {"kg-g", 1000},
            {"hr-min", 60}, {"min-s", 60},
            {"hr-s", 3600},
            {"C-F", 1.8}, {"K-F", 1.8}, {"R-F", 1},
            {"C-K", 1}, {"C-R", 1.8},
            {"K-R", 1.8},
            {"hp-W", 745.699872}, {"kW-hp", 1.3410220888}, {"TR-hp", 4.71427994638076}, {"hp-(btu/hr)", 2544.433748},
            {"kW-W", 1000}, {"TR-kW", 3.51685284}, {"kW-(btu/hr)", 3412.142},
            {"TR-W", 3516.85284}, {"W-(btu/hr)", 3.412142},
            {"TR-(btu/hr)", 12000},
            {"btu-J", 1055.0558526 }, {"btu-kJ", 1.0550558526},
            {"kJ-J", 1000},
            {"bar-(lb/in^2)", 14.50377}, {"bar-psi", 14.50377}, {"Bar-Pa", 100000}, {"bar-kPa", 100}, {"atm-bar", 1.01325}, {"bar-inH2O", 401.46307866177}, {"bar-inHg", 29.530070866}, {"bar-mmHg", 750.0638}, {"bar-tor", 750.0638},
            {"inH2O-Pa", 249.082}, {"inH2O-mmHg", 1.8683201548767}, {"inH2O-tor", 1.8683201548767}, {"atm-inH2O", 406.782504600357}, {"kPa-inH2O", 4.0146307866177}, {"inHg-inH2O", 13.595101534864}, {"psi-inH2O", 27.679904842545}, {"(lb/in^2)-inH2O", 27.679904842545},
            {"mmHg-Pa", 133.32239}, {"tor-Pa", 133.32239}, {"atm-Pa", 101325}, {"kPa-Pa", 1000}, {"inHg-Pa", 33386.38866667}, {"psi-Pa", 6894.757}, {"(lb/in^2)-Pa", 6894.757},
            {"mmHg-tor", 1}, {"atm-mmHg", 759.999951996078}, {"kPa-mmHg", 7.500638}, {"inHg-mmHg", 25.34}, {"psi-mmHg", 51.71508}, {"(lb/in^2)-mmHg", 51.71508},
            {"atm-tor", 759.999951996078}, {"kPa-tor", 7.500638}, {"inHg-tor", 25.34}, {"psi-tor", 51.71508}, {"(lb/in^2)-tor", 51.71508},
            {"atm-kPa", 101.325}, {"atm-inHg", 29.8212583001399}, {"atm-psi", 14.69595}, {"atm-(lb/in^2)", 14.69595},
            {"kPa-inHg", 0.295301}, {"psi-kPa", 6.894757}, {"(lb/in^2)-kPa", 6.894757},
            {"psi-inHg", 2.03602045718904}, {"(lb/in^2)-inHg", 2.03602045718904},
            {"psi-(lb/in^2)", 1},
            {"in/fin-mm/fin", 25.4}, {"fin/in-fin/mm", 1/25.4},
            {"m^3-L", 1000}, {"ft^3-L", 28.3168}, {"L-in^3", 61.0237}, {"gal-L", 3.7854},
            {"ft^3-gal", 7.480543}, {"m^3-gal", 264.1729}, {"gal-in^3", 230.9993},
        };

        private static Dictionary<string, double> UnitAdders = new Dictionary<string, double>
        {
            {"C-F", 32}, {"C-K", 273.15}, {"F-K", 273.15}, {"F-R", -460.67}, {"R-K", 0}, {"C-R", 241.15}
        };
    }

    [Serializable]
    internal class InvalidConversionException : Exception
    {
        public InvalidConversionException()
        {
        }

        public InvalidConversionException(string message) : base(message)
        {
        }

        public InvalidConversionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidConversionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
