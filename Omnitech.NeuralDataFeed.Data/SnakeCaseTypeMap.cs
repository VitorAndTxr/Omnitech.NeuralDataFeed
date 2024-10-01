using Dapper;
using System;
using System.Linq;
using System.Reflection;

namespace Omnitech.NeuralDataFeed.Data
{
    public class SnakeCaseToCamelCaseMapper : SqlMapper.ITypeMap
    {
        private readonly SqlMapper.ITypeMap _defaultMap;

        public SnakeCaseToCamelCaseMapper(Type t)
        {
            _defaultMap = new CustomPropertyTypeMap(
                t,
                (type, columnName) => type.GetProperties().FirstOrDefault(prop =>
                    String.Equals(ToCamelCase(prop.Name), columnName, StringComparison.OrdinalIgnoreCase))
            );
        }

        public ConstructorInfo FindConstructor(string[] names, Type[] types)
            => _defaultMap.FindConstructor(names, types);

        public SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName)
            => _defaultMap.GetConstructorParameter(constructor, columnName);

        public SqlMapper.IMemberMap GetMember(string columnName)
            => _defaultMap.GetMember(columnName);

        public ConstructorInfo FindExplicitConstructor()
            => _defaultMap.FindExplicitConstructor();

        private string ToCamelCase(string name)
        {
            // Converte o nome para snake_case para comparar com as colunas do banco de dados
            return string.Concat(name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString().ToLower() : x.ToString().ToLower()));
        }
    }

}
