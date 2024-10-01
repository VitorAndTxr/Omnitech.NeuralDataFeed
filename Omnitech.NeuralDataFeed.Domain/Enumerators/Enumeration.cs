using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Omnitech.NeuralDataFeed.Domain.Enumerators
{
    public abstract class Enumeration : IComparable
    {
        public string Name { get; private set; }

        public string Code { get; private set; }

        public int Id { get; private set; }

        protected Enumeration(int id, string code, string name)
        {
            Id = id;
            Code = code;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }

        public static IEnumerable<T> GetAll<T>() where T : Enumeration
        {
            return (from f in typeof(T).GetFields(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public)
                    select f.GetValue(null)).Cast<T>();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Enumeration enumeration))
            {
                return false;
            }

            bool num = GetType().Equals(obj.GetType());
            bool flag = Id.Equals(enumeration.Id);
            return num && flag;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static int AbsoluteDifference(Enumeration firstValue, Enumeration secondValue)
        {
            return Math.Abs(firstValue.Id - secondValue.Id);
        }

        public static T FromValue<T>(int value) where T : Enumeration
        {
            return Parse(value, "value", (T item) => item.Id == value);
        }

        public static T FromDisplayName<T>(string displayName) where T : Enumeration
        {
            return Parse(displayName, "display name", (T item) => item.Name == displayName);
        }

        public static T FromCode<T>(string code) where T : Enumeration
        {
            return Parse(code, "code", (T item) => item.Code == code);
        }

        private static T Parse<T, K>(K value, string description, Func<T, bool> predicate) where T : Enumeration
        {
            return GetAll<T>().FirstOrDefault(predicate) ?? throw new InvalidOperationException($"'{value}' is not a valid {description} in {typeof(T)}");
        }

        public int CompareTo(object other)
        {
            return Id.CompareTo(((Enumeration)other).Id);
        }
    }
}
